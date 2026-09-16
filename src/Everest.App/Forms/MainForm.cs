using Everest.App.Data;

namespace Everest.App.Forms;

public class MainForm : Form
{
    private readonly Panel _content = new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        Padding = new Padding(0)
    };

    private readonly Panel _sidebar = new()
    {
        Dock = DockStyle.Right,
        Width = 210,
        BackColor = AppTheme.Sidebar
    };

    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 26,
        TextAlign = ContentAlignment.MiddleRight,
        BackColor = AppTheme.Surface,
        ForeColor = Color.DimGray,
        Font = new Font("Segoe UI", 9F),
        Padding = new Padding(10, 0, 10, 0)
    };

    private readonly List<Button> _navButtons = new();

    public MainForm()
    {
        Text = "إيفرست — نظام طباعة دفاتر الوصفات";
        Width = 1200;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        Font = AppTheme.UiFont;
        MinimumSize = new Size(1000, 620);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = AppTheme.Accent,
            Padding = new Padding(20, 0, 20, 0)
        };
        header.Controls.Add(new Label
        {
            Text = "إيفرست للطباعة الأمنية",
            Font = AppTheme.TitleFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        });

        BuildSidebar();

        Controls.Add(_content);
        Controls.Add(_sidebar);
        Controls.Add(header);
        Controls.Add(_status);

        Load += (_, _) =>
        {
            CheckDb();
            Navigate(0);
        };
    }

    private void BuildSidebar()
    {
        var items = new (string Text, Func<UserControl> Open)[]
        {
            ("الأطباء والمستشفيات", () => new EntitiesPage()),
            ("الطلبات",             () => new OrdersPage()),
            ("الطباعة",             () => new PrintPage()),
            ("مصمم القوالب",        () => new TemplateDesignerPage()),
            ("التقارير",            () => new ReportsPage()),
            ("الإعدادات",           () => new SettingsPage()),
        };

        for (int i = items.Length - 1; i >= 0; i--)
        {
            int index = i;
            var btn = new Button
            {
                Text = items[i].Text,
                Dock = DockStyle.Top,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Sidebar,
                ForeColor = Color.Gainsboro,
                Font = AppTheme.UiFont,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 16, 0),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AppTheme.SidebarHot;
            btn.Click += (_, _) => Navigate(index);

            _sidebar.Controls.Add(btn);
            _navButtons.Insert(0, btn);
        }

        _navActions = items.Select(x => x.Open).ToArray();
        _sidebar.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.Sidebar });
    }

    private Func<UserControl>[] _navActions = Array.Empty<Func<UserControl>>();
    private readonly Dictionary<int, UserControl> _pages = new();

    private void Navigate(int index)
    {
        for (int i = 0; i < _navButtons.Count; i++)
        {
            _navButtons[i].BackColor = i == index ? AppTheme.SidebarHot : AppTheme.Sidebar;
            _navButtons[i].ForeColor = i == index ? Color.White : Color.Gainsboro;
        }

        if (!_pages.TryGetValue(index, out var page))
        {
            page = _navActions[index]();
            _pages[index] = page;
        }

        Show(page);
    }

    private void Show(UserControl page)
    {
        _content.Controls.Clear();
        _content.Controls.Add(page);
    }

    private void CheckDb()
    {
        try
        {
            using var conn = Db.Open();
            _status.Text = $"متصل بقاعدة البيانات  •  {DateTime.Now.ToString(AppTheme.DateFormat)}";
            _status.ForeColor = Color.FromArgb(30, 110, 60);
        }
        catch (Exception ex)
        {
            _status.Text = "تعذر الاتصال بقاعدة البيانات";
            _status.ForeColor = Color.Firebrick;
            MessageBox.Show("فشل الاتصال بقاعدة البيانات:\n\n" + ex.Message,
                "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}