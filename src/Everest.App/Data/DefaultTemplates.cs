using Everest.App.Models;

namespace Everest.App.Data;

public static class DefaultTemplates
{
    public const double SheetW = 290;
    public const double SheetH = 270;
    private const double Half  = SheetW / 2.0;

    public static void EnsureCreated()
    {
        AppPaths.EnsureFolders();
        foreach (var t in BuildAll())
            if (!TemplateStore.Exists(t.Category, t.Name))
                TemplateStore.Save(t);

        if (!TemplateStore.Exists(TemplateCategory.Cover, "اختبار A4 — غلاف طبيب"))
            CreateA4TestSet();
    }

    public static void RestoreAll()
    {
        AppPaths.EnsureFolders();
        foreach (var t in BuildAll()) TemplateStore.Save(t);
    }

    private static IEnumerable<TemplateDef> BuildAll()
    {
        yield return DoctorDouble();
        yield return DoctorSingle();
        yield return HospitalDouble();
        yield return HospitalSingle();
        yield return Cover();
        yield return CoverHospital();
        yield return CoverSingle();
        yield return CoverHospitalSingle();
        yield return SampleFor(false);
        yield return SampleFor(true);
        yield return DeliveryNote();
    }

    /// <summary>Copy of a template rescaled to a different sheet size.</summary>
    private static TemplateDef ScaledTo(TemplateDef src, string name, double w, double h)
    {
        double sx = w / src.PageWidthMm;
        double sy = h / src.PageHeightMm;

        var t = new TemplateDef
        {
            Name = name,
            Category = src.Category,
            PdfFile = "",
            PrintArtwork = false,
            PageWidthMm = w,
            PageHeightMm = h,
            OffsetXMm = 0,
            OffsetYMm = 0
        };

        foreach (var f in src.Fields)
        {
            var c = f.Clone();
            c.XMm     = Math.Round(f.XMm * sx, 1);
            c.YMm     = Math.Round(f.YMm * sy, 1);
            c.WidthMm = Math.Round(f.WidthMm * sx, 1);
            c.FontSize = Math.Round(f.FontSize * Math.Min(sx, sy), 1);
            t.Fields.Add(c);
        }

        return t;
    }

    public static void CreateA4TestSet()
    {
        AppPaths.EnsureFolders();
        TemplateStore.Save(ScaledTo(DoctorDouble(),   "اختبار A4 — أطباء مزدوج",     297, 210));
        TemplateStore.Save(ScaledTo(HospitalDouble(), "اختبار A4 — مستشفيات مزدوج", 297, 210));
     TemplateStore.Save(ScaledTo(Cover(),         "اختبار A4 — غلاف طبيب",   297, 210));
        TemplateStore.Save(ScaledTo(CoverHospital(), "اختبار A4 — غلاف مستشفى", 297, 210));    }

    private static TemplateField F(string token, double x, double y, double w,
                                   double size, bool bold = false,
                                   FieldAlign align = FieldAlign.Right,
                                   string staticText = "", int rot = 0) => new()
    {
        Token = token,
        StaticText = staticText,
        Slot = FieldSlot.Right,
        XMm = x, YMm = y, WidthMm = w,
        FontName = AppFonts.PrintFamily,
        FontSize = size,
        Bold = bold,
        Align = align,
        Rotation = rot
    };

    private static void Mirror(TemplateDef t)
    {
        foreach (var r in t.Fields.Where(f => f.Slot == FieldSlot.Right).ToList())
        {
            var c = r.Clone();
            c.Slot = FieldSlot.Left;
            c.XMm = Math.Round(Math.Max(0, r.XMm - Half), 1);
            t.Fields.Add(c);
        }
    }

    /// <summary>Right-half field layout measured off the supplied samples.</summary>
    private static List<TemplateField> BodyRightHalf(bool hospital)
    {
        var f = new List<TemplateField>
        {
            // top strip
            F("Name",   232, 30.5, 52, 9.5, true),
            F("Serial", 155, 30.5, 22, 11, false, FieldAlign.Center),

            // main block
            F("Name",   232, 79.5, 52, 9.5, true),
            F("Major",  232, 86.0, 52, 8.5),
            F("Serial", 260, 103.0, 24, 11, false, FieldAlign.Center),
            // contact column
            F(hospital ? "Phone1"       : "Phone1",       150, 84.0, 40, 7.5),
            F(hospital ? "ClinicPhone1" : "ClinicPhone1", 150, 88.5, 40, 7.5),
            F("AddressLine1", 148, 93.0, 74, 7.5),
            F("AddressLine2", 148, 97.5, 74, 7.5),
        };
        if (hospital)
            f.Add(F("Static", 149, 150, 84, 8.5, false, FieldAlign.Right,
                    "هذه الوصفة للإستخدام داخل المستشفى فقط", 90));

        return f;
    }

