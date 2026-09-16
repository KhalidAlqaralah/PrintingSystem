using Everest.App.Models;

namespace Everest.App.Printing;

public class NoteRow
{
    public int RowNumber { get; set; }
    public string EntityName { get; set; } = "";
    public string Phones { get; set; } = "";
    public int Notebooks { get; set; }
    public string StartSerial { get; set; } = "";
    public string EndSerial { get; set; } = "";

    public string Value(string token) => token switch
    {
        "RowNumber"   => RowNumber.ToString(),
        "EntityName"  => EntityName,
        "Phones"      => Phones,
        "Notebooks"   => Notebooks.ToString(),
        "StartSerial" => StartSerial,
        "EndSerial"   => EndSerial,
        _             => ""
    };
}

public class DeliveryNoteData
{
    public string NoteNumber { get; set; } = "";
    public DateTime NoteDate { get; set; } = DateTime.Today;
    public List<NoteRow> Rows { get; set; } = new();

    public int TotalNotebooks => Rows.Sum(r => r.Notebooks);

    public static DeliveryNoteData Build(IEnumerable<Order> orders,
                                         Func<int, Entity?> lookup,
                                         string noteNumber, DateTime date)
    {
        var d = new DeliveryNoteData { NoteNumber = noteNumber, NoteDate = date };
        int n = 1;

        foreach (var o in orders.OrderBy(x => x.PrintedAt ?? x.CreatedAt).ThenBy(x => x.OrderId))
        {
            var ent = lookup(o.EntityId);
            bool hosp = ent?.Type == EntityType.Hospital;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ent?.ClinicPhone1))
                parts.Add($"{(hosp ? "هاتف المستشفى" : "هاتف العيادة")} : {ent!.ClinicPhone1}");
            if (!string.IsNullOrWhiteSpace(ent?.Phone1))
                parts.Add($"{(hosp ? "موبايل المستشفى" : "هاتف الدكتور")} : {ent!.Phone1}");

            d.Rows.Add(new NoteRow
            {
                RowNumber = n++,
                EntityName = o.EntityName,
                Phones = string.Join("    ", parts),
                Notebooks = o.NotebookCount,
                StartSerial = o.StartSerial,
                EndSerial = o.EndSerial
            });
        }

        return d;
    }
}