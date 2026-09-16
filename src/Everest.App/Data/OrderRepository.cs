using Microsoft.Data.SqlClient;
using System.Data;
using Everest.App.Models;

namespace Everest.App.Data;

public static class OrderRepository
{
    public static Order Create(int entityId, int notebookCount, int appointmentsPerNotebook = 3)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand("sp_CreateOrder", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@NotebookCount", notebookCount);
        cmd.Parameters.AddWithValue("@AppointmentsPerNotebook", appointmentsPerNotebook);

        var outParam = new SqlParameter("@OrderId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outParam);
        cmd.ExecuteNonQuery();

        int newId = (int)outParam.Value;
        return GetById(newId)!;
    }

    public static void MarkPrinted(int orderId, string? lastSerial)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand(
            "UPDATE Orders SET PrintedAt = SYSDATETIME(), LastPrintedSerial = @s WHERE OrderId = @id;", conn);
        cmd.Parameters.AddWithValue("@id", orderId);
        cmd.Parameters.AddWithValue("@s", (object?)lastSerial ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static int AppointmentsPerNotebook
    {
        get
        {
            using var conn = Db.Open();
            using var cmd = new SqlCommand(
                "SELECT [Value] FROM Settings WHERE [Key]='AppointmentsPerNotebook';", conn);
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? 3 : int.Parse((string)v);
        }
    }

    public static void Update(int orderId, int entityId, int notebookCount)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand("sp_UpdateOrder", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@NotebookCount", notebookCount);
        cmd.ExecuteNonQuery();
    }

    public static Order? GetById(int orderId) =>
        Query("WHERE o.OrderId = @id", ("@id", orderId)).FirstOrDefault();

    public static List<Order> GetAll() =>
        Query("ORDER BY o.OrderId DESC");

    public static List<Order> GetByDateRange(DateTime from, DateTime to) =>
        Query("WHERE o.CreatedAt >= @f AND o.CreatedAt < @t ORDER BY o.OrderId DESC",
              ("@f", from.Date), ("@t", to.Date.AddDays(1)));

    public static void Delete(int orderId)
    {
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();

        int entityId, total;
        string endSerial;

        using (var cmd = new SqlCommand(
            "SELECT EntityId, TotalAppointments, EndSerial, PrintedAt FROM Orders WHERE OrderId=@id;", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", orderId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) { tx.Rollback(); return; }
            if (!r.IsDBNull(3)) { r.Close(); tx.Rollback(); throw new InvalidOperationException("PRINTED"); }
            entityId  = r.GetInt32(0);
            total     = r.GetInt32(1);
            endSerial = r.GetString(2);
        }

        // only roll the sequence back if this is the newest order for that entity
        using (var cmd = new SqlCommand(
            @"UPDATE Entities SET LastSequence = LastSequence - @t
              WHERE EntityId = @e
                AND LastSequence = CAST(RIGHT(@end, 6) AS INT);", conn, tx))
        {
            cmd.Parameters.AddWithValue("@t", total);
            cmd.Parameters.AddWithValue("@e", entityId);
            cmd.Parameters.AddWithValue("@end", endSerial);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = new SqlCommand("DELETE FROM Orders WHERE OrderId=@id;", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", orderId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public static List<Order> GetByEntity(int entityId) =>
        Query("WHERE o.EntityId = @e ORDER BY o.OrderId DESC", ("@e", entityId));

    private static List<Order> Query(string where, params (string, object)[] args)
    {
        var list = new List<Order>();
        using var conn = Db.Open();
        using var cmd = new SqlCommand(
            @"SELECT o.OrderId, o.EntityId, e.Name, o.NotebookCount, o.AppointmentsPerNotebook,
                     o.StartSerial, o.EndSerial, o.TotalAppointments, o.DeliveryNoteId,
                     o.PrintedAt, o.LastPrintedSerial, o.CreatedAt
              FROM Orders o
              JOIN Entities e ON e.EntityId = o.EntityId " + where, conn);

        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Order
            {
                OrderId                 = r.GetInt32(0),
                EntityId                = r.GetInt32(1),
                EntityName              = r.GetString(2),
                NotebookCount           = r.GetInt32(3),
                AppointmentsPerNotebook = r.GetInt32(4),
                StartSerial             = r.GetString(5),
                EndSerial               = r.GetString(6),
                TotalAppointments       = r.GetInt32(7),
                DeliveryNoteId          = r.IsDBNull(8) ? null : r.GetInt32(8),
                PrintedAt               = r.IsDBNull(9) ? null : r.GetDateTime(9),
                LastPrintedSerial       = Db.Str(r.GetValue(10)),
                CreatedAt               = r.GetDateTime(11)
            });
        }
        return list;
    }
}