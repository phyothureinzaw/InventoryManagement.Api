using Microsoft.AspNetCore.Identity;

namespace InventoryManagement.Api.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
}
