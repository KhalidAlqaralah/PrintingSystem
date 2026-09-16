using Microsoft.Data.SqlClient;

namespace Everest.App.Data;

public static class Db
{
    public static string ConnectionString { get; set; } =
        @"Server=.\SQLEXPRESS;Database=Everest;Trusted_Connection=True;TrustServerCertificate=True;";

    public static SqlConnection Open()
    {
        var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public static string? Str(object value) =>
        value == DBNull.Value ? null : (string)value;
}