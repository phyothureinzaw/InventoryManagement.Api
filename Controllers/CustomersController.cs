using InventoryManagement.Api.Features.Customers;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/customers"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class CustomersController(CustomerService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> Get([FromQuery] CustomerQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? Error(new("not_found", "Customer was not found.")) : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<CustomerResponse>> Create(CustomerWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.Error is not null) return Error(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, CustomerUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Error is null ? Ok(result.Response) : Error(result.Error);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<IActionResult> SetStatus(Guid id, CustomerStatusRequest request, CancellationToken cancellationToken)
    {
        var error = await service.SetStatusAsync(id, request, cancellationToken);
        return error is null ? NoContent() : Error(error);
    }

    private ActionResult Error(CustomerServiceError error)
    {
        var status = error.Code == "not_found" ? 404 : error.Code == "concurrency" ? 409 : 400;
        var title = status == 404 ? "Not found" : status == 409 ? "Conflict" : "Validation failed";
        return new ObjectResult(InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(status, title, error.Detail, error.Code)) { StatusCode = status };
    }
}
