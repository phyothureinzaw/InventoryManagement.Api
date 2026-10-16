using System.Security.Claims;
using System.Text;
using InventoryManagement.Api.Configuration;
using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Auth;
using InventoryManagement.Api.Features.Users;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Features.PurchaseOrders;
using InventoryManagement.Api.Features.Customers;
using InventoryManagement.Api.Features.SalesOrders;
using InventoryManagement.Api.Features.Returns;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inventory Management API",
        Version = "v1",
        Description = "Authenticated inventory management endpoints.",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT as: Bearer {token}",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => options.AccessTokenLifetimeMinutes is > 0 and <= 60,
        "Access token lifetime must be between 1 and 60 minutes.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey)
        && Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
        "JWT signing key must be at least 32 bytes.")
    .ValidateOnStart();
builder.Services.AddOptions<BootstrapOptions>()
    .Bind(builder.Configuration.GetSection("Bootstrap"));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    });
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<StockMovementService>();
builder.Services.AddScoped<StockPostingService>();
builder.Services.AddScoped<MovementReadService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<PurchaseOrderReceivingService>();
builder.Services.AddScoped<SalesOrderService>();
builder.Services.AddScoped<SalesOrderFulfillmentService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<IdentityBootstrapService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentFrontend", policy =>
    {
        var frontendOrigin = builder.Configuration["FrontendOrigin"]
            ?? throw new InvalidOperationException("FrontendOrigin is not configured.");
        var frontendOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            frontendOrigin,
        };

        if (builder.Environment.IsDevelopment())
        {
            frontendOrigins.Add("http://localhost:5173");
            frontendOrigins.Add("http://127.0.0.1:5173");
        }

        policy.WithOrigins(frontendOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
        && !response.HasStarted)
    {
        var problemDetailsService = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = response.StatusCode,
                Title = response.StatusCode == StatusCodes.Status401Unauthorized
                    ? "Unauthorized"
                    : "Forbidden",
                Detail = response.StatusCode == StatusCodes.Status401Unauthorized
                    ? "Authentication is required."
                    : "You do not have permission to perform this action.",
            },
        });
    }
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Management API v1");
    });
}
app.UseCors("DevelopmentFrontend");
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("The admin bootstrap command is development-only.");
        Environment.ExitCode = 1;
        return;
    }

    using var scope = app.Services.CreateScope();
    var bootstrap = scope.ServiceProvider.GetRequiredService<IdentityBootstrapService>();
    var result = await bootstrap.CreateInitialAdminAsync(CancellationToken.None);
    if (result is BootstrapResult.MissingConfiguration or BootstrapResult.Disabled)
    {
        Console.Error.WriteLine("Bootstrap configuration is missing or disabled.");
        Environment.ExitCode = 1;
    }
    else if (result == BootstrapResult.AlreadyExists)
    {
        Console.WriteLine("The bootstrap admin already exists; no changes were made.");
    }
    else
    {
        Console.WriteLine("The bootstrap admin was created.");
    }

    return;
}

app.Run();

public partial class Program;
