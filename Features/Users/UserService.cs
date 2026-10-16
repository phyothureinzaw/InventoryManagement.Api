using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace InventoryManagement.Api.Features.Users;

public sealed record CreateStaffResult(
    bool Succeeded,
    bool DuplicateEmail,
    IReadOnlyCollection<string> Errors,
    Guid? UserId);

public sealed class UserService(UserManager<ApplicationUser> userManager)
{
    public async Task<CreateStaffResult> CreateStaffAsync(
        CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return new(false, true, Array.Empty<string>(), null);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName.Trim(),
            Email = email,
            UserName = email,
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new(false, false, createResult.Errors.Select(error => error.Description).ToArray(), null);
        }

        var roleResult = await userManager.AddToRoleAsync(user, RoleConstants.Staff);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return new(false, false, roleResult.Errors.Select(error => error.Description).ToArray(), null);
        }

        return new(true, false, Array.Empty<string>(), user.Id);
    }
}
