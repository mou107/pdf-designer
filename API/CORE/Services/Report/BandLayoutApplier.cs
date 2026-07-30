using API.CORE.Schemas;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Met en page les bandes de donnees : colonnes visibles et leurs largeurs, hauteur de ligne,
    /// alternance des lignes, bordures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Une colonne est un couple de composants portant la MEME cle derriere deux prefixes, l'un dans la
    /// bande d'entete, l'autre dans la bande de donnees (par exemple <c>HCol_prix</c> / <c>DCol_prix</c>).
    /// Les prefixes et les cles sont fournis par l'appelant : le moteur n'en connait aucun.
    /// </para>
    /// <para>
    /// Masquer une colonne laisserait un trou : les colonnes restantes sont donc recompactees vers la
    /// gauche, et celle qui est declaree flexible absorbe la largeur liberee.
    /// </para>
    /// </remarks>
    internal static class BandLayoutApplier
    {
        public static void Apply(StiReport report, IEnumerable<BandDirective>? directives)
        {
            foreach (var directive in directives ?? Enumerable.Empty<BandDirective>())
            {
                foreach (var band in ReportPrimitives.BandsNamed(report, directive.Name).ToList())
                {
                    var header = ReportPrimitives.BandsNamed(report, directive.HeaderBand).FirstOrDefault();

                    ApplyRowHeight(band, directive);
                    ApplyColumns(band, header, directive);
                    ApplyAlternation(band, directive);
                    ApplyBorders(band, header, directive);
                }
            }
        }

        /// <summary>Hauteur des lignes de la bande. Les cellules suivent lors du reflow des colonnes.</summary>
        private static void ApplyRowHeight(StiBand band, BandDirective directive)
        {
            if (directive.RowHeightCm is > 0)
                band.Height = directive.RowHeightCm.Value;
        }

        private static void ApplyColumns(StiBand band, StiBand? header, BandDirective directive)
        {
            var columns = directive.Columns;
            if (columns == null || columns.Count == 0) return;
            if (header == null || directive.ColumnPrefixes == null) return;

            var heads = IndexByKey(header, directive.ColumnPrefixes.Header);
            var cells = IndexByKey(band, directive.ColumnPrefixes.Data);
            if (heads.Count == 0) return; // modele sans colonnes nommees : sa mise en page est conservee

            // Largeur naturelle (celle du modele) des colonnes fixes visibles, pour deduire ce qui reste
            // a la colonne flexible.
            var fixedWidth = 0.0;
            foreach (var column in columns)
            {
                if (column.Key == null || !column.Visible) continue;
                if (string.Equals(column.Key, directive.FlexColumn, StringComparison.Ordinal)) continue;
                if (!heads.TryGetValue(column.Key, out var head)) continue;
                fixedWidth += column.WidthCm ?? head.Width;
            }

            var flexWidth = directive.TotalWidthCm is > 0
                ? Math.Max(directive.FlexMinWidthCm, directive.TotalWidthCm.Value - fixedWidth)
                : 0;

            var rowHeight = directive.RowHeightCm ?? band.Height;
            var left = 0.0;

            foreach (var column in columns)
            {
                if (column.Key == null) continue;
                if (!heads.TryGetValue(column.Key, out var head) || !cells.TryGetValue(column.Key, out var cell))
                    continue;

                if (!column.Visible)
                {
                    head.Enabled = false;
                    cell.Enabled = false;
                    continue;
                }

                head.Enabled = true;
                cell.Enabled = true;

                var width = string.Equals(column.Key, directive.FlexColumn, StringComparison.Ordinal) && flexWidth > 0
                    ? flexWidth
                    : column.WidthCm ?? head.Width;

                head.Left = left;
                head.Width = width;
                cell.Left = left;
                cell.Width = width;

                if (cell is StiImage)
                {
                    // Une image occupe la ligne moins une marge, pour rester lisible et centree.
                    cell.Top = 0.1;
                    cell.Height = Math.Max(0.3, rowHeight - 0.2);
                }
                else
                {
                    // Cellules texte : meme hauteur que la ligne, pour que les bordures s'alignent.
                    cell.Height = rowHeight;
                }

                left += width;
            }
        }

        /// <summary>
        /// Alternance des lignes via <c>OddStyle</c> / <c>EvenStyle</c> de la bande : c'est le seul
        /// mecanisme dont le fond couvre reellement toute la largeur de la ligne au rendu.
        /// </summary>
        private static void ApplyAlternation(StiBand band, BandDirective directive)
        {
            if (band is not StiDataBand dataBand) return;

            if (directive.OddStyle != null) dataBand.OddStyle = directive.OddStyle;
            if (directive.EvenStyle != null) dataBand.EvenStyle = directive.EvenStyle;
        }

        private static void ApplyBorders(StiBand band, StiBand? header, BandDirective directive)
        {
            var exclude = new HashSet<string>(directive.BorderExclude ?? new List<string>(), StringComparer.Ordinal);

            // Une bordure Stimulsoft est un objet mutable : chaque cellule recoit la sienne, sinon une
            // retouche ulterieure sur une cellule les repeindrait toutes.
            if (directive.Border != null)
                foreach (StiComponent component in band.Components)
                    if (component is StiText text && !exclude.Contains(text.Name ?? string.Empty))
                        text.Border = ReportPrimitives.ToBorder(directive.Border)!;

            if (directive.HeaderBorder != null && header != null)
                foreach (StiComponent component in header.Components)
                    if (component is StiText text)
                        text.Border = ReportPrimitives.ToBorder(directive.HeaderBorder)!;
        }

        /// <summary>Composants d'une bande indexes par la cle qui suit le prefixe dans leur nom.</summary>
        private static Dictionary<string, StiComponent> IndexByKey(StiBand band, string? prefix)
        {
            var map = new Dictionary<string, StiComponent>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(prefix)) return map;

            foreach (StiComponent component in band.Components)
                if (component.Name != null && component.Name.StartsWith(prefix, StringComparison.Ordinal))
                    map[component.Name.Substring(prefix.Length)] = component;

            return map;
        }
    }
}
