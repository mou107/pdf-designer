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
            band.Components.Add(Rect(x, y + 0.12, 0.35, 0.35, Accent));
            band.Components.Add(Txt(x + 0.5, y, 5.0, 0.7, "{societe.nom}", 13, bold: true, color: Dark));
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
                : "{client.civilite} {client.nom}\n{adresseFacturation.rue}\n{adresseFacturation.codePostal} {adresseFacturation.ville}\nTel : {client.telephone}";
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

            page.Components.Add(footer);
        }

        private static void AddBonLivraisonFooter(StiPage page)
        {
            var footer = new StiFooterBand { Name = "SignatureBL", Height = 4.4, CanShrink = true };
            footer.Components.Add(Txt(0, 0.3, 10.5, 0.5, "Note", 9, bold: true, color: Dark));
            footer.Components.Add(Txt(0, 0.8, 10.5, 1.6, "{document.notes}", 8, color: Color.DimGray));
            footer.Components.Add(Txt(12.5, 0.3, 6.5, 0.5, "Recu par (nom, date, signature)", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            footer.Components.Add(Box(12.5, 0.8, 6.5, 3.0, "", 8));
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
            var footer = new StiFooterBand { Name = "ObservationsSignatures", Height = 5.6, CanShrink = true };

            footer.Components.Add(Txt(0, 0.4, W, 0.5, "Observations", 9, bold: true, color: Dark));
            footer.Components.Add(Box(0, 0.9, W, 1.6, "{document.observations}", 9));

            footer.Components.Add(Txt(0, 2.9, 9.2, 0.5, "Signature client", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            footer.Components.Add(Box(0, 3.4, 9.2, 2.0, "", 9));
            footer.Components.Add(Txt(9.8, 2.9, 9.2, 0.5, "Signature technicien", 9, bold: true, align: StiTextHorAlignment.Center, color: Dark));
            footer.Components.Add(Box(9.8, 3.4, 9.2, 2.0, "", 9));

            page.Components.Add(footer);
        }

        // ================================================================= PIED DE PAGE

        private static StiPageFooterBand BuildPageFooter(int model)
        {
            var band = new StiPageFooterBand { Name = "PiedDePage", Height = 1.2 };
            var mentions = "{societe.nom} — {societe.mentionsLegales} — SIRET : {societe.siret} — TVA : {societe.tvaIntracommunautaire}";

            if (model == 5 || model == 6)
            {
                band.Components.Add(Rect(0, 0.1, W, 0.03, Color.Silver));
                band.Components.Add(Txt(0, 0.25, 16, 0.9, mentions, 7, color: Color.Gray));
            }
            else
            {
                if (model == 1)
                    band.Components.Add(RoundedRect(0, 0.15, W, 0.9, Accent));
                else
                    band.Components.Add(Rect(0, 0.15, W, 0.9, Accent));
                band.Components.Add(Txt(0.4, 0.35, 15.6, 0.6, mentions, 7, color: Color.White, transparent: true));
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
