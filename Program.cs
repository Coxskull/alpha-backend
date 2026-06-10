using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// =====================================================
// DATABASE CONNECTION
// =====================================================

var databaseUrl = builder.Configuration["DATABASE_URL"];

if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);

    var userInfo = uri.UserInfo.Split(':');

    var connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port,
        Username = userInfo[0],
        Password = userInfo[1],
        Database = uri.AbsolutePath.Trim('/'),
        SslMode = SslMode.Require,
        TrustServerCertificate = true
    }.ToString();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// =====================================================
// CORS
// =====================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("X-Total-Count");
        });
});

var app = builder.Build();

// =====================================================
// DEVELOPMENT
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// =====================================================
// MIDDLEWARE
// =====================================================

app.UseSwagger();

app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// =====================================================
// ROUTES
// =====================================================

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapGet("/health", () => "API WORKING");

app.Run();
