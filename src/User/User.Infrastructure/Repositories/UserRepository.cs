using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using User.Application.Abstractions;
using User.Application.DTOs;
using User.Application.Exceptions;
using User.Application.Queries;
using User.Domain.Entities;
using User.Infrastructure.Sql;

namespace User.Infrastructure.Repositories;

public sealed class UserRepository(UserDbContext database) : IUserRepository
{
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(UserSearchQuery query, CancellationToken cancellationToken)
    {
        var users = await database.Users.FromSqlRaw(SqlQueries.GetUsers,
                Parameter("search", NpgsqlDbType.Text, query.Search),
                Parameter("city", NpgsqlDbType.Text, query.City),
                Parameter("minAge", NpgsqlDbType.Integer, query.MinAge),
                Parameter("maxAge", NpgsqlDbType.Integer, query.MaxAge),
                Parameter("pageSize", NpgsqlDbType.Integer, query.PageSize),
                Parameter("offset", NpgsqlDbType.Bigint, ((long)query.Page - 1) * query.PageSize))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return users.Select(UserDto.FromEntity).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var users = await database.Users.FromSqlRaw(SqlQueries.GetUserById,
                Parameter("id", NpgsqlDbType.Bigint, id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var user = users.SingleOrDefault();
        return user is null ? null : UserDto.FromEntity(user);
    }

    public async Task<UserDto> CreateAsync(UserAccount user, CancellationToken cancellationToken)
    {
        database.Users.Add(user);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return UserDto.FromEntity(user);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_users_email" })
        {
            throw new DuplicateEmailException(exception);
        }
    }

    private static NpgsqlParameter Parameter(string name, NpgsqlDbType type, object? value) =>
        new(name, type) { Value = value ?? DBNull.Value };
}
