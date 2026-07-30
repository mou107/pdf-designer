using System.Drawing;
using API.CORE.Schemas;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Conversions et parcours partages par les appliers. Rien de metier : uniquement de quoi lire une
    /// couleur, parcourir les composants d'un rapport et retrouver une bande par son nom.
    /// </summary>
    internal static class ReportPrimitives
    {
        /// <summary>1 px CSS (96 dpi) en centimetres — les .mrt sont en centimetres.</summary>
        public const double PxToCm = 2.54 / 96.0;

        /// <summary>
        /// Couleur hexadecimale (#RRGGBB, avec ou sans diese). <c>transparent</c> est reconnu.
        /// </summary>
        /// <remarks>
        /// <c>ColorTranslator</c> n'est pas portable hors Windows : on convertit a la main.
        /// </remarks>
        public static Color? ParseColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var text = value.Trim();
            if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
                return Color.Transparent;

            var hex = text.TrimStart('#');
            if (hex.Length != 6) return null;
            try
            {
                return Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Deux couleurs se ressemblent-elles a <paramref name="tolerance"/> pres, canal par canal ?</summary>
        public static bool ColorsMatch(Color left, Color right, int tolerance)
            => Math.Abs(left.R - right.R) <= tolerance
            && Math.Abs(left.G - right.G) <= tolerance
            && Math.Abs(left.B - right.B) <= tolerance;

        /// <summary>Image d'un base64, prefixe <c>data:</c> accepte. null si la valeur est vide ou illisible.</summary>
        public static Stimulsoft.Drawing.Image? LoadImage(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;

            var data = base64;
            if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = data.IndexOf(',');
                if (comma >= 0) data = data[(comma + 1)..];
            }

            try
            {
                return Stimulsoft.Drawing.Image.FromStream(new MemoryStream(Convert.FromBase64String(data)));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Tous les composants du rapport, conteneurs compris, en profondeur.</summary>
        public static IEnumerable<StiComponent> AllComponents(StiReport report)
        {
            foreach (StiPage page in report.Pages)
                foreach (var component in Descend(page.Components))
                    yield return component;
        }

        /// <summary>Les composants d'une collection, conteneurs compris, en profondeur.</summary>
        public static IEnumerable<StiComponent> Descend(StiComponentsCollection? components)
        {
            if (components == null) yield break;

            foreach (StiComponent component in components)
            {
                yield return component;

                if (component is StiContainer container)
                    foreach (var child in Descend(container.Components))
                        yield return child;
            }
        }

        /// <summary>Bandes portant exactement ce nom, sur toutes les pages.</summary>
        public static IEnumerable<StiBand> BandsNamed(StiReport report, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) yield break;

            foreach (var component in AllComponents(report))
                if (component is StiBand band && string.Equals(band.Name, name, StringComparison.Ordinal))
                    yield return band;
        }

        /// <summary>Conteneurs dont le nom commence par ce prefixe — une bande peut etre suffixee (Totaux1, Totaux2…).</summary>
        public static IEnumerable<StiContainer> ContainersStartingWith(StiReport report, string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) yield break;

            foreach (var component in AllComponents(report))
                if (component is StiContainer container && container.Name != null
                    && container.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    yield return container;
        }

        /// <summary>Texte affiche par un composant (expression du modele), ou une chaine vide.</summary>
        public static string TextValue(StiComponent component)
            => component is StiText text ? text.Text?.Value ?? string.Empty : string.Empty;

        /// <summary>
        /// Cotes de bordure decrits par une liste separee par des virgules :
        /// <c>none</c>, <c>all</c>, <c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>.
        /// </summary>
        public static StiBorderSides ParseSides(string? sides)
        {
            if (string.IsNullOrWhiteSpace(sides)) return StiBorderSides.None;

            var result = StiBorderSides.None;
            foreach (var part in sides.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                result |= part.ToLowerInvariant() switch
                {
                    "all" => StiBorderSides.All,
                    "top" => StiBorderSides.Top,
                    "bottom" => StiBorderSides.Bottom,
                    "left" => StiBorderSides.Left,
                    "right" => StiBorderSides.Right,
                    _ => StiBorderSides.None
                };
            }
            return result;
        }

        /// <summary>Bordure Stimulsoft decrite par une directive. null si la directive est absente.</summary>
        public static StiBorder? ToBorder(BorderDirective? directive)
        {
            if (directive == null) return null;

            var sides = ParseSides(directive.Sides);
            var color = ParseColor(directive.Color) ?? Color.Silver;
            return sides == StiBorderSides.None
                ? new StiBorder(StiBorderSides.None, Color.Transparent, 0, StiPenStyle.Solid)
                : new StiBorder(sides, color, directive.Width, StiPenStyle.Solid);
        }
    }
}
