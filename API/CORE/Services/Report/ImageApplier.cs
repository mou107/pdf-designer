using API.CORE.Schemas;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Pose les images de la requete : sur des composants existants (designes par leur nom), en filigrane
    /// de page, ou en rangees creees au rendu.
    /// </summary>
    /// <remarks>
    /// Le moteur ne connait aucun nom de composant : c'est l'appelant qui nomme ses emplacements dans son
    /// propre modele.
    /// </remarks>
    internal static class ImageApplier
    {
        /// <summary>
        /// Alimente les composants image nommes. Une image absente EFFACE celle du modele : sans cela,
        /// l'image d'illustration livree dans le .mrt apparaitrait sur les documents.
        /// </summary>
        public static void ApplyImages(StiReport report, IEnumerable<ImageDirective>? directives)
        {
            foreach (var directive in directives ?? Enumerable.Empty<ImageDirective>())
            {
                if (string.IsNullOrWhiteSpace(directive.Component)) continue;
                if (report.GetComponentByName(directive.Component) is not StiComponent component) continue;

                if (component is StiImage image)
                {
                    var loaded = ReportPrimitives.LoadImage(directive.Base64);
                    image.Image = loaded;
                    if (loaded != null)
                    {
                        image.Stretch = true;
                        image.AspectRatio = true;
                    }
                }

                // Dimensions en pixels CSS : appliquees APRES l'image, sinon Stimulsoft les recalcule.
                if (directive.WidthPx is > 0) component.Width = directive.WidthPx.Value * ReportPrimitives.PxToCm;
                if (directive.HeightPx is > 0) component.Height = directive.HeightPx.Value * ReportPrimitives.PxToCm;
            }
        }

        /// <summary>Filigrane pleine page sur toutes les pages. Directive absente ou vide = sans effet.</summary>
        public static void ApplyWatermark(StiReport report, WatermarkDirective? directive)
        {
            var image = ReportPrimitives.LoadImage(directive?.Base64);
            if (image == null) return;

            foreach (StiPage page in report.Pages)
            {
                page.Watermark.Image = image;
                page.Watermark.ImageStretch = directive!.Stretch;
                page.Watermark.ImageTransparency = directive.Transparency;
            }
        }

        /// <summary>
        /// Ajoute des rangees d'images que le modele ne prevoit pas. Les composants sont crees ici, donc
        /// la rangee fonctionne aussi sur un modele personnalise dans l'editeur avance.
        /// </summary>
        public static void ApplyImageRows(StiReport report, IEnumerable<ImageRowDirective>? directives)
        {
            foreach (var directive in directives ?? Enumerable.Empty<ImageRowDirective>())
            {
                var images = (directive.Images ?? new List<string>())
                    .Select(ReportPrimitives.LoadImage)
                    .Where(image => image != null)
                    .ToList();

                if (directive.MaxCount > 0) images = images.Take(directive.MaxCount).ToList();
                if (images.Count == 0) continue;

                var band = ResolveHostBand(report, directive.Band);
                if (band == null) continue;

                var top = band.Height + directive.MarginTopCm;
                var prefix = string.IsNullOrWhiteSpace(directive.NamePrefix) ? "RowImage" : directive.NamePrefix;

                for (var index = 0; index < images.Count; index++)
                {
                    var left = index * (directive.SizeCm + directive.GapCm);
                    band.Components.Add(new StiImage(new RectangleD(left, top, directive.SizeCm, directive.SizeCm))
                    {
                        Name = $"{prefix}{index + 1}",
                        Image = images[index],
                        Stretch = true,
                        AspectRatio = true
                    });
                }

                // La bande grandit pour accueillir la rangee : sans cela les images seraient rognees.
                band.Height = top + directive.SizeCm + directive.MarginTopCm;
            }
        }

        /// <summary>
        /// Bande d'accueil : celle qui est nommee, sinon le premier pied de page, sinon un pied cree pour
        /// l'occasion — tous les modeles n'en ont pas.
        /// </summary>
        private static StiBand? ResolveHostBand(StiReport report, string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                return ReportPrimitives.BandsNamed(report, name).FirstOrDefault();

            foreach (var component in ReportPrimitives.AllComponents(report))
                if (component is StiFooterBand footer) return footer;

            if (report.Pages.Count == 0 || report.Pages[0] is not StiPage page) return null;

            var band = new StiFooterBand { Name = "ImageRow", Height = 0 };
            page.Components.Add(band);
            return band;
        }
    }
}
