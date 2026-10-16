using System.Security.Claims;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.Returns;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/supplier-returns"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class SupplierReturnsController(ReturnService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SupplierReturnSummaryResponse>>> Get([FromQuery] SupplierReturnQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetSuppliersAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierReturnDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetSupplierByIdAsync(id, cancellationToken);
        return result is null ? Error(new("not_found", "Supplier return was not found.")) : Ok(result);
    }

    [HttpGet("sources")]
    public async Task<ActionResult<PagedResponse<SupplierReturnSourceResponse>>> GetSources([FromQuery] SupplierReturnSourceQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetSupplierSourcesAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<SupplierReturnDetailResponse>> Create(SupplierReturnCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateSupplierAsync(request, UserId(), cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static ActionResult Error(ReturnServiceError error) => new ObjectResult(InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(Status(error.Code), Title(error.Code), error.Detail, error.Code)) { StatusCode = Status(error.Code) };
    private static int Status(string code) => code is "not_found" ? 404 : code is "duplicate_request" or "duplicate_line" or "invalid_line" or "over_return" or "insufficient_available_stock" or "stock_overflow" or "concurrency" or "database_error" ? code == "database_error" ? 500 : 409 : 400;
    private static string Title(string code) => Status(code) switch { 404 => "Not found", 409 => "Conflict", 500 => "Unable to process supplier return", _ => "Validation failed" };
}
