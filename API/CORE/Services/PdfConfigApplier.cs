using System.Drawing;
using System.Text.Json;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;
using Stimulsoft.Report.Components.ShapeTypes;
using Stimulsoft.Report.Styles;

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
            Color? main = null, alt1 = null, alt2 = null;
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(configJson);
                    if (doc.RootElement.TryGetProperty("colors", out var colors))
                    {
                        var raw = GetColorString(colors, "main");
                        // Le bleu par defaut du package = « non choisi » -> on laisse le fallback societe primer.
                        if (!string.IsNullOrWhiteSpace(raw) && !string.Equals(raw, DefaultUiColor, StringComparison.OrdinalIgnoreCase))
                            main = ParseHex(raw);
                        // Couleurs des lignes alternees (« Ligne alternee 1/2 ») choisies dans l'editeur.
                        alt1 = ParseHex(GetColorString(colors, "alt1"));
                        alt2 = ParseHex(GetColorString(colors, "alt2"));
                    }
                }
                catch { /* ignore, on tente le fallback */ }
            }

            main ??= ParseHex(fallbackHex);
            if (main == null && alt1 == null && alt2 == null) return;

            if (main != null)
            {
                var light = Lighten(main.Value, 0.86); // version claire (lignes alternees par defaut, boites)
                foreach (StiPage page in report.Pages)
                    RecolorComponents(page.Components, main.Value, light);
            }

            // Fond des lignes alternees :
            //  - si l'utilisateur a PERSONNALISE au moins une couleur alternee -> on respecte EXACTEMENT
            //    ses deux choix (impaire = alt1, paire = alt2) ;
            //  - sinon (les deux au defaut quasi-blanc) -> trame visible derivee de la principale
            //    (impaire teintee, paire blanche), pour ne pas afficher un tableau tout blanc.
            bool alt1Unset = alt1 == null || IsNearWhite(alt1.Value);
            bool alt2Unset = alt2 == null || IsNearWhite(alt2.Value);
            Color? oddRow;
            Color evenRow;
            if (alt1Unset && alt2Unset)
            {
                oddRow = main != null ? Lighten(main.Value, 0.68) : alt1;
                evenRow = Color.White;
            }
            else
            {
                oddRow = alt1 ?? (main != null ? (Color?)Lighten(main.Value, 0.68) : null);
                evenRow = alt2 ?? Color.White;
            }

            if (oddRow != null)
            {
                // L'alternance passe par OddStyle + EvenStyle du DataBand (fond de bande, plein largeur).
                //  - Les StiCondition ne posent qu'un BackColor, IGNORE au rendu Stimulsoft.
                //  - Le composant RowBg (opaque) recouvrait le fond de bande -> on le rend TRANSPARENT
                //    pour que la couleur alternee de la bande apparaisse sur toute la ligne.
                const string oddStyleName = "AltOddRow", evenStyleName = "AltEvenRow";
                SetBrushStyle(report, oddStyleName, oddRow.Value);
                SetBrushStyle(report, evenStyleName, evenRow);

                foreach (StiPage page in report.Pages)
                {
                    ApplyAltRowColors(page.Components, oddStyleName, evenStyleName);
                    // Prolonge l'alternance sur les 2 premieres lignes du bloc totaux (Total HT / Total TVA).
                    ColorTotalsRows(page.Components, oddRow.Value, evenRow);
                }
            }
        }

        /// <summary>Cree (ou met a jour) un style nomme portant un fond de la couleur donnee.</summary>
        private static void SetBrushStyle(StiReport report, string name, Color color)
        {
            var style = report.Styles[name] as StiStyle;
            if (style == null)
            {
                style = new StiStyle { Name = name, AllowUseBrush = true };
                report.Styles.Add(style);
            }
            style.AllowUseBrush = true;
            style.Brush = new StiSolidBrush(color);
        }

        /// <summary>
        /// Colore les 2 premieres lignes du bloc totaux : « Total HT » (impaire = <paramref name="htColor"/>)
        /// et « Total TVA » (paire = <paramref name="tvaColor"/>). Les lignes sont reperees par leur valeur
        /// liee ({totaux.totalHT} / {totaux.totalTva}) dans la bande « Totaux… » ; on colore label + valeur
        /// (memes hauteurs, cote droit) sans toucher au bloc de gauche (conditions de reglement).
        /// </summary>
        private static void ColorTotalsRows(StiComponentsCollection components, Color htColor, Color tvaColor)
        {
            foreach (StiComponent component in components)
            {
                if (component is StiContainer band && band.Name != null
                    && band.Name.StartsWith("Totaux", StringComparison.OrdinalIgnoreCase))
                {
                    double? htTop = null, tvaTop = null;
                    foreach (StiComponent c in band.Components)
                    {
                        if (c is StiText t)
                        {
                            var s = t.Text?.ToString() ?? string.Empty;
                            if (s.Contains("totaux.totalHT")) htTop = c.Top;
                            else if (s.Contains("totaux.totalTva")) tvaTop = c.Top;
                        }
                    }
                    foreach (StiComponent c in band.Components)
                    {
                        if (c is not StiText t || c.Left < 10) continue; // cote droit uniquement (totaux)
                        if (htTop.HasValue && System.Math.Abs(c.Top - htTop.Value) < 0.05)
                            t.Brush = new StiSolidBrush(htColor);
                        else if (tvaTop.HasValue && System.Math.Abs(c.Top - tvaTop.Value) < 0.05)
                            t.Brush = new StiSolidBrush(tvaColor);
                    }
                }
                else if (component is StiContainer nested && nested.Components != null && nested.Components.Count > 0)
                {
                    ColorTotalsRows(nested.Components, htColor, tvaColor);
                }
            }
        }

        private static string? GetColorString(System.Text.Json.JsonElement colors, string prop)
            => colors.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>
        /// Lignes alternees : fond des lignes impaires = <paramref name="oddRow"/> (composant « RowBg »),
        /// lignes paires colorees via le <c>EvenStyle</c> de la bande de donnees « Lignes ».
        /// </summary>
        private static void ApplyAltRowColors(StiComponentsCollection components, string oddStyleName, string evenStyleName)
        {
            foreach (StiComponent component in components)
            {
                // RowBg opaque recouvrait le fond de bande -> transparent pour laisser l'alternance passer.
                if (string.Equals(component.Name, "RowBg", StringComparison.Ordinal) && component is StiText text)
                    text.Brush = new StiSolidBrush(Color.Transparent);

                if (component is StiDataBand band && string.Equals(band.Name, "Lignes", StringComparison.Ordinal))
                {
                    band.OddStyle = oddStyleName;
                    band.EvenStyle = evenStyleName;
                }

                if (component is StiContainer container && container.Components != null && container.Components.Count > 0)
                    ApplyAltRowColors(container.Components, oddStyleName, evenStyleName);
            }
        }

        private static bool IsNearWhite(Color c) => c.R >= 235 && c.G >= 235 && c.B >= 235;

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
