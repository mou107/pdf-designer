using System.Drawing;
using API.CORE.Schemas;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Pose des conditions Stimulsoft sur les cellules d'une bande.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Une bande de donnees rend toutes ses lignes avec les MEMES composants : il est donc impossible de
    /// fixer une police differente par ligne directement sur la cellule. Les conditions sont le mecanisme
    /// prevu pour cela — elles sont evaluees ligne par ligne, sur l'expression fournie par l'appelant.
    /// </para>
    /// <para>
    /// Le format des valeurs (montants, dates) est laisse intact : une condition ne touche qu'a la mise
    /// en forme, la ou envelopper le contenu dans du HTML obligerait a reformater soi-meme.
    /// </para>
    /// </remarks>
    internal static class ConditionsApplier
    {
        public static void Apply(StiReport report, IEnumerable<ConditionDirective>? directives)
        {
            foreach (var directive in directives ?? Enumerable.Empty<ConditionDirective>())
            {
                if (string.IsNullOrWhiteSpace(directive.Expression)) continue;

                var include = directive.Components is { Count: > 0 }
                    ? new HashSet<string>(directive.Components, StringComparer.Ordinal)
                    : null;
                var exclude = new HashSet<string>(directive.ExcludeComponents ?? new List<string>(), StringComparer.Ordinal);

                foreach (var band in ReportPrimitives.BandsNamed(report, directive.Band).ToList())
                {
                    foreach (StiComponent component in band.Components)
                    {
                        if (component is not StiText cell) continue;

                        var name = cell.Name ?? string.Empty;
                        if (include != null && !include.Contains(name)) continue;
                        if (exclude.Contains(name)) continue;

                        cell.Conditions.Add(BuildCondition(cell, directive));
                    }
                }
            }
        }

        private static StiCondition BuildCondition(StiText cell, ConditionDirective directive)
        {
            var fontStyle = FontStyle.Regular;
            if (directive.Bold) fontStyle |= FontStyle.Bold;
            if (directive.Italic) fontStyle |= FontStyle.Italic;
            if (directive.Underline) fontStyle |= FontStyle.Underline;

            // On repart de la police de la cellule : un modele personnalise peut avoir change la famille,
            // et la directive ne porte que la taille.
            var family = cell.Font?.FontFamily?.Name ?? "Arial";
            var size = (float)(directive.FontSize ?? cell.Font?.Size ?? 8.5f);

            return new StiCondition
            {
                Item = StiFilterItem.Expression,
                Expression = directive.Expression,

                // Stimulsoft.Drawing.Font et NON System.Drawing.Font : ce dernier est un objet GDI+
                // marque Windows-only, qui leverait dans le conteneur Linux de production.
                Font = new Stimulsoft.Drawing.Font(family, size, fontStyle),

                // TextColor n'est pas nullable et vaut Red par defaut : sans couleur demandee, on repose
                // celle de la cellule pour ne pas repeindre la ligne en rouge.
                TextColor = ReportPrimitives.ParseColor(directive.TextColor) ?? CellColor(cell),

                // Une condition n'applique QUE ce que ses permissions autorisent, et le defaut n'inclut
                // pas la police : sans cette ligne, elle serait evaluee sans rien changer. Fond et
                // bordures sont volontairement exclus — ils sont pilotes par les autres directives.
                Permissions = StiConditionPermissions.Font
                    | StiConditionPermissions.FontSize
                    | StiConditionPermissions.FontStyleBold
                    | StiConditionPermissions.FontStyleItalic
                    | StiConditionPermissions.FontStyleUnderline
                    | StiConditionPermissions.TextColor
            };
        }

        private static Color CellColor(StiText cell)
            => cell.TextBrush is Stimulsoft.Base.Drawing.StiSolidBrush solid ? solid.Color : Color.Black;
    }
}
