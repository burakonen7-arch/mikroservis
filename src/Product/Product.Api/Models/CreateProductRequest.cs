using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Product.Api.Models;

public sealed record CreateProductRequest(
    [property: Required] string Name,
    [property: JsonRequired] decimal Price);
