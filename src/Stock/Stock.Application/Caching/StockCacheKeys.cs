namespace Stock.Application.Caching;

internal static class StockCacheKeys
{
    public static string ByProductId(Guid productId) => $"stock:{productId}";
}
