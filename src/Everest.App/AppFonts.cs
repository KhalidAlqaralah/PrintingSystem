using System.Drawing.Text;
using Everest.App.Data;

namespace Everest.App;

public static class AppFonts
{
    public const string ArabicFamily = "Sakkal Majalla";
    public const int NotebookCapacityDisplay = 50;

    private static PrivateFontCollection? _pfc;
    private static readonly Dictionary<string, FontFamily> Private = new(StringComparer.OrdinalIgnoreCase);

    private static string? _printFamily;

    public static string PrintFamily
    {
        get => _printFamily ??= SettingsRepository.Get("PrintFont") ?? ArabicFamily;
        set
        {
            _printFamily = value;
            SettingsRepository.Set("PrintFont", value);
        }
    }

    public static string AssetsFolder
    {
        get
        {
            var dir = @"C:\Everest\Assets";
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static void Load()
    {
        try
        {
            _pfc = new PrivateFontCollection();

            foreach (var file in Directory.GetFiles(AssetsFolder, "*.ttf")
                                          .Concat(Directory.GetFiles(AssetsFolder, "*.otf")))
            {
                try { _pfc.AddFontFile(file); } catch { }
            }

            foreach (var fam in _pfc.Families)
                Private[fam.Name] = fam;
        }
        catch { }
    }

    public static bool HasPrivate(string family) => Private.ContainsKey(family);

    /// <summary>Private font if bundled, otherwise the installed one, otherwise Arial.</summary>
    public static Font Create(string family, float size, FontStyle style,
                              GraphicsUnit unit = GraphicsUnit.Point)
    {
        if (size <= 0) size = 8f;

        if (Private.TryGetValue(family, out var pf))
        {
            var s = pf.IsStyleAvailable(style) ? style
                  : pf.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular
                  : style;
            try { return new Font(pf, size, s, unit); } catch { }
        }

        try { return new Font(family, size, style, unit); }
        catch { return new Font("Arial", size, style, unit); }
    }

    private static List<string>? _availableFamilies;

    /// <summary>Family names to offer in the designer — bundled first.</summary>
    public static List<string> AvailableFamilies()
    {
        if (_availableFamilies != null) return _availableFamilies;

        var list = Private.Keys.OrderBy(n => n).ToList();
        list.AddRange(FontFamily.Families.Select(f => f.Name)
                      .Where(n => !Private.ContainsKey(n)).OrderBy(n => n));

        _availableFamilies = list;
        return list;
    }

    /// <summary>Absolute path of an asset file, or null.</summary>
    public static string? Asset(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var p = Path.Combine(AssetsFolder, fileName);
        return File.Exists(p) ? p : null;
    }
}