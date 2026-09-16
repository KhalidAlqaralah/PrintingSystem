using Everest.App.Forms;

namespace Everest.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppFonts.Load();
        Application.Run(new MainForm());
    }
}