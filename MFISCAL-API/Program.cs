using MFISCAL_BLL.Services;
using MFISCAL_BLL.Loggers;
using MFISCAL_INF.Environments;
using MFISCAL_DAL.Repositories;
using MFISCAL_DAL.Models;
using MFISCAL_BLL.Models;
using Microsoft.EntityFrameworkCore;
using MFISCAL_DAL.Contexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Register local environment as singleton
builder.Services.AddSingleton<ILocalEnvironment>(LocalEnvironment.Instance);
var env = LocalEnvironment.Instance;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services
builder.Services.AddScoped<InviteCodeService>();
builder.Services.AddScoped<ServiceLogger>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SeedingService>();
builder.Services.AddScoped<InvoiceService>();

// Register repositories
builder.Services.AddScoped<IIdentifiableRepository<UserDB>, IdentifiableRepository<UserDB>>();
builder.Services.AddScoped<IIdentifiableRepository<LoginTokenDB>, IdentifiableRepository<LoginTokenDB>>();
builder.Services.AddScoped<IIdentifiableRepository<InviteCodeDB>, IdentifiableRepository<InviteCodeDB>>();
builder.Services.AddScoped<IIdentifiableRepository<InvoiceDB>, IdentifiableRepository<InvoiceDB>>();

// Register HTTP context
builder.Services.AddHttpContextAccessor();

// Register main database context using env values
string baseDbConnectionString = $"Host={env.Values.PostgresBaseDbHost};Port={env.Values.PostgresBaseDbPort};Database={env.Values.PostgresBaseDbDbName};Username={env.Values.PostgresBaseDbUser};Password={env.Values.PostgresBaseDbPassword};Ssl Mode={env.Values.PostgresBaseDbSslMode}";
builder.Services.AddDbContext<BaseDataContext>(options =>
    options.UseNpgsql(baseDbConnectionString));

// JWT Authentication setup
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = env.Values.JwtIssuerName,
            ValidateAudience = true,
            ValidAudience = env.Values.JwtIssuerAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(env.GetSigningKeyBytes())
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Run seeding service
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<SeedingService>();
    seeder.Seed();
}

app.Run();
