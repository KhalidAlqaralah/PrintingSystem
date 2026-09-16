using System.Drawing.Printing;
using Everest.App.Data;
using Everest.App.Models;
using Everest.App.Printing;

namespace Everest.App.Forms;

public class ReportsPage : UserControl
{
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill };

    private readonly CheckBox _useRange = new() { Text = "تصفية بالتاريخ", AutoSize = true, Checked = true,
                                                  Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly DateTimePicker _from = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = AppTheme.DateFormat,
        Width = 110, Value = DateTime.Today.AddMonths(-1)
    };
    private readonly DateTimePicker _to = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = AppTheme.DateFormat,
        Width = 110, Value = DateTime.Today
    };
    private readonly TextBox _search = new()
    {
        Width = 210, Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes,
        PlaceholderText = "بحث بالاسم أو رقم النقابة / الرقم الوطني..."
    };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Button _btnRefresh = new() { Text = "تحديث" };

    private readonly Label _summary = new();
    private readonly ComboBox _printer = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tray    = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnPreview = new() { Text = "معاينة التقرير" };
    private readonly Button _btnPrint   = new() { Text = "طباعة التقرير" };
    private readonly Button _btnPdf     = new() { Text = "تصدير PDF" };

    private readonly TextBox _noteNo = new() { Font = AppTheme.UiFont, TextAlign = HorizontalAlignment.Center };
    private readonly Button _btnNotePreview = new() { Text = "معاينة سند التسليم" };
    private readonly Button _btnNotePrint   = new() { Text = "طباعة سند التسليم" };

    private List<Order> _view = new();

    public ReportsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;

        Controls.Add(BuildGridArea());
        Controls.Add(BuildPanel());

        _useRange.CheckedChanged += (_, _) => { _from.Enabled = _to.Enabled = _useRange.Checked; LoadReport(); };
        _from.ValueChanged   += (_, _) => { if (_useRange.Checked) LoadReport(); };
        _to.ValueChanged     += (_, _) => { if (_useRange.Checked) LoadReport(); };
        _search.TextChanged  += (_, _) => LoadReport();
        _status.SelectedIndexChanged += (_, _) => LoadReport();
        _btnRefresh.Click    += (_, _) => LoadReport();

        _btnPreview.Click += (_, _) => RunReport(ReportAction.Preview);
        _btnPrint.Click   += (_, _) => RunReport(ReportAction.Print);
        _btnPdf.Click     += (_, _) => RunReport(ReportAction.Pdf);

        _btnNotePreview.Click += (_, _) => RunNote(true);
        _btnNotePrint.Click   += (_, _) => RunNote(false);

        _status.Items.AddRange(new object[] { "الكل", "المطبوعة", "غير المطبوعة" });
        _status.SelectedIndex = 0;

        LoadPrinters();
        LoadReport();
    }

    // ---------- layout ----------

    private Control BuildGridArea()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 12, 20) };

        var title = new Label
        {
            Text = "التقارير",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleRight
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 46 };

        _btnRefresh.Width = 84; _btnRefresh.Height = 28;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.BackColor = Color.White;
        _btnRefresh.Cursor = Cursors.Hand;

        foreach (Control c in new Control[] { _search, _status, _useRange, _from, _to, _btnRefresh })
        {
            c.Font = AppTheme.UiFont;
            c.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(c);
        }

        bar.Resize += (_, _) =>
        {
            int x = bar.Width;
            _search.Location     = new Point(x - _search.Width - 2, 10);  x -= _search.Width + 10;
            _status.Location     = new Point(x - _status.Width, 10);      x -= _status.Width + 14;
            _useRange.Location   = new Point(x - _useRange.Width, 15);    x -= _useRange.Width + 10;
            _from.Location       = new Point(x - _from.Width, 11);        x -= _from.Width + 6;
            _to.Location         = new Point(x - _to.Width, 11);          x -= _to.Width + 14;
            _btnRefresh.Location = new Point(x - _btnRefresh.Width, 10);
        };

        AppTheme.StyleGrid(_grid);
        _grid.AllowUserToOrderColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        host.Controls.Add(_grid);
        host.Controls.Add(bar);
        host.Controls.Add(title);
        return host;
    }

    private Control BuildPanel()
    {
        var p = new Panel { Dock = DockStyle.Left, Width = 340, BackColor = AppTheme.Surface,
                            Padding = new Padding(16), AutoScroll = true };
        int y = 12;

        void Head(string text)
        {
            p.Controls.Add(new Label
            {
                Text = text, Font = AppTheme.UiFont, ForeColor = Color.DimGray,
                Location = new Point(16, y), Size = new Size(292, 18),
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
            });
            y += 20;
        }

        void Section(string text)
        {
            p.Controls.Add(new Label
            {
                Text = text, Font = AppTheme.TitleFont, ForeColor = AppTheme.Sidebar,
                Location = new Point(16, y), Size = new Size(292, 30),
                TextAlign = ContentAlignment.MiddleRight
            });
            y += 40;
        }

        void Field(Control c)
        {
            c.SetBounds(16, y, 292, c.Height);
            c.Font = AppTheme.UiFont;
            c.RightToLeft = RightToLeft.Yes;
            p.Controls.Add(c);
            y += c.Height + 12;
        }

        void Btn(Button b, int h, bool primary)
        {
            b.SetBounds(16, y, 292, h);
            b.Font = primary ? AppTheme.UiFontBold : AppTheme.UiFont;
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = AppTheme.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Color.White;
                b.FlatAppearance.BorderColor = AppTheme.Line;
            }
            p.Controls.Add(b);
            y += h + 8;
        }

        Section("ملخص");

        _summary.Font = AppTheme.UiFontBold;
        _summary.ForeColor = AppTheme.Sidebar;
        _summary.BackColor = Color.White;
        _summary.BorderStyle = BorderStyle.FixedSingle;
        _summary.SetBounds(16, y, 292, 84);
        _summary.TextAlign = ContentAlignment.MiddleCenter;
        p.Controls.Add(_summary);
        y += 98;

        Head("الطابعة");
        Field(_printer);

        Head("درج الورق");
        Field(_tray);

        Btn(_btnPreview, 34, false);
        Btn(_btnPrint, 38, true);
        Btn(_btnPdf, 34, false);

        y += 16;
        Section("سند التسليم");

        Head("رقم السند");
        Field(_noteNo);

        Btn(_btnNotePreview, 32, false);
        Btn(_btnNotePrint, 34, false);

        return p;
    }

    private const string AutoTray = "(تلقائي)";

    private void LoadPrinters()
    {
        foreach (string s in PrinterSettings.InstalledPrinters) _printer.Items.Add(s);

        _printer.SelectedItem = SettingsRepository.Get("PrinterReport")
                                ?? new PrinterSettings().PrinterName;
        if (_printer.SelectedIndex < 0 && _printer.Items.Count > 0) _printer.SelectedIndex = 0;

        RefreshTrays();

        _printer.SelectedIndexChanged += (_, _) =>
        {
            SettingsRepository.Set("PrinterReport", _printer.SelectedItem?.ToString());
            RefreshTrays();
        };

        _tray.SelectedIndexChanged += (_, _) =>
            SettingsRepository.Set("TrayReport", CurrentTray());
    }

    private string? CurrentTray() =>
        _tray.SelectedIndex <= 0 ? null : _tray.SelectedItem?.ToString();

    private void RefreshTrays()
    {
        _tray.Items.Clear();
        _tray.Items.Add(AutoTray);

        foreach (var s in PrintJobRunner.Trays(_printer.SelectedItem?.ToString() ?? ""))
            if (!string.IsNullOrWhiteSpace(s.SourceName) && !_tray.Items.Contains(s.SourceName))
                _tray.Items.Add(s.SourceName);

        string? saved = SettingsRepository.Get("TrayReport");
        int i = saved != null ? _tray.Items.IndexOf(saved) : -1;
        _tray.SelectedIndex = i >= 0 ? i : 0;
    }

    // ---------- data ----------

    private void LoadReport()
    {
        try
        {
            var all = _useRange.Checked
                ? OrderRepository.GetByDateRange(_from.Value, _to.Value)
                : OrderRepository.GetAll();

            if (_status.SelectedIndex == 1) all = all.Where(o => o.PrintedAt.HasValue).ToList();
            else if (_status.SelectedIndex == 2) all = all.Where(o => !o.PrintedAt.HasValue).ToList();

            string q = _search.Text.Trim();
            if (q.Length > 0)
            {
                var matching = EntityRepository.GetAll(true)
                    .Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                             || e.LicenseNumber.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.EntityId)
                    .ToHashSet();

                all = all.Where(o => matching.Contains(o.EntityId)).ToList();
            }

            _view = all.OrderBy(o => o.PrintedAt ?? o.CreatedAt).ThenBy(o => o.OrderId).ToList();

            BuildGrid();
            UpdateSummary();

            if (_noteNo.Text.Trim().Length == 0) _noteNo.Text = NextNoteNumber();
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في تحميل التقرير:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static readonly (string Header, int Weight)[] GridCols =
    {
        ("التسلسل", 45), ("رقم الطلب", 55), ("الجهة", 160), ("الرقم النقابي", 80), ("النوع", 50),
        ("الدفاتر", 50), ("الوصفات", 55), ("من رقم", 75), ("إلى رقم", 75),
        ("تاريخ الطلب", 75), ("تاريخ الطباعة", 80)
    };

    private void BuildGrid()
    {
        _grid.DataSource = null;
        _grid.Columns.Clear();
        _grid.Rows.Clear();

        foreach (var (h, w) in GridCols)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = h, HeaderText = h, FillWeight = w, ReadOnly = true });

        int n = 1;
        foreach (var o in _view)
        {
            var ent = EntityRepository.GetById(o.EntityId);
            _grid.Rows.Add(
                n++, o.OrderId, o.EntityName,
                ent?.LicenseNumber ?? "",
                ent?.TypeLabel ?? "",
                o.NotebookCount, o.TotalAppointments,
                o.StartSerial, o.EndSerial,
                o.CreatedAt.ToString(AppTheme.DateFormat),
                o.PrintedAt.HasValue ? o.PrintedAt.Value.ToString(AppTheme.DateFormat) : "لم تُطبع");
        }

        if (_view.Count > 0)
        {
            int i = _grid.Rows.Add("", "", "المجموع", "", "",
                _view.Sum(o => o.NotebookCount),
                _view.Sum(o => o.TotalAppointments),
                "", "", "", "");

            var r = _grid.Rows[i];
            r.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            r.DefaultCellStyle.BackColor = Color.FromArgb(225, 235, 242);
            r.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 235, 242);
            r.DefaultCellStyle.SelectionForeColor = Color.Black;
        }

        _grid.ClearSelection();
    }

    private void UpdateSummary()
    {
        bool any = _view.Count > 0;

        _summary.Text = any
            ? $"{_view.Count} طلب{Environment.NewLine}" +
              $"مجموع الدفاتر: {_view.Sum(o => o.NotebookCount)}{Environment.NewLine}" +
              $"مجموع الوصفات: {_view.Sum(o => o.TotalAppointments)}"
            : "لا توجد نتائج";

        _btnPreview.Enabled = _btnPrint.Enabled = _btnPdf.Enabled = any;
        _btnNotePreview.Enabled = _btnNotePrint.Enabled = any;
    }

    private static string NextNoteNumber()
    {
        string? last = SettingsRepository.Get("LastNoteNumber");
        return int.TryParse(last, out int n) ? (n + 1).ToString() : "1000";
    }

    // ---------- output ----------

    private enum ReportAction { Preview, Print, Pdf }

    private string RangeText() => _useRange.Checked
        ? $"من {_from.Value:dd/MM/yyyy} إلى {_to.Value:dd/MM/yyyy}"
        : "كل الفترات";

    private GridReportRenderer BuildRenderer()
    {
        var cols = new List<ReportColumn>
        {
            new() { Header = "التسلسل",       WidthMm = 14 },
            new() { Header = "رقم الطلب",     WidthMm = 17 },
            new() { Header = "الجهة",         WidthMm = 54, Align = FieldAlign.Right },
            new() { Header = "الرقم النقابي", WidthMm = 24 },
            new() { Header = "النوع",         WidthMm = 16 },
            new() { Header = "الدفاتر",       WidthMm = 17 },
            new() { Header = "الوصفات",       WidthMm = 18 },
            new() { Header = "من رقم",        WidthMm = 26 },
            new() { Header = "إلى رقم",       WidthMm = 26 },
            new() { Header = "تاريخ الطلب",   WidthMm = 25 },
            new() { Header = "تاريخ الطباعة", WidthMm = 25 },
        };

        int n = 1;
        var rows = _view.Select(o =>
        {
            var ent = EntityRepository.GetById(o.EntityId);
            return new[]
            {
                (n++).ToString(), o.OrderId.ToString(), o.EntityName,
                ent?.LicenseNumber ?? "",
                ent?.TypeLabel ?? "",
                o.NotebookCount.ToString(), o.TotalAppointments.ToString(),
                o.StartSerial, o.EndSerial,
                o.CreatedAt.ToString(AppTheme.DateFormat),
                o.PrintedAt.HasValue ? o.PrintedAt.Value.ToString(AppTheme.DateFormat) : "لم تُطبع"
            };
        }).ToList();

        string sub = $"{RangeText()}   •   {_view.Count} طلب" +
                     (_search.Text.Trim().Length > 0 ? $"   •   بحث: {_search.Text.Trim()}" : "");

        return new GridReportRenderer(
            "تقرير الطلبات —  إيفرست للطباعة الأمنية", sub, cols, rows,
            "مجموع عدد الدفاتر", _view.Sum(o => o.NotebookCount).ToString());
    }

    private void RunReport(ReportAction action)
    {
        if (_view.Count == 0) return;

        string printer = _printer.SelectedItem?.ToString() ?? "";

        try
        {
            if (action == ReportAction.Pdf) { ExportPdf(BuildRenderer()); return; }

            var r = BuildRenderer();
            r.TrayName = CurrentTray();
            using var doc = r.BuildDocument(printer);

            if (action == ReportAction.Preview)
            {
                using var dlg = new PrintPreviewDialog
                {
                    Document = doc, Width = 1080, Height = 800,
                    StartPosition = FormStartPosition.CenterParent, UseAntiAlias = true
                };
                dlg.ShowDialog(FindForm()!);
                return;
            }

            doc.Print();
            MessageBox.Show($"تمت طباعة التقرير ({r.PageCount} صفحة).", "تم",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذرت العملية:\n\n" + ex.Message, "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportPdf(GridReportRenderer r)
    {
        const string pdfPrinter = "Microsoft Print to PDF";

        bool available = PrinterSettings.InstalledPrinters.Cast<string>()
            .Any(p => p.Equals(pdfPrinter, StringComparison.OrdinalIgnoreCase));

        if (!available)
        {
            MessageBox.Show(
                "طابعة \"Microsoft Print to PDF\" غير مثبّتة على هذا الجهاز.\n\n" +
                "فعّلها من: إعدادات ويندوز ← الأجهزة ← الطابعات.",
                "غير متاح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var save = new SaveFileDialog
        {
            Filter = "PDF|*.pdf",
            FileName = $"تقرير الطلبات {DateTime.Now:yyyy-MM-dd}.pdf",
            OverwritePrompt = true
        };
        if (save.ShowDialog(FindForm()) != DialogResult.OK) return;

        using var doc = r.BuildDocument(pdfPrinter);
        doc.PrinterSettings.PrintToFile = true;
        doc.PrinterSettings.PrintFileName = save.FileName;
        doc.Print();

        var open = MessageBox.Show($"تم التصدير إلى:\n{save.FileName}\n\nهل تريد فتحه؟",
            "تم", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (open == DialogResult.Yes)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(save.FileName)
            { UseShellExecute = true });
    }

    private void RunNote(bool preview)
    {
        if (_view.Count == 0) return;

        var t = TemplateStore.LoadCategory(TemplateCategory.DeliveryNote).FirstOrDefault();
        if (t == null)
        {
            MessageBox.Show("لا يوجد قالب لسند التسليم.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var data = DeliveryNoteData.Build(_view, EntityRepository.GetById,
                                          _noteNo.Text.Trim(), _to.Value);

        var renderer = new DeliveryNoteRenderer(t, data) { TrayName = CurrentTray() };
        string printer = _printer.SelectedItem?.ToString() ?? "";

        try
        {
            using var doc = renderer.BuildDocument(printer);

            if (preview)
            {
                using var dlg = new PrintPreviewDialog
                {
                    Document = doc, Width = 1050, Height = 780,
                    StartPosition = FormStartPosition.CenterParent, UseAntiAlias = true
                };
                dlg.ShowDialog(FindForm()!);
                return;
            }

            doc.Print();
            SettingsRepository.Set("LastNoteNumber", _noteNo.Text.Trim());
            MessageBox.Show($"تمت طباعة سند التسليم رقم {_noteNo.Text.Trim()}.",
                "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذرت الطباعة:\n\n" + ex.Message, "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}