    public static TemplateDef DoctorDouble()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — أطباء مزدوج",
            Category = TemplateCategory.DoctorDouble,
            PageWidthMm = SheetW,
            PageHeightMm = SheetH
        };
        t.Fields.AddRange(BodyRightHalf(false));
        Mirror(t);
        return t;
    }

    public static TemplateDef HospitalDouble()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — مستشفيات مزدوج",
            Category = TemplateCategory.HospitalDouble,
            PageWidthMm = SheetW,
            PageHeightMm = SheetH
        };
        t.Fields.AddRange(BodyRightHalf(true));
        Mirror(t);
        return t;
    }

    public static TemplateDef DoctorSingle()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — أطباء مفرد",
            Category = TemplateCategory.DoctorSingle,
            PageWidthMm = Half,
            PageHeightMm = SheetH
        };

        foreach (var f in BodyRightHalf(false))
        {
            var c = f.Clone();
            c.Slot = FieldSlot.Both;
            c.XMm = Math.Round(Math.Max(0, f.XMm - Half), 1);
            t.Fields.Add(c);
        }
        return t;
    }

    public static TemplateDef HospitalSingle()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — مستشفيات مفرد",
            Category = TemplateCategory.HospitalSingle,
            PageWidthMm = Half,
            PageHeightMm = SheetH
        };

        foreach (var f in BodyRightHalf(true))
        {
            var c = f.Clone();
            c.Slot = FieldSlot.Both;
            c.XMm = Math.Round(Math.Max(0, f.XMm - Half), 1);
            t.Fields.Add(c);
        }
        return t;
    }

    public static TemplateDef CoverHospital()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — غلاف مستشفى",
            Category = TemplateCategory.CoverHospital,
            PageWidthMm = SheetW,
            PageHeightMm = SheetH
        };

        t.Fields.Add(F("Name",           180, 199.0, 50, 9.5, true, FieldAlign.Center));
        t.Fields.Add(F("NotebookNumber", 180, 226.5, 50, 11,  false, FieldAlign.Center));

        Mirror(t);
        return t;
    }

    public static TemplateDef Cover()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — غلاف طبيب",
            Category = TemplateCategory.Cover,
            PageWidthMm = SheetW,
            PageHeightMm = SheetH
        };

        t.Fields.Add(F("Name",   180, 195.5, 50, 9.5, true, FieldAlign.Center));
        t.Fields.Add(F("Major",  180, 203.0, 50, 9.0, true, FieldAlign.Center));
        t.Fields.Add(F("NotebookNumber", 180, 226.5, 50, 11, false, FieldAlign.Center));

        Mirror(t);
        return t;
    }

    public static TemplateDef CoverSingle()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — غلاف طبيب مفرد",
            Category = TemplateCategory.CoverSingle,
            PageWidthMm = Half,
            PageHeightMm = SheetH
        };

        foreach (var f in Cover().Fields.Where(f => f.Slot == FieldSlot.Right))
        {
            var c = f.Clone();
            c.Slot = FieldSlot.Both;
            c.XMm = Math.Round(Math.Max(0, f.XMm - Half), 1);
            t.Fields.Add(c);
        }
        return t;
    }

    public static TemplateDef CoverHospitalSingle()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — غلاف مستشفى مفرد",
            Category = TemplateCategory.CoverHospitalSingle,
            PageWidthMm = Half,
            PageHeightMm = SheetH
        };

        foreach (var f in CoverHospital().Fields.Where(f => f.Slot == FieldSlot.Right))
        {
            var c = f.Clone();
            c.Slot = FieldSlot.Both;
            c.XMm = Math.Round(Math.Max(0, f.XMm - Half), 1);
            t.Fields.Add(c);
        }
        return t;
    }

    /// <summary>Audit sample — 150 × 270 mm, coordinates measured from the supplied PDF.</summary>
    private static TemplateDef SampleFor(bool hospital)
    {
        var t = new TemplateDef
        {
            Name = hospital ? "افتراضي — عينة مستشفى" : "افتراضي — عينة طبيب",
            Category = TemplateCategory.Sample,
            PdfFile = hospital ? "عينة_المستشفى.pdf" : "عينة_الدكتور.pdf",
            PrintArtwork = true,
            PageWidthMm = 150,
            PageHeightMm = 270
        };

        TemplateField S(string token, double x, double y, double w, double size,
                        FieldAlign a = FieldAlign.Right, int rot = 0)
        {
            var f = F(token, x, y, w, size, false, a, "", rot);
            f.Slot = FieldSlot.Both;
            return f;
        }

        t.Fields.Add(S("Name",         42.8, 26.5, 100,  14.3));
        t.Fields.Add(S("Name",         41.8, 68.0, 100,  14.3));
        t.Fields.Add(S("Phone1",        0,   71.2,  45.8, 13.4));
        t.Fields.Add(S("ClinicPhone1",  0,   77.7,  42.8, 13.4));
        t.Fields.Add(S("Address",       0,   83.7,  52.4, 13.4));

        if (hospital)
            t.Fields.Add(S("Static", 13.4, 109.0, 78, 11.4, FieldAlign.Right, 90));
        else
            t.Fields.Add(S("Major", 41.8, 76.0, 100, 13.4));

        if (hospital)
            t.Fields[^1].StaticText = "هذه الوصفة للإستخدام داخل المستشفى فقط";

        return t;
    }

    /// <summary>Union delivery note — A4 landscape, table-driven.</summary>
    public static TemplateDef DeliveryNote()
    {
        var t = new TemplateDef
        {
            Name = "افتراضي — سند التسليم",
            Category = TemplateCategory.DeliveryNote,
            PrintArtwork = false,
            PageWidthMm = 297,
            PageHeightMm = 210,
            LogoFile = "logo.pdf",
            LogoXMm = 238.3, LogoYMm = 10.0,
            LogoWidthMm = 47.8, LogoHeightMm = 23.9
        };

        t.Table = new TableSpec
        {
            HeaderYMm = 50, HeaderHeightMm = 8,
            DataTopMm = 58, DataBottomMm = 160,
            NaturalRowHeightMm = 8, MinRowHeightMm = 4.5,
            TotalsHeightMm = 7, TotalsSplitXMm = 70,
            TotalsLabel = "مجموع عدد الدفاتر",
            LeftEdgeMm = 12, RightEdgeMm = 287,
            FontName = AppFonts.PrintFamily, FontSize = 12.5,
            Columns =
            {
                new TableColumn { Token = "RowNumber",   Header = "التسلسل",              X0Mm = 268, X1Mm = 287 },
                new TableColumn { Token = "EntityName",  Header = "اسم الدكتور/المستشفى", X0Mm = 192, X1Mm = 268, Align = FieldAlign.Right },
                new TableColumn { Token = "Phones",      Header = "رقم الهاتف",           X0Mm =  86, X1Mm = 192, Align = FieldAlign.Right },
                new TableColumn { Token = "Notebooks",   Header = "عدد الدفاتر",          X0Mm =  64, X1Mm =  86 },
                new TableColumn { Token = "StartSerial", Header = "من رقم",               X0Mm =  38, X1Mm =  64 },
                new TableColumn { Token = "EndSerial",   Header = "إلى رقم",              X0Mm =  12, X1Mm =  38 },
            }
        };

        return t;
    }

    /// <summary>Deletes every template config and rebuilds the factory set.</summary>
    public static void HardReset()
    {
        AppPaths.EnsureFolders();

        foreach (var c in Categories.All)
            foreach (var n in TemplateStore.Names(c.Cat))
                TemplateStore.Delete(c.Cat, n);

        foreach (var t in BuildAll()) TemplateStore.Save(t);
        CreateA4TestSet();
    }

    /// <summary>Repoints every saved template to the company font without touching positions.</summary>
    public static int ApplyFont(string family)
    {
        int changed = 0;

        foreach (var cat in Categories.All)
        {
            foreach (var name in TemplateStore.Names(cat.Cat))
            {
                var t = TemplateStore.Load(cat.Cat, name);
                if (t == null) continue;

                bool touched = false;

                foreach (var f in t.Fields)
                    if (f.FontName != family)
                    { f.FontName = family; touched = true; }

                if (t.Table != null && t.Table.FontName != family)
                { t.Table.FontName = family; touched = true; }

                if (t.Category == TemplateCategory.DeliveryNote &&
                    !string.Equals(t.LogoFile, "logo.pdf", StringComparison.OrdinalIgnoreCase))
                {
                    t.LogoFile = "logo.pdf";
                    t.LogoXMm = 238.3; t.LogoYMm = 10.0;
                    t.LogoWidthMm = 47.8; t.LogoHeightMm = 23.9;
                    touched = true;
                }

                if (touched) { TemplateStore.Save(t); changed++; }
            }
        }

        return changed;
    }

    public static int ApplyCompanyFont() => ApplyFont(AppFonts.PrintFamily);
}
