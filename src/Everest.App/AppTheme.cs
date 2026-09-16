namespace Everest.App;

public static class AppTheme
{
    public const string DateFormat     = "dd/MM/yyyy";
    public const string DateTimeFormat = "dd/MM/yyyy HH:mm";

    public static readonly Color Sidebar    = Color.FromArgb(31, 58, 78);
    public static readonly Color SidebarHot = Color.FromArgb(45, 82, 108);
    public static readonly Color Accent     = Color.FromArgb(62, 138, 178);
    public static readonly Color Surface    = Color.FromArgb(246, 248, 250);
    public static readonly Color Line       = Color.FromArgb(214, 221, 228);

    public static readonly Font UiFont     = new("Segoe UI", 10F);
    public static readonly Font UiFontBold = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font TitleFont  = new("Segoe UI", 15F, FontStyle.Bold);

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = Color.White;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Sidebar;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = UiFontBold;
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        g.ColumnHeadersHeight = 38;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.RowTemplate.Height = 32;
        g.DefaultCellStyle.Font = UiFont;
        g.DefaultCellStyle.SelectionBackColor = Accent;
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        g.AlternatingRowsDefaultCellStyle.BackColor = Surface;
        g.GridColor = Line;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.ReadOnly = true;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.MultiSelect = false;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }
}