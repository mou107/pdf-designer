using System.Drawing;
using API.CORE.Schemas;
using API.CORE.Services.Report;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report.Components;

namespace API.Tests
{
    /// <summary>
    /// Les directives qui ne touchent pas a la mise en page : couleurs, images, reecritures, retouches,
    /// conditions.
    /// </summary>
    public class DirectiveAppliersShould
    {
        #region couleurs

        [Fact]
        public void Remplacer_une_couleur_partout_ou_elle_apparait()
        {
            var report = ReportBuilder.Empty();
            var page = report.Pages[0];
            var text = ReportBuilder.Text("Titre");
            text.Brush = new StiSolidBrush(Color.FromArgb(0x56, 0xAA, 0xC6));
            text.TextBrush = new StiSolidBrush(Color.FromArgb(0x56, 0xAA, 0xC6));
            page.Components.Add(text);

            ColorApplier.ApplyReplacements(report, new[]
            {
                new ColorReplacement { From = "#56AAC6", To = "#E67E22" }
            });

            Assert.Equal(Color.FromArgb(0xE6, 0x7E, 0x22), ((StiSolidBrush)text.Brush).Color);
            Assert.Equal(Color.FromArgb(0xE6, 0x7E, 0x22), ((StiSolidBrush)text.TextBrush).Color);
        }

        [Fact]
        public void Absorber_un_ecart_darrondi_dans_la_tolerance()
        {
            // L'editeur et le fichier ne stockent pas toujours la meme valeur au bit pres.
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("Titre");
            text.Brush = new StiSolidBrush(Color.FromArgb(0x59, 0xAD, 0xC9));
            report.Pages[0].Components.Add(text);

            ColorApplier.ApplyReplacements(report, new[]
            {
                new ColorReplacement { From = "#56AAC6", To = "#E67E22", Tolerance = 6 }
            });

            Assert.Equal(Color.FromArgb(0xE6, 0x7E, 0x22), ((StiSolidBrush)text.Brush).Color);
        }

        [Fact]
        public void Laisser_une_couleur_hors_tolerance_intacte()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("Titre");
            text.Brush = new StiSolidBrush(Color.FromArgb(0x00, 0x00, 0x00));
            report.Pages[0].Components.Add(text);

            ColorApplier.ApplyReplacements(report, new[]
            {
                new ColorReplacement { From = "#56AAC6", To = "#E67E22", Tolerance = 6 }
            });

            Assert.Equal(Color.FromArgb(0, 0, 0), ((StiSolidBrush)text.Brush).Color);
        }

        [Fact]
        public void Creer_les_styles_nommes_demandes()
        {
            var report = ReportBuilder.Empty();

            ColorApplier.ApplyStyles(report, new[]
            {
                new StyleDirective { Name = "AltOddRow", BackColor = "#EAF6FA" }
            });

            var style = report.Styles["AltOddRow"];
            Assert.NotNull(style);
        }

        #endregion

        #region images

        [Fact]
        public void Effacer_limage_du_modele_quand_aucune_nest_fournie()
        {
            // Sans cela, l'illustration livree dans le .mrt apparaitrait sur les documents reels.
            var report = ReportBuilder.Empty();
            var image = new StiImage(new RectangleD(0, 0, 2, 2)) { Name = "Logo" };
            report.Pages[0].Components.Add(image);

            ImageApplier.ApplyImages(report, new[] { new ImageDirective { Component = "Logo", Base64 = null } });

            Assert.Null(image.Image);
        }

        [Fact]
        public void Redimensionner_la_boite_dune_image_en_pixels()
        {
            var report = ReportBuilder.Empty();
            var image = new StiImage(new RectangleD(0, 0, 2, 2)) { Name = "Logo" };
            report.Pages[0].Components.Add(image);

            ImageApplier.ApplyImages(report, new[]
            {
                new ImageDirective { Component = "Logo", WidthPx = 96, HeightPx = 48 }
            });

            // 96 px CSS = 1 pouce = 2,54 cm.
            Assert.Equal(2.54, image.Width, 2);
            Assert.Equal(1.27, image.Height, 2);
        }

