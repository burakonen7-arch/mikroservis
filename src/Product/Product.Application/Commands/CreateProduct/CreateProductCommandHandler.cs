using Product.Application.Abstractions;
using Product.Application.DTOs;
using Product.Application.Events;
using Product.Domain.Entities;
using Shared.Application.Abstractions;
using MediatR;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shared.Application.Exceptions;

namespace Product.Application.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(
    IProductWriteRepository products,
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    IProductCreationReceiptStore receipts) : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128 ||
            command.IdempotencyKey.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.' and not ':'))
            throw new ArgumentException("Idempotency-Key must contain 1-128 ASCII letters, digits, dots, colons, underscores or hyphens.");
      
        if (string.IsNullOrWhiteSpace(command.RequestOwner))
            throw new ArgumentException("An authenticated request owner is required.");

        // Validate and normalize the business input before fingerprinting it.
        var product = new ProductItem(Guid.NewGuid(), command.Name, command.Price);
        var receiptId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.RequestOwner + "\0" + command.IdempotencyKey)));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            product.Name,
            Price = product.Price.ToString("G29", CultureInfo.InvariantCulture)
        }))));
        
        var existing = await receipts.FindAsync(receiptId, cancellationToken);
        if (existing is not null) 
            return Replay(existing, fingerprint);

        products.Add(product);
        outbox.Add(new ProductCreatedEvent(product.Id, product.Name));
        
        var result = ProductDto.FromEntity(product);
        receipts.Add(new ProductCreationReceipt(receiptId, fingerprint, result, DateTime.UtcNow));
        
        try
        {
            // Product, event and unique receipt are committed atomically.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            // A concurrent request may already have committed the same receipt.
            existing = await receipts.FindAsync(receiptId, cancellationToken);
            if (existing is not null) return Replay(existing, fingerprint);
            throw new IdempotencyConflictException("A request with this Idempotency-Key is being committed. Retry the same request with the same key.");
        }

        return new CreateProductResult(result, false);
    }

    private static CreateProductResult Replay(ProductCreationReceipt receipt, string fingerprint)
    {
        if (receipt.Fingerprint != fingerprint)
            throw new IdempotencyConflictException("This Idempotency-Key was already used with different product data.");
        return new CreateProductResult(receipt.Result, true);
    }
}

// CreateProductCommandHandler: Yeni ürünü oluşturur ve ProductCreatedEvent olayını outbox akışına ekler.
// Ürün ile olay kaydını aynı unit of work üzerinden kaydeder; olay daha sonra RabbitMQ üzerinden yayımlanır.
