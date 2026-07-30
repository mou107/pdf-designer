using System.Drawing;
using API.CORE.Schemas;
using API.CORE.Services.Report;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report.Components;

namespace API.Tests
{
    /// <summary>
    /// L'application d'un jeu de directives complet, partagée par le rendu et le pont du designer.
    /// </summary>
    public class DirectiveApplierShould
    {
        private static (RenderRequest Request, StiImage Logo) Fixture()
        {
            var request = new RenderRequest
            {
                Styles = new List<StyleDirective> { new StyleDirective { Name = "AltOddRow", BackColor = "#EAF6FA" } },
                ColorReplacements = new List<ColorReplacement> { new ColorReplacement { From = "#56AAC6", To = "#E67E22" } },
                Images = new List<ImageDirective> { new ImageDirective { Component = "Logo", WidthPx = 96 } },
                Watermark = new WatermarkDirective { Base64 = OnePixelPng },
                Bands = new List<BandDirective>
                {
                    new BandDirective
                    {
                        Name = "Lignes", HeaderBand = "EnteteLignes",
                        ColumnPrefixes = new ColumnPrefixes { Header = "HCol_", Data = "DCol_" },
                        TotalWidthCm = 19, FlexColumn = "designation", RowHeightCm = 0.5,
                        Columns = new List<ColumnDirective>
                        {
                            new ColumnDirective { Key = "designation", Visible = true },
                            new ColumnDirective { Key = "qte", Visible = false }
                        }
                    }
                }
            };

            return (request, new StiImage(new RectangleD(0, 0, 2, 2)) { Name = "Logo" });
        }

        /// <summary>Un PNG 1×1 valide — de quoi vérifier qu'une image est bien posée, ou bien ignorée.</summary>
        private const string OnePixelPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        [Fact]
        public void Appliquer_la_mise_en_page_sans_les_images_pour_le_designer()
        {
            // Ce que le designer affiche est ce qui sera figé dans le .mrt à l'enregistrement : une image
            // en base64 figée rendrait le modèle insensible à un changement de logo.
            var report = ReportBuilder.WithTable(("designation", 10), ("qte", 2));
            var (request, logo) = Fixture();
            report.Pages[0].Components.Add(logo);

            DirectiveApplier.Apply(report, request, includeImages: false);

            // La mise en page, elle, est bien appliquée.
            Assert.False(ReportBuilder.Find(report, "HCol_qte")!.Enabled);
            Assert.Equal(19, ReportBuilder.Find(report, "HCol_designation")!.Width);

            // Les images ne le sont pas.
            Assert.Null(logo.Image);
            Assert.Equal(2, logo.Width);
            Assert.Null(report.Pages[0].Watermark.Image);
        }

        [Fact]
        public void Appliquer_les_images_pour_le_rendu()
        {
            var report = ReportBuilder.WithTable(("designation", 10), ("qte", 2));
            var (request, logo) = Fixture();
            request.Images[0].Base64 = OnePixelPng;
            report.Pages[0].Components.Add(logo);

            DirectiveApplier.Apply(report, request, includeImages: true);

            Assert.NotNull(logo.Image);
            Assert.Equal(2.54, logo.Width, 2);
            Assert.NotNull(report.Pages[0].Watermark.Image);
        }

        [Fact]
        public void Recolorer_dans_les_deux_modes()
        {
            // La couleur société doit se voir dans le designer comme au rendu : c'est le premier écart
            // que l'utilisateur remarque.
            var report = ReportBuilder.WithTable(("designation", 10));
            var text = ReportBuilder.FindText(report, "DCol_designation");
            text.Brush = new StiSolidBrush(Color.FromArgb(0x56, 0xAA, 0xC6));

            DirectiveApplier.Apply(report, Fixture().Request, includeImages: false);

            Assert.Equal(Color.FromArgb(0xE6, 0x7E, 0x22), ((StiSolidBrush)text.Brush).Color);
        }
    }
}
