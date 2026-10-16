using InventoryManagement.Api.Configuration;
using InventoryManagement.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InventoryManagement.Api.Identity;

public sealed class IdentityBootstrapService(
    ApplicationDbContext db,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<BootstrapOptions> options)
{
    public async Task EnsureRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in new[] { RoleConstants.Admin, RoleConstants.Staff })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                });
                result.ThrowIfFailed();
            }
        }
    }

    public async Task<BootstrapResult> CreateInitialAdminAsync(CancellationToken cancellationToken)
    {
        var bootstrap = options.Value;
        if (!bootstrap.Enabled)
        {
            return BootstrapResult.Disabled;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.AdminEmail)
            || string.IsNullOrWhiteSpace(bootstrap.AdminPassword)
            || string.IsNullOrWhiteSpace(bootstrap.AdminDisplayName))
        {
            return BootstrapResult.MissingConfiguration;
        }

        await db.Database.MigrateAsync(cancellationToken);
        await EnsureRolesAsync(cancellationToken);

        var existing = await userManager.FindByEmailAsync(bootstrap.AdminEmail.Trim());
        if (existing is not null)
        {
            return BootstrapResult.AlreadyExists;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = bootstrap.AdminDisplayName.Trim(),
            Email = bootstrap.AdminEmail.Trim(),
            UserName = bootstrap.AdminEmail.Trim(),
        };
        var createResult = await userManager.CreateAsync(user, bootstrap.AdminPassword);
        createResult.ThrowIfFailed();
        var roleResult = await userManager.AddToRoleAsync(user, RoleConstants.Admin);
        roleResult.ThrowIfFailed();

        return BootstrapResult.Created;
    }
}

public enum BootstrapResult
{
    Created,
    AlreadyExists,
    Disabled,
    MissingConfiguration,
}

internal static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
