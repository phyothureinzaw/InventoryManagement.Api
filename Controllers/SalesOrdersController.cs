using System.Security.Claims;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.SalesOrders;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/sales-orders"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class SalesOrdersController(SalesOrderService service, SalesOrderFulfillmentService fulfillmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SalesOrderSummaryResponse>>> Get([FromQuery] SalesOrderQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesOrderDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? Error(new("not_found", "Sales order was not found.")) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SalesOrderDetailResponse>> Create(SalesOrderCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, UserId(), cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SalesOrderDetailResponse>> Update(Guid id, SalesOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<SalesOrderDetailResponse>> Confirm(Guid id, SalesOrderConfirmRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ConfirmAsync(id, request, UserId(), cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPost("{id:guid}/fulfillments")]
    public async Task<ActionResult<SalesFulfillmentDetailResponse>> Fulfill(Guid id, SalesFulfillmentCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.FulfillAsync(id, request, UserId(), cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetFulfillmentById), new { id, fulfillmentId = result.Response!.Id }, result.Response);
    }

    [HttpGet("{id:guid}/fulfillments")]
    public async Task<ActionResult<PagedResponse<SalesFulfillmentSummaryResponse>>> GetFulfillments(Guid id, [FromQuery] SalesFulfillmentQuery query, CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.GetAsync(id, query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}/fulfillments/{fulfillmentId:guid}")]
    public async Task<ActionResult<SalesFulfillmentDetailResponse>> GetFulfillmentById(Guid id, Guid fulfillmentId, CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.GetByIdAsync(id, fulfillmentId, cancellationToken);
        return result is null ? Error(new("not_found", "Sales fulfillment was not found.")) : Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<SalesOrderDetailResponse>> Cancel(Guid id, SalesOrderCancelRequest request, CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.CancelAsync(id, request, UserId(), cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private ActionResult Error(SalesOrderServiceError error)
    {
        var status = error.Code is "not_found" or "customer_not_found" or "product_not_found" ? 404 : error.Code == "database_error" ? 500 : error.Code is "concurrency" or "stale_order" or "invalid_transition" or "inactive_customer" or "inactive_product" or "duplicate_product" or "duplicate_order_number" or "insufficient_available_stock" or "insufficient_reserved_stock" or "insufficient_stock" or "duplicate_line" or "invalid_line" or "over_fulfillment" or "duplicate_request" ? 409 : 400;
        var title = status == 404 ? "Not found" : status == 409 ? "Conflict" : status == 500 ? "Unable to process sales order" : "Validation failed";
        return new ObjectResult(InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(status, title, error.Detail, error.Code)) { StatusCode = status };
    }
}
