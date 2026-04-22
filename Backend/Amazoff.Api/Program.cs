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

    var user = new LoginUserResponse(
        reader.GetString(reader.GetOrdinal("user_id")),
        reader.GetString(reader.GetOrdinal("username")),
        reader.GetString(reader.GetOrdinal("email")),
        reader["first_name"] as string,
        reader["last_name"] as string);

    return Results.Ok(new LoginResponse(true, "Utilizador encontrado.", user));
})
.WithName("Login");

app.Run();

record DatabaseHealthResponse(bool Connected, string? Database);

record LoginRequest(string Username);

record LoginResponse(bool Success, string Message, LoginUserResponse? User);

record LoginUserResponse(
    string Id,
    string Username,
    string Email,
    string? FirstName,
    string? LastName);
