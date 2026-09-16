namespace Everest.App.Models;

public class Order
{
    public int OrderId { get; set; }
    public int EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public int NotebookCount { get; set; }
    public int AppointmentsPerNotebook { get; set; }
    public string StartSerial { get; set; } = "";
    public string EndSerial { get; set; } = "";
    public int TotalAppointments { get; set; }
    public int? DeliveryNoteId { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string? LastPrintedSerial { get; set; }
    public DateTime CreatedAt { get; set; }
}