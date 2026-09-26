using CreditWorks.Api.Dtos;
using CreditWorks.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditWorks.Api.Controllers;

/// <summary>
/// All endpoints here require a valid Admin session (httpOnly-cookie JWT).
/// This is enforced by ASP.NET Core's auth middleware independent of
/// whatever the frontend does — see Program.cs for how the cookie is read.
/// </summary>
[ApiController]
[Route("api/categories")]
[Authorize(Roles = "Admin")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll()
    {
        return Ok(await _categoryService.GetAllAsync());
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CategoryRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var created = await _categoryService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromBody] CategoryRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        return Ok(await _categoryService.UpdateAsync(id, request));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _categoryService.DeleteAsync(id);
        return NoContent();
    }
}
