using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// JWT settings from configuration (env overrides appsettings)
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSecret = builder.Configuration["Jwt:Secret"];

byte[] signingKeyBytes;
try
{
    signingKeyBytes = Convert.FromBase64String(jwtSecret ?? string.Empty);
}
catch (FormatException)
{
    signingKeyBytes = Encoding.UTF8.GetBytes(jwtSecret ?? string.Empty);
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev only
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogError(ctx.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogInformation("JWT validated for sub: {sub}", ctx.Principal?.FindFirst("sub")?.Value);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Log if config is missing
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience) || string.IsNullOrEmpty(jwtSecret))
{
    logger.LogWarning("JWT config values are missing. Check Jwt:Issuer, Jwt:Audience, Jwt:Secret.");
}

// Swagger always on for now
app.UseSwagger();
app.UseSwaggerUI();

// Do not force HTTPS in container dev to avoid header drops on redirect
// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
