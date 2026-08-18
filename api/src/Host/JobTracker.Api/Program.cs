using FluentValidation;
using Jobs.Application;
using Jobs.Infrastructure;
using Jobs.Infrastructure.Persistence;
using JobTracker.Api.Infrastructure;
using JobTracker.Api.Infrastructure.Auth;
using JobTracker.Api.Infrastructure.Files;
using JobTracker.Api.Infrastructure.Idempotency;
using JobTracker.Api.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Behaviors;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IDateTimeProvider, SystemClock>();
builder.Services.AddScoped<ITenantContext, JwtTenantContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<IdempotencyOptions>(builder.Configuration.GetSection(IdempotencyOptions.SectionName));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtEnabled = jwtSection.GetValue("Enabled", true);

if (jwtEnabled && !string.IsNullOrWhiteSpace(jwtSection["Authority"]))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = jwtSection["Authority"];
            options.Audience = jwtSection["Audience"];
            options.RequireHttpsMetadata = jwtSection.GetValue("RequireHttpsMetadata", true);
            options.TokenValidationParameters.ValidateIssuer = !string.IsNullOrEmpty(jwtSection["Issuer"]);
            options.TokenValidationParameters.ValidIssuer = jwtSection["Issuer"];
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Jobs.Application.AssemblyMarker>();
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
builder.Services.AddValidatorsFromAssemblyContaining<Jobs.Application.AssemblyMarker>(includeInternalTypes: true);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddJobsApplication();
builder.Services.AddJobsInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(Jobs.Presentation.PresentationAssemblyMarker).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<JobTracker.Api.Infrastructure.Swagger.FileUploadOperationFilter>();
});

const string CorsPolicy = "JobTrackerOpen";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Location", "Idempotency-Key");
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionToProblemDetailsMapper>();

builder.Services
    .AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("JobTracker")
        ?? throw new InvalidOperationException("Missing connection string 'JobTracker'."));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
    app.Logger.LogInformation("Applying database migrations...");
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors(CorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (jwtEnabled && !string.IsNullOrWhiteSpace(jwtSection["Authority"]))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseMiddleware<IdempotencyMiddleware>();

var storageOpts = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
var uploadsRoot = Path.GetFullPath(storageOpts.RootPath);
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = storageOpts.PublicBaseUrl.TrimEnd('/'),
});

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program;
