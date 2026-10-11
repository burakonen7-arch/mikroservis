using Microsoft.AspNetCore.Mvc;
using Product.Api.Models;
using Product.Application.Abstractions;
using Product.Application.DTOs;

namespace Product.Api.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(new CreateProductInput(request.Name, request.Price), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }
}
