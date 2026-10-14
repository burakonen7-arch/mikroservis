using Microsoft.AspNetCore.Mvc;
using User.Api.Models;
using User.Application.Commands;
using User.Application.DTOs;
using User.Application.Queries;
using User.Application.Services;

namespace User.Api.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(
        [FromQuery] string? search, [FromQuery] string? city, [FromQuery] int? minAge, [FromQuery] int? maxAge,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = new UserSearchQuery(search, city, minAge, maxAge, page, pageSize);
        return Ok(await users.GetUsersAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(request.FirstName, request.LastName, request.Email, request.Age!.Value, request.City);
        var user = await users.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }
}
