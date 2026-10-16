using System.Security.Claims;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace InventoryManagement.Api.Features.Auth;

public enum LoginFailure
{
    None,
    InvalidCredentials,
}

public sealed record LoginResult(LoginFailure Failure, LoginResponse? Response);

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService tokenService)
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return new(LoginFailure.InvalidCredentials, null);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return new(LoginFailure.InvalidCredentials, null);
        }

        var responseUser = await BuildUserResponseAsync(user);
        if (responseUser is null)
        {
            return new(LoginFailure.InvalidCredentials, null);
        }

        var token = await tokenService.CreateAsync(user, cancellationToken);
        return new(
            LoginFailure.None,
            new LoginResponse(token.Token, token.ExpiresAtUtc, responseUser));
    }

    public async Task<UserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idValue, out var id)
            ? await BuildUserResponseAsync(await userManager.FindByIdAsync(id.ToString()))
            : null;
    }

    private async Task<UserResponse?> BuildUserResponseAsync(ApplicationUser? user)
    {
        if (user is null || user.Email is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains(RoleConstants.Admin, StringComparer.Ordinal)
            ? RoleConstants.Admin
            : roles.Contains(RoleConstants.Staff, StringComparer.Ordinal)
                ? RoleConstants.Staff
                : null;

        return role is null ? null : new UserResponse(user.Id, user.DisplayName, user.Email, role);
    }
}
