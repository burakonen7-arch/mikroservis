using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Stock.Api.Models;

public sealed record StockAdjustmentRequest(
    [property: JsonRequired] int Delta,
    [property: JsonRequired, Range(1, long.MaxValue)] long ExpectedVersion);
