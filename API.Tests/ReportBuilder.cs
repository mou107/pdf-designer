using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.Tests
{
    /// <summary>
    /// Construit des rapports minimaux pour les tests : juste ce qu'il faut de composants pour observer
    /// l'effet d'une directive.
    /// </summary>
    /// <remarks>
    /// Volontairement en memoire plutot qu'a partir d'un .mrt de reference : un fichier fige lierait les
    /// tests a un modele particulier, alors que le moteur ne doit en connaitre aucun.
    /// </remarks>
    internal static class ReportBuilder
    {
        /// <summary>Un rapport d'une page, vide.</summary>
        public static StiReport Empty()
        {
            var report = StiReport.CreateNewReport();
            report.Pages.Clear();
            report.Pages.Add(new StiPage(report) { Name = "Page1" });
            return report;
        }

        /// <summary>
        /// Un rapport avec une bande de donnees et son entete, dotees de colonnes nommees selon la
        /// convention prefixe + cle.
        /// </summary>
        public static StiReport WithTable(params (string Key, double Width)[] columns)
        {
            var report = Empty();
            var page = (StiPage)report.Pages[0];

            var header = new StiHeaderBand { Name = "EnteteLignes", Height = 0.5 };
            var data = new StiDataBand { Name = "Lignes", Height = 0.5 };

            var left = 0.0;
            foreach (var (key, width) in columns)
            {
                header.Components.Add(Text("HCol_" + key, left, width));
                data.Components.Add(Text("DCol_" + key, left, width));
                left += width;
            }

            // Fond de ligne : opaque dans les modeles, pose sur toute la largeur.
            data.Components.Add(Text("RowBg", 0, left));

            page.Components.Add(header);
            page.Components.Add(data);
            return report;
        }

        public static StiText Text(string name, double left = 0, double width = 1, string value = "")
            => new StiText(new RectangleD(left, 0, width, 0.5))
            {
                Name = name,
                Text = { Value = value }
            };

        /// <summary>Le composant nomme, ou null — l'assertion du test dit ce qu'on en attend.</summary>
        public static StiComponent? Find(StiReport report, string name)
            => report.GetComponentByName(name);

        public static StiText FindText(StiReport report, string name)
            => (StiText)report.GetComponentByName(name);
    }
}
