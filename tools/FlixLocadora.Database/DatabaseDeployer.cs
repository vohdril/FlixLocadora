using DbUp;
using DbUp.Engine.Output;
using Microsoft.Data.SqlClient;

namespace FlixLocadora.Database;

/// <summary>
/// Creates the database when missing and applies the SQL scripts in order (DbUp). Scripts are immutable once
/// applied: a change to the schema is a NEW script (0002_..., 0003_...), never an edit of an old one.
/// </summary>
public static class DatabaseDeployer
{
    public const string JournalSchema = "deploy";
    public const string JournalTable = "SchemaVersions";

    public static bool Deploy(string connectionString, string databaseFolder, bool seed, TextWriter output)
    {
        EnsureDatabase(connectionString);
        EnsureJournalSchema(connectionString);

        if (!Run(connectionString, Path.Combine(databaseFolder, "schema"), output))
            return false;

        return !seed || Run(connectionString, Path.Combine(databaseFolder, "seed"), output);
    }

    /// <summary>Drops the database (used by the tests for their throwaway databases).</summary>
    public static void Drop(string connectionString)
    {
        var name = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        ExecuteOnMaster(connectionString, """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; '
                                           + N'DROP DATABASE ' + QUOTENAME(@name) + N';';
                EXEC (@sql);
            END
            """, name);
    }

    private static bool Run(string connectionString, string folder, TextWriter output)
    {
        if (!Directory.Exists(folder))
            return true;

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsFromFileSystem(folder)
            .JournalToSqlTable(JournalSchema, JournalTable)
            .WithTransactionPerScript()
            .LogTo(new TextWriterLog(output))
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            output.WriteLine($"FAILED on {result.ErrorScript?.Name}: {result.Error?.Message}");

        return result.Successful;
    }

    // Done here instead of DbUp's EnsureDatabase so that no connection string is ever written to the log.
    private static void EnsureDatabase(string connectionString)
    {
        var name = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("The connection string must name the database (Database=... / Initial Catalog=...).");

        ExecuteOnMaster(connectionString, """
            IF DB_ID(@name) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name) + N';';
                EXEC (@sql);
            END
            """, name);
    }

    private static void EnsureJournalSchema(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"IF SCHEMA_ID(N'{JournalSchema}') IS NULL EXEC (N'CREATE SCHEMA {JournalSchema}');";
        command.ExecuteNonQuery();
    }

    private static void ExecuteOnMaster(string connectionString, string sql, string databaseName)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;
        using var connection = new SqlConnection(master);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@name", databaseName);
        command.ExecuteNonQuery();
    }

    private sealed class TextWriterLog(TextWriter output) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) { }

        public void LogDebug(string format, params object[] args) { }

        public void LogInformation(string format, params object[] args) => output.WriteLine(format, args);

        public void LogWarning(string format, params object[] args) => output.WriteLine("WARN  " + format, args);

        public void LogError(string format, params object[] args) => output.WriteLine("ERROR " + format, args);

        public void LogError(Exception ex, string format, params object[] args) => output.WriteLine("ERROR " + format + " — " + ex.Message, args);
    }
}
