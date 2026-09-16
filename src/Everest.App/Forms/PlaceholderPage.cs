namespace Everest.App.Forms;

public class PlaceholderPage : UserControl
{
    public PlaceholderPage(string title, string note)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;
        Padding = new Padding(30, 26, 30, 20);

        var lblNote = new Label
        {
            Text = note,
            Font = AppTheme.UiFont,
            ForeColor = Color.Gray,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleRight,
            Height = 30
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleRight,
            Height = 40
        };

        Controls.Add(lblNote);
        Controls.Add(lblTitle);
    }
}