        [Fact]
        public void Ignorer_une_directive_visant_un_composant_absent()
        {
            var report = ReportBuilder.Empty();

            var exception = Record.Exception(() => ImageApplier.ApplyImages(report, new[]
            {
                new ImageDirective { Component = "Inexistant", Base64 = "AAAA" }
            }));

            Assert.Null(exception);
        }

        #endregion

        #region reecriture de texte

        [Fact]
        public void Reecrire_un_texte_portant_un_marqueur()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("Client", value: "{client.civilite} {client.nom}");
            report.Pages[0].Components.Add(text);

            TextRewriteApplier.Apply(report, new[]
            {
                new TextRewriteDirective
                {
                    WhenContainsAll = new List<string> { "{client.civilite}", "{client.nom}" },
                    SetTo = "{client.bloc}"
                }
            });

            Assert.Equal("{client.bloc}", text.Text.Value);
        }

        [Fact]
        public void Exiger_tous_les_marqueurs_de_WhenContainsAll()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("Client", value: "{client.nom}");
            report.Pages[0].Components.Add(text);

            TextRewriteApplier.Apply(report, new[]
            {
                new TextRewriteDirective
                {
                    WhenContainsAll = new List<string> { "{client.civilite}", "{client.nom}" },
                    SetTo = "{client.bloc}"
                }
            });

            Assert.Equal("{client.nom}", text.Text.Value);
        }

        [Fact]
        public void Rester_idempotente_grace_a_SkipIfContains()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("Client", value: "{client.bloc}");
            report.Pages[0].Components.Add(text);

            TextRewriteApplier.Apply(report, new[]
            {
                new TextRewriteDirective
                {
                    WhenContainsAny = new List<string> { "{client" },
                    SkipIfContains = new List<string> { "{client.bloc}" },
                    SetTo = "AUTRE"
                }
            });

            Assert.Equal("{client.bloc}", text.Text.Value);
        }

        [Fact]
        public void Appliquer_la_premiere_regle_qui_correspond()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("Client", value: "{client.siret}");
            report.Pages[0].Components.Add(text);

            TextRewriteApplier.Apply(report, new[]
            {
                new TextRewriteDirective { WhenContainsAny = new List<string> { "{client.siret}" }, SetTo = "PREMIER" },
                new TextRewriteDirective { WhenContainsAny = new List<string> { "{client" }, SetTo = "SECOND" }
            });

            Assert.Equal("PREMIER", text.Text.Value);
        }

        #endregion

        #region retouches et rangees

        [Fact]
        public void Rendre_un_composant_transparent()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("RowBg");
            text.Brush = new StiSolidBrush(Color.White);
            report.Pages[0].Components.Add(text);

            ComponentOverrideApplier.Apply(report, new[]
            {
                new ComponentOverride { Name = "RowBg", BackColor = "transparent" }
            });

            Assert.Equal(Color.Transparent, ((StiSolidBrush)text.Brush).Color);
        }

        [Fact]
        public void Effacer_les_conditions_posees_par_le_modele()
        {
            var report = ReportBuilder.Empty();
            var text = ReportBuilder.Text("RowBg");
            text.Conditions.Add(new StiCondition { Expression = "true" });
            report.Pages[0].Components.Add(text);

            ComponentOverrideApplier.Apply(report, new[]
            {
                new ComponentOverride { Name = "RowBg", ClearConditions = true }
            });

            Assert.Empty(text.Conditions);
        }

        [Fact]
        public void Colorer_la_rangee_ancree_sur_une_expression()
        {
            var report = ReportBuilder.Empty();
            var band = new StiFooterBand { Name = "Totaux1", Height = 3 };

            var libelle = ReportBuilder.Text("Libelle", left: 12, value: "Total HT");
            var montant = ReportBuilder.Text("Montant", left: 16, value: "{totaux.totalHT}");
            var gauche = ReportBuilder.Text("Conditions", left: 0, value: "Conditions de reglement");
            var autre = ReportBuilder.Text("Autre", left: 16, value: "{totaux.totalTva}");
            autre.Top = 1;

            band.Components.Add(libelle);
            band.Components.Add(montant);
            band.Components.Add(gauche);
            band.Components.Add(autre);
            report.Pages[0].Components.Add(band);

            ComponentOverrideApplier.ApplyRowFills(report, new[]
            {
                new RowFillDirective
                {
                    Band = "Totaux",
                    AnchorExpression = "totaux.totalHT",
                    BackColor = "#EAF6FA",
                    MinLeftCm = 10
                }
            });

            var expected = Color.FromArgb(0xEA, 0xF6, 0xFA);
            Assert.Equal(expected, ((StiSolidBrush)libelle.Brush).Color);
            Assert.Equal(expected, ((StiSolidBrush)montant.Brush).Color);
            // A gauche de minLeftCm : ce bloc porte autre chose, il ne doit pas etre colore.
            Assert.NotEqual(expected, ((StiSolidBrush)gauche.Brush).Color);
            // Sur une autre rangee.
            Assert.NotEqual(expected, ((StiSolidBrush)autre.Brush).Color);
        }

        #endregion

        #region conditions

        [Fact]
        public void Poser_une_condition_sur_les_cellules_dune_bande()
        {
            var report = ReportBuilder.WithTable(("num", 1), ("designation", 10));

            ConditionsApplier.Apply(report, new[]
            {
                new ConditionDirective
                {
                    Band = "Lignes",
                    Expression = "lignes.type == \"lot\"",
                    FontSize = 10,
                    Bold = true,
                    TextColor = "#FF0000"
                }
            });

            var cell = ReportBuilder.FindText(report, "DCol_num");
            var condition = Assert.Single(cell.Conditions.Cast<StiCondition>());
            Assert.Equal("lignes.type == \"lot\"", condition.Expression);
            // Comparaison sur la valeur : une couleur nommee et son equivalent ARGB ne sont pas egaux.
            Assert.Equal(Color.Red.ToArgb(), condition.TextColor.ToArgb());
            Assert.Equal(10, condition.Font.Size);
        }

        [Fact]
        public void Epargner_les_composants_exclus()
        {
            var report = ReportBuilder.WithTable(("designation", 10));

            ConditionsApplier.Apply(report, new[]
            {
                new ConditionDirective
                {
                    Band = "Lignes",
                    ExcludeComponents = new List<string> { "RowBg", "DCol_designation" },
                    Expression = "lignes.type == \"lot\"",
                    FontSize = 10
                }
            });

            Assert.Empty(ReportBuilder.FindText(report, "RowBg").Conditions);
            Assert.Empty(ReportBuilder.FindText(report, "DCol_designation").Conditions);
        }

        [Fact]
        public void Conserver_la_couleur_de_la_cellule_sans_couleur_demandee()
        {
            // StiCondition.TextColor vaut Red par defaut : sans cette reprise, la ligne virerait au rouge.
            var report = ReportBuilder.WithTable(("num", 1));
            var cell = ReportBuilder.FindText(report, "DCol_num");
            cell.TextBrush = new StiSolidBrush(Color.FromArgb(0x33, 0x33, 0x33));

            ConditionsApplier.Apply(report, new[]
            {
                new ConditionDirective { Band = "Lignes", Expression = "true", FontSize = 9 }
            });

            var condition = Assert.Single(cell.Conditions.Cast<StiCondition>());
            Assert.Equal(Color.FromArgb(0x33, 0x33, 0x33), condition.TextColor);
        }

        #endregion
    }
}
