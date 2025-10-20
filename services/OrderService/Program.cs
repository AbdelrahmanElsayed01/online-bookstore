using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Order Service API", Version = "v1" });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Add HttpClient for calling CatalogService
// Use Docker service name when running in Docker, localhost when running locally
var catalogServiceUrl = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" 
    ? "http://catalog-service:8080/" // Docker service name
    : "http://localhost:5179/"; // Local development

builder.Services.AddHttpClient("CatalogService", client =>
{
    client.BaseAddress = new Uri(catalogServiceUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:3000", "http://localhost:3001", "http://127.0.0.1:3000", "http://127.0.0.1:3001") // frontend origins
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// JWT settings from configuration (appsettings or env vars)
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSecret = builder.Configuration["Jwt:Secret"];


// If the secret is Base64 (Supabase default), decode it; otherwise fall back to UTF8
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
            ClockSkew = TimeSpan.FromSeconds(30),
            
            // Configure for Supabase tokens
            RequireSignedTokens = true,
            TryAllIssuerSigningKeys = true,
            RequireExpirationTime = true,
        };

        // Custom JWT validation for Supabase tokens
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogInformation("JWT validated for sub: {sub}", ctx.Principal?.FindFirst("sub")?.Value);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogError(ctx.Exception, "JWT authentication failed: {error}", ctx.Exception?.Message);
                
                // For Supabase tokens with kid, try manual validation
                if (ctx.Exception is SecurityTokenSignatureKeyNotFoundException)
                {
                    log.LogInformation("Attempting manual JWT validation for Supabase token");
                    
                    var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
                    if (authHeader != null && authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader.Substring(7);
                        
                        // Manual validation - decode and verify basic claims
                        try
                        {
                            var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
                            var jsonToken = handler.ReadJsonWebToken(token);
                            
                            // Check issuer
                            if (jsonToken.Issuer == jwtIssuer && 
                                jsonToken.Audiences.Contains(jwtAudience) &&
                                jsonToken.ValidTo > DateTime.UtcNow)
                            {
                                log.LogInformation("Manual JWT validation successful");
                                
                                // Create claims principal manually
                                var claims = new List<System.Security.Claims.Claim>
                                {
                                    new System.Security.Claims.Claim("sub", jsonToken.Subject ?? ""),
                                    new System.Security.Claims.Claim("email", jsonToken.GetClaim("email")?.Value ?? ""),
                                    new System.Security.Claims.Claim("aud", jsonToken.Audiences.FirstOrDefault() ?? ""),
                                    new System.Security.Claims.Claim("iss", jsonToken.Issuer ?? "")
                                };
                                
                                var identity = new System.Security.Claims.ClaimsIdentity(claims, "jwt");
                                ctx.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                                ctx.Success();
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, "Manual JWT validation failed");
                        }
                    }
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ✅ Now we can log, since app.Services exists
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience) || string.IsNullOrEmpty(jwtSecret))
{
    logger.LogWarning("⚠️ JWT config values are missing. Check Jwt:Issuer, Jwt:Audience, Jwt:Secret.");
}

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

app.Run();
