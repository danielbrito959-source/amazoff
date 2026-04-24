using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Portal", policy =>
    {
        policy
            .WithOrigins("http://localhost:5091", "https://localhost:7021")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Portal");

app.MapGet("/", () => Results.Ok(new
{
    name = "Amazoff API",
    status = "online",
    endpoints = new[]
    {
        "/health/database",
        "/auth/login",
        "/employees"
    }
}));

app.MapGet("/health/database", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = configuration.GetConnectionString("AmazoffDb");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("Connection string 'AmazoffDb' is not configured.");
    }

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DATABASE();";

    var databaseName = await command.ExecuteScalarAsync(cancellationToken);

    return Results.Ok(new DatabaseHealthResponse(true, databaseName?.ToString()));
})
.WithName("DatabaseHealth");

app.MapPost("/auth/login", async (
    LoginRequest request,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
    {
        return Results.BadRequest(new LoginResponse(false, "Username obrigatorio.", null));
    }

    var connectionString = configuration.GetConnectionString("AmazoffDb");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("Connection string 'AmazoffDb' is not configured.");
    }

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT
            CAST(id AS CHAR) AS user_id,
            username,
            email,
            password_hash,
            first_name,
            last_name,
            CAST(is_active AS UNSIGNED) AS is_active
        FROM users
        WHERE username = @username
        LIMIT 1;
        """;
    command.Parameters.AddWithValue("@username", request.Username.Trim());

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    if (!await reader.ReadAsync(cancellationToken))
    {
        return Results.NotFound(new LoginResponse(false, "Utilizador nao encontrado.", null));
    }

    var isActive = Convert.ToInt32(reader["is_active"]) == 1;

    if (!isActive)
    {
        return Results.Unauthorized();
    }

    var userId = reader.GetString(reader.GetOrdinal("user_id"));
    var username = reader.GetString(reader.GetOrdinal("username"));
    var email = reader.GetString(reader.GetOrdinal("email"));
    var firstName = reader["first_name"] as string;
    var lastName = reader["last_name"] as string;

    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        var storedPassword = reader["password_hash"]?.ToString();

        if (!string.Equals(storedPassword, request.Password, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        await reader.CloseAsync();

        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = """
            UPDATE users
            SET last_login_at = UTC_TIMESTAMP()
            WHERE id = @userId;
            """;
        updateCommand.Parameters.AddWithValue("@userId", userId);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    var user = new LoginUserResponse(
        userId,
        username,
        email,
        firstName,
        lastName);

    return Results.Ok(new LoginResponse(true, "Utilizador encontrado.", user));
})
.WithName("Login");

app.MapGet("/employees", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = EmployeeEndpoints.GetConnectionString(configuration);

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT
            CAST(id AS CHAR) AS user_id,
            username,
            email,
            first_name,
            last_name,
            image_path,
            CAST(is_active AS UNSIGNED) AS is_active,
            created_at,
            updated_at,
            last_login_at
        FROM users
        ORDER BY created_at DESC, username ASC;
        """;

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    var employees = new List<EmployeeResponse>();

    while (await reader.ReadAsync(cancellationToken))
    {
        employees.Add(EmployeeEndpoints.MapEmployee(reader));
    }

    return Results.Ok(employees);
})
.WithName("GetEmployees");

app.MapPost("/employees", async (
    EmployeeUpsertRequest request,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var validationError = EmployeeEndpoints.ValidateEmployeeRequest(request, requirePassword: true);

    if (validationError is not null)
    {
        return validationError;
    }

    var connectionString = EmployeeEndpoints.GetConnectionString(configuration);

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    var id = Guid.NewGuid().ToString();

    await using var command = connection.CreateCommand();
    command.CommandText = """
        INSERT INTO users (
            id,
            username,
            email,
            password_hash,
            first_name,
            last_name,
            image_path,
            is_active
        )
        VALUES (
            @id,
            @username,
            @email,
            @passwordHash,
            @firstName,
            @lastName,
            @imagePath,
            @isActive
        );
        """;
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@username", request.Username.Trim());
    command.Parameters.AddWithValue("@email", request.Email.Trim());
    command.Parameters.AddWithValue("@passwordHash", request.Password!.Trim());
    command.Parameters.AddWithValue("@firstName", EmployeeEndpoints.NullIfWhiteSpace(request.FirstName));
    command.Parameters.AddWithValue("@lastName", EmployeeEndpoints.NullIfWhiteSpace(request.LastName));
    command.Parameters.AddWithValue("@imagePath", EmployeeEndpoints.NullIfWhiteSpace(request.ImagePath));
    command.Parameters.AddWithValue("@isActive", request.IsActive);

    try
    {
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new ApiMessageResponse("Ja existe um utilizador com esse username ou email."));
    }

    var employee = await EmployeeEndpoints.GetEmployeeByIdAsync(connection, id, cancellationToken);

    return employee is null
        ? Results.Problem("Nao foi possivel carregar o trabalhador criado.")
        : Results.Created($"/employees/{employee.Id}", employee);
})
.WithName("CreateEmployee");

