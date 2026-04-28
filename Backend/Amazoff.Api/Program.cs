using Amazoff.Api.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AmazoffDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("AmazoffDb");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'AmazoffDb' is not configured.");
    }

    var connectionStringBuilder = new MySqlConnectionStringBuilder(connectionString);

    if (connectionStringBuilder.SslMode == MySqlSslMode.Preferred)
    {
        connectionStringBuilder.SslMode = MySqlSslMode.None;
    }

    var normalizedConnectionString = connectionStringBuilder.ConnectionString;

    options.UseMySql(normalizedConnectionString, ServerVersion.AutoDetect(normalizedConnectionString));
});

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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AmazoffDbContext>();
    await DatabaseSchemaBootstrapper.EnsureIdentityColumnsAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseCors("Portal");
app.MapControllers();

app.Run();
