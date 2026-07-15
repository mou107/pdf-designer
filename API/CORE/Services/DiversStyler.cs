using System.Text.Json.Nodes;

namespace API.CORE.Services
{
    /// <summary>
    /// Pre-traite les donnees selon les options « Divers » de la config (Section client + infos generales) :
    /// construit des champs pretes a afficher (HTML) que les modeles bindent directement — plutot que d'avoir
    /// des libelles/champs en dur dans le .mrt. Permet de masquer civilite / SIRET / TVA / pays / adresse
    /// d'intervention / affaire suivie selon les cases cochees, sans restructurer chaque entete.
    ///   - client.blocClient : identite client (civilite, nom, TVA/SIRET, adresse [+ pays])
    ///   - client.affaire    : « Affaire suivie par : … » ([email/tel]) ou vide
    ///   - client.adresseInter : « Adresse d'intervention : … » ou vide
    /// </summary>
    public static class DiversStyler
    {
        public static string Apply(string dataJson, string? configJson)
        {
            var d = ParseDivers(configJson);
            try
            {
                var root = JsonNode.Parse(dataJson)!.AsObject();
                var doc = root["document"]?.AsObject() ?? root;
                var client = doc["client"]?.AsObject();
                if (client == null) return dataJson;

                var fa = client["adresseFacturation"]?.AsObject();
                var ch = client["adresseChantier"]?.AsObject();

                var civilite = client["civilite"]?.ToString() ?? "";
                var nom = client["nom"]?.ToString() ?? "";
                var siret = client["siret"]?.ToString() ?? "";
                var email = client["email"]?.ToString() ?? "";
                var tel = client["telephone"]?.ToString() ?? "";

                // ── Bloc client (texte simple, une info par ligne) ──
                var lines = new List<string>();
                var titre = (d.Civilite && !string.IsNullOrWhiteSpace(civilite) ? civilite + " " : "") + nom;
                lines.Add(titre.Trim());
                if (d.Tva && !string.IsNullOrWhiteSpace(siret)) lines.Add("TVA Intra : " + siret);
                if (d.Siret && !string.IsNullOrWhiteSpace(siret)) lines.Add("SIRET : " + siret);
                lines.Add(AddrLine(fa, d.Pays));
                client["blocClient"] = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));

                // ── Affaire suivie par ──
                if (d.Responsable && (!string.IsNullOrWhiteSpace(nom) || !string.IsNullOrWhiteSpace(email)))
                {
                    var contact = d.ResponsableEmailTel
                        ? string.Join(" - ", new[] { nom, email, tel }.Where(v => !string.IsNullOrWhiteSpace(v)))
                        : nom;
                    client["affaire"] = "Affaire suivie par : " + contact;
                }
                else client["affaire"] = "";

                // ── Adresse d'intervention ──
                client["adresseInter"] = d.AdresseIntervention
                    ? "Adresse d'intervention : " + AddrLine(ch, true)
                    : "";

                return root.ToJsonString();
            }
            catch { return dataJson; }
        }

        private static string AddrLine(JsonObject? a, bool withPays)
        {
            if (a == null) return "";
            var cp = a["codePostal"]?.ToString() ?? "";
            var ville = a["ville"]?.ToString() ?? "";
            var rue = a["rue"]?.ToString() ?? "";
            var pays = withPays ? (a["pays"]?.ToString() ?? "") : "";
            var parts = new[] { rue, string.Join(" ", new[] { cp, ville }.Where(v => !string.IsNullOrWhiteSpace(v))), pays };
            return string.Join(" ", parts.Where(v => !string.IsNullOrWhiteSpace(v))).Trim();
        }

        private struct DiversOpts
        {
            public bool Civilite, Pays, Contact, Siret, Tva, AdresseIntervention, Responsable, ResponsableEmailTel;
        }

        private static DiversOpts ParseDivers(string? configJson)
        {
            // Defauts « raisonnables » si config absente (civilite + tva + adresse inter + responsable).
            var o = new DiversOpts { Civilite = true, Tva = true, AdresseIntervention = true, Responsable = true };
            if (string.IsNullOrWhiteSpace(configJson)) return o;
            try
            {
                var div = JsonNode.Parse(configJson)?["divers"]?.AsObject();
                if (div == null) return o;
                bool B(string k, bool def) => div[k] is JsonNode n && bool.TryParse(n.ToString(), out var v) ? v : def;
                o.Civilite = B("civilite", o.Civilite);
                o.Pays = B("pays", o.Pays);
                o.Contact = B("contact", o.Contact);
                o.Siret = B("siret", o.Siret);
                o.Tva = B("tva", o.Tva);
                o.AdresseIntervention = B("adresseIntervention", o.AdresseIntervention);
                o.Responsable = B("responsable", o.Responsable);
                o.ResponsableEmailTel = B("responsableEmailTel", o.ResponsableEmailTel);
            }
            catch { }
            return o;
        }
    }
}
