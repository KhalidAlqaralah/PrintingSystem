using Microsoft.Data.SqlClient;
using Everest.App.Models;

namespace Everest.App.Data;

public static class EntityRepository
{
    private static readonly Dictionary<int, Entity> Cache = new();

    public static List<Entity> GetAll(bool includeInactive = false)
    {
        var list = new List<Entity>();
        var map = new Dictionary<int, Entity>();

        using var conn = Db.Open();

        var sql = @"SELECT EntityId, EntityType, Name, LicenseNumber, Major, Address,
                           LastSequence, IsActive, CreatedAt
                    FROM Entities" + (includeInactive ? "" : " WHERE IsActive = 1") +
                  " ORDER BY Name;";

        using (var cmd = new SqlCommand(sql, conn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var e = new Entity
                {
                    EntityId      = r.GetInt32(0),
                    Type          = (EntityType)r.GetByte(1),
                    Name          = r.GetString(2),
                    LicenseNumber = r.GetString(3),
                    Major         = Db.Str(r.GetValue(4)),
                    Address       = Db.Str(r.GetValue(5)),
                    LastSequence  = r.GetInt32(6),
                    IsActive      = r.GetBoolean(7),
                    CreatedAt     = r.GetDateTime(8)
                };
                list.Add(e);
                map[e.EntityId] = e;
            }
        }

        using (var cmd = new SqlCommand(
            "SELECT EntityId, PhoneType, SlotIndex, PhoneNumber FROM EntityPhones;", conn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                if (!map.TryGetValue(r.GetInt32(0), out var e)) continue;
                byte type = r.GetByte(1), slot = r.GetByte(2);
                string num = r.GetString(3);

                if (type == 1 && slot == 1) e.Phone1 = num;
                else if (type == 1 && slot == 2) e.Phone2 = num;
                else if (type == 2 && slot == 1) e.ClinicPhone1 = num;
                else if (type == 2 && slot == 2) e.ClinicPhone2 = num;
            }
        }

        foreach (var e in list) Cache[e.EntityId] = e;

        return list;
    }

    public static Entity? GetById(int entityId)
    {
        if (Cache.TryGetValue(entityId, out var hit)) return hit;

        var e = LoadOne(entityId);
        if (e != null) Cache[entityId] = e;
        return e;
    }

    private static Entity? LoadOne(int entityId)
    {
        using var conn = Db.Open();
        Entity? e = null;

        using (var cmd = new SqlCommand(
            @"SELECT EntityId, EntityType, Name, LicenseNumber, Major, Address,
                     LastSequence, IsActive, CreatedAt
              FROM Entities WHERE EntityId = @id;", conn))
        {
            cmd.Parameters.AddWithValue("@id", entityId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                e = new Entity
                {
                    EntityId      = r.GetInt32(0),
                    Type          = (EntityType)r.GetByte(1),
                    Name          = r.GetString(2),
                    LicenseNumber = r.GetString(3),
                    Major         = Db.Str(r.GetValue(4)),
                    Address       = Db.Str(r.GetValue(5)),
                    LastSequence  = r.GetInt32(6),
                    IsActive      = r.GetBoolean(7),
                    CreatedAt     = r.GetDateTime(8)
                };
            }
        }

        if (e == null) return null;

        using (var cmd = new SqlCommand(
            "SELECT PhoneType, SlotIndex, PhoneNumber FROM EntityPhones WHERE EntityId = @id;", conn))
        {
            cmd.Parameters.AddWithValue("@id", entityId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                byte type = r.GetByte(0), slot = r.GetByte(1);
                string num = r.GetString(2);

                if (type == 1 && slot == 1) e.Phone1 = num;
                else if (type == 1 && slot == 2) e.Phone2 = num;
                else if (type == 2 && slot == 1) e.ClinicPhone1 = num;
                else if (type == 2 && slot == 2) e.ClinicPhone2 = num;
            }
        }

        return e;
    }

    public static int Insert(Entity e)
    {
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = new SqlCommand(
            @"INSERT INTO Entities (EntityType, Name, LicenseNumber, Major, Address)
              VALUES (@t, @n, @l, @m, @a);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
        {
            cmd.Parameters.AddWithValue("@t", (byte)e.Type);
            cmd.Parameters.AddWithValue("@n", e.Name);
            cmd.Parameters.AddWithValue("@l", e.LicenseNumber);
            cmd.Parameters.AddWithValue("@m", (object?)e.Major ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", (object?)e.Address ?? DBNull.Value);
            e.EntityId = (int)cmd.ExecuteScalar();
        }

        SavePhones(conn, tx, e);
        tx.Commit();
        Cache.Remove(e.EntityId);
        return e.EntityId;
    }

    public static void Update(Entity e)
    {
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = new SqlCommand(
            @"UPDATE Entities SET EntityType=@t, Name=@n, LicenseNumber=@l,
                     Major=@m, Address=@a, IsActive=@act
              WHERE EntityId=@id;", conn, tx))
        {
            cmd.Parameters.AddWithValue("@t", (byte)e.Type);
            cmd.Parameters.AddWithValue("@n", e.Name);
            cmd.Parameters.AddWithValue("@l", e.LicenseNumber);
            cmd.Parameters.AddWithValue("@m", (object?)e.Major ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", (object?)e.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@act", e.IsActive);
            cmd.Parameters.AddWithValue("@id", e.EntityId);
            cmd.ExecuteNonQuery();
        }

        using (var del = new SqlCommand(
            "DELETE FROM EntityPhones WHERE EntityId=@id;", conn, tx))
        {
            del.Parameters.AddWithValue("@id", e.EntityId);
            del.ExecuteNonQuery();
        }

        SavePhones(conn, tx, e);
        tx.Commit();
        Cache.Remove(e.EntityId);
    }

    public static bool HasOrders(int entityId)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Orders WHERE EntityId = @id;", conn);
        cmd.Parameters.AddWithValue("@id", entityId);
        return (int)cmd.ExecuteScalar() > 0;
    }

    public static void Delete(int entityId)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand("DELETE FROM Entities WHERE EntityId=@id;", conn);
        cmd.Parameters.AddWithValue("@id", entityId);
        cmd.ExecuteNonQuery();
        Cache.Remove(entityId);
    }

    public static void SetActive(int entityId, bool active)
    {
        using var conn = Db.Open();
        using var cmd = new SqlCommand(
            "UPDATE Entities SET IsActive=@a WHERE EntityId=@id;", conn);
        cmd.Parameters.AddWithValue("@a", active);
        cmd.Parameters.AddWithValue("@id", entityId);
        cmd.ExecuteNonQuery();
        Cache.Remove(entityId);
    }

    private static void SavePhones(SqlConnection conn, SqlTransaction tx, Entity e)
    {
        void Add(byte type, byte slot, string? number)
        {
            if (string.IsNullOrWhiteSpace(number)) return;
            using var cmd = new SqlCommand(
                @"INSERT INTO EntityPhones (EntityId, PhoneType, PhoneNumber, SlotIndex)
                  VALUES (@e, @t, @p, @s);", conn, tx);
            cmd.Parameters.AddWithValue("@e", e.EntityId);
            cmd.Parameters.AddWithValue("@t", type);
            cmd.Parameters.AddWithValue("@p", number.Trim());
            cmd.Parameters.AddWithValue("@s", slot);
            cmd.ExecuteNonQuery();
        }

        Add(1, 1, e.Phone1);
        Add(1, 2, e.Phone2);
        Add(2, 1, e.ClinicPhone1);
        Add(2, 2, e.ClinicPhone2);
    }
}