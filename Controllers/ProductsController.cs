using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/products"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> Get([FromQuery] ProductQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result is null ? Problem(statusCode: 400, title: "Invalid query", detail: "Page must be at least 1, pageSize must be between 1 and 100, and stockStatus must be all, low, or outOfStock.") : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? Problem(statusCode: 404, title: "Not found", detail: "Product was not found.") : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ProductResponse>> Create(ProductWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, ProductWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<IActionResult> SetStatus(Guid id, ProductStatusRequest request, CancellationToken cancellationToken)
    {
        var error = await service.SetStatusAsync(id, request, cancellationToken);
        return error is null ? NoContent() : Error(error);
    }

    private ActionResult Error(ServiceError error) => error.Code switch
    {
        "not_found" or "category_not_found" => Problem(statusCode: 404, title: "Not found", detail: error.Detail),
        "duplicate" or "inactive_category" or "concurrency" => Problem(statusCode: 409, title: "Conflict", detail: error.Detail),
        _ => Problem(statusCode: 400, title: "Validation failed", detail: error.Detail),
    };
}
