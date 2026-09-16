using Microsoft.Data.SqlClient;
using Everest.App.Data;
using Everest.App.Models;
using Everest.App.Printing;

namespace Everest.App.Forms;

public class OrdersPage : UserControl
{
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill };

    private readonly ComboBox _entity = new()
    {
        DropDownStyle = ComboBoxStyle.DropDown,
        AutoCompleteMode = AutoCompleteMode.None
    };
    private readonly NumericUpDown _notebooks = new() { Minimum = 1, Maximum = 500, Value = 2 };
    private readonly Label _preview = new();
    private readonly Label _entityInfo = new();
    private readonly Label _editorTitle = new();
    private readonly Label _perInfo = new();

    private readonly Button _btnSave   = new() { Text = "إنشاء الطلب" };
    private readonly Button _btnNew    = new() { Text = "طلب جديد" };
    private readonly Button _btnDelete = new() { Text = "حذف الطلب" };
    private readonly Button _btnExportSample = new() { Text = "تصدير عينة PDF" };
    private readonly Button _btnRefresh = new() { Text = "تحديث" };

    private readonly DateTimePicker _from = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = AppTheme.DateFormat,
        Width = 120,
        Value = DateTime.Today.AddMonths(-3)
    };
    private readonly DateTimePicker _to = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = AppTheme.DateFormat,
        Width = 120,
        Value = DateTime.Today
    };
    private readonly CheckBox _useRange = new() { Text = "تصفية بالتاريخ", AutoSize = true,
                                                  Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };

    private List<Entity> _entities = new();
    private List<Entity> _entityView = new();
    private int _pickedEntityId = -1;
    private List<Order> _view = new();
    private Order? _current;
    private int _perNotebook = 3;
    private bool _suspend;

    public OrdersPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;

        Controls.Add(BuildGridArea());
        Controls.Add(BuildEditor());

        _entity.TextChanged += (_, _) => OnEntityTextChanged();
        _entity.SelectionChangeCommitted += (_, _) => CommitPick();
        _entity.Leave += (_, _) => UpdatePreview();
        _notebooks.ValueChanged      += (_, _) => UpdatePreview();
        _btnSave.Click               += (_, _) => Save();
        _btnNew.Click                += (_, _) => NewOrder();
        _btnDelete.Click             += (_, _) => DeleteOrder();
        _btnExportSample.Click       += (_, _) => ExportSample();
        _btnRefresh.Click            += (_, _) => LoadOrders();
        _grid.CellClick              += (_, e) => { if (e.RowIndex >= 0) LoadSelected(); };
        _useRange.CheckedChanged     += (_, _) => { _from.Enabled = _to.Enabled = _useRange.Checked; LoadOrders(); };
        _from.ValueChanged           += (_, _) => { if (_useRange.Checked) LoadOrders(); };
        _to.ValueChanged             += (_, _) => { if (_useRange.Checked) LoadOrders(); };

        _from.Enabled = _to.Enabled = false;

        try { _perNotebook = OrderRepository.AppointmentsPerNotebook; } catch { }
        _perInfo.Text = $"كل دفتر يحتوي {AppFonts.NotebookCapacityDisplay} وصفة";

        LoadEntities();
        LoadOrders();
        NewOrder();
    }

    private Control BuildGridArea()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 12, 20) };

        var title = new Label
        {
            Text = "الطلبات",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleRight
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 44 };
        foreach (Control c in new Control[] { _useRange, _from, _to, _btnRefresh })
        {
            c.Font = AppTheme.UiFont;
            c.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(c);
        }

        _btnRefresh.Width = 90;
        _btnRefresh.Height = 28;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.BackColor = Color.White;
        _btnRefresh.Cursor = Cursors.Hand;

        bar.Resize += (_, _) =>
        {
            int x = bar.Width;
            _useRange.Location   = new Point(x - _useRange.Width - 4, 13); x -= _useRange.Width + 12;
            _from.Location       = new Point(x - _from.Width, 11);         x -= _from.Width + 8;
            _to.Location         = new Point(x - _to.Width, 11);           x -= _to.Width + 16;
            _btnRefresh.Location = new Point(x - _btnRefresh.Width, 10);
        };

        AppTheme.StyleGrid(_grid);

        host.Controls.Add(_grid);
        host.Controls.Add(bar);
        host.Controls.Add(title);
        return host;
    }

    private Control BuildEditor()
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
            y += c.Height + 14;
            panel.Controls.Add(c);
        }

        _editorTitle.Text = "طلب جديد";
        _editorTitle.Font = AppTheme.TitleFont;
        _editorTitle.ForeColor = AppTheme.Sidebar;
        _editorTitle.Location = new Point(16, y);
        _editorTitle.Size = new Size(292, 32);
        _editorTitle.TextAlign = ContentAlignment.MiddleRight;
        panel.Controls.Add(_editorTitle);
        y += 44;

        Head("الطبيب / المستشفى *   (اكتب للبحث)");
        Field(_entity);

        _entityInfo.Font = new Font("Segoe UI", 9F);
        _entityInfo.ForeColor = Color.Gray;
        _entityInfo.Location = new Point(16, y);
        _entityInfo.Size = new Size(292, 20);
        _entityInfo.TextAlign = ContentAlignment.MiddleRight;
        panel.Controls.Add(_entityInfo);
        y += 26;

        Head("عدد الدفاتر");
        Field(_notebooks);

        _perInfo.Font = new Font("Segoe UI", 9F);
        _perInfo.ForeColor = Color.Gray;
        _perInfo.Location = new Point(16, y);
        _perInfo.Size = new Size(292, 20);
        _perInfo.TextAlign = ContentAlignment.MiddleRight;
        panel.Controls.Add(_perInfo);
        y += 26;

        _preview.Font = AppTheme.UiFontBold;
        _preview.ForeColor = AppTheme.Sidebar;
        _preview.BackColor = Color.White;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.Location = new Point(16, y);
        _preview.Size = new Size(292, 68);
        _preview.TextAlign = ContentAlignment.MiddleCenter;
        panel.Controls.Add(_preview);
        y += 82;

        _btnSave.SetBounds(16, y, 292, 38);
        _btnSave.Font = AppTheme.UiFontBold;
        _btnSave.FlatStyle = FlatStyle.Flat;
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.BackColor = AppTheme.Accent;
        _btnSave.ForeColor = Color.White;
        _btnSave.Cursor = Cursors.Hand;
        panel.Controls.Add(_btnSave);
        y += 48;

        _btnNew.SetBounds(16, y, 292, 34);
        _btnNew.Font = AppTheme.UiFont;
        _btnNew.FlatStyle = FlatStyle.Flat;
        _btnNew.FlatAppearance.BorderColor = AppTheme.Line;
        _btnNew.BackColor = Color.White;
        _btnNew.Cursor = Cursors.Hand;
        panel.Controls.Add(_btnNew);
        y += 44;

        _btnDelete.SetBounds(16, y, 292, 34);
        _btnDelete.Font = AppTheme.UiFont;
        _btnDelete.FlatStyle = FlatStyle.Flat;
        _btnDelete.FlatAppearance.BorderColor = AppTheme.Line;
        _btnDelete.BackColor = Color.White;
        _btnDelete.ForeColor = Color.Firebrick;
        _btnDelete.Cursor = Cursors.Hand;
        panel.Controls.Add(_btnDelete);
        y += 44;

        _btnExportSample.SetBounds(16, y, 292, 34);
        _btnExportSample.Font = AppTheme.UiFont;
        _btnExportSample.FlatStyle = FlatStyle.Flat;
        _btnExportSample.FlatAppearance.BorderColor = AppTheme.Line;
        _btnExportSample.BackColor = Color.White;
        _btnExportSample.Cursor = Cursors.Hand;
        _btnExportSample.Enabled = false;
        panel.Controls.Add(_btnExportSample);

        return panel;
    }

    private static string Display(Entity e) => $"{e.TypeLabel} — {e.Name} ({e.LicenseNumber})";

    private void LoadEntities()
    {
        try
        {
            _entities = EntityRepository.GetAll();
            FillEntityList(_entities);
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في تحميل الجهات:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FillEntityList(List<Entity> list)
    {
        _entityView = list;
        _entity.Items.Clear();
        foreach (var e in list) _entity.Items.Add(Display(e));
    }

    private void OnEntityTextChanged()
    {
        if (_suspend) return;

        _pickedEntityId = -1;
        string q = _entity.Text.Trim();
        var match = _entities.Where(e =>
            q.Length == 0 ||
            e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            e.LicenseNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            Display(e).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _suspend = true;
        int sel = _entity.SelectionStart;
        bool wasDropped = _entity.DroppedDown;
        _entity.DroppedDown = false;

        FillEntityList(match);
        _entity.Text = q;
        _entity.SelectionStart = Math.Min(sel, _entity.Text.Length);
        _suspend = false;

        if (_entity.Focused && match.Count > 0 && q.Length > 0)
        {
            _entity.DroppedDown = true;
            _entity.SelectionStart = _entity.Text.Length;
        }
        else if (wasDropped && match.Count == 0)
        {
            _entity.DroppedDown = false;
        }

        _entityInfo.Text = match.Count == 0 && q.Length > 0 ? "لا توجد نتائج مطابقة" : _entityInfo.Text;
        UpdatePreview();
    }

    private void CommitPick()
    {
        int i = _entity.SelectedIndex;
        if (i < 0 || i >= _entityView.Count) return;

        var e = _entityView[i];
        _pickedEntityId = e.EntityId;

        _suspend = true;
        _entity.DroppedDown = false;
        _entity.Text = Display(e);
        _entity.SelectionStart = _entity.Text.Length;
        _suspend = false;

        UpdatePreview();
    }

    private Entity? Selected
    {
        get
        {
            string t = _entity.Text.Trim();

            var byText = _entities.FirstOrDefault(e => Display(e) == t);
            if (byText != null) return byText;

            if (_pickedEntityId > 0)
                return _entities.FirstOrDefault(x => x.EntityId == _pickedEntityId)
                    ?? EntityRepository.GetById(_pickedEntityId);

            return null;
        }
    }

    private void UpdatePreview()
    {
        var e = Selected;
        if (e == null)
        {
            _preview.Text = "اختر جهة";
            _entityInfo.Text = "";
            _btnSave.Enabled = false;
            _btnExportSample.Enabled = false;
            return;
        }

        _btnSave.Enabled = true;
        _btnExportSample.Enabled = true;

        int total = (int)_notebooks.Value * _perNotebook;
        string yy = DateTime.Now.Year.ToString()[2..];

        // when editing, the order's own range is released first
        int baseSeq = e.LastSequence;
        if (_current != null && _current.EntityId == e.EntityId)
            baseSeq -= _current.TotalAppointments;
        if (baseSeq < 0) baseSeq = 0;

        _entityInfo.Text = $"رقم: {e.LicenseNumber}   •   آخر رقم مستخدم: {baseSeq}";

        string start = yy + (baseSeq + 1).ToString("D6");
        string end   = yy + (baseSeq + total).ToString("D6");

        _preview.Text = $"{total} وصفة{Environment.NewLine}{start}  ←  {end}";
    }

    private void LoadOrders()
    {
        try
        {
            var data = _useRange.Checked
                ? OrderRepository.GetByDateRange(_from.Value, _to.Value)
                : OrderRepository.GetAll();

            _view = data;

            _grid.DataSource = data.Select(o => new
            {
                الرقم   = o.OrderId,
                الجهة   = o.EntityName,
                الدفاتر = o.NotebookCount,
                من      = o.StartSerial,
                إلى     = o.EndSerial,
                المجموع = o.TotalAppointments,
                التاريخ = o.CreatedAt.ToString(AppTheme.DateFormat),
                الطباعة = o.PrintedAt.HasValue
                            ? o.PrintedAt.Value.ToString(AppTheme.DateFormat)
                            : "لم تُطبع"
            }).ToList();

            if (_grid.Columns.Count > 0)
            {
                _grid.Columns["الجهة"]!.FillWeight = 220;
                _grid.Columns["الرقم"]!.FillWeight = 60;
                _grid.Columns["الدفاتر"]!.FillWeight = 70;
            }

            _grid.ClearSelection();
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في تحميل الطلبات:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadSelected()
    {
        if (_grid.CurrentRow == null) return;
        int i = _grid.CurrentRow.Index;
        if (i < 0 || i >= _view.Count) return;

        _current = _view[i];
        _editorTitle.Text = $"تعديل الطلب {_current.OrderId}";
        _btnSave.Text = "حفظ التعديل";
        _btnDelete.Enabled = true;

        var e = _entities.FirstOrDefault(x => x.EntityId == _current.EntityId)
                ?? EntityRepository.GetById(_current.EntityId);

        _suspend = true;
        FillEntityList(_entities);
        _entity.Text = e != null ? Display(e) : _current.EntityName;
        _pickedEntityId = _current.EntityId;
        _suspend = false;
        _entity.Text = "";
        _suspend = false;

        _notebooks.Value = Math.Min(_notebooks.Maximum, _current.NotebookCount);
        UpdatePreview();
    }

    private void NewOrder()
    {
        _current = null;
        _editorTitle.Text = "طلب جديد";
        _btnSave.Text = "إنشاء الطلب";
        _btnDelete.Enabled = false;

        _suspend = true;
        _entity.DroppedDown = false;
        FillEntityList(_entities);
        _entity.SelectedIndex = -1;
        _entity.Text = "";
        _pickedEntityId = -1;
        _suspend = false;

        _notebooks.Value = 2;
        _grid.ClearSelection();
        UpdatePreview();
    }

    private void Save()
    {
        var e = Selected;
        if (e == null)
        {
            MessageBox.Show("اختر جهة من القائمة.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int notebooks = (int)_notebooks.Value;

        if (notebooks % 2 != 0)
        {
            var ask = MessageBox.Show(
                "عدد الدفاتر فردي، والقالب الأساسي يطبع وصفتين في الصفحة.\n\nهل تريد المتابعة؟",
                "تنبيه", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return;
        }

        try
        {
            if (_current == null)
            {
                var order = OrderRepository.Create(e.EntityId, notebooks, _perNotebook);
                LoadEntities();
                LoadOrders();
                NewOrder();
                MessageBox.Show(
                    $"تم إنشاء الطلب رقم {order.OrderId}\n\n{order.StartSerial} ← {order.EndSerial}",
                    "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                OrderRepository.Update(_current.OrderId, e.EntityId, notebooks);
                int id = _current.OrderId;
                LoadEntities();
                LoadOrders();
                var updated = OrderRepository.GetById(id);
                NewOrder();
                MessageBox.Show(
                    $"تم تعديل الطلب رقم {id}\n\n{updated?.StartSerial} ← {updated?.EndSerial}",
                    "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (SqlException ex) when (ex.Message.Contains("PRINTED"))
        {
            MessageBox.Show("لا يمكن تعديل طلب تمت طباعته.", "غير مسموح",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (SqlException ex) when (ex.Message.Contains("NOTLAST"))
        {
            MessageBox.Show(
                "لا يمكن تعديل هذا الطلب لأنه ليس الأحدث لهذه الجهة.\n\n" +
                "التعديل سيؤدي إلى تكرار الأرقام التسلسلية.",
                "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر الحفظ:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteOrder()
    {
        if (_current == null) return;

        var confirm = MessageBox.Show(
            $"حذف الطلب رقم {_current.OrderId} ({_current.StartSerial} ← {_current.EndSerial})؟\n\n" +
            "إذا كان آخر طلب لهذه الجهة فسيتم إرجاع الترقيم.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            OrderRepository.Delete(_current.OrderId);
            LoadEntities();
            LoadOrders();
            NewOrder();
        }
        catch (InvalidOperationException)
        {
            MessageBox.Show("لا يمكن حذف طلب تمت طباعته.", "غير مسموح",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر الحذف:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportSample()
    {
        var e = Selected;
        if (e == null) return;

        using var save = new SaveFileDialog
        {
            Filter = "PDF|*.pdf",
            FileName = $"عينة - {e.Name}.pdf",
            OverwritePrompt = true
        };
        if (save.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            string? warn = SampleExporter.Export(e, save.FileName);

            var open = MessageBox.Show(
                $"تم التصدير إلى:\n{save.FileName}" + (warn != null ? "\n\n" + warn : "") +
                "\n\nهل تريد فتحه؟",
                "تم", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (open == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(save.FileName)
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر التصدير:\n\n" + ex.Message, "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}