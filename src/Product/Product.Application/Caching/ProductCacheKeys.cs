namespace Product.Application.Caching;

internal static class ProductCacheKeys
{
    public static string ById(Guid id) => $"product:{id}";
}
