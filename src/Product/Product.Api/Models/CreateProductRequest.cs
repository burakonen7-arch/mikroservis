using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Product.Api.Models;

public sealed record CreateProductRequest(
    [Required] string Name,
    [property: JsonRequired] decimal Price);
