using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

internal static partial class DatabaseInitializer
{
    private static readonly string[] Scripts = ["schema.sql", "procedures.sql", "seed.sql"];

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BatchSeparator();

    public static async Task ApplyAsync(string connectionString, string scriptsDirectory, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var scriptName in Scripts)
        {
            var script = await File.ReadAllTextAsync(Path.Combine(scriptsDirectory, scriptName), cancellationToken);

            foreach (var batch in BatchSeparator().Split(script))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}
