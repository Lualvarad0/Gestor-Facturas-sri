using Back.Data;
using Back.Helpers;
using Back.Interfaces;
using Back.Repositories;
using Back.Services;
using Back.Sri;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Base de datos (PostgreSQL). La cadena se toma de ConnectionStrings:Default
// (user-secrets en desarrollo, variable de entorno ConnectionStrings__Default en producción).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Default'.");
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

// Repositorios y servicios
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddOptions<RecoveryMailOptions>().Bind(builder.Configuration.GetSection("RecoveryMail"))
    .Validate(o => !o.Configured || (Uri.TryCreate(o.FrontendUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || (builder.Environment.IsDevelopment() && uri.IsLoopback && uri.Scheme == "http")))
        && o.Port is > 0 and <= 65535, "RecoveryMail:FrontendUrl debe ser HTTPS (HTTP local solo en desarrollo).")
    .ValidateOnStart();
builder.Services.AddSingleton<RecoveryMail>();
builder.Services.AddSingleton<IRecoveryMail>(sp => sp.GetRequiredService<RecoveryMail>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RecoveryMail>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { detail = "Demasiados intentos. Espera un minuto e inténtalo de nuevo." }, ct);
    };
});
// Detrás de un proxy (Render, etc.): toma la IP real y el esquema de X-Forwarded-For/Proto.
// ForwardLimit indica cuántos proxies de confianza hay delante; se aceptan desde cualquier red
// porque la plataforma no publica sus direcciones.
var detrasDeProxy = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (detrasDeProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}
builder.Services.AddScoped<IComprobanteRepository, ComprobanteRepository>();
builder.Services.AddScoped<ComprobanteService>();
builder.Services.AddScoped<PerfilService>();

// Servicio web de autorización del SRI
builder.Services.Configure<SriOptions>(builder.Configuration.GetSection(SriOptions.Section));
builder.Services.AddHttpClient<SriAutorizacionClient>(http =>
    http.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Sri:TimeoutSegundos", 30)));

// Autenticación JWT
var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwtOptions.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtOptions.SigningKey(),
            ValidateLifetime = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(idValue, out var id)) { context.Fail("Sesión inválida."); return; }
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var hash = await db.Usuarios.Where(u => u.Id == id).Select(u => u.PasswordHash)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                if (hash is null || context.Principal?.FindFirstValue("pwd") != PasswordSecurity.Fingerprint(hash))
                    context.Fail("Sesión revocada.");
            },
        };
    });
builder.Services.AddAuthorization();

// CORS: orígenes permitidos del frontend (Cors:Origins)
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services
    .AddControllers(options => options.Filters.Add<BusinessExceptionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// Swagger con soporte para Bearer token
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Nudo API", Version = "v1" });
    var esquema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", esquema);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquema] = Array.Empty<string>() });
});

var app = builder.Build();
if (detrasDeProxy)
    app.UseForwardedHeaders();

// Errores no controlados -> 500 genérico, sin exponer detalles internos.
// (Los errores de negocio los resuelve BusinessExceptionFilter.)
app.UseExceptionHandler(errorApp => errorApp.Run(context =>
    Results.Problem(detail: "Ocurrió un error inesperado.", statusCode: StatusCodes.Status500InternalServerError)
        .ExecuteAsync(context)));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Aplica migraciones pendientes al iniciar (Database:MigrateOnStartup), útil al desplegar.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    // Detrás de proxy la plataforma termina TLS; redirigir rompería sus health checks por HTTP.
    if (!detrasDeProxy)
        app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
// Frontend compilado en wwwroot (imagen Docker); en desarrollo no existe y se omite.
var conFrontend = app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;
if (conFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
if (conFrontend)
{
    // Rutas /api desconocidas -> 404; el resto son rutas del SPA.
    app.MapFallback("/api/{**resto}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

app.Run();

// Expone Program a WebApplicationFactory en las pruebas de integración.
public partial class Program { }
