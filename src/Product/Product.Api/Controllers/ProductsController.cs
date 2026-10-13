using MediatR;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Models;
using Product.Application.Commands.CreateProduct;
using Product.Application.Commands.RemoveProduct;
using Product.Application.Commands.UpdateProduct;
using Product.Application.DTOs;
using Product.Application.Queries.GetProductById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Presentation.Security;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Product.Api.Controllers;

[ApiController]
[Route("products")]
[Authorize]
[EnableRateLimiting(ApiPolicies.Requests)]
public sealed class ProductsController(ISender mediatr) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await mediatr.Send(new GetProductByIdQuery(id), cancellationToken);
        if (product is null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = ApiPolicies.ProductWrite)]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(128)] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await mediatr.Send(new CreateProductCommand(request.Name, request.Price, idempotencyKey,
            User.FindFirstValue("sub")!), cancellationToken);
        Response.Headers["Idempotency-Replayed"] = result.Replayed ? "true" : "false";
        return CreatedAtAction(nameof(GetById), new { id = result.Product.Id }, result.Product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApiPolicies.ProductWrite)]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Update(
        Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await mediatr.Send(new UpdateProductCommand(id, request.Name, request.Price), cancellationToken);
        if (product is null)
            return NotFound();

        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApiPolicies.ProductWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var removed = await mediatr.Send(new RemoveProductCommand(id), cancellationToken);
        if (!removed)
            return NotFound();

        return NoContent();
    }
}
