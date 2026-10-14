using System.Diagnostics;
using BuildingBlocks.Observability;
using Product.Application.Commands.ApplyStockProjection;
using Product.Application.Commands.RemoveProduct;
using Product.Application.Commands.UpdateProduct;
using Product.Application.DTOs;
using Product.Application.Queries.GetProductById;

namespace Product.Infrastructure.Observability;

public sealed class ProductTelemetryEnricher : IRequestTelemetryEnricher
{
    public void Enrich(Activity? activity, object request, object? response)
    {
        switch (request)
        {
            case GetProductByIdQuery query: activity?.SetTag("product.id", query.Id); break;
            case RemoveProductCommand command: activity?.SetTag("product.id", command.Id); break;
            case UpdateProductCommand command: activity?.SetTag("product.id", command.Id); break;
            case ApplyStockProjectionCommand command:
                activity?.SetTag("product.id", command.ProductId);
                activity?.SetTag("stock.quantity", command.Quantity);
                activity?.SetTag("stock.version", command.Version);
                break;
        }
        if (response is ProductDto product)
        {
            activity?.SetTag("product.id", product.Id);
            activity?.SetTag("product.name", product.Name);
            activity?.SetTag("product.price", (double)product.Price);
            activity?.SetTag("stock.quantity", product.AvailableQuantity);
            activity?.SetTag("stock.version", product.StockVersion);
        }
    }
}
