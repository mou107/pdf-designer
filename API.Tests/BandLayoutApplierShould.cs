using API.CORE.Schemas;
using API.CORE.Services.Report;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report.Components;

namespace API.Tests
{
    /// <summary>
    /// La mise en page d'une bande : colonnes visibles, largeurs, hauteur de ligne, bordures.
    /// </summary>
    public class BandLayoutApplierShould
    {
        private static BandDirective Table(params ColumnDirective[] columns) => new BandDirective
        {
            Name = "Lignes",
            HeaderBand = "EnteteLignes",
            ColumnPrefixes = new ColumnPrefixes { Header = "HCol_", Data = "DCol_" },
            TotalWidthCm = 19,
            FlexColumn = "designation",
            FlexMinWidthCm = 3,
            RowHeightCm = 0.5,
            Columns = columns.ToList()
        };

        [Fact]
        public void Masquer_une_colonne_dans_les_deux_bandes()
        {
            var report = ReportBuilder.WithTable(("num", 1), ("designation", 10), ("qte", 2));

            BandLayoutApplier.Apply(report, new[]
            {
                Table(
                    new ColumnDirective { Key = "num", Visible = true },
                    new ColumnDirective { Key = "designation", Visible = true },
                    new ColumnDirective { Key = "qte", Visible = false })
            });

            Assert.False(ReportBuilder.Find(report, "HCol_qte")!.Enabled);
            Assert.False(ReportBuilder.Find(report, "DCol_qte")!.Enabled);
            Assert.True(ReportBuilder.Find(report, "HCol_num")!.Enabled);
        }

        [Fact]
        public void Recompacter_les_colonnes_restantes_vers_la_gauche()
        {
            // Masquer une colonne sans recompacter laisserait un trou au milieu du tableau.
            var report = ReportBuilder.WithTable(("num", 1), ("qte", 2), ("designation", 10), ("prixU", 3));

            BandLayoutApplier.Apply(report, new[]
            {
                Table(
                    new ColumnDirective { Key = "num", Visible = true },
                    new ColumnDirective { Key = "qte", Visible = false },
                    new ColumnDirective { Key = "designation", Visible = true },
                    new ColumnDirective { Key = "prixU", Visible = true })
            });

            var num = ReportBuilder.Find(report, "HCol_num")!;
            var designation = ReportBuilder.Find(report, "HCol_designation")!;
            var prix = ReportBuilder.Find(report, "HCol_prixU")!;

            Assert.Equal(0, num.Left);
            Assert.Equal(num.Width, designation.Left);
            Assert.Equal(designation.Left + designation.Width, prix.Left);
        }

        [Fact]
        public void Faire_absorber_la_largeur_liberee_par_la_colonne_flexible()
        {
            var report = ReportBuilder.WithTable(("num", 1), ("designation", 5), ("qte", 2));

            BandLayoutApplier.Apply(report, new[]
            {
                Table(
                    new ColumnDirective { Key = "num", Visible = true },
                    new ColumnDirective { Key = "designation", Visible = true },
                    new ColumnDirective { Key = "qte", Visible = false })
            });

            // 19 cm de large, seul « num » (1 cm) reste fixe : la designation prend les 18 restants.
            Assert.Equal(18, ReportBuilder.Find(report, "HCol_designation")!.Width);
            Assert.Equal(18, ReportBuilder.Find(report, "DCol_designation")!.Width);
        }

        [Fact]
        public void Respecter_la_largeur_plancher_de_la_colonne_flexible()
        {
            var report = ReportBuilder.WithTable(("designation", 5), ("qte", 20));

            BandLayoutApplier.Apply(report, new[]
            {
                Table(
                    new ColumnDirective { Key = "designation", Visible = true },
                    new ColumnDirective { Key = "qte", Visible = true })
            });

            // Les colonnes fixes debordent : la flexible ne doit pas devenir negative.
            Assert.Equal(3, ReportBuilder.Find(report, "HCol_designation")!.Width);
        }

