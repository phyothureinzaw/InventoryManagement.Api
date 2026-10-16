using InventoryManagement.Api.Features.Auth;
using InventoryManagement.Api.Features.Users;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleConstants.Admin)]
public sealed class UsersController(UserService userService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateStaff(
        CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.CreateStaffAsync(request, cancellationToken);
        if (result.DuplicateEmail)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account already exists",
                detail: "An account with that email already exists.");
        }

        if (!result.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unable to create account",
                detail: string.Join(" ", result.Errors));
        }

        var response = new UserResponse(
            result.UserId!.Value,
            request.DisplayName.Trim(),
            request.Email.Trim(),
            RoleConstants.Staff);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
