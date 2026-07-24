using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace API.CORE.Services
{
    /// <summary>
    /// Pre-traite les donnees selon les options « Divers &gt; Textes » de la config : titre et sous-titre
    /// du document. Resout les tags « #reference / #type / #compteur » (parite avec le legacy pdfmake
    /// <c>PdfHeaderHelper.parseInvoiceTags</c>) contre les champs du document, puis injecte le resultat
    /// fusionne dans <c>options.titreDocument</c> / <c>options.sousTitreDocument</c> (et miroir
    /// <c>document.titre</c> / <c>document.sousTitre</c>) que les modeles bindent.
    ///
    /// Priorite de la source : config (ce que l'utilisateur saisit dans l'ecran) &gt; valeur deja presente
    /// dans le payload &gt; libelle du document. Ainsi le titre configure est pris en compte au rendu ET
    /// dans l'apercu, et les « #tags » ne s'affichent plus en clair.
    /// </summary>
    public static class TextsStyler
    {
        public static string Apply(string dataJson, string? configJson)
        {
            var (cfgTitre, cfgSousTitre) = ParseTextes(configJson);
            try
            {
                var root = JsonNode.Parse(dataJson)!.AsObject();
                var doc = root["document"]?.AsObject();

                var options = root["options"]?.AsObject();
                if (options == null) { options = new JsonObject(); root["options"] = options; }

                // Titre : config prioritaire, sinon valeur du payload, sinon libelle du document.
                var titreSrc = FirstNonEmpty(cfgTitre, Str(options["titreDocument"]), Str(doc?["titre"]));
                var sousSrc = FirstNonEmpty(cfgSousTitre, Str(options["sousTitreDocument"]), Str(doc?["sousTitre"]));

                var titre = ResolveTags(titreSrc, doc).Trim();
                var sousTitre = ResolveTags(sousSrc, doc).Trim();

                options["titreDocument"] = titre;
                options["sousTitreDocument"] = sousTitre;
                if (doc != null) { doc["titre"] = titre; doc["sousTitre"] = sousTitre; }

                return root.ToJsonString();
            }
            catch { return dataJson; }
        }

        /// <summary>Remplace les tags « #reference / #compteur / #type » par les valeurs du document.</summary>
        private static string ResolveTags(string text, JsonObject? doc)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";

            var reference = Str(doc?["reference"]);
            var compteur = FirstNonEmpty(Str(doc?["compteur"]), Str(doc?["numero"]));
            var type = FirstNonEmpty(Str(doc?["type"]), Str(doc?["titre"]));

            var r = Regex.Replace(text, "#reference", reference, RegexOptions.IgnoreCase);
            r = Regex.Replace(r, "#compteur", compteur, RegexOptions.IgnoreCase);
            r = Regex.Replace(r, "#type", type, RegexOptions.IgnoreCase);
            return r;
        }

        private static (string titre, string sousTitre) ParseTextes(string? configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson)) return ("", "");
            try
            {
                var div = JsonNode.Parse(configJson)?["divers"]?.AsObject();
                if (div == null) return ("", "");
                return (Str(div["titreDocument"]), Str(div["sousTitreDocument"]));
            }
            catch { return ("", ""); }
        }

        private static string Str(JsonNode? n) => n?.ToString() ?? "";

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
    }
}