app.MapPut("/employees/{id}", async (
    string id,
    EmployeeUpsertRequest request,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var validationError = EmployeeEndpoints.ValidateEmployeeRequest(request, requirePassword: false);

    if (validationError is not null)
    {
        return validationError;
    }

    var connectionString = EmployeeEndpoints.GetConnectionString(configuration);

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE users
        SET
            username = @username,
            email = @email,
            first_name = @firstName,
            last_name = @lastName,
            image_path = @imagePath,
            is_active = @isActive,
            password_hash = CASE
                WHEN @passwordHash IS NULL OR @passwordHash = '' THEN password_hash
                ELSE @passwordHash
            END
        WHERE id = @id;
        """;
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@username", request.Username.Trim());
    command.Parameters.AddWithValue("@email", request.Email.Trim());
    command.Parameters.AddWithValue("@firstName", EmployeeEndpoints.NullIfWhiteSpace(request.FirstName));
    command.Parameters.AddWithValue("@lastName", EmployeeEndpoints.NullIfWhiteSpace(request.LastName));
    command.Parameters.AddWithValue("@imagePath", EmployeeEndpoints.NullIfWhiteSpace(request.ImagePath));
    command.Parameters.AddWithValue("@isActive", request.IsActive);
    command.Parameters.AddWithValue("@passwordHash", EmployeeEndpoints.NullIfWhiteSpace(request.Password));

    try
    {
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
        {
            return Results.NotFound(new ApiMessageResponse("Trabalhador nao encontrado."));
        }
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new ApiMessageResponse("Ja existe um utilizador com esse username ou email."));
    }

    var employee = await EmployeeEndpoints.GetEmployeeByIdAsync(connection, id, cancellationToken);

    return employee is null
        ? Results.Problem("Nao foi possivel carregar o trabalhador atualizado.")
        : Results.Ok(employee);
})
.WithName("UpdateEmployee");

app.MapDelete("/employees/{id}", async (
    string id,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var connectionString = EmployeeEndpoints.GetConnectionString(configuration);

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM users WHERE id = @id;";
    command.Parameters.AddWithValue("@id", id);

    var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

    return affectedRows == 0
        ? Results.NotFound(new ApiMessageResponse("Trabalhador nao encontrado."))
        : Results.Ok(new ApiMessageResponse("Trabalhador removido com sucesso."));
})
.WithName("DeleteEmployee");

app.Run();

record DatabaseHealthResponse(bool Connected, string? Database);

record LoginRequest(string Username, string? Password);

record LoginResponse(bool Success, string Message, LoginUserResponse? User);

record LoginUserResponse(
    string Id,
    string Username,
    string Email,
    string? FirstName,
    string? LastName);

record EmployeeUpsertRequest(
    string Username,
    string Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? ImagePath,
    bool IsActive);

record EmployeeResponse(
    string Id,
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    string? ImagePath,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);

record ApiMessageResponse(string Message);

static class EmployeeEndpoints
{
    internal static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AmazoffDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'AmazoffDb' is not configured.");
        }

        return connectionString;
    }

    internal static IResult? ValidateEmployeeRequest(EmployeeUpsertRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Results.BadRequest(new ApiMessageResponse("Username obrigatorio."));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new ApiMessageResponse("Email obrigatorio."));
        }

        if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new ApiMessageResponse("Password obrigatoria."));
        }

        return null;
    }

    internal static async Task<EmployeeResponse?> GetEmployeeByIdAsync(MySqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                CAST(id AS CHAR) AS user_id,
                username,
                email,
                first_name,
                last_name,
                image_path,
                CAST(is_active AS UNSIGNED) AS is_active,
                created_at,
                updated_at,
                last_login_at
            FROM users
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapEmployee(reader);
    }

    internal static EmployeeResponse MapEmployee(MySqlDataReader reader)
    {
        return new EmployeeResponse(
            reader.GetString(reader.GetOrdinal("user_id")),
            reader.GetString(reader.GetOrdinal("username")),
            reader.GetString(reader.GetOrdinal("email")),
            reader["first_name"] as string,
            reader["last_name"] as string,
            reader["image_path"] as string,
            Convert.ToInt32(reader["is_active"]) == 1,
            reader.GetDateTime(reader.GetOrdinal("created_at")),
            reader.GetDateTime(reader.GetOrdinal("updated_at")),
            reader.IsDBNull(reader.GetOrdinal("last_login_at"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("last_login_at")));
    }

    internal static object? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}
