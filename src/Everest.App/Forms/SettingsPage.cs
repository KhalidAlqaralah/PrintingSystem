using System.Drawing.Printing;
using Everest.App.Data;
using Everest.App.Printing;

namespace Everest.App.Forms;

public class SettingsPage : UserControl
{
    private sealed class DocRow
    {
        public string Label = "";
        public string PrinterKey = "";
        public string TrayKey = "";
        public readonly ComboBox Printer = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        public readonly ComboBox Tray    = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    }

    private const string AutoTray = "(تلقائي)";

    private readonly DocRow[] _rows =
    {
        new() { Label = "الغلاف",      PrinterKey = "PrinterCover",  TrayKey = "TrayCover"  },
        new() { Label = "الوصفات",     PrinterKey = "PrinterBody",   TrayKey = "TrayBody"   },
        new() { Label = "العينة",      PrinterKey = "PrinterSample", TrayKey = "TraySample" },
        new() { Label = "سند التسليم", PrinterKey = "PrinterNote",   TrayKey = "TrayNote"   },
        new() { Label = "التقارير",    PrinterKey = "PrinterReport", TrayKey = "TrayReport" },
    };

    private readonly CheckBox _showAllTrays = new() { Text = "إظهار كل الأدراج", AutoSize = true,
                                                       Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };

    private readonly ComboBox _font = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _fontPreview = new();
    private readonly Button _btnApplyFont = new() { Text = "تطبيق الخط على القوالب" };

    private readonly Label _capacity = new();

    private bool _suspend;

    public SettingsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;
        AutoScroll = true;

        Build();

