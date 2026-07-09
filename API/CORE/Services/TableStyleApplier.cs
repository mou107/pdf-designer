using System.Drawing;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services
{
    /// <summary>
    /// Applique le style de tableau choisi (1-3) au rendu, comme les 3 tableaux pdfmake d'Axiobat :
    ///   1 = bordures completes (grille), sans trame de lignes
    ///   2 = lignes alternees sans bordures
    ///   3 = entete plein + bordures (grille + trame)
    /// </summary>
    public static class TableStyleApplier
    {
        public static void Apply(StiReport report, int style)
        {
            if (style is < 1 or > 3) return;

            foreach (StiPage page in report.Pages)
            {
                foreach (StiComponent c in page.Components)
                {
                    if (c is not StiBand band) continue;
                    if (band.Name == "EnteteLignes") StyleHeader(band, style);
                    else if (band.Name == "Lignes") StyleData(band, style);
                }
            }
        }

        private static void StyleHeader(StiBand band, int style)
        {
            foreach (StiComponent comp in band.Components)
            {
                if (comp is not StiText t) continue;
                t.Border = style == 2
                    ? new StiBorder(StiBorderSides.None, Color.Transparent, 0, StiPenStyle.Solid)
                    : new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
            }
        }

        private static void StyleData(StiBand band, int style)
        {
            foreach (StiComponent comp in band.Components)
            {
                if (comp is not StiText t) continue;

                // Trame de lignes (RowBg) : desactivee en style 1 (grille sans trame).
                if (t.Name == "RowBg")
                {
                    if (style == 1)
                    {
                        t.Brush = new StiSolidBrush(Color.White);
                        t.Conditions?.Clear();
                    }
                    continue;
                }

                t.Border = style switch
                {
                    1 => new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid),
                    3 => new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid),
                    _ => new StiBorder(StiBorderSides.None, Color.Transparent, 0, StiPenStyle.Solid) // style 2 : sans bordures
                };
            }
        }
    }
}
