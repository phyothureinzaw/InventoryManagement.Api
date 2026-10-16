using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/suppliers"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class SuppliersController(SupplierService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SupplierResponse>>> Get([FromQuery] SupplierQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result is null ? Problem(statusCode: 400, title: "Invalid pagination", detail: "Page must be at least 1 and pageSize must be between 1 and 100.") : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? Problem(statusCode: 404, title: "Not found", detail: "Supplier was not found.") : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<SupplierResponse>> Create(SupplierWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.Error is not null) return Problem(statusCode: 400, title: "Validation failed", detail: result.Error.Detail);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<SupplierResponse>> Update(Guid id, SupplierWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        if (result.Error?.Code == "not_found") return Problem(statusCode: 404, title: "Not found", detail: result.Error.Detail);
        return result.Error is null ? Ok(result.Response) : Problem(statusCode: 400, title: "Validation failed", detail: result.Error.Detail);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken)
    {
        var error = await service.SetStatusAsync(id, request.IsActive, cancellationToken);
        if (error?.Code == "not_found") return Problem(statusCode: 404, title: "Not found", detail: error.Detail);
        return error is null ? NoContent() : Problem(statusCode: 400, title: "Unable to change status", detail: error.Detail);
    }
}
