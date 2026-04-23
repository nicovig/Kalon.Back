using Kalon.Back.Configuration;
using Kalon.Back.Data;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Text;

QuestPDF.Settings.License = LicenseType.Community;

// après les autres builder.Services...
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PasswordOptions>(builder.Configuration.GetSection("Password"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserOrganizationAccessService, UserOrganizationAccessService>();
builder.Services.AddScoped<IDocumentGeneratorService, DocumentGeneratorService>();
builder.Services.AddScoped<IVariableResolverService, VariableResolverService>();
builder.Services.AddScoped<ISendingService, SendingService>();
builder.Services.AddScoped<IAiMailGeneratorService, AiMailGeneratorService>();

builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection(AnthropicOptions.Section));

builder.Services.Configure<BrevoOptions>(
    builder.Configuration.GetSection(BrevoOptions.Section));

builder.Services.AddScoped<IMailService, MailService>();
builder.Services.Configure<MeranOptions>(builder.Configuration.GetSection("MeranOptions"));
builder.Services.AddHttpClient("MeranOAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<IMeranTokenProvider, MeranTokenProvider>();
builder.Services.AddHttpClient<MeranClient>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
    throw new InvalidOperationException("Jwt:SigningKey is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[]
        {
            "http://localhost:4300",
            "https://localhost:4300"
        };
        var normalizedAllowedOrigins = new List<string>(allowedOrigins);
        if (!normalizedAllowedOrigins.Contains("http://localhost:4300"))
            normalizedAllowedOrigins.Add("http://localhost:4300");
        if (!normalizedAllowedOrigins.Contains("https://localhost:4300"))
            normalizedAllowedOrigins.Add("https://localhost:4300");

        policy.WithOrigins(normalizedAllowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
