namespace Everest.App.Models;

public enum TemplateCategory
{
    DoctorSingle   = 0,
    DoctorDouble   = 1,
    HospitalSingle = 2,
    HospitalDouble = 3,
    Cover          = 4,
    Sample         = 5,
    DeliveryNote   = 6,
    CoverHospital  = 7,
    CoverSingle         = 8,
    CoverHospitalSingle = 9
}

public enum FieldSlot  { Right = 0, Left = 1, Both = 2 }
public enum FieldAlign { Right = 0, Center = 1, Left = 2 }

public class TemplateField
{
    public string Token { get; set; } = "Name";
    public string StaticText { get; set; } = "";
    public FieldSlot Slot { get; set; } = FieldSlot.Right;

    public double XMm { get; set; } = 20;
    public double YMm { get; set; } = 20;
    public double WidthMm { get; set; } = 60;

    public string FontName { get; set; } = "Arial";
    public double FontSize { get; set; } = 10;
    public bool Bold { get; set; }
    public FieldAlign Align { get; set; } = FieldAlign.Right;
    public int Rotation { get; set; }

    public TemplateField Clone() => (TemplateField)MemberwiseClone();
}

public class TemplateDef
{
    public string Name { get; set; } = "قالب جديد";
    public TemplateCategory Category { get; set; } = TemplateCategory.DoctorDouble;

    /// <summary>Artwork file name (with extension) sitting beside the .json. Empty = blank page.</summary>
    public string PdfFile { get; set; } = "";

    public double PageWidthMm  { get; set; } = 290;
    public double PageHeightMm { get; set; } = 270;

    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }

    public string LogoFile { get; set; } = "";
    public double LogoXMm { get; set; } = 238.3;
    public double LogoYMm { get; set; } = 11.7;
    public double LogoWidthMm { get; set; } = 47.8;
    public double LogoHeightMm { get; set; } = 17.8;

    /// <summary>True = the artwork is sent to the printer. False = artwork is only an on-screen guide.</summary>
    public bool PrintArtwork { get; set; }

    public List<TemplateField> Fields { get; set; } = new();

    /// <summary>Repeating-row layout. Only used by سند التسليم.</summary>
    public TableSpec? Table { get; set; }

    public bool TwoUp => Category is TemplateCategory.DoctorDouble
                                  or TemplateCategory.HospitalDouble
                                  or TemplateCategory.Cover
                                  or TemplateCategory.CoverHospital;
}

public class TableColumn
{
    /// <summary>RowNumber, EntityName, Phones, Notebooks, StartSerial, EndSerial</summary>
    public string Token { get; set; } = "EntityName";
    public string Header { get; set; } = "";
    public double X0Mm { get; set; }
    public double X1Mm { get; set; }
    public FieldAlign Align { get; set; } = FieldAlign.Center;
}

public class TableSpec
{
    public double HeaderYMm { get; set; } = 50;
    public double HeaderHeightMm { get; set; } = 8;

    public double DataTopMm { get; set; } = 58;
    public double DataBottomMm { get; set; } = 160;

    public double NaturalRowHeightMm { get; set; } = 8;
    public double MinRowHeightMm { get; set; } = 4.5;

    public double TotalsHeightMm { get; set; } = 7;
    public double TotalsSplitXMm { get; set; } = 70;
    public string TotalsLabel { get; set; } = "مجموع عدد الدفاتر";

    public double LeftEdgeMm { get; set; } = 12;
    public double RightEdgeMm { get; set; } = 287;

    public string FontName { get; set; } = "Arial";
    public double FontSize { get; set; } = 11.5;

    public List<TableColumn> Columns { get; set; } = new();

    public int NaturalMaxRows => (int)((DataBottomMm - DataTopMm) / NaturalRowHeightMm);
    public int AbsoluteMaxRows => (int)((DataBottomMm - DataTopMm) / MinRowHeightMm);

