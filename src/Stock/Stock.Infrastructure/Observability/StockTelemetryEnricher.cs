using System.Diagnostics;
using BuildingBlocks.Observability;
using Stock.Application.Commands.AdjustStock;
using Stock.Application.Commands.InitializeStock;
using Stock.Application.DTOs;
using Stock.Application.Queries.GetStockByProductId;

namespace Stock.Infrastructure.Observability;

public sealed class StockTelemetryEnricher : IRequestTelemetryEnricher
{
    public void Enrich(Activity? activity, object request, object? response)
    {
        switch (request)
        {
            case GetStockByProductIdQuery query: activity?.SetTag("product.id", query.ProductId); break;
            case InitializeStockCommand command: activity?.SetTag("product.id", command.ProductId); break;
            case AdjustStockCommand command:
                activity?.SetTag("product.id", command.ProductId);
                activity?.SetTag("stock.delta", command.Delta);
                activity?.SetTag("stock.expected_version", command.ExpectedVersion);
                break;
        }
        if (response is StockDto stock)
        {
            activity?.SetTag("product.id", stock.ProductId);
            activity?.SetTag("stock.quantity", stock.Quantity);
            activity?.SetTag("stock.version", stock.Version);
        }
    }
}
