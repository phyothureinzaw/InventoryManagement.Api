using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController, Route("api/categories"), Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Staff)]
public sealed class CategoriesController(CategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> Get([FromQuery] CategoryQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(query, cancellationToken);
        return result is null ? InvalidPagination() : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFoundProblem("Category was not found.") : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.Error is not null) return FromError(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<CategoryResponse>> Update(Guid id, CategoryWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Error is null ? Ok(result.Response) : FromError(result.Error);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken)
    {
        var error = await service.SetStatusAsync(id, request.IsActive, cancellationToken);
        return error is null ? NoContent() : FromError(error);
    }

    private ObjectResult InvalidPagination() => ProblemResult(400, "Invalid pagination", "Page must be at least 1 and pageSize must be between 1 and 100.");
    private ActionResult FromError(ServiceError error) => error.Code switch { "not_found" => NotFoundProblem(error.Detail), "duplicate" or "conflict" => ProblemResult(409, "Conflict", error.Detail), _ => ProblemResult(400, "Validation failed", error.Detail) };
    private NotFoundObjectResult NotFoundProblem(string detail) => new(global::InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(404, "Not found", detail));
    private ObjectResult ProblemResult(int status, string title, string detail) => new(global::InventoryManagement.Api.Features.Inventory.ProblemDetailsFactory.Create(status, title, detail)) { StatusCode = status };
}
