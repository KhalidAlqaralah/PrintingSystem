using System.Drawing.Printing;
using Everest.App.Data;
using Everest.App.Models;
using Everest.App.Printing;

namespace Everest.App.Forms;

public class PrintPage : UserControl
{
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill };

    private readonly TextBox _search = new() { Width = 200, Font = AppTheme.UiFont,
                                               RightToLeft = RightToLeft.Yes,
                                               PlaceholderText = "بحث بالاسم أو الرقم..." };
    private readonly CheckBox _showPrinted = new() { Text = "إظهار المطبوعة", AutoSize = true,
                                                     Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly Button _btnAll  = new() { Text = "تحديد الكل" };
    private readonly Button _btnNone = new() { Text = "إلغاء التحديد" };

    private readonly CheckBox _useRange = new() { Text = "تصفية بالتاريخ", AutoSize = true,
                                                  Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly DateTimePicker _from = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = AppTheme.DateFormat,
        Width = 110, Value = DateTime.Today.AddMonths(-3), Enabled = false
    };
    private readonly DateTimePicker _to = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = AppTheme.DateFormat,
        Width = 110, Value = DateTime.Today, Enabled = false
    };

    private readonly ComboBox _tplCover  = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tplCoverH = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tplBody  = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tplBodyH = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnPreview = new() { Text = "معاينة" };
    private readonly ComboBox _layout   = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _prnCover = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _prnBody  = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _trayCover = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _trayBody  = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly CheckBox _resume = new() { Text = "بدء الطباعة من ورقة معيّنة", AutoSize = true,
                                                Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly TextBox _resumeSerial = new() { Enabled = false, MaxLength = 8,
                                                     TextAlign = HorizontalAlignment.Center,
                                                     PlaceholderText = "26000001" };
    private readonly Label _resumeInfo = new();

    private readonly Label _summary = new();
    private readonly Button _btnPrint = new() { Text = "بدء الطباعة" };

    private List<Order> _view = new();
    private readonly HashSet<int> _checked = new();

    private const string ColCheck = "تحديد";

    public PrintPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;

        Controls.Add(BuildGridArea());
        Controls.Add(BuildPanel());

        _search.TextChanged         += (_, _) => LoadOrders();
        _showPrinted.CheckedChanged += (_, _) => LoadOrders();
        _useRange.CheckedChanged    += (_, _) => { _from.Enabled = _to.Enabled = _useRange.Checked; LoadOrders(); };
        _from.ValueChanged          += (_, _) => { if (_useRange.Checked) LoadOrders(); };
        _to.ValueChanged            += (_, _) => { if (_useRange.Checked) LoadOrders(); };
        _btnAll.Click               += (_, _) => SetAll(true);
        _btnNone.Click              += (_, _) => SetAll(false);
        _btnPrint.Click             += (_, _) => StartPrint();
        _resume.CheckedChanged += (_, _) =>
        {
            _resumeSerial.Enabled = _resume.Checked;
            if (_resume.Checked)
            {
                var one = SelectedOrders();
                if (one.Count == 1 && _resumeSerial.Text.Trim().Length == 0)
                    _resumeSerial.Text = one[0].StartSerial;
                _resumeSerial.Focus();
            }
            UpdateSummary();
        };
        _resumeSerial.TextChanged += (_, _) => UpdateSummary();
        _resumeSerial.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        };
        _layout.SelectedIndexChanged += (_, _) => { LoadTemplates(); UpdateSummary(); };

        _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 0) ToggleRow(e.RowIndex); };
        _grid.CellClick        += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 0) ToggleRow(e.RowIndex); };

        LoadPrinters();
        LoadTemplates();
        LoadOrders();
    }

    private Control BuildGridArea()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 12, 20) };

        var title = new Label
        {
            Text = "الطباعة",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleRight
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 44 };
        foreach (var b in new[] { _btnAll, _btnNone })
        {
            b.Height = 28; b.Width = 100;
            b.Font = AppTheme.UiFont;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(b);
        }
        foreach (Control c in new Control[] { _search, _showPrinted, _useRange, _from, _to })
        {
            c.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            c.Font = AppTheme.UiFont;
            bar.Controls.Add(c);
        }

        bar.Resize += (_, _) =>
        {
            int x = bar.Width;
            _search.Location      = new Point(x - _search.Width - 2, 9);   x -= _search.Width + 12;
            _showPrinted.Location = new Point(x - _showPrinted.Width, 14); x -= _showPrinted.Width + 14;
            _useRange.Location    = new Point(x - _useRange.Width, 14);    x -= _useRange.Width + 10;
            _from.Location        = new Point(x - _from.Width, 11);        x -= _from.Width + 6;
            _to.Location          = new Point(x - _to.Width, 11);          x -= _to.Width + 16;
            _btnAll.Location      = new Point(x - _btnAll.Width, 9);       x -= _btnAll.Width + 8;
            _btnNone.Location     = new Point(x - _btnNone.Width, 9);
        };

        AppTheme.StyleGrid(_grid);
        _grid.ReadOnly = false;

        host.Controls.Add(_grid);
        host.Controls.Add(bar);
        host.Controls.Add(title);
        return host;
    }

    private Control BuildPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 340,
            BackColor = AppTheme.Surface,
            Padding = new Padding(16),
            AutoScroll = true
        };

        int y = 12;

        void Head(string text)
        {
            panel.Controls.Add(new Label
            {
                Text = text, Font = AppTheme.UiFont, ForeColor = Color.DimGray,
                Location = new Point(16, y), Size = new Size(292, 18),
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
            });
            y += 20;
        }

        void Field(Control c)
        {
            c.Location = new Point(16, y);
            c.Width = 292;
            c.Font = AppTheme.UiFont;
            c.RightToLeft = RightToLeft.Yes;
            y += c.Height + 12;
            panel.Controls.Add(c);
        }

        panel.Controls.Add(new Label
        {
            Text = "إعدادات الطباعة",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Location = new Point(16, y),
            Size = new Size(292, 32),
            TextAlign = ContentAlignment.MiddleRight
        });
        y += 44;

        Head("قالب غلاف الأطباء");
        Field(_tplCover);

        Head("قالب غلاف المستشفيات");
        Field(_tplCoverH);

        Head("طابعة الغلاف");
        Field(_prnCover);

        Head("درج ورق الغلاف");
        Field(_trayCover);

        Head("توزيع الوصفات");
        Field(_layout);

        Head("قالب وصفات الأطباء");
        Field(_tplBody);

        Head("قالب وصفات المستشفيات");
        Field(_tplBodyH);

        Head("طابعة الوصفات");
        Field(_prnBody);

        Head("درج ورق الوصفات");
        Field(_trayBody);

        var allTrays = new CheckBox
        {
            Text = "إظهار كل الأدراج",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.Gray,
            RightToLeft = RightToLeft.Yes,
            Location = new Point(16, y),
            Checked = SettingsRepository.Get("ShowAllTrays") == "1"
        };
        allTrays.CheckedChanged += (_, _) =>
        {
            SettingsRepository.Set("ShowAllTrays", allTrays.Checked ? "1" : "0");
            PrintJobRunner.ClearTrayCache();
            LoadTrays(_prnCover, _trayCover, "TrayCover");
            LoadTrays(_prnBody,  _trayBody,  "TrayBody");
        };
        panel.Controls.Add(allTrays);
        y += 30;

        _resume.Location = new Point(16, y);
        panel.Controls.Add(_resume);
        y += 26;

        Field(_resumeSerial);

        _resumeInfo.Font = new Font("Segoe UI", 9F);
        _resumeInfo.ForeColor = Color.Gray;
        _resumeInfo.Location = new Point(16, y);
        _resumeInfo.Size = new Size(292, 34);
        _resumeInfo.TextAlign = ContentAlignment.MiddleRight;
        panel.Controls.Add(_resumeInfo);
        y += 40;

        _summary.Font = AppTheme.UiFontBold;
        _summary.ForeColor = AppTheme.Sidebar;
        _summary.BackColor = Color.White;
        _summary.BorderStyle = BorderStyle.FixedSingle;
        _summary.Location = new Point(16, y);
        _summary.Size = new Size(292, 92);
        _summary.TextAlign = ContentAlignment.MiddleCenter;
        panel.Controls.Add(_summary);
        y += 106;

        _btnPreview.SetBounds(16, y, 292, 34);
        _btnPreview.Font = AppTheme.UiFont;
        _btnPreview.FlatStyle = FlatStyle.Flat;
        _btnPreview.FlatAppearance.BorderColor = AppTheme.Line;
        _btnPreview.BackColor = Color.White;
        _btnPreview.Cursor = Cursors.Hand;
        _btnPreview.Click += (_, _) => Run(preview: true);
        panel.Controls.Add(_btnPreview);
        y += 42;

        _btnPrint.SetBounds(16, y, 292, 40);
        _btnPrint.Font = AppTheme.UiFontBold;
        _btnPrint.FlatStyle = FlatStyle.Flat;
        _btnPrint.FlatAppearance.BorderSize = 0;
        _btnPrint.BackColor = AppTheme.Accent;
        _btnPrint.ForeColor = Color.White;
        _btnPrint.Cursor = Cursors.Hand;
        panel.Controls.Add(_btnPrint);

        _layout.Items.AddRange(new object[] { "وصفتان في الصفحة", "وصفة واحدة في الصفحة" });
        _layout.SelectedIndex = 0;

        return panel;
    }

    private bool TwoUp => _layout.SelectedIndex == 0;

    private static int BodySheets(Order o, bool twoUp) =>
        twoUp ? (o.TotalAppointments + 1) / 2 : o.TotalAppointments;

    private void LoadPrinters()
    {
        foreach (string p in PrinterSettings.InstalledPrinters)
        {
            _prnCover.Items.Add(p);
            _prnBody.Items.Add(p);
        }

        string def = new PrinterSettings().PrinterName;
        _prnCover.SelectedItem = SettingsRepository.Get("PrinterCover") ?? def;
        _prnBody.SelectedItem  = SettingsRepository.Get("PrinterBody")  ?? def;

        if (_prnCover.SelectedIndex < 0 && _prnCover.Items.Count > 0) _prnCover.SelectedIndex = 0;
        if (_prnBody.SelectedIndex  < 0 && _prnBody.Items.Count  > 0) _prnBody.SelectedIndex  = 0;

        LoadTrays(_prnCover, _trayCover, "TrayCover");
        LoadTrays(_prnBody,  _trayBody,  "TrayBody");

        _prnCover.SelectedIndexChanged += (_, _) =>
        {
            SettingsRepository.Set("PrinterCover", _prnCover.SelectedItem?.ToString());
            LoadTrays(_prnCover, _trayCover, "TrayCover");
        };
        _prnBody.SelectedIndexChanged += (_, _) =>
        {
            SettingsRepository.Set("PrinterBody", _prnBody.SelectedItem?.ToString());
            LoadTrays(_prnBody, _trayBody, "TrayBody");
        };

        _trayCover.SelectedIndexChanged += (_, _) =>
            SettingsRepository.Set("TrayCover", TrayValue(_trayCover));
        _trayBody.SelectedIndexChanged += (_, _) =>
            SettingsRepository.Set("TrayBody", TrayValue(_trayBody));
    }

    private const string AutoTray = "(تلقائي)";

    private static string? TrayValue(ComboBox cb) =>
        cb.SelectedIndex <= 0 ? null : cb.SelectedItem?.ToString();

    private static void LoadTrays(ComboBox printer, ComboBox tray, string settingKey)
    {
        string name = printer.SelectedItem?.ToString() ?? "";
        bool showAll = SettingsRepository.Get("ShowAllTrays") == "1";

        tray.Items.Clear();
        tray.Items.Add(AutoTray);

        var sources = showAll ? PrintJobRunner.AllTrays(name) : PrintJobRunner.Trays(name);

        foreach (var s in sources)
            if (!string.IsNullOrWhiteSpace(s.SourceName) && !tray.Items.Contains(s.SourceName))
                tray.Items.Add(s.SourceName);

        string? saved = SettingsRepository.Get(settingKey);
        int i = saved != null ? tray.Items.IndexOf(saved) : -1;
        tray.SelectedIndex = i >= 0 ? i : 0;
    }

    private void LoadTemplates()
    {
        DefaultTemplates.EnsureCreated();

        bool twoUp = TwoUp;

        void Fill(ComboBox cb, TemplateCategory cat, string empty)
        {
            string? previous = cb.SelectedItem?.ToString();

            cb.Items.Clear();
            var names = TemplateStore.Names(cat);

            if (names.Count == 0) cb.Items.Add(empty);
            else foreach (var n in names) cb.Items.Add(n);

            int i = previous != null ? cb.Items.IndexOf(previous) : -1;
            cb.SelectedIndex = i >= 0 ? i : 0;
        }

        Fill(_tplCover,  twoUp ? TemplateCategory.Cover         : TemplateCategory.CoverSingle,         "(لا يوجد)");
        Fill(_tplCoverH, twoUp ? TemplateCategory.CoverHospital : TemplateCategory.CoverHospitalSingle, "(لا يوجد)");

        Fill(_tplBody,  twoUp ? TemplateCategory.DoctorDouble   : TemplateCategory.DoctorSingle,   "(لا يوجد)");
        Fill(_tplBodyH, twoUp ? TemplateCategory.HospitalDouble : TemplateCategory.HospitalSingle, "(لا يوجد)");
    }

    private void LoadOrders()
    {
        try
        {
            var all = _useRange.Checked
                ? OrderRepository.GetByDateRange(_from.Value, _to.Value)
                : OrderRepository.GetAll();

            if (!_showPrinted.Checked)
                all = all.Where(o => o.PrintedAt == null).ToList();

            string q = _search.Text.Trim();
            if (q.Length > 0)
                all = all.Where(o => o.EntityName.Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || o.StartSerial.Contains(q)
                                  || o.EndSerial.Contains(q)
                                  || o.OrderId.ToString() == q).ToList();

            _view = all;
            _checked.RemoveWhere(id => !_view.Any(o => o.OrderId == id));

            BuildGrid();
            UpdateSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في تحميل الطلبات:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildGrid()
    {
        _grid.DataSource = null;
        _grid.Columns.Clear();

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = ColCheck, HeaderText = ColCheck, FillWeight = 50, ReadOnly = false
        });

        foreach (var (name, weight) in new[]
        {
            ("الرقم", 55), ("الجهة", 220), ("الدفاتر", 65),
            ("من", 90), ("إلى", 90), ("المجموع", 70), ("الحالة", 90)
        })
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = name, FillWeight = weight, ReadOnly = true
            });
        }

        _grid.Rows.Clear();
        foreach (var o in _view)
        {
            int i = _grid.Rows.Add(
                _checked.Contains(o.OrderId),
                o.OrderId, o.EntityName, o.NotebookCount,
                o.StartSerial, o.EndSerial, o.TotalAppointments,
                o.PrintedAt.HasValue ? o.PrintedAt.Value.ToString(AppTheme.DateFormat) : "لم تُطبع");

            if (o.PrintedAt.HasValue)
                _grid.Rows[i].DefaultCellStyle.ForeColor = Color.Gray;
        }
    }

    private void ToggleRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _view.Count) return;

        int id = _view[rowIndex].OrderId;
        if (!_checked.Remove(id)) _checked.Add(id);

        _grid.Rows[rowIndex].Cells[0].Value = _checked.Contains(id);
        _grid.EndEdit();
        UpdateSummary();
    }

    private void SetAll(bool on)
    {
        _checked.Clear();
        if (on) foreach (var o in _view) _checked.Add(o.OrderId);

        for (int i = 0; i < _grid.Rows.Count; i++)
            _grid.Rows[i].Cells[0].Value = on;

        UpdateSummary();
    }

    private List<Order> SelectedOrders() =>
        _view.Where(o => _checked.Contains(o.OrderId)).ToList();

    private void UpdateSummary()
    {
        var sel = SelectedOrders();

        // resume is a single-order feature only
        bool single = sel.Count == 1;
        if (!single && _resume.Checked)
        {
            _resume.Checked = false;
            _resumeSerial.Enabled = false;
        }
        _resume.Enabled = single;
        _resume.Text = single ? "بدء الطباعة من رقم تسلسلي"
                              : "بدء الطباعة من رقم تسلسلي (طلب واحد فقط)";

        if (sel.Count == 0)
        {
            _summary.Text = "لم يتم تحديد أي طلب";
            _resumeInfo.Text = "";
            _btnPrint.Enabled = false;
            return;
        }

        bool twoUp = TwoUp;
        int notebooks = 0, appts = 0, bodySheets = 0, coverSheets = 0;

        foreach (var o in sel)
        {
            notebooks   += o.NotebookCount;
            appts       += o.TotalAppointments;
            bodySheets  += BodySheets(o, twoUp);
            coverSheets += twoUp ? (o.NotebookCount + 1) / 2 : o.NotebookCount;
        }

        int skipped = 0;

        if (single)
        {
            var o = sel[0];
            _resumeInfo.Text = _resume.Checked
                ? ResumeInfo(o, _resumeSerial.Text.Trim(), twoUp, out skipped)
                : $"النطاق: {o.StartSerial} ← {o.EndSerial}";
        }
        else
        {
            _resumeInfo.Text = "";
        }

        if (_resume.Checked && skipped < 0)
        {
            _summary.Text = "رقم تسلسلي غير صالح";
            _btnPrint.Enabled = false;
            return;
        }

        int printBody = bodySheets - skipped;

        _summary.Text =
            $"{sel.Count} طلب  •  {notebooks} دفتر  •  {appts} وصفة{Environment.NewLine}" +
            $"أغلفة: {coverSheets}   وصفات: {printBody}   فارغة: {coverSheets}{Environment.NewLine}" +
            $"إجمالي الصفحات: {coverSheets + printBody + coverSheets}" +
            (skipped > 0 ? $"{Environment.NewLine}(تم تخطي {skipped} ورقة)" : "");

        _btnPrint.Enabled = true;
    }

    /// <summary>Sheet number (1-based) that carries a given serial. 0 if out of range.</summary>
    private static int SheetOf(Order o, string serial, bool twoUp)
    {
        if (serial.Length != 8 || !serial.All(char.IsDigit)) return 0;
        if (string.CompareOrdinal(serial, o.StartSerial) < 0) return 0;
        if (string.CompareOrdinal(serial, o.EndSerial) > 0) return 0;

        int offset = int.Parse(serial) - int.Parse(o.StartSerial);
        if (!twoUp) return offset + 1;

        int half = (o.TotalAppointments + 1) / 2;
        return offset < half ? offset + 1 : offset - half + 1;
    }

    private static string ResumeInfo(Order o, string serial, bool twoUp, out int skipped)
    {
        skipped = 0;

        if (serial.Length == 0)
        {
            skipped = -1;
            return $"أدخل رقماً بين {o.StartSerial} و {o.EndSerial}";
        }

        int sheet = SheetOf(o, serial, twoUp);
        if (sheet == 0)
        {
            skipped = -1;
            return $"الرقم خارج النطاق ({o.StartSerial} ← {o.EndSerial})";
        }

        skipped = sheet - 1;

        if (!twoUp)
            return $"يبدأ من الورقة {sheet}";

        int start = int.Parse(o.StartSerial);
        int end   = int.Parse(o.EndSerial);
        int half  = (o.TotalAppointments + 1) / 2;
        int right = start + sheet - 1;
        int left  = start + half + sheet - 1;

        string sideNote = int.Parse(serial) < start + half ? "" :
            $"{Environment.NewLine}تنبيه: الرقم على الجهة اليسرى، وستُعاد طباعة اليمين ({right})";

        return left > end
            ? $"الورقة {sheet}: يمين {right} • يسار فارغ{sideNote}"
            : $"الورقة {sheet}: يمين {right} • يسار {left}{sideNote}";
    }

    private void StartPrint() => Run(preview: false);

    private void Run(bool preview)
    {
        var sel = SelectedOrders();
        if (sel.Count == 0) return;

        bool twoUp = TwoUp;

        var cover  = TemplateStore.Load(twoUp ? TemplateCategory.Cover : TemplateCategory.CoverSingle,
                                        _tplCover.SelectedItem?.ToString() ?? "");
        var coverH = TemplateStore.Load(twoUp ? TemplateCategory.CoverHospital : TemplateCategory.CoverHospitalSingle,
                                        _tplCoverH.SelectedItem?.ToString() ?? "");
        var bodyD = TemplateStore.Load(twoUp ? TemplateCategory.DoctorDouble : TemplateCategory.DoctorSingle,
                                       _tplBody.SelectedItem?.ToString() ?? "");
        var bodyH = TemplateStore.Load(twoUp ? TemplateCategory.HospitalDouble : TemplateCategory.HospitalSingle,
                                       _tplBodyH.SelectedItem?.ToString() ?? "");

        if (cover == null || bodyD == null)
        {
            MessageBox.Show("اختر قوالب صالحة للغلاف والوصفات أولاً.", "قوالب ناقصة",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Odd notebook counts in two-up mode need a single-up cover/body for the trailing notebook.
        bool anyOdd = twoUp && sel.Any(o => o.NotebookCount % 2 != 0);

        TemplateDef? coverSingleD = null, coverSingleH = null, bodySingleD = null, bodySingleH = null;
        if (anyOdd)
        {
            coverSingleD = TemplateStore.LoadCategory(TemplateCategory.CoverSingle).FirstOrDefault();
            coverSingleH = TemplateStore.LoadCategory(TemplateCategory.CoverHospitalSingle).FirstOrDefault();
            bodySingleD  = TemplateStore.LoadCategory(TemplateCategory.DoctorSingle).FirstOrDefault();
            bodySingleH  = TemplateStore.LoadCategory(TemplateCategory.HospitalSingle).FirstOrDefault();
        }

        if (!preview)
        {
            var reprint = sel.Where(o => o.PrintedAt.HasValue).ToList();
            if (reprint.Count > 0)
            {
                var ask = MessageBox.Show(
                    $"{reprint.Count} من الطلبات المحددة تمت طباعتها مسبقاً.\n\nهل تريد إعادة طباعتها؟",
                    "إعادة طباعة", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask != DialogResult.Yes) return;
            }
        }

        var all = new List<Sheet>();

        foreach (var o in sel)
        {
            var ent = EntityRepository.GetById(o.EntityId);
            if (ent == null) continue;

            bool hosp = ent.Type == EntityType.Hospital;
            var body = hosp ? (bodyH ?? bodyD) : bodyD;
            var cvr  = hosp ? (coverH ?? cover) : cover;

            TemplateDef coverSingle = cover, bodySingle = bodyD;

            if (twoUp && o.NotebookCount % 2 != 0)
            {
                var cS = hosp ? (coverSingleH ?? coverSingleD) : coverSingleD;
                var bS = hosp ? (bodySingleH  ?? bodySingleD)  : bodySingleD;

                if (cS == null || bS == null)
                {
                    MessageBox.Show(
                        $"عدد دفاتر الطلب رقم {o.OrderId} فردي، ولا يوجد قالب غلاف/وصفات مفرد " +
                        $"({ent.TypeLabel}) لإكمال الدفتر الأخير.\n\n" +
                        "أنشئ قالباً في فئة \"غلاف أمامي — مفرد\" أو \"وصفات — مفرد\" من مصمم القوالب.",
                        "قالب مفرد مفقود", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                coverSingle = cS;
                bodySingle  = bS;
            }

            var sheets = SheetPlan.Build(o, ent, cvr, coverSingle, body, bodySingle, twoUp);

            if (sel.Count == 1 && _resume.Checked)
            {
                int from = SheetPlan.IndexOfSerial(sheets, _resumeSerial.Text.Trim());
                sheets = sheets.Skip(from).ToList();
            }

            all.AddRange(sheets);
        }

        if (all.Count == 0) return;

        string prnBody  = _prnBody.SelectedItem?.ToString()  ?? "";
        string prnCover = _prnCover.SelectedItem?.ToString() ?? "";

        try
        {
            if (preview)
            {
                new PrintJobRunner(all)
                {
                    TrayName = TrayValue(_trayBody),
                    CoverTrayName = TrayValue(_trayCover)
                }.Preview(FindForm()!, bodyD, prnBody);
                return;
            }
            // (print path below)

            int sent;
            string? warn;

            string? tCover = TrayValue(_trayCover);
            string? tBody  = TrayValue(_trayBody);

            if (prnCover != prnBody)
            {
                MessageBox.Show(
                    "لا يمكن ضمان ترتيب الأوراق عند استخدام طابعتين مختلفتين.\n\n" +
                    "استخدم طابعة واحدة مع درجين مختلفين بدلاً من ذلك.",
                    "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var runner = new PrintJobRunner(all)
            {
                TrayName = tBody,
                CoverTrayName = tCover
            };

            sent = runner.Print(bodyD, prnBody);
            warn = runner.LastWarning;

            if (sent == 0)
            {
                MessageBox.Show("لم تُرسل أي ورقة إلى الطابعة.", "تنبيه",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var o in sel)
                OrderRepository.MarkPrinted(o.OrderId, o.EndSerial);

            LoadOrders();

            MessageBox.Show(
                $"تم إرسال {sent} ورقة إلى الطابعة." +
                (warn != null ? "\n\n" + warn : ""),
                "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذرت الطباعة:\n\n" + ex.Message, "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}