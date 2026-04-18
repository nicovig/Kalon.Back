using Kalon.Back.Configuration;
using Kalon.Back.Data;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Collections.Generic;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PasswordOptions>(builder.Configuration.GetSection("Password"));
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IUserOrganizationAccessService, UserOrganizationAccessService>();
builder.Services.AddScoped<IDocumentGeneratorService, DocumentGeneratorService>();
builder.Services.AddScoped<IVariableResolverService, VariableResolverService>();
builder.Services.AddScoped<ISendingService, SendingService>();
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
builder.Services.AddSwaggerGen();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
