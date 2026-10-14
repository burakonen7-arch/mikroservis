namespace User.Infrastructure.Sql;

internal static class SqlQueries
{
    public static readonly string GetUsers = Read("GetUsers.sql");
    public static readonly string GetUserById = Read("GetUserById.sql");

    private static string Read(string fileName)
    {
        using var stream = typeof(SqlQueries).Assembly.GetManifestResourceStream(
            $"User.Infrastructure.Sql.Queries.{fileName}")
            ?? throw new InvalidOperationException($"SQL resource {fileName} was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
