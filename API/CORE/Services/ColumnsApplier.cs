using System.Text.Json;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services
{
    /// <summary>
    /// Applique la visibilite des colonnes du tableau des lignes (config `cols` du template) au rendu :
    /// masque les colonnes desactivees (Enabled=false) et recompacte les positions X / largeurs des
    /// colonnes restantes pour remplir la ligne. La colonne "designation" est flexible (absorbe le reste).
    /// S'appuie sur les Name poses par SeedTemplateFactory (HCol_{cle} / DCol_{cle}). Sans config `cols`
    /// ou sur un .mrt sans colonnes nommees (ancien seed), l'application est sans effet (layout du seed conserve).
    /// </summary>
    public static class ColumnsApplier
    {
        // Ordre canonique gauche->droite ; doit correspondre aux cles posees dans SeedTemplateFactory.QuoteColumns.
        private static readonly string[] Order = { "num", "vignette", "designation", "qte", "unite", "prixU", "tva", "prixHT", "ttc" };
        private const string FlexKey = "designation";
        private const string ImageKey = "vignette";
        private const double PageWidth = 19.0; // = SeedTemplateFactory.W (largeur utile A4 en cm)
        private const double MinFlexWidth = 3.0;
        private const double DefaultRowHeight = 0.5;
        private const double VignetteRowHeight = 1.6; // hauteur de ligne quand la vignette (image) est affichee

        public static void Apply(StiReport report, string? configJson)
        {
            var cols = ParseCols(configJson);
            if (cols == null) return; // pas de config `cols` -> on laisse le layout du seed

            foreach (StiPage page in report.Pages)
            {
                StiBand? header = null, data = null;
                foreach (StiComponent c in page.Components)
                {
                    if (c is not StiBand b) continue;
                    if (b.Name == "EnteteLignes") header = b;
                    else if (b.Name == "Lignes") data = b;
                }
                if (header != null && data != null) Reflow(header, data, cols);
            }
        }

        private static void Reflow(StiBand header, StiBand data, Dictionary<string, bool> cols)
        {
            var heads = Index(header, "HCol_");
            var cells = Index(data, "DCol_");
            if (heads.Count == 0) return; // .mrt sans colonnes nommees -> rien a faire

            bool Visible(string key) => key == FlexKey || (cols.TryGetValue(key, out var v) && v);

            // Colonne vignette (image) affichee -> lignes plus hautes pour que la photo soit visible.
            bool vignetteVisible = cells.ContainsKey(ImageKey) && Visible(ImageKey);
            double rowH = vignetteVisible ? VignetteRowHeight : DefaultRowHeight;
            data.Height = rowH;
            foreach (StiComponent comp in data.Components)
                if (comp.Name == "RowBg") { comp.Height = rowH; break; }

            // Largeur naturelle (issue du seed) des colonnes fixes visibles.
            double fixedSum = 0;
            foreach (var key in Order)
            {
                if (key == FlexKey || !heads.TryGetValue(key, out var h)) continue;
                if (Visible(key)) fixedSum += h.Width;
            }
            double flexW = Math.Max(MinFlexWidth, PageWidth - fixedSum);

            double x = 0;
            foreach (var key in Order)
            {
                if (!heads.TryGetValue(key, out var h) || !cells.TryGetValue(key, out var d)) continue;

                if (!Visible(key))
                {
                    h.Enabled = false;
                    d.Enabled = false;
                    continue;
                }

                h.Enabled = true;
                d.Enabled = true;
                double w = key == FlexKey ? flexW : h.Width;
                h.Left = x; h.Width = w;
                d.Left = x; d.Width = w;

                if (d is StiImage) { d.Top = 0.1; d.Height = Math.Max(0.3, rowH - 0.2); } // vignette centree dans la ligne
                else d.Height = rowH;                                                     // cellules texte : bordure basse alignee

                x += w;
            }
        }

        private static Dictionary<string, StiComponent> Index(StiBand band, string prefix)
        {
            var map = new Dictionary<string, StiComponent>();
            foreach (StiComponent c in band.Components)
                if (c.Name != null && c.Name.StartsWith(prefix))
                    map[c.Name.Substring(prefix.Length)] = c;
            return map;
        }

        private static Dictionary<string, bool>? ParseCols(string? configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (!doc.RootElement.TryGetProperty("cols", out var cols) || cols.ValueKind != JsonValueKind.Object)
                    return null;
                var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in cols.EnumerateObject())
                    if (p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        map[p.Name] = p.Value.GetBoolean();
                return map;
            }
            catch { return null; }
        }
    }
}
