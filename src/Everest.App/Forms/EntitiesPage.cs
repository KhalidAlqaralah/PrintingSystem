using Microsoft.Data.SqlClient;
using Everest.App.Data;
using Everest.App.Models;

namespace Everest.App.Forms;

public class EntitiesPage : UserControl
{
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Width = 240, Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _filter = new() { Width = 130, Font = AppTheme.UiFont,
                                                DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly CheckBox _showInactive = new() { Text = "إظهار المعطّلة", AutoSize = true,
                                                      Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };

    private readonly ComboBox _type   = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name    = new();
    private readonly TextBox _license = new();
    private readonly TextBox _major   = new();
    private readonly TextBox _address = new() { Multiline = true, Height = 56, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _phone1  = new();
    private readonly TextBox _phone2  = new();
    private readonly TextBox _clinic1 = new();
    private readonly TextBox _clinic2 = new();
    private readonly CheckBox _active = new() { Text = "مفعّل", AutoSize = true, Checked = true,
                                                Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };

    private readonly Label _lblMajor   = new();
    private readonly Label _lblLicense = new();
    private readonly Label _lblPhone1  = new();
    private readonly Label _lblClinic  = new();
    private readonly Label _editorTitle = new();
    private readonly Label _seqInfo = new();

    private readonly Button _btnSave   = new() { Text = "حفظ" };
    private readonly Button _btnNew    = new() { Text = "جديد" };
    private readonly Button _btnDelete = new() { Text = "حذف" };

    private List<Entity> _all = new();
    private List<Entity> _view = new();
    private Entity? _current;
    private bool _suspendSelection;
    private bool _newMode;

    public EntitiesPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;

        Controls.Add(BuildGridArea());
        Controls.Add(BuildEditor());

        _type.SelectedIndexChanged += (_, _) => ApplyTypeLabels();
        _search.TextChanged        += (_, _) => ApplyFilter();
        _filter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _showInactive.CheckedChanged += (_, _) => Reload();
        _grid.SelectionChanged += (_, _) => OnRowSelected();
        _grid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            _newMode = false;
            OnRowSelected();
        };
        _btnNew.Click              += (_, _) => NewEntity();
        _btnSave.Click             += (_, _) => Save();
        _btnDelete.Click           += (_, _) => DeleteCurrent();

        _filter.Items.AddRange(new object[] { "الكل", "أطباء", "مستشفيات" });
        _filter.SelectedIndex = 0;

        _type.Items.AddRange(new object[] { "طبيب", "مستشفى" });
        _type.SelectedIndex = 0;

        Reload();
        NewEntity();
    }

