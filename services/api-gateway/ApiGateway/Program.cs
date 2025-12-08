using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JWT auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Issuer"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        // Allow local/dev HTTP authority (Minikube) instead of requiring HTTPS metadata
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"]
        };
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// ENV
var env = app.Services.GetRequiredService<IWebHostEnvironment>();

// Swagger w/ custom HTML
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.IndexStream = () => env.ContentRootFileProvider
        .GetFileInfo("swagger-custom/index.html")
        .CreateReadStream();
});

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Proxy
app.MapReverseProxy();

app.Run();
