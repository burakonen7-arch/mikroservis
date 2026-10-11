using Xunit;
using Product.Domain;
using Stock.Domain;
namespace Domain.Tests;

public class RulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ProductRejectsEmptyName(string name) => Assert.Throws<ArgumentException>(() => new ProductItem(Guid.NewGuid(), name, 10));
    [Theory]
    [InlineData(-1)]
    [InlineData(1.123)]
    public void ProductRejectsInvalidPrice(double price) => Assert.Throws<ArgumentException>(() => new ProductItem(Guid.NewGuid(), "test", (decimal)price));
    [Fact]
    public void ProjectionIgnoresDuplicateAndOutOfOrderEvents()
    {
        var p = new ProductItem(Guid.NewGuid(), "test", 1); Assert.True(p.ApplyStock(7, 3));
        Assert.False(p.ApplyStock(2, 2)); Assert.False(p.ApplyStock(9, 3)); Assert.Equal(7, p.AvailableQuantity); Assert.Equal(3, p.StockVersion);
    }
    [Fact]
    public void StockCannotGoNegativeAndFailureDoesNotMutateState()
    {
        var stock = new StockItem(Guid.NewGuid()); stock.Adjust(5); Assert.Throws<ArgumentException>(() => stock.Adjust(-6)); Assert.Equal(5, stock.Quantity); Assert.Equal(2, stock.Version);
    }
    [Fact] public void StockRejectsOverflow() { var stock = new StockItem(Guid.NewGuid()); stock.Adjust(int.MaxValue); Assert.Throws<OverflowException>(() => stock.Adjust(1)); Assert.Equal(int.MaxValue, stock.Quantity); }
}