    private Control BuildGridArea()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 12, 20) };

        var title = new Label
        {
            Text = "الأطباء والمستشفيات",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleRight
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 44 };

        _search.Location = new Point(bar.Width - 250, 8);
        _search.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _search.PlaceholderText = "بحث بالاسم أو الرقم...";

        _filter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _showInactive.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        bar.Controls.Add(_search);
        bar.Controls.Add(_filter);
        bar.Controls.Add(_showInactive);

        bar.Resize += (_, _) =>
        {
            _search.Location = new Point(bar.Width - _search.Width - 2, 8);
            _filter.Location = new Point(_search.Left - _filter.Width - 8, 8);
            _showInactive.Location = new Point(_filter.Left - _showInactive.Width - 12, 13);
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

        int y = 10;

        Label Head(string text)
        {
            var l = new Label
            {
                Text = text, Font = AppTheme.UiFont, ForeColor = Color.DimGray,
                Location = new Point(16, y), Size = new Size(292, 18),
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
            };
            y += 20;
            panel.Controls.Add(l);
            return l;
        }

        void Field(Control c)
        {
            c.Location = new Point(16, y);
            c.Width = 292;
            c.Font = AppTheme.UiFont;
            c.RightToLeft = RightToLeft.Yes;
            if (c is TextBox tb) tb.TextAlign = HorizontalAlignment.Right;
            y += c.Height + 12;
            panel.Controls.Add(c);
        }

        _editorTitle.Text = "سجل جديد";
        _editorTitle.Font = AppTheme.TitleFont;
        _editorTitle.ForeColor = AppTheme.Sidebar;
        _editorTitle.Location = new Point(16, y);
        _editorTitle.Size = new Size(292, 32);
        _editorTitle.TextAlign = ContentAlignment.MiddleRight;
        panel.Controls.Add(_editorTitle);
        y += 42;

        Head("النوع");
        Field(_type);

        Head("الاسم *");
        Field(_name);

        _lblLicense.Text = "رقم النقابة *";
        panel.Controls.Add(Configure(_lblLicense, ref y));
        Field(_license);

        _lblMajor.Text = "التخصص";
        panel.Controls.Add(Configure(_lblMajor, ref y));
        Field(_major);

        Head("العنوان");
        Field(_address);

        _lblPhone1.Text = "هاتف الطبيب";
        panel.Controls.Add(Configure(_lblPhone1, ref y));
        Field(_phone1);
        Field(_phone2);

        _lblClinic.Text = "هاتف العيادة";
        panel.Controls.Add(Configure(_lblClinic, ref y));
        Field(_clinic1);
        Field(_clinic2);

        _active.Location = new Point(16, y);
        panel.Controls.Add(_active);
        y += 30;

        _seqInfo.Text = "";
        _seqInfo.Font = new Font("Segoe UI", 9F);
        _seqInfo.ForeColor = Color.Gray;
        _seqInfo.Location = new Point(16, y);
        _seqInfo.Size = new Size(292, 20);
        _seqInfo.TextAlign = ContentAlignment.MiddleRight;
        panel.Controls.Add(_seqInfo);
        y += 30;

        foreach (var b in new[] { _btnSave, _btnNew, _btnDelete })
        {
            b.Height = 34;
            b.Font = AppTheme.UiFont;
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.FlatAppearance.BorderColor = AppTheme.Line;
        }

        _btnSave.BackColor = AppTheme.Accent;
        _btnSave.ForeColor = Color.White;
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.SetBounds(174, y, 134, 34);

        _btnNew.BackColor = Color.White;
        _btnNew.SetBounds(16, y, 150, 34);
        y += 44;

        _btnDelete.BackColor = Color.White;
        _btnDelete.ForeColor = Color.Firebrick;
        _btnDelete.SetBounds(16, y, 292, 34);

        panel.Controls.Add(_btnSave);
        panel.Controls.Add(_btnNew);
        panel.Controls.Add(_btnDelete);

        return panel;
    }

    private Label Configure(Label l, ref int y)
    {
        l.Font = AppTheme.UiFont;
        l.ForeColor = Color.DimGray;
        l.Location = new Point(16, y);
        l.Size = new Size(292, 18);
        l.TextAlign = ContentAlignment.MiddleRight;
        l.RightToLeft = RightToLeft.Yes;
        y += 20;
        return l;
    }

    private bool IsDoctor => _type.SelectedIndex == 0;

    private void ApplyTypeLabels()
    {
        _lblLicense.Text = IsDoctor ? "رقم النقابة *" : "الرقم الوطني *";
        _lblPhone1.Text  = IsDoctor ? "هاتف الطبيب" : "موبايل المستشفى";
        _lblClinic.Text  = IsDoctor ? "هاتف العيادة" : "هاتف المستشفى";
        _lblMajor.Visible = IsDoctor;
        _major.Visible    = IsDoctor;
    }

    private void Reload()
    {
        try
        {
            _all = EntityRepository.GetAll(_showInactive.Checked);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في تحميل البيانات:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        string q = _search.Text.Trim();
        IEnumerable<Entity> rows = _all;

        if (_filter.SelectedIndex == 1) rows = rows.Where(e => e.Type == EntityType.Doctor);
        else if (_filter.SelectedIndex == 2) rows = rows.Where(e => e.Type == EntityType.Hospital);

        if (q.Length > 0)
            rows = rows.Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || e.LicenseNumber.Contains(q, StringComparison.OrdinalIgnoreCase));

        _view = rows.ToList();

        _suspendSelection = true;
        _grid.DataSource = _view.Select(e => new
        {
            النوع     = e.TypeLabel,
            الاسم     = e.Name,
            الرقم     = e.LicenseNumber,
            التخصص    = e.Major ?? "",
            هاتف      = Join(e.Phone1, e.Phone2),
            العيادة   = Join(e.ClinicPhone1, e.ClinicPhone2),
            آخر_رقم   = e.LastSequence,
            الحالة    = e.IsActive ? "مفعّل" : "معطّل"
        }).ToList();

        if (_grid.Columns.Count > 0)
        {
            _grid.Columns["الاسم"]!.FillWeight = 200;
            _grid.Columns["التخصص"]!.FillWeight = 150;
            _grid.Columns["النوع"]!.FillWeight = 70;
        }

        _grid.ClearSelection();
        _suspendSelection = false;
    }

    private void OnRowSelected()
    {
        if (_suspendSelection || _newMode) return;
        if (_grid.CurrentRow == null) return;
        int i = _grid.CurrentRow.Index;
        if (i < 0 || i >= _view.Count) return;
        LoadEntity(_view[i]);
    }

    private void LoadEntity(Entity e)
    {
        _newMode = false;
        _current = e;
        _editorTitle.Text = "تعديل السجل";
        _type.SelectedIndex = e.Type == EntityType.Doctor ? 0 : 1;
        _name.Text    = e.Name;
        _license.Text = e.LicenseNumber;
        _major.Text   = e.Major ?? "";
        _address.Text = e.Address ?? "";
        _phone1.Text  = e.Phone1 ?? "";
        _phone2.Text  = e.Phone2 ?? "";
        _clinic1.Text = e.ClinicPhone1 ?? "";
        _clinic2.Text = e.ClinicPhone2 ?? "";
        _active.Checked = e.IsActive;
        _seqInfo.Text = $"آخر رقم تسلسلي مستخدم: {e.LastSequence}";
        _btnDelete.Enabled = true;
    }

    private void NewEntity()
    {
        _newMode = true;
        _current = null;
        _editorTitle.Text = "سجل جديد";
        _type.SelectedIndex = 0;
        _name.Clear(); _license.Clear(); _major.Clear(); _address.Clear();
        _phone1.Clear(); _phone2.Clear(); _clinic1.Clear(); _clinic2.Clear();
        _active.Checked = true;
        _seqInfo.Text = "";
        _btnDelete.Enabled = false;
        _grid.ClearSelection();
        _name.Focus();
    }

    private void Save()
    {
        if (_name.Text.Trim().Length == 0)
        {
            MessageBox.Show("الاسم مطلوب.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _name.Focus();
            return;
        }

        if (_license.Text.Trim().Length == 0)
        {
            MessageBox.Show(IsDoctor ? "رقم النقابة مطلوب." : "الرقم الوطني مطلوب.",
                "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _license.Focus();
            return;
        }

        var e = _current ?? new Entity();
        e.Type          = IsDoctor ? EntityType.Doctor : EntityType.Hospital;
        e.Name          = _name.Text.Trim();
        e.LicenseNumber = _license.Text.Trim();
        e.Major         = IsDoctor ? NullIfBlank(_major.Text) : null;
        e.Address       = NullIfBlank(_address.Text);
        e.Phone1        = NullIfBlank(_phone1.Text);
        e.Phone2        = NullIfBlank(_phone2.Text);
        e.ClinicPhone1  = NullIfBlank(_clinic1.Text);
        e.ClinicPhone2  = NullIfBlank(_clinic2.Text);
        e.IsActive      = _active.Checked;

        try
        {
            if (_current == null)
            {
                EntityRepository.Insert(e);
                Reload();
                NewEntity();
                MessageBox.Show("تم الحفظ.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                EntityRepository.Update(e);
                int id = e.EntityId;
                Reload();
                SelectById(id);
                MessageBox.Show("تم التحديث.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            MessageBox.Show("هذا الرقم مسجّل مسبقاً لجهة أخرى.",
                "رقم مكرر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _license.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر الحفظ:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteCurrent()
    {
        if (_current == null) return;

        if (EntityRepository.HasOrders(_current.EntityId))
        {
            var ask = MessageBox.Show(
                "لا يمكن حذف هذا السجل لوجود طلبات مرتبطة به.\n\nهل تريد تعطيله بدلاً من ذلك؟",
                "غير قابل للحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (ask == DialogResult.Yes)
            {
                EntityRepository.SetActive(_current.EntityId, false);
                Reload();
                NewEntity();
            }
            return;
        }

        var confirm = MessageBox.Show(
            $"حذف \"{_current.Name}\" نهائياً؟", "تأكيد الحذف",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        EntityRepository.Delete(_current.EntityId);
        Reload();
        NewEntity();
    }

    private void SelectById(int id)
    {
        int i = _view.FindIndex(x => x.EntityId == id);
        if (i >= 0 && i < _grid.Rows.Count)
        {
            _newMode = false;
            _grid.ClearSelection();
            _grid.Rows[i].Selected = true;
            _grid.CurrentCell = _grid.Rows[i].Cells[0];
        }
    }

    private static string Join(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b ?? "";
        if (string.IsNullOrWhiteSpace(b)) return a;
        return $"{a} / {b}";
    }

    private static string? NullIfBlank(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}