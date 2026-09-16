using Everest.App.Models;

namespace Everest.App.Printing;

public class PrintData
{
    public Entity Entity { get; init; } = null!;
    public string Serial { get; init; } = "";
    public string NotebookNumber { get; init; } = "";
    public string NoteNumber { get; init; } = "";
    public string NoteDate { get; init; } = "";

    public string Value(string token, string staticText)
    {
        bool hosp = Entity.Type == EntityType.Hospital;

        return token switch
        {
            "Name"         => Entity.Name,
            "Major"        => Entity.Major ?? "",
            "Address"      => Entity.Address ?? "",
            "AddressLine1" => SplitAddress(Entity.Address).Line1,
            "AddressLine2" => SplitAddress(Entity.Address).Line2,
            "License"      => Entity.LicenseNumber,
            "Serial"         => Serial,
            "NotebookNumber" => NotebookNumber,
            "NoteNumber"   => NoteNumber,
            "NoteDate"     => NoteDate,
            "Phone1"       => Labeled(hosp ? "موبايل المستشفى" : "هاتف الدكتور", Entity.Phone1),
            "Phone2"       => Labeled(hosp ? "موبايل المستشفى" : "هاتف الدكتور", Entity.Phone2),
            "ClinicPhone1" => Labeled(hosp ? "هاتف المستشفى" : "هاتف العيادة", Entity.ClinicPhone1),
            "ClinicPhone2" => Labeled(hosp ? "هاتف المستشفى" : "هاتف العيادة", Entity.ClinicPhone2),
            _              => staticText
        };
    }

    private static string Labeled(string label, string? number) =>
        string.IsNullOrWhiteSpace(number) ? "" : $"{label} : {number}";

    /// <summary>
    /// Splits at the word boundary nearest the midpoint, rounding up:
    /// if the midpoint lands inside a word, that whole word stays on line 1.
    /// </summary>
    public static (string Line1, string Line2) SplitAddress(string? address)
    {
        string s = (address ?? "").Trim();
        if (s.Length == 0) return ("", "");

        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return (s, "");

        int mid = s.Length / 2;
        int best = -1, run = 0;

        for (int i = 0; i < words.Length - 1; i++)
        {
            run += words[i].Length + 1;      // consumed through this word + its space
            if (run >= mid) { best = i; break; }
        }

        if (best < 0) best = words.Length - 2;

        string l1 = string.Join(' ', words.Take(best + 1));
        string l2 = string.Join(' ', words.Skip(best + 1));
        return (l1, l2);
    }
}