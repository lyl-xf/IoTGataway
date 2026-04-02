using IoTGateway.Models;
using IoTGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all network interfaces (0.0.0.0) instead of just localhost
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // HTTP
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize Int64 as string to avoid precision loss in browser JavaScript.
        options.JsonSerializerOptions.Converters.Add(new Int64ToStringJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableInt64ToStringJsonConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IProtocolPluginHost, ProtocolPluginHost>();
builder.Services.AddSingleton<ProtocolSessionFactory>();
builder.Services.AddHostedService<DataCollectionWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE DeviceVariables ADD COLUMN Description TEXT NOT NULL DEFAULT ''");
    }
    catch { /* column may already exist */ }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Devices ADD COLUMN PluginId TEXT NOT NULL DEFAULT ''");
    }
    catch { /* column may already exist */ }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Devices ADD COLUMN PluginConfigJson TEXT NOT NULL DEFAULT ''");
    }
    catch { /* column may already exist */ }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();