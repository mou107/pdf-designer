using System.Text.Json;
using System.Text.Json.Nodes;

namespace API.CORE.Services
{
    /// <summary>
    /// Pre-traite les donnees avant rendu : ajoute a chaque ligne un champ "designationHtml" ou la
    /// designation est enveloppee dans les balises HTML du style de son type (article/ouvrage/
    /// sousOuvrage/lot/ligne : couleur, gras, italique, souligne). Plus robuste qu'une expression
    /// Stimulsoft (qui cassait en mode interpretation). La cellule affiche {lignes.designationHtml}.
    /// </summary>
    public static class DataStyler
    {
        public static string AddDesignationHtml(string dataJson, string? configJson)
        {
            JsonObject? lineStyles = null;
            var numColumn = false; // colonne N° affichee separement -> ne pas prefixer le numero dans la designation
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                try
                {
                    var cfg = JsonNode.Parse(configJson)?.AsObject();
                    lineStyles = cfg?["lineStyles"]?.AsObject();
                    numColumn = IsTrue(cfg?["cols"]?["num"]);
                }
                catch { /* defauts */ }
            }

            try
            {
                var root = JsonNode.Parse(dataJson)!.AsObject();
                var lignes = root["document"]?["lignes"]?.AsArray() ?? root["lignes"]?.AsArray();
                if (lignes == null) return dataJson;

                foreach (var node in lignes)
                {
                    if (node is not JsonObject ligne) continue;
                    var type = ligne["type"]?.ToString() ?? "";
                    var numero = ligne["numero"]?.ToString() ?? "";
                    var designation = ligne["designation"]?.ToString() ?? "";
                    var text = (numColumn || string.IsNullOrEmpty(numero)) ? designation : $"{numero}  {designation}";
                    ligne["designationHtml"] = Wrap(text, lineStyles, type);
                }

                return root.ToJsonString();
            }
            catch { return dataJson; }
        }

        private static string Wrap(string text, JsonObject? lineStyles, string type)
        {
            var content = Escape(text);
            var style = lineStyles?[type]?.AsObject();
            if (style == null) return content;

            string pre = "", suf = "";
            var color = Hex(style["color"]?.ToString());
            if (color != null) { pre += $"<font color=\"{color}\">"; suf = "</font>"; }
            if (IsTrue(style["bold"])) { pre += "<b>"; suf = "</b>" + suf; }
            if (IsTrue(style["italic"])) { pre += "<i>"; suf = "</i>" + suf; }
            if (IsTrue(style["underline"])) { pre += "<u>"; suf = "</u>" + suf; }

            return pre.Length == 0 ? content : pre + content + suf;
        }

        private static bool IsTrue(JsonNode? n)
        {
            if (n == null) return false;
            try { return n.GetValue<bool>(); } catch { return string.Equals(n.ToString(), "true", StringComparison.OrdinalIgnoreCase); }
        }

        private static string? Hex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return hex.Length == 7 ? hex : null;
        }

        private static string Escape(string s)
            => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
