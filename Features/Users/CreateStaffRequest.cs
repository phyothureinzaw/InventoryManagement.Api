using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Api.Features.Users;

public sealed class CreateStaffRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
