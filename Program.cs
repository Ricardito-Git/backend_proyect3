//arturo
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using backend_proyect.Models;
using backend_proyect.Services;
using backend_proyect.Services.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
// using HealthChecks.UI.Client; // Opcional si instalas el paquete

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllersWithViews();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 0))
    ));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Health checks (sin UI, para que compile)
builder.Services.AddHealthChecks()
    .AddMySql(connectionString);

// JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]))
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ================================================
//   Verificación de base de datos al iniciar
// ================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var startupLogger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        startupLogger.LogInformation("Verificando conexion a la base de datos Aiven...");

        var context = services.GetRequiredService<ApplicationDbContext>();
        var canConnect = await context.Database.CanConnectAsync();

        startupLogger.LogInformation("Puede conectar a la BD? {CanConnect}", canConnect);

        if (canConnect)
        {
            var pending = await context.Database.GetPendingMigrationsAsync();
            var applied = await context.Database.GetAppliedMigrationsAsync();

            startupLogger.LogInformation("Migraciones aplicadas: {Count}", applied.Count());

            if (pending.Any())
            {
                startupLogger.LogWarning("Migraciones pendientes: {Count}", pending.Count());
                foreach (var m in pending)
                    startupLogger.LogWarning(" - {Migration}", m);

                await context.Database.MigrateAsync();
                startupLogger.LogInformation("Migraciones aplicadas");
            }

            try
            {
                var usuarios = await context.Usuarios.CountAsync();
                var perfiles = await context.Perfiles.CountAsync();
                var productos = await context.Productos.CountAsync();
                var empresas = await context.Empresas.CountAsync();

                startupLogger.LogInformation(
                    "Stats - Usuarios:{U} Perfiles:{P} Productos:{Pr} Empresas:{E}",
                    usuarios, perfiles, productos, empresas
                );
            }
            catch (Exception exQuery)
            {
                startupLogger.LogWarning("Error en consultas: {Error}", exQuery.Message);
            }

            startupLogger.LogInformation("Conexion a Aiven MySQL OK");
        }
        else
        {
            startupLogger.LogError("No se puede conectar a la BD");
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Error critico al conectar con MySQL");
    }
}

// ================================================
//   Pipeline
// ================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ================================================
//   Health Checks (versión sin UI para Render)
// ================================================
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(report);
        await context.Response.WriteAsync(json);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(report);
        await context.Response.WriteAsync(json);
    }
});

// Simple ping
app.MapGet("/api/ping", () => new
{
    message = "API is running",
    timestamp = DateTime.Now,
    status = "OK",
    database = "Aiven MySQL",
    environment = app.Environment.EnvironmentName
});

// Database check
app.MapGet("/api/database-check", async (ApplicationDbContext context) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync();

        return Results.Ok(new
        {
            status = canConnect ? "Connected" : "Disconnected",
            timestamp = DateTime.Now
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error de base de datos",
            detail: ex.Message);
    }
});

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation("Aplicacion iniciada correctamente");

// For Render: use the port assigned by the platform
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");


app.Run();
