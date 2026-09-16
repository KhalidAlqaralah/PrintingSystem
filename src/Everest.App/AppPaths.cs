using Everest.App.Models;

namespace Everest.App;

public static class AppPaths
{
    public static string TemplatesRoot
    {
        get
        {
            var dir = @"C:\Everest\Templates";
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string FolderFor(TemplateCategory c)
    {
        var dir = Path.Combine(TemplatesRoot, Categories.Folder(c));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void EnsureFolders()
    {
        foreach (var c in Categories.All) FolderFor(c.Cat);
    }

    public static List<string> NamesIn(TemplateCategory c) =>
        Directory.GetFiles(FolderFor(c), "*.json")
                 .Select(Path.GetFileNameWithoutExtension)
                 .Where(n => !string.IsNullOrEmpty(n))
                 .Select(n => n!)
                 .OrderBy(n => n)
                 .ToList();
}