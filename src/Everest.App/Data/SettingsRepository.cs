using Microsoft.Data.SqlClient;

namespace Everest.App.Data;

public static class SettingsRepository
{
    public static string? Get(string key)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand("SELECT [Value] FROM Settings WHERE [Key]=@k;", conn);
        cmd.Parameters.AddWithValue("@k", key);
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? null : (string)v;
    }

    public static void Set(string key, string? value)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand(
            @"MERGE Settings AS t
              USING (SELECT @k AS [Key]) AS s ON t.[Key] = s.[Key]
              WHEN MATCHED THEN UPDATE SET [Value] = @v
              WHEN NOT MATCHED THEN INSERT ([Key],[Value]) VALUES (@k, @v);", conn);
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", (object?)value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}