        [Fact]
        public void Conserver_la_mise_en_page_du_modele_sans_colonnes_declarees()
        {
            var report = ReportBuilder.WithTable(("num", 1), ("designation", 10));

            BandLayoutApplier.Apply(report, new[] { new BandDirective { Name = "Lignes", RowHeightCm = 1.6 } });

            Assert.Equal(1, ReportBuilder.Find(report, "HCol_num")!.Width);
            Assert.Equal(10, ReportBuilder.Find(report, "HCol_designation")!.Width);
        }

        [Fact]
        public void Appliquer_la_hauteur_de_ligne_a_la_bande_et_a_ses_cellules()
        {
            var report = ReportBuilder.WithTable(("designation", 10));

            var directive = Table(new ColumnDirective { Key = "designation", Visible = true });
            directive.RowHeightCm = 1.6;
            BandLayoutApplier.Apply(report, new[] { directive });

            Assert.Equal(1.6, ReportBuilder.Find(report, "DCol_designation")!.Height);
        }

        [Fact]
        public void Poser_les_bordures_en_epargnant_les_composants_exclus()
        {
            // Le fond de ligne n'est pas une cellule : lui poser une bordure dessinerait un cadre parasite.
            var report = ReportBuilder.WithTable(("designation", 10));

            var directive = Table(new ColumnDirective { Key = "designation", Visible = true });
            directive.Border = new BorderDirective { Sides = "all", Color = "#C0C0C0", Width = 1 };
            directive.BorderExclude = new List<string> { "RowBg" };
            BandLayoutApplier.Apply(report, new[] { directive });

            Assert.Equal(StiBorderSides.All, ReportBuilder.FindText(report, "DCol_designation").Border.Side);
            Assert.NotEqual(StiBorderSides.All, ReportBuilder.FindText(report, "RowBg").Border.Side);
        }

        [Fact]
        public void Donner_a_chaque_cellule_sa_propre_bordure()
        {
            // Une bordure Stimulsoft est mutable : partagee, une retouche sur une cellule les repeindrait
            // toutes.
            var report = ReportBuilder.WithTable(("num", 1), ("designation", 10));

            var directive = Table(
                new ColumnDirective { Key = "num", Visible = true },
                new ColumnDirective { Key = "designation", Visible = true });
            directive.Border = new BorderDirective { Sides = "all", Color = "#C0C0C0" };
            BandLayoutApplier.Apply(report, new[] { directive });

            var first = ReportBuilder.FindText(report, "DCol_num").Border;
            var second = ReportBuilder.FindText(report, "DCol_designation").Border;
            Assert.NotSame(first, second);
        }

        [Fact]
        public void Nommer_les_styles_dalternance_sur_la_bande_de_donnees()
        {
            var report = ReportBuilder.WithTable(("designation", 10));

            var directive = Table(new ColumnDirective { Key = "designation", Visible = true });
            directive.OddStyle = "AltOddRow";
            directive.EvenStyle = "AltEvenRow";
            BandLayoutApplier.Apply(report, new[] { directive });

            var band = (StiDataBand)ReportBuilder.Find(report, "Lignes")!;
            Assert.Equal("AltOddRow", band.OddStyle);
            Assert.Equal("AltEvenRow", band.EvenStyle);
        }

        [Fact]
        public void Ignorer_une_bande_absente_du_modele()
        {
            var report = ReportBuilder.WithTable(("designation", 10));

            var directive = Table(new ColumnDirective { Key = "designation", Visible = true });
            directive.Name = "Inconnue";

            // Un modele personnalise peut avoir renomme ou supprime une bande : le rendu continue.
            BandLayoutApplier.Apply(report, new[] { directive });

            Assert.Equal(10, ReportBuilder.Find(report, "HCol_designation")!.Width);
        }
    }
}
