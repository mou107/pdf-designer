using System.Drawing;
using API.CORE.Schemas;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;
using Stimulsoft.Report.Components.ShapeTypes;
using Stimulsoft.Report.Components.TextFormats;

namespace API.CORE.Services
{
    /// <summary>
    /// Genere les 7 modeles de depart (.mrt) par type de document — repliques des 7 modeles
    /// pdfmake historiques d'Axiobat (ecran Visualisation du parametrage PDF) :
    ///   1. Bandeau bleu pleine largeur en tete (courbe) + pied de page pilule arrondie
    ///   2. Filet colore en haut, logo encadre a gauche, societe a droite + pied colore
    ///   3. Bloc societe en couleur a droite, logo a gauche, client encadre dessous
    ///   4. Bloc societe arrondi en couleur en haut a droite
    ///   5. Sobre sans couleur d'entete, infos a gauche / client a droite
    ///   6. Sobre centre (reference centree)
    ///   7. Ligne de champs encadres (n0/dates), titre centre, client + adresse encadres
    /// Corps commun : tableau des lignes (entete colore), conditions de reglement a gauche,
    /// Montant HT / TVA / bande Total TTC a droite, note, bloc signature "Mon Entreprise".
    /// </summary>
    public static class SeedTemplateFactory
    {
        private const double W = 19.0; // largeur utile A4 (cm), marges 1 cm

        // Bleu Axiobat des modeles historiques
        private static readonly Color Accent = Color.FromArgb(0x56, 0xAA, 0xC6);
        private static readonly Color AccentLight = Color.FromArgb(0x9E, 0xD4, 0xE2);
        private static readonly Color Dark = Color.FromArgb(0x37, 0x47, 0x4F);

        public static StiReport Build(int model, string docType, string label, string sampleJson)
        {
            var report = StiReport.CreateNewReport();
            report.ReportName = $"{label} — Modele {model}";

            var page = report.Pages[0];
            page.Components.Clear();

            // Devis : entete distincte selon le modele (1-7) + corps fidele (tableau, totaux, pied legal).
            if (docType == DocTypes.Quote)
            {
                page.Components.Add(model switch
                {
                    7 => BuildQuoteHeaderModel7(),
                    6 => BuildQuoteHeaderModel6(),
                    5 => BuildQuoteHeaderModel5(),
                    4 => BuildQuoteHeaderModel4(),
                    3 => BuildQuoteHeaderModel3(),
                    2 => BuildQuoteHeaderModel2(),
                    _ => BuildTitleBand(model, docType)
                });
                AddQuoteTable(page);
                AddQuoteTotals(page);
                // Modeles 5/6 sobres (papier entête) : pied de page discret ; sinon bande couleur.
                page.Components.Add(model == 5 || model == 6 ? BuildQuotePlainFooter() : BuildQuotePageFooter());
                RenderService.RegisterData(report, sampleJson);
                return report;
            }

            page.Components.Add(BuildTitleBand(model, docType));

            if (docType == DocTypes.OperationSheet || docType == DocTypes.MaintenanceOperationSheet)
            {
                AddEquipementsTable(page);
                AddObservationsSignatures(page);
            }
            else if (docType == DocTypes.BonLivraison)
            {
                AddLignesTable(page, priced: false);
                AddBonLivraisonFooter(page);
            }
            else
            {
                AddLignesTable(page, priced: true);
                AddTotauxFooter(page);
            }

            page.Components.Add(BuildPageFooter(model));

            RenderService.RegisterData(report, sampleJson);
            return report;
        }

        // ================================================================= DEVIS FIDELE

        /// <summary>Entete Devis fidele au modele 7 Axiobat : bandeau orange, logo, boites Devis N°/Date/Client,
        /// titre « Devis », bloc societe, bloc client a crochets d'angle, adresse d'intervention, affaire suivie par.</summary>
        private static StiReportTitleBand BuildQuoteHeaderModel7()
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 7.4, CanShrink = false };

            // Bandeau orange en tete
            band.Components.Add(Rect(0, 0, W, 0.28, Accent));

            // Logo (image societe)
            band.Components.Add(new StiImage(new RectangleD(0, 0.5, 5.0, 1.7))
            {
                Name = SocieteAssetsService.LogoComponentName,
                Stretch = true,
                AspectRatio = true
            });

            // Boites Devis N° / Date / Client (haut droite)
            AddQuoteInfoBox(band, 11.0, 0.5, "Devis N°", "{document.reference}");
            AddQuoteInfoBox(band, 13.7, 0.5, "Date", "{document.dateCreation}", isDate: true);
            AddQuoteInfoBox(band, 16.4, 0.5, "Client", "{client.nom}");

            // Titre centre
            band.Components.Add(Txt(0, 2.6, W, 0.9, "{document.titre}", 21, bold: true, align: StiTextHorAlignment.Center, color: Dark));

            // Bloc societe (gauche)
            band.Components.Add(Txt(0, 3.9, 8.0, 2.2,
                "{societe.nom}\n{adresse.rue}\n{adresse.codePostal} {adresse.ville}\nTél : {societe.telephone}\nEmail : {societe.email}",
                8.5f, color: Dark));

            // Bloc client a crochets d'angle (droite)
            band.Components.Add(Txt(11.3, 4.0, 6.5, 1.8,
                "{client.blocClient}",
                8.5f, color: Dark));
            AddCornerBrackets(band, 10.7, 3.7, 8.0, 2.1);

