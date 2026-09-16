using System.Text.Json;
using Everest.App.Models;

namespace Everest.App.Data;

public static class TemplateStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string JsonPath(TemplateCategory c, string name) =>
        Path.Combine(AppPaths.FolderFor(c), name + ".json");

    /// <summary>Absolute path of the artwork file, or null when there isn't one.</summary>
    public static string? ArtworkPath(TemplateDef t)
    {
        if (string.IsNullOrWhiteSpace(t.PdfFile)) return null;
        var p = Path.Combine(AppPaths.FolderFor(t.Category), t.PdfFile);
        return File.Exists(p) ? p : null;
    }

    public static List<string> Names(TemplateCategory c) => AppPaths.NamesIn(c);

    public static TemplateDef? Load(TemplateCategory c, string name)
    {
        var p = JsonPath(c, name);
        if (!File.Exists(p)) return null;
        var t = JsonSerializer.Deserialize<TemplateDef>(File.ReadAllText(p));
        if (t != null) { t.Name = name; t.Category = c; }
        return t;
    }

    public static List<TemplateDef> LoadCategory(TemplateCategory c)
    {
        var list = new List<TemplateDef>();
        foreach (var n in Names(c))
        {
            try { var t = Load(c, n); if (t != null) list.Add(t); } catch { }
        }
        return list;
    }

    public static void Save(TemplateDef t) =>
        File.WriteAllText(JsonPath(t.Category, t.Name), JsonSerializer.Serialize(t, Opts));

    public static void Delete(TemplateCategory c, string name)
    {
        var p = JsonPath(c, name);
        if (File.Exists(p)) File.Delete(p);
    }

    public static bool Exists(TemplateCategory c, string name) => File.Exists(JsonPath(c, name));

    /// <summary>Artwork files in a folder that have no .json companion yet.</summary>
    public static List<string> OrphanArtwork(TemplateCategory c)
    {
        var dir = AppPaths.FolderFor(c);
        var jsons = Names(c).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Directory.GetFiles(dir)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".pdf" or ".png" or ".jpg" or ".jpeg")
            .Where(f => !jsons.Contains(Path.GetFileNameWithoutExtension(f)))
            .Select(Path.GetFileName)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
    }
}