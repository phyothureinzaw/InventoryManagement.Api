using System.Security.Claims;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.PurchaseOrders;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/purchase-orders"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class PurchaseOrdersController(
    PurchaseOrderService service,
    PurchaseOrderReceivingService receivingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PurchaseOrderSummaryResponse>>> Get([FromQuery] PurchaseOrderQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? Error(new("not_found", "Purchase order was not found.")) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> Create(PurchaseOrderCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, UserId(), cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> Update(Guid id, PurchaseOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, PurchaseOrderVersionRequest request, CancellationToken cancellationToken)
    {
        var error = await service.SubmitAsync(id, request, UserId(), cancellationToken);
        return error is null ? NoContent() : Error(error);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<IActionResult> Approve(Guid id, PurchaseOrderVersionRequest request, CancellationToken cancellationToken)
    {
        var error = await service.ApproveAsync(id, request, UserId(), cancellationToken);
        return error is null ? NoContent() : Error(error);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<IActionResult> Cancel(Guid id, PurchaseOrderCancelRequest request, CancellationToken cancellationToken)
    {
        var error = await service.CancelAsync(id, request, UserId(), cancellationToken);
        return error is null ? NoContent() : Error(error);
    }

    [HttpPost("{id:guid}/receipts")]
    public async Task<ActionResult<PurchaseReceiptDetailResponse>> Receive(Guid id, PurchaseReceiptCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await receivingService.ReceiveAsync(id, request, UserId(), cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetReceiptById), new { id, receiptId = result.Response!.Id }, result.Response);
    }

    [HttpGet("{id:guid}/receipts")]
    public async Task<ActionResult<PagedResponse<PurchaseReceiptSummaryResponse>>> GetReceipts(Guid id, [FromQuery] PurchaseReceiptQuery query, CancellationToken cancellationToken)
    {
        var result = await receivingService.GetAsync(id, query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}/receipts/{receiptId:guid}")]
    public async Task<ActionResult<PurchaseReceiptDetailResponse>> GetReceiptById(Guid id, Guid receiptId, CancellationToken cancellationToken)
    {
        var result = await receivingService.GetByIdAsync(id, receiptId, cancellationToken);
        return result is null ? Error(new("not_found", "Purchase receipt was not found.")) : Ok(result);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private ActionResult Error(PurchaseOrderServiceError error)
    {
        var status = error.Code switch
        {
            "not_found" or "supplier_not_found" or "product_not_found" => 404,
            "concurrency" or "stale_order" or "duplicate_product" or "duplicate_line" or "inactive_supplier" or "inactive_product" or "invalid_transition" or "duplicate_order_number" or "invalid_line" or "over_receipt" or "duplicate_request" or "insufficient_stock" or "stock_overflow" => 409,
            _ => 400,
        };
        var title = status == 404 ? "Not found" : status == 409 ? "Conflict" : "Validation failed";
        return new ObjectResult(InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(status, title, error.Detail, error.Code)) { StatusCode = status };
    }
}
