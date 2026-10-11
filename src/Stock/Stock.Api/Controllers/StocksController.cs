using Microsoft.AspNetCore.Mvc;
using Stock.Api.Models;
using Stock.Application.Abstractions;
using Stock.Application.DTOs;

namespace Stock.Api.Controllers;

[ApiController]
[Route("stocks")]
public sealed class StocksController(IStockService stocks) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<StockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDto>> GetByProductId(Guid id, CancellationToken cancellationToken)
    {
        var stock = await stocks.GetByProductIdAsync(id, cancellationToken);
        if (stock is null)
            return NotFound();

        return Ok(stock);
    }

    [HttpPost("{id:guid}/adjustments")]
    [ProducesResponseType<StockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockDto>> Adjust(
        Guid id, [FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var stock = await stocks.AdjustAsync(id, new AdjustStockInput(request.Delta, request.ExpectedVersion), cancellationToken);
        if (stock is null)
            return NotFound();

        return Ok(stock);
    }
}
