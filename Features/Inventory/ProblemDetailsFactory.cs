using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Features.Inventory;

internal static class ProblemDetailsFactory
{
    public static ProblemDetails Create(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
    };

    public static ProblemDetails Create(int status, string title, string detail, string code)
    {
        var problem = Create(status, title, detail);
        problem.Extensions["code"] = code;
        return problem;
    }
}
