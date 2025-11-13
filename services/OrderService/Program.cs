using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Orders Service API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// CORS
builder.Services.AddCors(o =>
{
    o.AddPolicy("AllowFrontend", p => p
        .WithOrigins("http://localhost:3000", "http://localhost:3001", "http://127.0.0.1:3000", "http://127.0.0.1:3001")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// HttpClientFactory
builder.Services.AddHttpClient();

// JWT env vars
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSecret = builder.Configuration["Jwt:Secret"];

if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("Missing JWT secret");

byte[] signingKeyBytes;
try
{
    signingKeyBytes = Convert.FromBase64String(jwtSecret);
}
catch (FormatException)
{
    signingKeyBytes = Encoding.UTF8.GetBytes(jwtSecret);
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireSignedTokens = true,
            TryAllIssuerSigningKeys = true,
            RequireExpirationTime = true
        };

        // Manual fallback (same as CatalogService)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogError(ctx.Exception, "❌ JWT authentication failed: {error}", ctx.Exception?.Message);

                if (ctx.Exception is SecurityTokenSignatureKeyNotFoundException or SecurityTokenInvalidSignatureException)
                {
                    log.LogInformation("Attempting manual Supabase token validation fallback...");

                    var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
                    if (authHeader != null && authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader.Substring(7);
                        try
                        {
                            var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
                            var jwt = handler.ReadJsonWebToken(token);

                            if (jwt.Issuer == jwtIssuer &&
                                jwt.Audiences.Contains(jwtAudience) &&
                                jwt.ValidTo > DateTime.UtcNow)
                            {
                                log.LogInformation("✅ Manual validation succeeded (Supabase token).");
                                var claims = new List<System.Security.Claims.Claim>
                                {
                                    new("sub", jwt.Subject ?? ""),
                                    new("email", jwt.GetClaim("email")?.Value ?? ""),
                                    new("aud", jwt.Audiences.FirstOrDefault() ?? ""),
                                    new("iss", jwt.Issuer ?? "")
                                };
                                var identity = new System.Security.Claims.ClaimsIdentity(claims, "jwt");
                                ctx.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                                ctx.Success();
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, "Manual validation failed.");
                        }
                    }
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogInformation("✅ JWT validated for sub: {sub}",
                    ctx.Principal?.FindFirst("sub")?.Value ?? "unknown");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => new
{
    Status = "Healthy",
    Service = "Orders Service",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0"
});

app.Run();
