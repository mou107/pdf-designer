using System.Drawing;
using API.CORE.Schemas;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services
{
    /// <summary>
    /// Genere les 7 modeles de depart (.mrt) par type de document — approximation V1
    /// des 7 modeles pdfmake historiques (pdf-entetes.ts / pdf-headers.ts) :
    ///   1. Bande de couleur pleine largeur en tete (entete01)
    ///   2. Bandeau colore + blocs client/societe encadres (entete02)
    ///   3. Classique : logo a gauche, titre a droite, filet de couleur (entete03)
    ///   4. Minimal centre, sans couleur (entete04)
    ///   5. Deux colonnes soulignees (entete05)
    ///   6. Blocs entierement encadres (entete06)
    ///   7. Bandeau colore avec infos document en blanc (entete07)
    /// La parite fine avec les etalons pdfmake sera faite dans le designer (PR 2.1).
    /// </summary>
    public static class SeedTemplateFactory
    {
        private const double W = 19.0; // largeur utile A4 (cm), marges 1cm

        private static readonly Color[] Accents =
        {
            Color.FromArgb(0x1F, 0x4E, 0x79), // 1 bleu fonce
            Color.FromArgb(0xC0, 0x39, 0x2B), // 2 rouge brique
            Color.FromArgb(0x2E, 0x7D, 0x32), // 3 vert
            Color.FromArgb(0x45, 0x5A, 0x64), // 4 gris bleu
            Color.FromArgb(0x6A, 0x1B, 0x9A), // 5 violet
            Color.FromArgb(0xE6, 0x7E, 0x22), // 6 orange
            Color.FromArgb(0x00, 0x69, 0x5C)  // 7 teal
        };

        public static StiReport Build(int model, string docType, string label, string sampleJson)
        {
            var accent = Accents[(model - 1) % Accents.Length];
            var report = StiReport.CreateNewReport();
            report.ReportName = $"{label} — Modele {model}";

            var page = report.Pages[0];
            page.Components.Clear();

            page.Components.Add(BuildTitleBand(model, docType, label, accent));

            if (docType == DocTypes.OperationSheet || docType == DocTypes.MaintenanceOperationSheet)
            {
                AddEquipementsSection(page, accent);
                AddSignaturesSection(page, accent);
            }
            else if (docType == DocTypes.BonLivraison)
            {
                AddLignesTable(page, accent, priced: false);
            }
            else
            {
                AddLignesTable(page, accent, priced: true);
                AddTotauxSection(page, accent, model);
            }

            page.Components.Add(BuildPageFooter(model, accent));

            RenderService.RegisterData(report, sampleJson);
            return report;
        }

        // ------------------------------------------------------------------ entete

        private static StiReportTitleBand BuildTitleBand(int model, string docType, string label, Color accent)
        {
            var band = new StiReportTitleBand { Name = "Entete", Height = 6.6, CanShrink = true };
            var isSupplier = docType == DocTypes.SupplierOrder;
            var tiersTitle = isSupplier ? "Fournisseur" : "Client";
            var tiers = isSupplier
                ? "{fournisseur.nom}\n{adresse.rue}\n{adresse.codePostal} {adresse.ville}\nTel : {fournisseur.telephone}"
                : "{client.civilite} {client.nom}\n{adresseFacturation.rue}\n{adresseFacturation.codePostal} {adresseFacturation.ville}\nTel : {client.telephone}";
            var societe = "{societe.nom}\n{adresse.rue}\n{adresse.codePostal} {adresse.ville}\nTel : {societe.telephone} — {societe.email}\nSIRET : {societe.siret}";

            switch (model)
            {
                case 1: // bande pleine largeur
                    band.Components.Add(Rect(0, 0, W, 1.0, accent));
                    band.Components.Add(Txt(0.3, 0.15, 12, 0.7, "{societe.nom}", 14, bold: true, color: Color.White, transparent: true));
                    band.Components.Add(Txt(12.3, 0.15, 6.4, 0.7, "{options.titreDocument}  {document.reference}", 12, bold: true, color: Color.White, align: StiTextHorAlignment.Right, transparent: true));
                    band.Components.Add(Txt(0, 1.3, 9.5, 2.6, societe, 9));
                    band.Components.Add(BoxTitle(11, 1.3, 8, 0.5, tiersTitle, accent));
                    band.Components.Add(Box(11, 1.8, 8, 2.1, tiers, 9));
                    band.Components.Add(Txt(0, 4.2, 9.5, 0.5, "Date : {document.dateCreation}", 9));
                    band.Components.Add(Txt(0, 4.7, 19, 0.6, "Objet : {document.objet}", 9, bold: true));
                    break;

                case 2: // bandeau + blocs encadres
                    band.Components.Add(Rect(0, 0, W, 1.6, accent));
                    band.Components.Add(Txt(0.3, 0.3, 11, 1.0, "{societe.nom}", 16, bold: true, color: Color.White, transparent: true));
                    band.Components.Add(Txt(12, 0.3, 6.7, 1.0, "{options.titreDocument}\n{document.reference} — {document.dateCreation}", 10, bold: true, color: Color.White, align: StiTextHorAlignment.Right, transparent: true));
                    band.Components.Add(BoxTitle(0, 2.0, 9.2, 0.5, "Societe", accent));
                    band.Components.Add(Box(0, 2.5, 9.2, 2.3, societe, 9));
                    band.Components.Add(BoxTitle(9.8, 2.0, 9.2, 0.5, tiersTitle, accent));
                    band.Components.Add(Box(9.8, 2.5, 9.2, 2.3, tiers, 9));
                    band.Components.Add(Txt(0, 5.1, 19, 0.6, "Objet : {document.objet}", 9, bold: true));
                    break;

                case 3: // classique + filet
                    band.Components.Add(Box(0, 0, 4.5, 2.2, "LOGO", 12, align: StiTextHorAlignment.Center, color: Color.Gray));
                    band.Components.Add(Txt(5, 0, 8, 2.2, societe, 9));
                    band.Components.Add(Txt(13.5, 0, 5.5, 0.8, "{options.titreDocument}", 16, bold: true, color: accent, align: StiTextHorAlignment.Right));
                    band.Components.Add(Txt(13.5, 0.9, 5.5, 1.0, "N0 {document.reference}\nDate : {document.dateCreation}", 9, align: StiTextHorAlignment.Right));
                    band.Components.Add(Rect(0, 2.5, W, 0.08, accent));
                    band.Components.Add(Txt(11, 2.9, 8, 2.2, tiersTitle + " :\n" + tiers, 9));
                    band.Components.Add(Txt(0, 2.9, 10, 0.6, "Objet : {document.objet}", 9, bold: true));
                    break;

                case 4: // minimal centre
                    band.Components.Add(Txt(0, 0.2, W, 0.9, "{societe.nom}", 13, bold: true, align: StiTextHorAlignment.Center));
                    band.Components.Add(Txt(0, 1.1, W, 0.7, "{options.titreDocument} {document.reference} — {document.dateCreation}", 11, align: StiTextHorAlignment.Center));
                    band.Components.Add(Rect(7.5, 1.9, 4, 0.05, accent));
                    band.Components.Add(Txt(0, 2.3, 9, 2.2, societe, 8, color: Color.DimGray));
                    band.Components.Add(Txt(11, 2.3, 8, 2.2, tiersTitle + " :\n" + tiers, 9));
                    band.Components.Add(Txt(0, 4.7, 19, 0.6, "{document.objet}", 9, align: StiTextHorAlignment.Center));
                    break;

                case 5: // deux colonnes soulignees
                    band.Components.Add(Txt(0, 0, 9.2, 0.6, "{societe.nom}", 12, bold: true, color: accent));
                    band.Components.Add(Rect(0, 0.65, 9.2, 0.05, accent));
                    band.Components.Add(Txt(0, 0.8, 9.2, 2.2, societe, 9));
                    band.Components.Add(Txt(9.8, 0, 9.2, 0.6, tiersTitle, 12, bold: true, color: accent));
                    band.Components.Add(Rect(9.8, 0.65, 9.2, 0.05, accent));
                    band.Components.Add(Txt(9.8, 0.8, 9.2, 2.2, tiers, 9));
                    band.Components.Add(Txt(0, 3.3, 19, 0.8, "{options.titreDocument} {document.reference} du {document.dateCreation}", 13, bold: true));
                    band.Components.Add(Txt(0, 4.2, 19, 0.6, "Objet : {document.objet}", 9));
                    break;

                case 6: // tout encadre
                    var titleBox = Txt(0, 0, W, 1.0, "{options.titreDocument}  {document.reference}", 14, bold: true, align: StiTextHorAlignment.Center, color: accent);
                    titleBox.Border = new StiBorder(StiBorderSides.All, accent, 2, StiPenStyle.Solid);
                    band.Components.Add(titleBox);
                    band.Components.Add(BoxTitle(0, 1.3, 9.2, 0.5, "Societe", accent));
                    band.Components.Add(Box(0, 1.8, 9.2, 2.3, societe, 9));
                    band.Components.Add(BoxTitle(9.8, 1.3, 9.2, 0.5, tiersTitle, accent));
                    band.Components.Add(Box(9.8, 1.8, 9.2, 2.3, tiers, 9));
                    band.Components.Add(Box(0, 4.4, W, 0.7, "Date : {document.dateCreation}    Objet : {document.objet}", 9));
                    break;

                default: // 7 : bandeau bas d'entete avec infos document
                    band.Components.Add(Txt(0, 0, 10, 2.4, societe, 9));
                    band.Components.Add(Txt(11, 0, 8, 2.4, tiersTitle + " :\n" + tiers, 9));
                    band.Components.Add(Rect(0, 2.7, W, 1.0, accent));
                    band.Components.Add(Txt(0.3, 2.85, 12, 0.7, "{options.titreDocument}  {document.reference}", 12, bold: true, color: Color.White, transparent: true));
                    band.Components.Add(Txt(12.3, 2.85, 6.4, 0.7, "Date : {document.dateCreation}", 10, color: Color.White, align: StiTextHorAlignment.Right, transparent: true));
                    band.Components.Add(Txt(0, 4.0, 19, 0.6, "Objet : {document.objet}", 9, bold: true));
                    break;
            }

            return band;
        }

        // ------------------------------------------------------------------ corps

        private static void AddLignesTable(StiPage page, Color accent, bool priced)
        {
            var header = new StiHeaderBand { Name = "EnteteLignes", Height = 0.6, PrintOnAllPages = true };
            var data = new StiDataBand { Name = "Lignes", Height = 0.55, DataSourceName = "lignes", CanShrink = true, CanGrow = true };

            if (priced)
            {
                AddColumn(header, data, 0.0, 1.2, "N0", "{lignes.numero}", accent, StiTextHorAlignment.Left);
                AddColumn(header, data, 1.2, 8.3, "Designation", "{lignes.designation}", accent, StiTextHorAlignment.Left);
                AddColumn(header, data, 9.5, 1.5, "Qte", "{lignes.quantite}", accent, StiTextHorAlignment.Right);
                AddColumn(header, data, 11.0, 1.3, "Unite", "{lignes.unite}", accent, StiTextHorAlignment.Center);
                AddColumn(header, data, 12.3, 2.2, "PU HT", "{lignes.prixUnitaire}", accent, StiTextHorAlignment.Right);
                AddColumn(header, data, 14.5, 1.5, "TVA %", "{lignes.tva}", accent, StiTextHorAlignment.Right);
                AddColumn(header, data, 16.0, 3.0, "Total HT", "{lignes.totalHT}", accent, StiTextHorAlignment.Right);
            }
            else
            {
                AddColumn(header, data, 0.0, 1.2, "N0", "{lignes.numero}", accent, StiTextHorAlignment.Left);
                AddColumn(header, data, 1.2, 8.8, "Designation", "{lignes.designation}", accent, StiTextHorAlignment.Left);
                AddColumn(header, data, 10.0, 1.5, "Unite", "{lignes.unite}", accent, StiTextHorAlignment.Center);
                AddColumn(header, data, 11.5, 2.5, "Qte cmdee", "{lignes.quantiteCommandee}", accent, StiTextHorAlignment.Right);
                AddColumn(header, data, 14.0, 2.5, "Qte livree", "{lignes.quantiteLivree}", accent, StiTextHorAlignment.Right);
                AddColumn(header, data, 16.5, 2.5, "Qte restante", "{lignes.quantiteRestante}", accent, StiTextHorAlignment.Right);
            }

            page.Components.Add(header);
            page.Components.Add(data);
        }

        private static void AddColumn(StiHeaderBand header, StiDataBand data, double x, double w, string title, string expr, Color accent, StiTextHorAlignment align)
        {
            var head = Txt(x, 0.05, w, 0.5, title, 9, bold: true, color: Color.White, align: align);
            head.Brush = new StiSolidBrush(accent);
            header.Components.Add(head);

            var cell = Txt(x, 0.0, w, 0.5, expr, 9, align: align);
            cell.Border = new StiBorder(StiBorderSides.Bottom, Color.Gainsboro, 1, StiPenStyle.Solid);
            cell.CanGrow = true;
            data.Components.Add(cell);
        }

        private static void AddTotauxSection(StiPage page, Color accent, int model)
        {
            var footer = new StiFooterBand { Name = "Totaux", Height = 4.4, CanShrink = true };

            footer.Components.Add(Txt(0, 0.3, 10.5, 2.4,
                "Conditions de reglement : {paiement.conditions}\nRIB : {paiement.rib}\n\n{options.mentionsSpecifiques}", 8, color: Color.DimGray));

            var boxed = model == 2 || model == 5 || model == 6;
            footer.Components.Add(Txt(12.5, 0.3, 4.0, 0.55, "Total HT", 9, bold: true, align: StiTextHorAlignment.Right));
            footer.Components.Add(Txt(16.5, 0.3, 2.5, 0.55, "{totaux.totalHT}", 9, align: StiTextHorAlignment.Right));
            footer.Components.Add(Txt(12.5, 0.85, 4.0, 0.55, "Total TTC", 9, bold: true, align: StiTextHorAlignment.Right));
            footer.Components.Add(Txt(16.5, 0.85, 2.5, 0.55, "{totaux.totalTTC}", 9, align: StiTextHorAlignment.Right));

            var net = Txt(12.5, 1.5, 6.5, 0.7, "NET A PAYER   {totaux.netAPayer}", 11, bold: true, align: StiTextHorAlignment.Right);
            if (boxed)
            {
                net.Border = new StiBorder(StiBorderSides.All, accent, 1, StiPenStyle.Solid);
            }
            else
            {
                net.Brush = new StiSolidBrush(accent);
                net.TextBrush = new StiSolidBrush(Color.White);
            }
            footer.Components.Add(net);

            footer.Components.Add(Txt(12.5, 2.3, 6.5, 0.5, "{totaux.totalEnLettres}", 8, color: Color.DimGray, align: StiTextHorAlignment.Right));
            footer.Components.Add(Box(12.5, 3.0, 6.5, 1.2, "{options.mentionsSpecifiques}", 8, align: StiTextHorAlignment.Center, color: Color.DimGray));

            page.Components.Add(footer);
        }

        private static void AddEquipementsSection(StiPage page, Color accent)
        {
            var header = new StiHeaderBand { Name = "EnteteEquipements", Height = 0.6, PrintOnAllPages = true };
            var data = new StiDataBand { Name = "Equipements", Height = 0.55, DataSourceName = "equipements", CanShrink = true, CanGrow = true };

            AddColumn(header, data, 0.0, 7.0, "Equipement", "{equipements.designation}", accent, StiTextHorAlignment.Left);
            AddColumn(header, data, 7.0, 4.0, "Marque", "{equipements.marque}", accent, StiTextHorAlignment.Left);
            AddColumn(header, data, 11.0, 4.0, "Modele", "{equipements.modele}", accent, StiTextHorAlignment.Left);
            AddColumn(header, data, 15.0, 4.0, "N0 serie", "{equipements.numeroSerie}", accent, StiTextHorAlignment.Left);

            page.Components.Add(header);
            page.Components.Add(data);
        }

        private static void AddSignaturesSection(StiPage page, Color accent)
        {
            var footer = new StiFooterBand { Name = "ObservationsSignatures", Height = 5.2, CanShrink = true };

            footer.Components.Add(BoxTitle(0, 0.3, W, 0.5, "Observations", accent));
            footer.Components.Add(Box(0, 0.8, W, 1.6, "{document.observations}", 9));

            footer.Components.Add(BoxTitle(0, 2.7, 9.2, 0.5, "Signature client", accent));
            footer.Components.Add(Box(0, 3.2, 9.2, 1.8, "", 9));
            footer.Components.Add(BoxTitle(9.8, 2.7, 9.2, 0.5, "Signature technicien", accent));
            footer.Components.Add(Box(9.8, 3.2, 9.2, 1.8, "", 9));

            page.Components.Add(footer);
        }

        private static StiPageFooterBand BuildPageFooter(int model, Color accent)
        {
            var band = new StiPageFooterBand { Name = "PiedDePage", Height = 1.1 };
            if (model is 1 or 2 or 7)
                band.Components.Add(Rect(0, 0, W, 0.06, accent));
            band.Components.Add(Txt(0, 0.15, 15.5, 0.9, "{societe.mentionsLegales}", 7, color: Color.Gray));
            band.Components.Add(Txt(15.5, 0.15, 3.5, 0.9, "Page {PageNumber} / {TotalPageCount}", 8, color: Color.Gray, align: StiTextHorAlignment.Right));
            return band;
        }

        // ------------------------------------------------------------------ helpers

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

        private static StiText Box(double x, double y, double w, double h, string expr, float size,
            StiTextHorAlignment align = StiTextHorAlignment.Left, Color? color = null)
        {
            var box = Txt(x, y, w, h, expr, size, align: align, color: color);
            box.Border = new StiBorder(StiBorderSides.All, Color.Silver, 1, StiPenStyle.Solid);
            return box;
        }

        private static StiText BoxTitle(double x, double y, double w, double h, string title, Color accent)
        {
            var box = Txt(x, y, w, h, title, 9, bold: true, color: Color.White, align: StiTextHorAlignment.Center);
            box.Brush = new StiSolidBrush(accent);
            return box;
        }
    }
}