        _suspend = true;
        LoadShowAllTrays();
        LoadPrinters();
        LoadFont();
        _suspend = false;
    }

    // ---------- layout ----------

    private void Build()
    {
        int y = 12;

        var title = new Label
        {
            Text = "الإعدادات",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Location = new Point(20, y),
            Size = new Size(600, 38),
            TextAlign = ContentAlignment.MiddleRight
        };
        Controls.Add(title);
        y += 50;

        void Section(string text)
        {
            Controls.Add(new Label
            {
                Text = text, Font = AppTheme.TitleFont, ForeColor = AppTheme.Sidebar,
                Location = new Point(20, y), Size = new Size(600, 30),
                TextAlign = ContentAlignment.MiddleRight
            });
            y += 40;
        }

        void Head(string text, int x, int w)
        {
            Controls.Add(new Label
            {
                Text = text, Font = AppTheme.UiFont, ForeColor = Color.DimGray,
                Location = new Point(x, y), Size = new Size(w, 18),
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
            });
        }

        Section("الطابعات والأدراج");

        Head("النوع", 470, 130);
        Head("الطابعة", 250, 210);
        Head("الدرج", 20, 220);
        y += 22;

        foreach (var r in _rows)
        {
            Controls.Add(new Label
            {
                Text = r.Label, Font = AppTheme.UiFontBold, ForeColor = AppTheme.Sidebar,
                Location = new Point(470, y + 3), Size = new Size(130, 26),
                TextAlign = ContentAlignment.MiddleRight
            });

            r.Printer.SetBounds(250, y, 210, 26);
            r.Printer.Font = AppTheme.UiFont;
            r.Printer.RightToLeft = RightToLeft.Yes;
            Controls.Add(r.Printer);

            r.Tray.SetBounds(20, y, 220, 26);
            r.Tray.Font = AppTheme.UiFont;
            r.Tray.RightToLeft = RightToLeft.Yes;
            Controls.Add(r.Tray);

            var row = r;
            row.Printer.SelectedIndexChanged += (_, _) =>
            {
                if (_suspend) return;
                SettingsRepository.Set(row.PrinterKey, row.Printer.SelectedItem?.ToString());
                RefreshTrays(row);
            };
            row.Tray.SelectedIndexChanged += (_, _) =>
            {
                if (_suspend) return;
                SettingsRepository.Set(row.TrayKey, CurrentTray(row));
            };

            y += 34;
        }

        y += 6;
        _showAllTrays.Location = new Point(20, y);
        Controls.Add(_showAllTrays);
        _showAllTrays.CheckedChanged += (_, _) =>
        {
            if (_suspend) return;
            SettingsRepository.Set("ShowAllTrays", _showAllTrays.Checked ? "1" : "0");
            PrintJobRunner.ClearTrayCache();
            foreach (var r in _rows) RefreshTrays(r);
        };
        y += 40;

        Section("خط الطباعة");

        Head("الخط", 470, 130);
        y += 22;

        _font.SetBounds(250, y, 300, 26);
        _font.Font = AppTheme.UiFont;
        _font.RightToLeft = RightToLeft.Yes;
        Controls.Add(_font);
        _font.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend) return;
            AppFonts.PrintFamily = _font.SelectedItem?.ToString() ?? AppFonts.ArabicFamily;
            UpdateFontPreview();
        };
        y += 34;

        _fontPreview.Text = "نموذج نص عربي — 0123456789";
        _fontPreview.BackColor = Color.White;
        _fontPreview.BorderStyle = BorderStyle.FixedSingle;
        _fontPreview.TextAlign = ContentAlignment.MiddleCenter;
        _fontPreview.SetBounds(20, y, 580, 50);
        Controls.Add(_fontPreview);
        y += 62;

        _btnApplyFont.SetBounds(20, y, 220, 32);
        _btnApplyFont.Font = AppTheme.UiFont;
        _btnApplyFont.FlatStyle = FlatStyle.Flat;
        _btnApplyFont.BackColor = AppTheme.Accent;
        _btnApplyFont.ForeColor = Color.White;
        _btnApplyFont.FlatAppearance.BorderSize = 0;
        _btnApplyFont.Cursor = Cursors.Hand;
        Controls.Add(_btnApplyFont);
        _btnApplyFont.Click += (_, _) =>
        {
            int n = DefaultTemplates.ApplyFont(AppFonts.PrintFamily);
            MessageBox.Show($"تم تحديث {n} قالب إلى خط {AppFonts.PrintFamily}.",
                "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        y += 46;

        Section("سعة الدفتر");

        _capacity.Text = $"كل دفتر يحتوي {AppFonts.NotebookCapacityDisplay} وصفة";
        _capacity.Font = AppTheme.UiFontBold;
        _capacity.ForeColor = AppTheme.Sidebar;
        _capacity.Location = new Point(20, y);
        _capacity.Size = new Size(580, 24);
        _capacity.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(_capacity);
        y += 26;

        Controls.Add(new Label
        {
            Text = "هذا الرقم ثابت في النظام ولا يمكن تغييره من هنا.",
            Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Gray,
            Location = new Point(20, y), Size = new Size(580, 20),
            TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
        });
    }

    // ---------- printers / trays ----------

    private string? CurrentTray(DocRow r) =>
        r.Tray.SelectedIndex <= 0 ? null : r.Tray.SelectedItem?.ToString();

    private void LoadShowAllTrays() =>
        _showAllTrays.Checked = SettingsRepository.Get("ShowAllTrays") == "1";

    private void LoadPrinters()
    {
        foreach (var r in _rows)
        {
            foreach (string s in PrinterSettings.InstalledPrinters) r.Printer.Items.Add(s);

            r.Printer.SelectedItem = SettingsRepository.Get(r.PrinterKey)
                                    ?? new PrinterSettings().PrinterName;
            if (r.Printer.SelectedIndex < 0 && r.Printer.Items.Count > 0) r.Printer.SelectedIndex = 0;

            RefreshTrays(r);
        }
    }

    private void RefreshTrays(DocRow r)
    {
        r.Tray.Items.Clear();
        r.Tray.Items.Add(AutoTray);

        string printer = r.Printer.SelectedItem?.ToString() ?? "";
        var trays = _showAllTrays.Checked
            ? PrintJobRunner.AllTrays(printer)
            : PrintJobRunner.Trays(printer);

        foreach (var s in trays)
            if (!string.IsNullOrWhiteSpace(s.SourceName) && !r.Tray.Items.Contains(s.SourceName))
                r.Tray.Items.Add(s.SourceName);

        string? saved = SettingsRepository.Get(r.TrayKey);
        int i = saved != null ? r.Tray.Items.IndexOf(saved) : -1;
        r.Tray.SelectedIndex = i >= 0 ? i : 0;
    }

    // ---------- font ----------

    private void LoadFont()
    {
        foreach (var n in AppFonts.AvailableFamilies()) _font.Items.Add(n);

        _font.SelectedItem = AppFonts.PrintFamily;
        if (_font.SelectedIndex < 0 && _font.Items.Count > 0) _font.SelectedIndex = 0;

        UpdateFontPreview();
    }

    private void UpdateFontPreview()
    {
        string family = _font.SelectedItem?.ToString() ?? AppFonts.ArabicFamily;
        _fontPreview.Font = AppFonts.Create(family, 14f, FontStyle.Regular);
    }
}
