using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get([FromQuery] DashboardQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Problem(statusCode: 400, title: "Invalid date range", detail: result.Error.Detail);
    }
}
