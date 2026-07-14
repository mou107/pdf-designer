using System.Collections.Concurrent;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services
{
    public class SocieteAssets
    {
        /// <summary>Logo (base64, avec ou sans prefixe data:image/...).</summary>
        public string? Logo { get; set; }
        /// <summary>Image de fond / papier entete (base64).</summary>
        public string? Background { get; set; }
        /// <summary>Cachet / label (base64).</summary>
        public string? Cachet { get; set; }
        /// <summary>Couleur principale de la societe (hex, ex #E67E22) — issue de ConfigurationPDF.colors.header.</summary>
        public string? MainColor { get; set; }
    }

    /// <summary>
    /// Assets editique par societe (logo, papier entete, cachet) — pousses par le webadmin depuis
    /// la vraie config Axiobat. Injectes dans les modeles au rendu ET a l'ouverture du designer,
    /// pour que l'apercu porte le logo et le fond comme l'ecran Configuration PDF actuel.
    /// Stockage memoire (suffit au POC ; a persister en base pour la prod).
    /// </summary>
    public class SocieteAssetsService
    {
        private readonly ConcurrentDictionary<string, SocieteAssets> _assets = new();

        public const string LogoComponentName = "SocieteLogo";
        public const string CachetComponentName = "SocieteCachet";

        public void Set(string societeId, SocieteAssets assets)
        {
            if (string.IsNullOrWhiteSpace(societeId)) return;
            _assets[societeId] = assets;
        }

        public SocieteAssets? Get(string? societeId)
            => societeId != null && _assets.TryGetValue(societeId, out var a) ? a : null;

        /// <summary>Applique logo + fond de la societe au rapport charge (avant render / avant designer).</summary>
        public void Apply(StiReport report, string? societeId)
        {
            var assets = Get(societeId);
            if (assets == null) return;

            var logo = LoadImage(assets.Logo);
            if (logo != null && report.GetComponentByName(LogoComponentName) is StiImage logoImage)
            {
                logoImage.Image = logo;
                logoImage.Stretch = true;
                logoImage.AspectRatio = true;
            }

            var cachet = LoadImage(assets.Cachet);
            if (cachet != null && report.GetComponentByName(CachetComponentName) is StiImage cachetImage)
            {
                cachetImage.Image = cachet;
                cachetImage.Stretch = true;
                cachetImage.AspectRatio = true;
            }

            var background = LoadImage(assets.Background);
            if (background != null)
            {
                foreach (StiPage page in report.Pages)
                {
                    page.Watermark.Image = background;
                    page.Watermark.ImageStretch = true;
                    page.Watermark.ImageTransparency = 0;
                }
            }
        }

        // 1 px CSS (96 dpi) = 2.54/96 cm ; les .mrt sont en centimetres.
        private const double PxToCm = 2.54 / 96.0;

        /// <summary>
        /// Redimensionne la boite d'un composant image (logo/cachet) selon des dimensions en pixels (axes > 0
        /// seulement). Appele au rendu APRES <see cref="Apply"/> : la taille vient de la config du template
        /// (requete de rendu), pas de l'asset stocke.
        /// </summary>
        public static void ApplyImageDimensions(StiReport report, string componentName, double? widthPx, double? heightPx)
        {
            if (widthPx is not > 0 && heightPx is not > 0) return;
            if (report.GetComponentByName(componentName) is not StiComponent comp) return;
            if (widthPx is > 0) comp.Width = widthPx.Value * PxToCm;
            if (heightPx is > 0) comp.Height = heightPx.Value * PxToCm;
        }

        private static Stimulsoft.Drawing.Image? LoadImage(string? base64)
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
    }
}