            // Adresse d'intervention + affaire suivie par
            band.Components.Add(Txt(0, 6.3, W, 0.5, "{client.adresseInter}", 9, bold: true, color: Dark));
            band.Components.Add(Txt(0, 6.8, W, 0.5, "{client.affaire}", 9, bold: true, color: Dark));

            return band;
        }

        // Expressions communes aux entetes Devis
        private const string SocieteExpr = "{societe.nom}\n{adresse.rue}\n{adresse.codePostal} {adresse.ville}\nTél : {societe.telephone}\nEmail : {societe.email}";
        private const string ClientExpr = "{client.blocClient}";
        private const string DateCreation = "{document.dateCreation.ToString(\"dd-MM-yyyy\")}";
        private const string DateValidite = "{document.dateValidite.ToString(\"dd-MM-yyyy\")}";
        private const string DevisInfoExpr = "Devis\nDate de création : " + DateCreation + "\nFin validité : " + DateValidite;
        private const string AdresseInterExpr = "Adresse d'intervention :\n{adresseChantier.rue}\n{adresseChantier.codePostal} {adresseChantier.ville}\n{adresseChantier.pays}";

        /// <summary>Modele 5 (sobre, papier entête) : Devis a gauche, client + adresse a droite. Pas de couleur d'entete.</summary>
        private static StiReportTitleBand BuildQuoteHeaderModel5()
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 5.6, CanShrink = false };
            AddLabeledBlock(band, 0, 1.8, 8.0, "Devis", "Date de création : " + DateCreation + "\nFin validité : " + DateValidite);
            AddLabeledBlock(band, 10.5, 1.8, 8.0, "{client.blocClient}", "");
            AddLabeledBlock(band, 10.5, 3.4, 8.0, "{client.adresseInter}", "");
            band.Components.Add(Txt(0, 5.0, W, 0.5, "{client.affaire}", 9, bold: true, color: Dark));
            return band;
        }

        /// <summary>Modele 6 (sobre, papier entête) : blocs en colonne centrale. Pas de couleur d'entete.</summary>
        private static StiReportTitleBand BuildQuoteHeaderModel6()
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 6.0, CanShrink = false };
            AddLabeledBlock(band, 10.5, 1.2, 8.0, "Devis", "Date de création : " + DateCreation + "\nFin validité : " + DateValidite);
            AddLabeledBlock(band, 10.5, 2.8, 8.0, "{client.blocClient}", "");
            AddLabeledBlock(band, 10.5, 4.2, 8.0, "{client.adresseInter}", "");
            band.Components.Add(Txt(0, 5.5, W, 0.5, "{client.affaire}", 9, bold: true, color: Dark));
            return band;
        }

        /// <summary>Bloc avec 1re ligne en gras (label) + detail normal — pour les modeles sobres 5/6.</summary>
        private static void AddLabeledBlock(StiBand band, double x, double y, double w, string label, string detail)
        {
            band.Components.Add(Txt(x, y, w, 0.5, label, 9.5f, bold: true, color: Dark));
            band.Components.Add(Txt(x, y + 0.5, w, 1.1, detail, 8.5f, color: Dark));
        }

        /// <summary>Pied de page discret (modeles sobres 5/6) : filet gris + mentions, pas de bande couleur.</summary>
        private static StiPageFooterBand BuildQuotePlainFooter()
        {
            var band = new StiPageFooterBand { Name = "PiedDePage", Height = 1.5 };
            band.Components.Add(Rect(0, 0.1, W, 0.02, Color.Silver));
            band.Components.Add(Txt(0, 0.2, 16, 1.2, "{societe.pied}", 7, color: Color.Gray));
            band.Components.Add(Txt(16.2, 0.3, 2.8, 0.5, "Page {PageNumber} / {TotalPageCount}", 8, color: Color.Gray, align: StiTextHorAlignment.Right));
            return band;
        }

        /// <summary>Modele 2 : bandeau orange, logo + societe en clair, boites claires (Devis / client / adresse).</summary>
        private static StiReportTitleBand BuildQuoteHeaderModel2()
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 7.4, CanShrink = false };
            band.Components.Add(Rect(0, 0, W, 0.5, Accent));
            AddLogo(band, 0, 0.9);
            band.Components.Add(Txt(11, 0.95, 8, 2.2, SocieteExpr, 8.5f, color: Dark));
            band.Components.Add(LightBox(0, 3.3, 7.5, 1.3, DevisInfoExpr));
            band.Components.Add(LightBox(11, 3.3, 8, 1.5, ClientExpr));
            band.Components.Add(LightBox(11, 5.0, 8, 1.3, AdresseInterExpr));
            band.Components.Add(Txt(0, 6.7, W, 0.5, "{client.affaire}", 9, bold: true, color: Dark));
            return band;
        }

        /// <summary>Modele 3 : logo a gauche, gros bloc societe orange (texte blanc) a droite, blocs a liseré.</summary>
        private static StiReportTitleBand BuildQuoteHeaderModel3()
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 7.4, CanShrink = false };
            AddLogo(band, 0, 0.5);
            band.Components.Add(Rect(10.0, 0.3, 9.0, 2.4, Accent));
            band.Components.Add(Txt(10.35, 0.5, 8.4, 2.1, SocieteExpr, 8.5f, color: Color.White, transparent: true));
            AddBorderBlock(band, 0, 3.3, 7.5, 1.3, DevisInfoExpr);
            AddBorderBlock(band, 10.5, 3.3, 8.0, 1.4, ClientExpr);
            AddBorderBlock(band, 10.5, 4.9, 8.0, 1.3, AdresseInterExpr);
            band.Components.Add(Txt(0, 6.6, W, 0.5, "{client.affaire}", 9, bold: true, color: Dark));
            return band;
        }

        /// <summary>Modele 4 : logo a gauche, bandeau orange arrondi (Devis/dates en blanc) a droite, blocs a liseré.</summary>
        private static StiReportTitleBand BuildQuoteHeaderModel4()
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 7.4, CanShrink = false };
            AddLogo(band, 0, 0.6);
            band.Components.Add(RoundedRect(8.8, 0, 10.2, 2.5, Accent));
            band.Components.Add(Txt(9.4, 0.5, 9.2, 1.8, DevisInfoExpr, 10, bold: true, color: Color.White, transparent: true));
            AddBorderBlock(band, 0, 3.1, 8.5, 1.8, SocieteExpr);
            AddBorderBlock(band, 10.5, 3.1, 8.0, 1.4, ClientExpr);
            AddBorderBlock(band, 10.5, 4.7, 8.0, 1.3, AdresseInterExpr);
            band.Components.Add(Txt(0, 6.5, W, 0.5, "{client.affaire}", 9, bold: true, color: Dark));
            return band;
        }

        /// <summary>Boite a fond clair (recoloree en version claire de la couleur societe).</summary>
        private static StiText LightBox(double x, double y, double w, double h, string expr)
        {
            var box = Txt(x + 0.25, y, w - 0.25, h, expr, 8.5f, color: Dark);
            box.Brush = new StiSolidBrush(AccentLight);
            return box;
        }

        /// <summary>Bloc a liseré orange a gauche (recolore) + texte, ajoutes a la bande.</summary>
        private static void AddBorderBlock(StiBand band, double x, double y, double w, double h, string expr)
        {
            band.Components.Add(Rect(x, y, 0.12, h, Accent)); // liseré vertical orange
            band.Components.Add(Txt(x + 0.35, y, w - 0.35, h, expr, 8.5f, color: Dark));
        }

        private static void AddQuoteInfoBox(StiBand band, double x, double y, string label, string expr, bool isDate = false)
        {
            const double w = 2.5;
            var head = Txt(x, y, w, 0.45, label, 8, bold: true, color: Color.White, align: StiTextHorAlignment.Center);
            head.Brush = new StiSolidBrush(Accent);
            band.Components.Add(head);

            var value = isDate
                ? DateValue(x, y + 0.45, w, expr, StiTextHorAlignment.Center)
                : Txt(x, y + 0.45, w, 0.5, expr, 8, align: StiTextHorAlignment.Center, color: Dark);
            value.Height = 0.5;
            value.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
            band.Components.Add(value);
        }

        /// <summary>Crochets d'angle gris (┌ ┐ └ ┘) autour d'une zone — comme le bloc client du modele 7.</summary>
        private static void AddCornerBrackets(StiBand band, double x, double y, double w, double h)
        {
            const double len = 0.55, th = 0.03;
            var gray = Color.DarkGray;
            // haut-gauche
            band.Components.Add(Rect(x, y, len, th, gray));
            band.Components.Add(Rect(x, y, th, len, gray));
            // haut-droite
            band.Components.Add(Rect(x + w - len, y, len, th, gray));
            band.Components.Add(Rect(x + w - th, y, th, len, gray));
            // bas-gauche
            band.Components.Add(Rect(x, y + h - th, len, th, gray));
            band.Components.Add(Rect(x, y + h - len, th, len, gray));
            // bas-droite
            band.Components.Add(Rect(x + w - len, y + h - th, len, th, gray));
            band.Components.Add(Rect(x + w - th, y + h - len, th, len, gray));
        }

        /// <summary>
        /// Colonnes du tableau devis (ordre gauche->droite). La cle pilote la visibilite via la config
        /// `cols` (voir ColumnsApplier) ; "designation" est la colonne flexible (absorbe la largeur restante).
        /// Toutes les colonnes sont construites dans le seed ; ColumnsApplier masque/recompacte au rendu.
        /// </summary>
        // image=true : cellule StiImage liee a une colonne base64 (vignette article) ; sinon cellule texte.
        private static readonly (string key, string title, string expr, StiTextHorAlignment align, bool money, bool flex, double w, bool html, bool image)[] QuoteColumns =
        {
            ("num",         "N°",          "{lignes.numero}",          StiTextHorAlignment.Left,   false, false, 1.3, false, false),
            ("vignette",    "Photo",       "lignes.vignette",          StiTextHorAlignment.Center, false, false, 1.8, false, true),
            ("designation", "Désignation", "{lignes.designationHtml}", StiTextHorAlignment.Left,   false, true,  0.0, true,  false),
            ("qte",         "Qté",         "{lignes.quantite}",        StiTextHorAlignment.Right,  false, false, 1.5, false, false),
            ("unite",       "Unité",       "{lignes.unite}",           StiTextHorAlignment.Center, false, false, 1.4, false, false),
            ("prixU",       "Prix U.",     "{lignes.prixUnitaire}",    StiTextHorAlignment.Right,  true,  false, 2.3, false, false),
            ("tva",         "TVA",         "{lignes.tva}",             StiTextHorAlignment.Right,  false, false, 1.5, false, false),
            ("prixHT",      "Prix HT",     "{lignes.totalHT}",         StiTextHorAlignment.Right,  true,  false, 2.6, false, false),
            ("ttc",         "TTC",         "{lignes.totalTTC}",        StiTextHorAlignment.Right,  true,  false, 2.6, false, false),
        };

        /// <summary>Tableau des lignes fidele au Devis Axiobat (entete coloree, colonnes pilotees par `cols`).</summary>
        private static void AddQuoteTable(StiPage page)
        {
            var header = new StiHeaderBand { Name = "EnteteLignes", Height = 0.6, PrintOnAllPages = true };
            var data = new StiDataBand { Name = "Lignes", Height = 0.5, DataSourceName = "lignes", CanShrink = true, CanGrow = true };

            // Fond clair des lignes (recolore en version claire de la couleur societe) — 1 ligne sur 2.
            var rowBg = Rect(0, 0, W, 0.5, AccentLight);
            rowBg.Name = "RowBg";
            rowBg.CanGrow = true;
            rowBg.Conditions.Add(new Stimulsoft.Report.Components.StiCondition
            {
                Expression = "(Line % 2) == 0",
                BackColor = Color.White
            });
            data.Components.Add(rowBg);

            // Layout initial « toutes colonnes visibles » ; ColumnsApplier recalcule x/largeurs selon `cols` au rendu.
            double fixedSum = 0;
            foreach (var c in QuoteColumns) if (!c.flex) fixedSum += c.w;
            double flexW = Math.Max(3.0, W - fixedSum);

            double x = 0;
            foreach (var c in QuoteColumns)
            {
                double w = c.flex ? flexW : c.w;
                AddQuoteColumn(header, data, x, w, c.key, c.title, c.expr, c.align, money: c.money, allowHtml: c.html, image: c.image);
                x += w;
            }

            page.Components.Add(header);
            page.Components.Add(data);
        }

        /// <summary>Bloc totaux fidele (Total HT / TVA / Total TTC + Net a payer en bandes couleur societe).</summary>
        private static void AddQuoteTotals(StiPage page)
        {
            var footer = new StiFooterBand { Name = "Totaux", Height = 5.2, CanShrink = true };

            footer.Components.Add(Txt(0, 0.4, 9.5, 0.5, "Conditions de règlement", 9, bold: true, color: Dark));
            footer.Components.Add(Txt(0, 0.9, 9.5, 1.4, "{paiement.conditions}\nRIB : {paiement.rib}", 8, color: Color.DimGray));

            footer.Components.Add(Txt(11.5, 0.4, 4.5, 0.5, "Total HT", 9, align: StiTextHorAlignment.Right, color: Dark));
            footer.Components.Add(MoneyValue(16.0, 0.4, 3.0, "{totaux.totalHT}"));
            footer.Components.Add(Txt(11.5, 0.95, 4.5, 0.5, "Total TVA", 9, align: StiTextHorAlignment.Right, color: Dark));
            footer.Components.Add(MoneyValue(16.0, 0.95, 3.0, "{totaux.totalTva}"));

            var ttcLabel = Txt(11.5, 1.6, 4.5, 0.62, "Total TTC", 10, bold: true, align: StiTextHorAlignment.Right, color: Color.White);
            ttcLabel.Brush = new StiSolidBrush(Accent);
            footer.Components.Add(ttcLabel);
            var ttcValue = MoneyValue(16.0, 1.6, 3.0, "{totaux.totalTTC}");
            ttcValue.Brush = new StiSolidBrush(Accent);
            ttcValue.TextBrush = new StiSolidBrush(Color.White);
            ttcValue.Font = new Stimulsoft.Drawing.Font("Arial", 10, FontStyle.Bold);
            ttcValue.Height = 0.62;
            footer.Components.Add(ttcValue);

            var netLabel = Txt(11.5, 2.25, 4.5, 0.62, "Net à payer", 10, bold: true, align: StiTextHorAlignment.Right, color: Color.White);
            netLabel.Brush = new StiSolidBrush(Accent);
            footer.Components.Add(netLabel);
            var netValue = MoneyValue(16.0, 2.25, 3.0, "{totaux.netAPayer}");
            netValue.Brush = new StiSolidBrush(Accent);
            netValue.TextBrush = new StiSolidBrush(Color.White);
            netValue.Font = new Stimulsoft.Drawing.Font("Arial", 10, FontStyle.Bold);
            netValue.Height = 0.62;
            footer.Components.Add(netValue);

            footer.Components.Add(Txt(11.5, 3.0, 7.5, 0.5, "{totaux.totalEnLettres}", 8, color: Color.DimGray, align: StiTextHorAlignment.Right));
            footer.Components.Add(Txt(0, 3.0, 5.5, 0.9, "{options.mentionsSpecifiques}", 8, color: Color.Gray));

            // Cachet / label societe (optionnel) : boite dans la zone signature, a droite des mentions.
            AddCachet(footer, 6.0, 3.0, 3.5, 1.9);

            page.Components.Add(footer);
        }

        /// <summary>Pied de page legal fidele (bande couleur societe : coordonnees + mentions legales).</summary>
        private static StiPageFooterBand BuildQuotePageFooter()
        {
            var pageFooter = new StiPageFooterBand { Name = "PiedDePage", Height = 1.7 };
            pageFooter.Components.Add(Rect(0, 0.15, W, 1.4, Accent));
            pageFooter.Components.Add(Txt(0.4, 0.25, 15.5, 1.25, "{societe.pied}", 7, color: Color.White, transparent: true));
            pageFooter.Components.Add(Txt(16.2, 0.4, 2.8, 0.6, "Page {PageNumber} / {TotalPageCount}", 8, color: Color.White, align: StiTextHorAlignment.Right, transparent: true));
            return pageFooter;
        }

        // Chaque colonne recoit un Name stable (HCol_{cle} pour l'entete, DCol_{cle} pour la cellule)
        // afin que ColumnsApplier puisse la masquer/repositionner au rendu selon la config `cols`.
        private static void AddQuoteColumn(StiBand header, StiDataBand data, double x, double w, string key, string title, string expr, StiTextHorAlignment align, bool money = false, bool allowHtml = false, bool image = false)
        {
            var head = Txt(x, 0.06, w, 0.5, title, 9, bold: true, color: Color.White, align: align);
            head.Brush = new StiSolidBrush(Accent);
            head.Name = $"HCol_{key}";
            header.Components.Add(head);

            if (image)
            {
                // Vignette article : image liee a une colonne base64 (expr = "lignes.<champ>"). Taille de ligne
                // ajustee par ColumnsApplier quand la colonne est visible.
                var img = new StiImage(new RectangleD(x + 0.1, 0.05, w - 0.2, 0.4))
                {
                    Name = $"DCol_{key}",
                    Stretch = true,
                    AspectRatio = true,
                    HorAlignment = StiHorAlignment.Center,
                    VertAlignment = StiVertAlignment.Center
                };
                img.DataColumn = expr;
                data.Components.Add(img);
                return;
            }

            var cell = Txt(x, 0.0, w, 0.5, expr, 8.5f, align: align, color: Dark);
            cell.Name = $"DCol_{key}";
            if (allowHtml) cell.AllowHtmlTags = true;
            cell.Border = new StiBorder(StiBorderSides.Bottom, AccentLight, 1, StiPenStyle.Solid);
            cell.CanGrow = true;
            if (money) cell.TextFormat = Money();
            data.Components.Add(cell);
        }

        // ================================================================= ENTETES

        private static StiReportTitleBand BuildTitleBand(int model, string docType)
        {
            var band = new StiReportTitleBand { Name = "Entete", CanShrink = true };
            var isSupplier = docType == DocTypes.SupplierOrder;

            switch (model)
            {
                case 1: // bandeau colore pleine largeur avec courbe blanche
                    band.Height = 5.6;
                    band.Components.Add(Rect(0, 0, W, 1.1, Accent));
                    band.Components.Add(RoundedRect(-0.5, 0.75, W + 1, 0.8, Color.White));
                    AddLogo(band, 0, 1.4);
                    band.Components.Add(SocieteBlock(11, 1.3, 8, StiTextHorAlignment.Right));
                    AddDocInfo(band, 0, 3.0, docType);
                    band.Components.Add(TiersBlock(11, 3.0, 8, isSupplier));
                    break;

                case 2: // filet en haut + logo encadre a gauche
                    band.Height = 5.9;
                    band.Components.Add(Rect(0, 0, W, 0.3, Accent));
                    var logoBox = Box(0, 0.7, 5.5, 2.2, "", 9);
                    band.Components.Add(logoBox);
                    AddLogo(band, 0.6, 1.4);
                    band.Components.Add(SocieteBlock(11, 0.7, 8, StiTextHorAlignment.Right));
                    AddDocInfo(band, 0, 3.3, docType);
                    band.Components.Add(TiersBlock(11, 3.3, 8, isSupplier));
                    break;

                case 3: // bloc societe en couleur a droite
                    band.Height = 5.4;
                    AddLogo(band, 0, 0.3);
                    var societe3 = SocieteBlock(10.5, 0, 8.5, StiTextHorAlignment.Left, white: true);
                    societe3.Brush = new StiSolidBrush(Accent);
                    band.Components.Add(societe3);
                    AddDocInfo(band, 0, 2.6, docType);
                    var tiers3 = TiersBlock(10.5, 2.6, 8.5, isSupplier);
                    tiers3.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
                    band.Components.Add(tiers3);
                    break;

                case 4: // bloc societe arrondi en couleur en haut a droite
                    band.Height = 5.6;
                    band.Components.Add(RoundedRect(10.5, 0, 8.5, 2.3, Accent));
                    var societe4 = SocieteBlock(10.8, 0.2, 7.9, StiTextHorAlignment.Left, white: true, transparent: true);
                    band.Components.Add(societe4);
                    AddLogo(band, 0, 0.4);
                    AddDocInfo(band, 0, 2.8, docType);
                    band.Components.Add(TiersBlock(10.5, 2.8, 8.5, isSupplier));
                    break;

                case 5: // sobre, sans couleur
                    band.Height = 4.6;
                    AddLogo(band, 0, 0.1);
                    band.Components.Add(SocieteBlock(11, 0, 8, StiTextHorAlignment.Right));
                    AddDocInfo(band, 0, 2.2, docType);
                    band.Components.Add(TiersBlock(11, 2.2, 8, isSupplier));
                    break;

                case 6: // sobre, reference centree
                    band.Height = 5.0;
                    band.Components.Add(Txt(0, 0.2, W, 0.7, "{options.titreDocument} {document.reference}", 13, bold: true, align: StiTextHorAlignment.Center, color: Dark));
                    band.Components.Add(DateValue(7.7, 0.9, 3.6, "{document.dateCreation}", StiTextHorAlignment.Center));
                    band.Components.Add(SocieteBlock(0, 1.8, 8, StiTextHorAlignment.Left, small: true));
                    band.Components.Add(TiersBlock(11, 1.8, 8, isSupplier));
                    break;

                default: // 7 : champs encadres + titre centre + client/adresse encadres
                    band.Height = 7.6;
                    AddLogo(band, 0, 0.1);
                    band.Components.Add(SocieteBlock(11, 0, 8, StiTextHorAlignment.Right, small: true));
                    band.Components.Add(Rect(0, 1.7, W, 0.05, Accent));

                    band.Components.Add(BoxLabel(0, 2.0, 6.2, 0.45, "N0 document"));
                    band.Components.Add(Box(0, 2.45, 6.2, 0.6, "{document.reference}", 9, align: StiTextHorAlignment.Center));
                    band.Components.Add(BoxLabel(6.4, 2.0, 6.2, 0.45, "Date de creation"));
                    var date7 = DateValue(6.4, 2.45, 6.2, "{document.dateCreation}", StiTextHorAlignment.Center);
                    date7.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
                    band.Components.Add(date7);
                    band.Components.Add(BoxLabel(12.8, 2.0, 6.2, 0.45, "Validite / Echeance"));
                    var date7b = DateValue(12.8, 2.45, 6.2, "{document.dateValidite}", StiTextHorAlignment.Center);
                    date7b.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
                    band.Components.Add(date7b);

                    band.Components.Add(Txt(0, 3.4, W, 0.8, "{options.titreDocument}", 15, bold: true, align: StiTextHorAlignment.Center, color: Dark));

                    var tiers7 = TiersBlock(0, 4.5, 9.2, isSupplier);
                    tiers7.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
                    band.Components.Add(tiers7);
                    var adresse7 = Txt(9.8, 4.5, 9.2, 2.2,
                        isSupplier
                            ? "Adresse de livraison\n{adresseLivraison.rue}\n{adresseLivraison.codePostal} {adresseLivraison.ville}"
                            : "Adresse d'intervention\n{adresseChantier.rue}\n{adresseChantier.codePostal} {adresseChantier.ville}", 9);
                    adresse7.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
                    band.Components.Add(adresse7);
                    break;
            }

            // objet du document sous l'entete (sauf modele 7 deja charge)
            if (model != 7)
                band.Components.Add(Txt(0, band.Height - 0.6, W, 0.55, "Objet : {document.objet}", 9, bold: true, color: Dark));

            return band;
        }

        private static void AddLogo(StiReportTitleBand band, double x, double y)
        {
            // Vraie image de logo (alimentee par les assets societe au rendu / a l'ouverture du designer).
            band.Components.Add(new StiImage(new RectangleD(x, y, 4.8, 1.9))
            {
                Name = SocieteAssetsService.LogoComponentName,
                Stretch = true,
                AspectRatio = true
            });
        }

        /// <summary>Cachet / label societe (image optionnelle, alimentee par les assets au rendu). Vide si non configure.</summary>
        private static void AddCachet(StiBand band, double x, double y, double w, double h)
        {
            band.Components.Add(new StiImage(new RectangleD(x, y, w, h))
            {
                Name = SocieteAssetsService.CachetComponentName,
                Stretch = true,
                AspectRatio = true
            });
        }

        private static void AddDocInfo(StiReportTitleBand band, double x, double y, string docType)
        {
            band.Components.Add(Txt(x, y, 8, 0.6, "{options.titreDocument} {document.reference}", 11, bold: true, color: Dark));
            band.Components.Add(Txt(x, y + 0.6, 4.2, 0.5, "Date de creation :", 9));
            band.Components.Add(DateValue(x + 4.2, y + 0.6, 3.5, "{document.dateCreation}", StiTextHorAlignment.Left));
            var secondLabel = docType == DocTypes.Invoice || docType == DocTypes.CreditNote ? "Date d'echeance :" : "Date de validite :";
            var secondValue = docType == DocTypes.Invoice ? "{document.dateEcheance}" : "{document.dateValidite}";
            band.Components.Add(Txt(x, y + 1.1, 4.2, 0.5, secondLabel, 9));
            band.Components.Add(DateValue(x + 4.2, y + 1.1, 3.5, secondValue, StiTextHorAlignment.Left));
        }

        private static StiText SocieteBlock(double x, double y, double w, StiTextHorAlignment align, bool white = false, bool small = false, bool transparent = false)
        {
            var text = Txt(x, y, w, 2.2,
                "{societe.nom}\n{adresse.rue}\n{adresse.codePostal} {adresse.ville}\nTel : {societe.telephone}\nEmail : {societe.email}",
                small ? 8 : 9, align: align, color: white ? Color.White : Dark, transparent: transparent);
            return text;
        }

        private static StiText TiersBlock(double x, double y, double w, bool isSupplier)
        {
            var expr = isSupplier
                ? "{fournisseur.nom}\n{adresse.rue}\n{adresse.codePostal} {adresse.ville}\nTel : {fournisseur.telephone}"
                : "{client.blocClient}";
            var text = Txt(x, y, w, 2.0, expr, 9, color: Dark);
            text.Font = new Stimulsoft.Drawing.Font("Arial", 9, FontStyle.Bold);
            return text;
        }

        // ================================================================= CORPS

        private static void AddLignesTable(StiPage page, bool priced)
        {
            var header = new StiHeaderBand { Name = "EnteteLignes", Height = 0.65, PrintOnAllPages = true };
            var data = new StiDataBand { Name = "Lignes", Height = 0.55, DataSourceName = "lignes", CanShrink = true, CanGrow = true };

            if (priced)
            {
                AddColumn(header, data, 0.0, 8.6, "Designation", "{lignes.numero}  {lignes.designation}", StiTextHorAlignment.Left);
                AddColumn(header, data, 8.6, 1.4, "Qte", "{lignes.quantite}", StiTextHorAlignment.Right);
                AddColumn(header, data, 10.0, 1.4, "Unite", "{lignes.unite}", StiTextHorAlignment.Center);
                AddColumn(header, data, 11.4, 2.2, "P.U", "{lignes.prixUnitaire}", StiTextHorAlignment.Right, money: true);
                AddColumn(header, data, 13.6, 1.4, "TVA %", "{lignes.tva}", StiTextHorAlignment.Right);
                AddColumn(header, data, 15.0, 4.0, "Prix HT", "{lignes.totalHT}", StiTextHorAlignment.Right, money: true);
            }
            else
            {
                AddColumn(header, data, 0.0, 9.4, "Designation", "{lignes.numero}  {lignes.designation}", StiTextHorAlignment.Left);
                AddColumn(header, data, 9.4, 1.6, "Unite", "{lignes.unite}", StiTextHorAlignment.Center);
                AddColumn(header, data, 11.0, 2.6, "Qte commandee", "{lignes.quantiteCommandee}", StiTextHorAlignment.Right);
                AddColumn(header, data, 13.6, 2.6, "Qte livree", "{lignes.quantiteLivree}", StiTextHorAlignment.Right);
                AddColumn(header, data, 16.2, 2.8, "Qte restante", "{lignes.quantiteRestante}", StiTextHorAlignment.Right);
            }

            page.Components.Add(header);
            page.Components.Add(data);
        }

        private static void AddColumn(StiBand header, StiDataBand data, double x, double w, string title, string expr, StiTextHorAlignment align, bool money = false)
        {
            var head = Txt(x, 0.08, w, 0.5, title, 9, bold: true, color: Color.White, align: align);
            head.Brush = new StiSolidBrush(Accent);
            header.Components.Add(head);

            var cell = Txt(x, 0.0, w, 0.5, expr, 9, align: align, color: Dark);
            cell.Border = new StiBorder(StiBorderSides.Bottom, AccentLight, 1, StiPenStyle.Solid);
            cell.CanGrow = true;
            if (money) cell.TextFormat = Money();
            data.Components.Add(cell);
        }

        private static void AddTotauxFooter(StiPage page)
        {
            var footer = new StiFooterBand { Name = "TotauxEtSignature", Height = 7.2, CanShrink = true };

            // Conditions de reglement (gauche)
            footer.Components.Add(Txt(0, 0.3, 10.5, 0.5, "Conditions de reglement", 9, bold: true, color: Dark));
            footer.Components.Add(Txt(0, 0.8, 10.5, 1.6, "{paiement.conditions}\nRIB : {paiement.rib}", 8, color: Color.DimGray));

            // Totaux (droite)
            footer.Components.Add(Txt(12.5, 0.3, 4.0, 0.5, "Montant HT", 9, align: StiTextHorAlignment.Right, color: Dark));
            footer.Components.Add(MoneyValue(16.5, 0.3, 2.5, "{totaux.totalHT}"));
            footer.Components.Add(Txt(12.5, 0.85, 4.0, 0.5, "TVA", 9, align: StiTextHorAlignment.Right, color: Dark));
            footer.Components.Add(MoneyValue(16.5, 0.85, 2.5, "{totaux.totalTva}"));

            var totalLabel = Txt(12.5, 1.5, 4.0, 0.65, "Total TTC", 10, bold: true, color: Color.White, align: StiTextHorAlignment.Right);
            totalLabel.Brush = new StiSolidBrush(Accent);
            footer.Components.Add(totalLabel);
            var totalValue = MoneyValue(16.5, 1.5, 2.5, "{totaux.totalTTC}");
            totalValue.Brush = new StiSolidBrush(Accent);
            totalValue.TextBrush = new StiSolidBrush(Color.White);
            totalValue.Font = new Stimulsoft.Drawing.Font("Arial", 10, FontStyle.Bold);
            totalValue.Height = 0.65;
            footer.Components.Add(totalValue);
            footer.Components.Add(Txt(12.5, 2.2, 6.5, 0.5, "{totaux.totalEnLettres}", 8, color: Color.DimGray, align: StiTextHorAlignment.Right));

            // Note (gauche)
            footer.Components.Add(Txt(0, 2.9, 10.5, 0.5, "Note", 9, bold: true, color: Dark));
            footer.Components.Add(Txt(0, 3.4, 10.5, 1.4, "{document.notes}", 8, color: Color.DimGray));

            // Bloc signature (droite)
            footer.Components.Add(Txt(12.5, 3.4, 6.5, 0.5, "Mon Entreprise", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            var signBox = Box(12.5, 3.9, 6.5, 2.6, "{options.mentionsSpecifiques}", 8, align: StiTextHorAlignment.Center, color: Color.Gray);
            footer.Components.Add(signBox);

            // Cachet / label societe (optionnel) : zone libre bas-gauche.
            AddCachet(footer, 0, 5.0, 3.5, 1.9);

            page.Components.Add(footer);
        }

        private static void AddBonLivraisonFooter(StiPage page)
        {
            var footer = new StiFooterBand { Name = "SignatureBL", Height = 4.4, CanShrink = true };
            footer.Components.Add(Txt(0, 0.3, 10.5, 0.5, "Note", 9, bold: true, color: Dark));
            footer.Components.Add(Txt(0, 0.8, 10.5, 1.6, "{document.notes}", 8, color: Color.DimGray));
            footer.Components.Add(Txt(12.5, 0.3, 6.5, 0.5, "Recu par (nom, date, signature)", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            footer.Components.Add(Box(12.5, 0.8, 6.5, 3.0, "", 8));
            // Cachet / label societe (optionnel) : zone libre bas-gauche.
            AddCachet(footer, 0, 2.5, 3.5, 1.8);
            page.Components.Add(footer);
        }

        private static void AddEquipementsTable(StiPage page)
        {
            var header = new StiHeaderBand { Name = "EnteteEquipements", Height = 0.65, PrintOnAllPages = true };
            var data = new StiDataBand { Name = "Equipements", Height = 0.55, DataSourceName = "equipements", CanShrink = true, CanGrow = true };

            AddColumn(header, data, 0.0, 7.0, "Equipement", "{equipements.designation}", StiTextHorAlignment.Left);
            AddColumn(header, data, 7.0, 4.0, "Marque", "{equipements.marque}", StiTextHorAlignment.Left);
            AddColumn(header, data, 11.0, 4.0, "Modele", "{equipements.modele}", StiTextHorAlignment.Left);
            AddColumn(header, data, 15.0, 4.0, "N0 serie", "{equipements.numeroSerie}", StiTextHorAlignment.Left);

            page.Components.Add(header);
            page.Components.Add(data);
        }

        private static void AddObservationsSignatures(StiPage page)
        {
            var footer = new StiFooterBand { Name = "ObservationsSignatures", Height = 7.6, CanShrink = true };

            footer.Components.Add(Txt(0, 0.4, W, 0.5, "Observations", 9, bold: true, color: Dark));
            footer.Components.Add(Box(0, 0.9, W, 1.6, "{document.observations}", 9));

            footer.Components.Add(Txt(0, 2.9, 9.2, 0.5, "Signature client", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            footer.Components.Add(Box(0, 3.4, 9.2, 2.0, "", 9));
            footer.Components.Add(Txt(9.8, 2.9, 9.2, 0.5, "Signature technicien", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            footer.Components.Add(Box(9.8, 3.4, 9.2, 2.0, "", 9));

            // Cachet / label societe (optionnel) : zone libre sous les signatures.
            AddCachet(footer, 0, 5.7, 3.5, 1.9);

            page.Components.Add(footer);
        }

        // ================================================================= PIED DE PAGE

        private static StiPageFooterBand BuildPageFooter(int model)
        {
            var band = new StiPageFooterBand { Name = "PiedDePage", Height = 1.7 };
            var mentions = "{societe.pied}";

            if (model == 5 || model == 6)
            {
                band.Components.Add(Rect(0, 0.1, W, 0.03, Color.Silver));
                band.Components.Add(Txt(0, 0.25, 16, 1.25, mentions, 7, color: Color.Gray));
            }
            else
            {
                if (model == 1)
                    band.Components.Add(RoundedRect(0, 0.15, W, 1.4, Accent));
                else
                    band.Components.Add(Rect(0, 0.15, W, 1.4, Accent));
                band.Components.Add(Txt(0.4, 0.28, 15.6, 1.25, mentions, 7, color: Color.White, transparent: true));
            }

            band.Components.Add(Txt(16.2, 0.35, 2.8, 0.6,
                "Page {PageNumber} / {TotalPageCount}", 8,
                color: model == 5 || model == 6 ? Color.Gray : Color.White,
                align: StiTextHorAlignment.Right, transparent: true));
            return band;
        }

        // ================================================================= HELPERS

        private static StiText Txt(double x, double y, double w, double h, string expr, float size,
            bool bold = false, Color? color = null, StiTextHorAlignment align = StiTextHorAlignment.Left, bool transparent = false)
        {
            var text = new StiText(new RectangleD(x, y, w, h))
            {
                Name = $"t{Guid.NewGuid():N}",
                HorAlignment = align,
                VertAlignment = StiVertAlignment.Top,
                WordWrap = true,
                Font = new Stimulsoft.Drawing.Font("Arial", size, bold ? FontStyle.Bold : FontStyle.Regular)
            };
            text.Text.Value = expr;
            if (color.HasValue) text.TextBrush = new StiSolidBrush(color.Value);
            if (transparent) text.Brush = new StiEmptyBrush();
            return text;
        }

        private static StiText Rect(double x, double y, double w, double h, Color fill)
        {
            var rect = new StiText(new RectangleD(x, y, w, h)) { Name = $"r{Guid.NewGuid():N}" };
            rect.Text.Value = string.Empty;
            rect.Brush = new StiSolidBrush(fill);
            return rect;
        }

        private static StiShape RoundedRect(double x, double y, double w, double h, Color fill)
        {
            return new StiShape(new RectangleD(x, y, w, h))
            {
                Name = $"s{Guid.NewGuid():N}",
                ShapeType = new StiRoundedRectangleShapeType(),
                Brush = new StiSolidBrush(fill),
                BorderColor = fill
            };
        }

        private static StiText Box(double x, double y, double w, double h, string expr, float size,
            StiTextHorAlignment align = StiTextHorAlignment.Left, Color? color = null)
        {
            var box = Txt(x, y, w, h, expr, size, align: align, color: color);
            box.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
            return box;
        }

        private static StiText BoxLabel(double x, double y, double w, double h, string title)
        {
            var box = Txt(x, y, w, h, title, 8, bold: true, color: Color.White, align: StiTextHorAlignment.Center);
            box.Brush = new StiSolidBrush(Accent);
            return box;
        }

        private static StiText DateValue(double x, double y, double w, string expr, StiTextHorAlignment align)
        {
            var text = Txt(x, y, w, 0.5, expr, 9, color: Dark, align: align);
            text.TextFormat = new StiDateFormatService { StringFormat = "dd/MM/yyyy" };
            return text;
        }

        private static StiText MoneyValue(double x, double y, double w, string expr)
        {
            var text = Txt(x, y, w, 0.5, expr, 9, align: StiTextHorAlignment.Right, color: Dark);
            text.TextFormat = Money();
            return text;
        }

        private static StiCurrencyFormatService Money() => new()
        {
            Symbol = "€",
            DecimalDigits = 2,
            PositivePattern = 3, // n €
            NegativePattern = 8  // -n €
        };
    }
}
