namespace Everest.App.Models;

public enum EntityType : byte
{
    Doctor = 1,
    Hospital = 2
}

public class Entity
{
    public int EntityId { get; set; }
    public EntityType Type { get; set; }
    public string Name { get; set; } = "";
    public string LicenseNumber { get; set; } = "";
    public string? Major { get; set; }
    public string? Address { get; set; }
    public int LastSequence { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public string? Phone1 { get; set; }   // doctor mobile / hospital mobile
    public string? Phone2 { get; set; }
    public string? ClinicPhone1 { get; set; }
    public string? ClinicPhone2 { get; set; }

    public string TypeLabel => Type == EntityType.Doctor ? "طبيب" : "مستشفى";
}