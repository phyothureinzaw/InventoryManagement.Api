using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Api.Features.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record UserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string Role);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User);
