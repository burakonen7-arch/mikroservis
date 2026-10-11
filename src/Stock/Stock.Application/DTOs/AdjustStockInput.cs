namespace Stock.Application.DTOs;

public sealed record AdjustStockInput(int Delta, long ExpectedVersion);
