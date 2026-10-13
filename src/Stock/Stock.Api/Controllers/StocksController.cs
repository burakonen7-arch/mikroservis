using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Api.Models;
using Stock.Application.Commands.AdjustStock;
using Stock.Application.DTOs;
using Stock.Application.Queries.GetStockByProductId;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Presentation.Security;

namespace Stock.Api.Controllers;

[ApiController]
[Route("stocks")]
[Authorize]
[EnableRateLimiting(ApiPolicies.Requests)]
public sealed class StocksController(ISender mediatr) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<StockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDto>> GetByProductId(Guid id, CancellationToken cancellationToken)
    {
        var stock = await mediatr.Send(new GetStockByProductIdQuery(id), cancellationToken);
        if (stock is null)
            return NotFound();

        return Ok(stock);
    }

    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Policy = ApiPolicies.StockWrite)]
    [ProducesResponseType<StockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockDto>> Adjust(
        Guid id, [FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var stock = await mediatr.Send(new AdjustStockCommand(id, request.Delta, request.ExpectedVersion), cancellationToken);
        if (stock is null)
            return NotFound();

        return Ok(stock);
    }
}
