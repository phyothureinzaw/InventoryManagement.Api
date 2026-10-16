using System.Security.Claims;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/stock-movements"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class StockMovementsController(
    StockMovementService service,
    MovementReadService readService) : ControllerBase
{
    [HttpPost("stock-in")]
    public async Task<ActionResult<StockMovementResponse>> StockIn(StockInRequest request, CancellationToken cancellationToken)
    {
        var result = await service.StockInAsync(request, UserId(), cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPost("stock-out")]
    public async Task<ActionResult<StockMovementResponse>> StockOut(StockOutRequest request, CancellationToken cancellationToken)
    {
        var result = await service.StockOutAsync(request, UserId(), cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<MovementHistoryResponse>>> Get([FromQuery] MovementHistoryQuery query, CancellationToken cancellationToken)
    {
        var result = await readService.GetAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Problem(statusCode: 400, title: "Invalid movement query", detail: result.Error.Detail);
    }

    [HttpGet("actors")]
    public async Task<ActionResult<PagedResponse<ActorResponse>>> GetActors([FromQuery] ActorQuery query, CancellationToken cancellationToken)
    {
        var result = await readService.GetActorsAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Problem(statusCode: 400, title: "Invalid actor query", detail: result.Error.Detail);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MovementHistoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await readService.GetByIdAsync(id, cancellationToken);
        return result is null ? Problem(statusCode: 404, title: "Not found", detail: "The stock movement was not found.") : Ok(result);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private ActionResult Error(StockMovementServiceError error) => error.Code switch
    {
        "not_found" or "supplier_not_found" => ErrorResponse(404, "Not found", error),
        "duplicate_request" or "inactive_product" or "inactive_supplier" or "insufficient_stock" or "insufficient_available_stock" or "stock_overflow" or "concurrency" => ErrorResponse(409, "Conflict", error),
        "database_error" => ErrorResponse(500, "Unable to process stock movement", error),
        _ => ErrorResponse(400, "Validation failed", error),
    };

    private static ActionResult ErrorResponse(int status, string title, StockMovementServiceError error) =>
        new ObjectResult(InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(status, title, error.Detail, error.Code)) { StatusCode = status };
}
