using Everest.App.Models;

namespace Everest.App.Printing;

public enum SheetKind { Cover, Body, Blank }

public class Sheet
{
    public SheetKind Kind { get; init; }
    public TemplateDef? Template { get; init; }
    public PrintData? Right { get; init; }
    public PrintData? Left { get; init; }
    public int OrderId { get; init; }
    public string Label { get; init; } = "";
}

public static class SheetPlan
{
    /// <summary>
    /// Builds the full sheet sequence for one order.
    /// Two-up: sheet i carries right = start+i-1 and left = start+half+i-1.
    /// Blocks of (cover + perNotebook bodies + blank) repeat notebooks/2 times.
    /// An odd NotebookCount in two-up mode gets floor(N/2) doubly blocks covering
    /// the first N-1 notebooks, then one single-up block for the last notebook.
    /// </summary>
    public static List<Sheet> Build(Order order, Entity entity,
                                    TemplateDef coverDouble, TemplateDef coverSingle,
                                    TemplateDef bodyDouble, TemplateDef bodySingle,
                                    bool twoUp)
    {
        var sheets = new List<Sheet>();

        int total = order.TotalAppointments;
        int per   = order.AppointmentsPerNotebook;
        string yy = order.StartSerial[..2];
        int start = int.Parse(order.StartSerial[2..]);   // sequence only, no year prefix

        string Ser(int n) => yy + n.ToString("D6");

        PrintData D(int serialNumber) => new()
        {
            Entity = entity,
            Serial = Ser(serialNumber)
        };

        // Cover numbering is per-order, 1..NotebookCount, independent of serials.
        PrintData C(int notebookNo) => new()
        {
            Entity = entity,
            Serial = Ser(start),
            NotebookNumber = notebookNo.ToString("D4")
        };

        if (!twoUp)
        {
            int blocks = order.NotebookCount;
            for (int b = 0; b < blocks; b++)
            {
                int first = start + b * per;

                sheets.Add(new Sheet
                {
                    Kind = SheetKind.Cover, Template = coverSingle,
                    Right = C(b + 1), OrderId = order.OrderId,
                    Label = $"غلاف — دفتر {b + 1:D4}"
                });

                for (int i = 0; i < per; i++)
                {
                    int s = first + i;
                    sheets.Add(new Sheet
                    {
                        Kind = SheetKind.Body, Template = bodySingle,
                        Right = D(s), OrderId = order.OrderId,
                        Label = $"وصفة — {Ser(s)}"
                    });
                }

                sheets.Add(new Sheet
                {
                    Kind = SheetKind.Blank, OrderId = order.OrderId, Label = "صفحة فارغة"
                });
            }
            return sheets;
        }

        if (order.NotebookCount % 2 != 0)
        {
            int d = order.NotebookCount - 1;
            int doublyTotal = d * per;
            int halfD = doublyTotal / 2;
            int blocksD = d / 2;

            for (int b = 0; b < blocksD; b++)
            {
                int firstSheet = b * per;

                int nbRight = b + 1;
                int nbLeft  = b + 1 + d / 2;

                sheets.Add(new Sheet
                {
                    Kind = SheetKind.Cover, Template = coverDouble,
                    Right = C(nbRight),
                    Left  = C(nbLeft),
                    OrderId = order.OrderId,
                    Label = $"غلاف — يمين {nbRight:D4} / يسار {nbLeft:D4}"
                });

                for (int i = 0; i < per; i++)
                {
                    int sheetIndex = firstSheet + i;
                    int r = start + sheetIndex;
                    int l = start + halfD + sheetIndex;

                    sheets.Add(new Sheet
                    {
                        Kind = SheetKind.Body, Template = bodyDouble,
                        Right = D(r),
                        Left  = D(l),
                        OrderId = order.OrderId,
                        Label = $"وصفة — يمين {Ser(r)} / يسار {Ser(l)}"
                    });
                }

                sheets.Add(new Sheet
                {
                    Kind = SheetKind.Blank, OrderId = order.OrderId, Label = "صفحة فارغة"
                });
            }

            sheets.Add(new Sheet
            {
                Kind = SheetKind.Cover, Template = coverSingle,
                Right = C(order.NotebookCount), OrderId = order.OrderId,
                Label = $"غلاف — دفتر {order.NotebookCount:D4}"
            });

            for (int i = 0; i < per; i++)
            {
                int s = start + doublyTotal + i;
                sheets.Add(new Sheet
                {
                    Kind = SheetKind.Body, Template = bodySingle,
                    Right = D(s), OrderId = order.OrderId,
                    Label = $"وصفة — {Ser(s)}"
                });
            }

            sheets.Add(new Sheet
            {
                Kind = SheetKind.Blank, OrderId = order.OrderId, Label = "صفحة فارغة"
            });

            return sheets;
        }

        int half   = (total + 1) / 2;      // right pile holds the first half
        int blocks2 = Math.Max(1, order.NotebookCount / 2);
        int perBlock = half / blocks2;     // body sheets per block

        for (int b = 0; b < blocks2; b++)
        {
            int firstSheet = b * perBlock;                  // 0-based sheet index
            int rightFirst = start + firstSheet;
            int leftFirst  = start + half + firstSheet;

            int nbRight = b + 1;
            int nbLeft  = b + 1 + order.NotebookCount / 2;

            sheets.Add(new Sheet
            {
                Kind = SheetKind.Cover, Template = coverDouble,
                Right = C(nbRight),
                Left  = nbLeft <= order.NotebookCount ? C(nbLeft) : null,
                OrderId = order.OrderId,
                Label = $"غلاف — يمين {nbRight:D4} / يسار {nbLeft:D4}"
            });

            for (int i = 0; i < perBlock; i++)
            {
                int sheetIndex = firstSheet + i;
                int r = start + sheetIndex;
                int l = start + half + sheetIndex;

                sheets.Add(new Sheet
                {
                    Kind = SheetKind.Body, Template = bodyDouble,
                    Right = D(r),
                    Left  = l < start + total ? D(l) : null,
                    OrderId = order.OrderId,
                    Label = $"وصفة — يمين {Ser(r)} / يسار {Ser(l)}"
                });
            }

            sheets.Add(new Sheet
            {
                Kind = SheetKind.Blank, OrderId = order.OrderId, Label = "صفحة فارغة"
            });
        }

        return sheets;
    }

    /// <summary>Index of the first body sheet carrying the given serial, or 0.</summary>
    public static int IndexOfSerial(List<Sheet> sheets, string serial)
    {
        for (int i = 0; i < sheets.Count; i++)
        {
            if (sheets[i].Kind != SheetKind.Body) continue;
            if (sheets[i].Right?.Serial == serial || sheets[i].Left?.Serial == serial)
                return i;
        }
        return 0;
    }
}