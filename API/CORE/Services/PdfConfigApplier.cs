using System.Drawing;
using System.Text.Json;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;
using Stimulsoft.Report.Components.ShapeTypes;

namespace API.CORE.Services
{
    /// <summary>
    /// Applique la config simple de la societe (couleurs) aux modeles seed au rendu :
    /// recolore les composants a la couleur d'accent seed vers la couleur principale de la societe.
    /// La config vient de la base Axiobat (ConfigurationPDF/{docType}) via ConfigJson du template.
    /// </summary>
    public static class PdfConfigApplier
    {
        // Couleurs d'accent des modeles seed (voir SeedTemplateFactory).
        private static readonly Color SeedAccent = Color.FromArgb(0x56, 0xAA, 0xC6);
        private static readonly Color SeedAccentLight = Color.FromArgb(0x9E, 0xD4, 0xE2); // zones claires (lignes alternees, boites)
        // Couleur par defaut du package (defaultConfig cote UI) : traitee comme « non parametree ».
        private const string DefaultUiColor = "#47a2c1";

        /// <param name="fallbackHex">Couleur societe (BDD) utilisee si la config n'a pas de couleur explicite.</param>
        public static void Apply(StiReport report, string? configJson, string? fallbackHex = null)
        {
            Color? main = null;
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(configJson);
                    if (doc.RootElement.TryGetProperty("colors", out var colors))
                    {
                        var raw = colors.TryGetProperty("main", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                        // Le bleu par defaut du package = « non choisi » -> on laisse le fallback societe primer.
                        if (!string.IsNullOrWhiteSpace(raw) && !string.Equals(raw, DefaultUiColor, StringComparison.OrdinalIgnoreCase))
                            main = ParseHex(raw);
                    }
                }
                catch { /* ignore, on tente le fallback */ }
            }

            main ??= ParseHex(fallbackHex);
            if (main == null) return;

            var light = Lighten(main.Value, 0.86); // version claire (lignes alternees, boites)
            foreach (StiPage page in report.Pages)
                RecolorComponents(page.Components, main.Value, light);
        }

        private static void RecolorComponents(StiComponentsCollection components, Color main, Color light)
        {
            foreach (StiComponent component in components)
            {
                switch (component)
                {
                    case StiText text:
                        if (text.Brush is StiSolidBrush b) { if (IsAccent(b.Color)) text.Brush = new StiSolidBrush(main); else if (IsAccentLight(b.Color)) text.Brush = new StiSolidBrush(light); }
                        if (text.TextBrush is StiSolidBrush tb && IsAccent(tb.Color)) text.TextBrush = new StiSolidBrush(main);
                        if (text.Border != null) { if (IsAccent(text.Border.Color)) text.Border.Color = main; else if (IsAccentLight(text.Border.Color)) text.Border.Color = light; }
                        break;
                    case StiShape shape:
                        if (shape.Brush is StiSolidBrush sb) { if (IsAccent(sb.Color)) shape.Brush = new StiSolidBrush(main); else if (IsAccentLight(sb.Color)) shape.Brush = new StiSolidBrush(light); }
                        if (IsAccent(shape.BorderColor)) shape.BorderColor = main;
                        break;
                }

                // Composants imbriques : les bandes (StiContainer) contiennent les textes/formes.
                if (component is StiContainer container && container.Components != null && container.Components.Count > 0)
                    RecolorComponents(container.Components, main, light);
            }
        }

        private static bool IsAccent(Color c) => Near(c, SeedAccent);
        private static bool IsAccentLight(Color c) => Near(c, SeedAccentLight);

        private static bool Near(Color c, Color r)
            => Math.Abs(c.R - r.R) <= 6 && Math.Abs(c.G - r.G) <= 6 && Math.Abs(c.B - r.B) <= 6;

        private static Color Lighten(Color c, double factor)
            => Color.FromArgb(
                (int)(c.R + (255 - c.R) * factor),
                (int)(c.G + (255 - c.G) * factor),
                (int)(c.B + (255 - c.B) * factor));

        private static Color? ParseHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return null;
            try
            {
                return Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { return null; }
        }
    }
}
