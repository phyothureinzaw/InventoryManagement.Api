using System.Security.Claims;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.Returns;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/customer-returns"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class CustomerReturnsController(ReturnService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerReturnSummaryResponse>>> Get([FromQuery] CustomerReturnQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetCustomersAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerReturnDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetCustomerByIdAsync(id, cancellationToken);
        return result is null ? Error(new("not_found", "Customer return was not found.")) : Ok(result);
    }

    [HttpGet("sources")]
    public async Task<ActionResult<PagedResponse<CustomerReturnSourceResponse>>> GetSources([FromQuery] CustomerReturnSourceQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetCustomerSourcesAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<CustomerReturnDetailResponse>> Create(CustomerReturnCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateCustomerAsync(request, UserId(), cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static ActionResult Error(ReturnServiceError error) => new ObjectResult(InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(Status(error.Code), Title(error.Code), error.Detail, error.Code)) { StatusCode = Status(error.Code) };
    private static int Status(string code) => code is "not_found" ? 404 : code is "duplicate_request" or "duplicate_line" or "invalid_line" or "over_return" or "insufficient_available_stock" or "stock_overflow" or "concurrency" or "database_error" ? code == "database_error" ? 500 : 409 : 400;
    private static string Title(string code) => Status(code) switch { 404 => "Not found", 409 => "Conflict", 500 => "Unable to process customer return", _ => "Validation failed" };
}
