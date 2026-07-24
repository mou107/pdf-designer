using System.Text.Json.Nodes;

namespace API.CORE.Services
{
    /// <summary>
    /// Resout les vignettes article : si lignes[].vignette est une URL (http...), telecharge l'image
    /// cote serveur (le microservice n'est pas soumis au CORS du serveur d'images statique) et la
    /// remplace par son base64, pour que la cellule StiImage liee a "lignes.vignette" l'affiche.
    /// Une valeur deja en base64 (ou vide) est laissee telle quelle.
    /// </summary>
    public static class VignetteResolver
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static async Task<string> ResolveAsync(string dataJson)
        {
            try
            {
                var root = JsonNode.Parse(dataJson)!.AsObject();
                var lignes = root["document"]?["lignes"]?.AsArray() ?? root["lignes"]?.AsArray();
                Console.WriteLine($"[VIGNETTE] lignes={(lignes?.Count ?? -1)}");
                if (lignes == null) return dataJson;

                var cache = new Dictionary<string, string>();
                foreach (var node in lignes)
                {
                    if (node is not JsonObject ligne) continue;
                    var vignette = ligne["vignette"]?.ToString();
                    Console.WriteLine($"[VIGNETTE] ligne vignette='{(vignette?.Length > 120 ? vignette.Substring(0, 120) : vignette)}'");
                    if (string.IsNullOrWhiteSpace(vignette)) continue;
                    // deja base64 : rien a faire
                    if (!vignette.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                    if (!cache.TryGetValue(vignette, out var b64))
                    {
                        b64 = await DownloadBase64Async(vignette);
                        cache[vignette] = b64;
                    }
                    Console.WriteLine($"[VIGNETTE] -> base64 len={b64.Length}");
                    // base64 si telechargee, sinon vide (colonne Photo laissee vide plutot que du texte)
                    ligne["vignette"] = b64;
                }

                return root.ToJsonString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VIGNETTE] EXCEPTION {ex.Message}");
                return dataJson;
            }
        }

        private static async Task<string> DownloadBase64Async(string url)
        {
            try
            {
                // encode les espaces du chemin (ex. "GERMAIN HENRI") sans toucher au reste de l'URL
                var safeUrl = url.Replace(" ", "%20");
                var bytes = await _http.GetByteArrayAsync(safeUrl);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VIGNETTE] download FAIL {url} -> {ex.Message}");
                return string.Empty;
            }
        }
    }
}