    public double RowHeightFor(int rows)
    {
        if (rows <= 0) return NaturalRowHeightMm;
        if (rows <= NaturalMaxRows) return NaturalRowHeightMm;
        return Math.Max(MinRowHeightMm, (DataBottomMm - DataTopMm) / rows);
    }

    public double FontSizeFor(double rowHeight) =>
        Math.Max(6.0, Math.Round(FontSize * (rowHeight / NaturalRowHeightMm), 1));
}

public static class Categories
{
    public static readonly (TemplateCategory Cat, string Folder, string Label)[] All =
    {
        (TemplateCategory.DoctorSingle,   "DoctorSingle",   "وصفات أطباء — مفرد"),
        (TemplateCategory.DoctorDouble,   "DoctorDouble",   "وصفات أطباء — مزدوج"),
        (TemplateCategory.HospitalSingle, "HospitalSingle", "وصفات مستشفيات — مفرد"),
        (TemplateCategory.HospitalDouble, "HospitalDouble", "وصفات مستشفيات — مزدوج"),
     (TemplateCategory.Cover,          "Cover",          "غلاف أمامي — أطباء"),
        (TemplateCategory.CoverHospital,  "CoverHospital",  "غلاف أمامي — مستشفيات"),
        (TemplateCategory.CoverSingle,         "CoverSingle",         "غلاف أمامي — أطباء مفرد"),
        (TemplateCategory.CoverHospitalSingle, "CoverHospitalSingle", "غلاف أمامي — مستشفيات مفرد"),
        (TemplateCategory.Sample,         "Sample",         "عينة للتدقيق"),
        (TemplateCategory.DeliveryNote,   "DeliveryNote",   "سند التسليم"),
    };
    public static string Folder(TemplateCategory c)
    {
        foreach (var x in All) if (x.Cat == c) return x.Folder;
        return c.ToString();
    }

    public static string Label(TemplateCategory c)
    {
        foreach (var x in All) if (x.Cat == c) return x.Label;
        return c.ToString();
    }
}

public static class Tokens
{
    public static readonly (string Token, string Label)[] All =
    {
     ("Name",         "الاسم"),
        ("Major",        "التخصص"),
     ("Address",      "العنوان"),
        ("AddressLine1", "العنوان — السطر الأول"),
        ("AddressLine2", "العنوان — السطر الثاني"),
        ("License",      "رقم النقابة / الوطني"),
        ("Phone1",       "الهاتف ١"),
        ("Phone2",       "الهاتف ٢"),
        ("ClinicPhone1", "هاتف العيادة ١"),
        ("ClinicPhone2", "هاتف العيادة ٢"),
     ("Serial",         "الرقم التسلسلي"),
        ("NotebookNumber", "رقم الدفتر (الغلاف)"),
        ("NoteNumber",     "رقم السند"),
        ("NoteDate",     "تاريخ السند"),
        ("Static",       "نص ثابت"),
    };

    public static string Label(string token) =>
        All.FirstOrDefault(t => t.Token == token).Label ?? token;

    public static string Sample(string token, string staticText) => token switch
    {
     "Name"         => "د. بلال عبد الحميد احمد الهواري",
        "Major"        => "اختصاصي معالجة الاورام بالاشعاع",
     "Address"      => "جبل عمان - 54 شارع ابن خلدون - الطابق 6",
        "AddressLine1" => "جبل عمان - 54 شارع ابن",
        "AddressLine2" => "خلدون - الطابق 6",        "License"      => "000401",
        "Phone1"       => "هاتف الدكتور : 0795971463",
        "Phone2"       => "0777777777",
        "ClinicPhone1" => "هاتف العيادة : 0790651971",
        "ClinicPhone2" => "065101010",
        "Serial"         => "26000001",
        "NotebookNumber" => "0001",
        "NoteNumber"     => "1038",
        "NoteDate"     => DateTime.Now.ToString("dd/MM/yyyy"),
        _              => string.IsNullOrEmpty(staticText) ? "نص" : staticText
    };
}