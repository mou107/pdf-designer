using System.Drawing;
using API.CORE.Schemas;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;
using Stimulsoft.Report.Components.ShapeTypes;
using Stimulsoft.Report.Styles;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Recolorie le rapport : remplacements de couleur sur tout le modele, et styles nommes que les
    /// bandes peuvent ensuite referencer.
    /// </summary>
    /// <remarks>
    /// Le moteur ne sait pas ce que « la couleur principale » veut dire : il applique les couples
    /// avant/apres fournis par l'appelant, qui est seul a connaitre la palette de son modele.
    /// </remarks>
    internal static class ColorApplier
    {
        /// <summary>
        /// Remplace les couleurs listees partout ou elles apparaissent : fond, texte, bordure des textes
        /// et des formes. Le premier remplacement qui correspond gagne.
        /// </summary>
        public static void ApplyReplacements(StiReport report, IEnumerable<ColorReplacement>? replacements)
        {
            var pairs = (replacements ?? Enumerable.Empty<ColorReplacement>())
                .Select(replacement => (
                    From: ReportPrimitives.ParseColor(replacement.From),
                    To: ReportPrimitives.ParseColor(replacement.To),
                    replacement.Tolerance))
                .Where(pair => pair.From != null && pair.To != null)
                .Select(pair => (From: pair.From!.Value, To: pair.To!.Value, pair.Tolerance))
                .ToList();

            if (pairs.Count == 0) return;

            Color? Replace(Color color)
            {
                foreach (var pair in pairs)
                    if (ReportPrimitives.ColorsMatch(color, pair.From, pair.Tolerance))
                        return pair.To;
                return null;
            }

            foreach (var component in ReportPrimitives.AllComponents(report))
            {
                switch (component)
                {
                    case StiText text:
                        if (text.Brush is StiSolidBrush brush && Replace(brush.Color) is Color newBrush)
                            text.Brush = new StiSolidBrush(newBrush);
                        if (text.TextBrush is StiSolidBrush textBrush && Replace(textBrush.Color) is Color newTextBrush)
                            text.TextBrush = new StiSolidBrush(newTextBrush);
                        if (text.Border != null && Replace(text.Border.Color) is Color newBorder)
                            text.Border.Color = newBorder;
                        break;

                    case StiShape shape:
                        if (shape.Brush is StiSolidBrush shapeBrush && Replace(shapeBrush.Color) is Color newShapeBrush)
                            shape.Brush = new StiSolidBrush(newShapeBrush);
                        if (Replace(shape.BorderColor) is Color newShapeBorder)
                            shape.BorderColor = newShapeBorder;
                        break;
                }
            }
        }

        /// <summary>
        /// Cree (ou met a jour) les styles nommes portant un fond. Ce sont eux que les bandes designent
        /// pour l'alternance des lignes : un <c>StiCondition</c> ne poserait qu'un BackColor, ignore au
        /// rendu Stimulsoft.
        /// </summary>
        public static void ApplyStyles(StiReport report, IEnumerable<StyleDirective>? styles)
        {
            foreach (var directive in styles ?? Enumerable.Empty<StyleDirective>())
            {
                if (string.IsNullOrWhiteSpace(directive.Name)) continue;

                var color = ReportPrimitives.ParseColor(directive.BackColor);
                if (color == null) continue;

                if (report.Styles[directive.Name] is not StiStyle style)
                {
                    style = new StiStyle { Name = directive.Name };
                    report.Styles.Add(style);
                }

                style.AllowUseBrush = true;
                style.Brush = new StiSolidBrush(color.Value);
            }
        }
    }
}
