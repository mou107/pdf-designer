using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using API.CORE.Schemas;

namespace API.CORE.Services
{
    /// <summary>
    /// Lit et ecrit le contenu d'un modele (.mrt) dans le magasin de l'application appelante, pour le
    /// pont du designer web.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pourquoi le moteur fait cet appel lui-meme : le composant <c>stimulsoft-designer-angular</c>
    /// dialogue directement avec son backend, sans passer par le code de la page hote. Le serveur doit
    /// donc pouvoir charger et sauver le modele.
    /// </para>
    /// <para>
    /// L'URL du magasin arrive dans la requete, elle n'est pas configuree ici : le pont marche avec
    /// n'importe quel backend. La forme de la reponse est decrite par <c>TemplateStore:ResponsePath</c>
    /// (chemin pointe, ex. <c>value.mrt</c>) et le corps envoye en ecriture par
    /// <c>TemplateStore:RequestProperty</c>.
    /// </para>
    /// <para>
    /// Le magasin peut joindre au modele les <b>directives</b> a lui appliquer
    /// (<c>TemplateStore:DirectivesPath</c>) : c'est ce qui permet au designer d'afficher la mise en
    /// page telle qu'elle sera imprimee, sans que le moteur ait a savoir d'ou elle sort.
    /// </para>
    /// </remarks>
    public class TemplateStoreClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<TemplateStoreClient> _logger;
        private readonly string[] _allowedHosts;
        private readonly string _responsePath;
        private readonly string _directivesPath;
        private readonly string _requestProperty;

        private static readonly JsonSerializerOptions DirectiveOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public TemplateStoreClient(HttpClient http, IConfiguration configuration, ILogger<TemplateStoreClient> logger)
        {
            _http = http;
            _logger = logger;
            _allowedHosts = configuration.GetSection("TemplateStore:AllowedHosts").Get<string[]>() ?? Array.Empty<string>();
            _responsePath = configuration["TemplateStore:ResponsePath"] ?? "value.mrt";
            _directivesPath = configuration["TemplateStore:DirectivesPath"] ?? "value.directives";
            _requestProperty = configuration["TemplateStore:RequestProperty"] ?? "mrt";
        }

        /// <summary>
        /// L'URL est-elle autorisee ?
        /// </summary>
        /// <remarks>
        /// Une URL librement choisie par l'appelant ferait du moteur un relais vers tout ce que son
        /// reseau peut joindre. La liste blanche est donc obligatoire : sans elle, aucune URL n'est
        /// acceptee.
        /// </remarks>
        public bool IsAllowed(string? templateUrl)
        {
            if (string.IsNullOrWhiteSpace(templateUrl)) return false;
            if (!Uri.TryCreate(templateUrl, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

            if (_allowedHosts.Length == 0)
            {
                _logger.LogWarning("TemplateStore:AllowedHosts est vide : le pont designer refuse toute URL.");
                return false;
            }

            return _allowedHosts.Any(host => string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Recupere le modele et, si le magasin en fournit, les directives a lui appliquer.
        /// </summary>
        /// <returns>le contenu du modele (null s'il est introuvable) et ses directives (null s'il n'en donne pas)</returns>
        public async Task<(string? Mrt, RenderRequest? Directives)> GetTemplateAsync(string templateUrl, string? bearerToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, templateUrl);
            Authorize(request, bearerToken);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Chargement du modele refuse par {Url} : HTTP {Status}",
                    templateUrl, (int)response.StatusCode);
                return (null, null);
            }

            var body = await response.Content.ReadAsStringAsync();
            return (Extract(body, _responsePath), ExtractDirectives(body));
        }

        /// <summary>Enregistre le modele edite. false si le magasin a refuse.</summary>
        public async Task<bool> SaveMrtAsync(string templateUrl, string mrt, string? bearerToken)
        {
            var body = new Dictionary<string, string> { [_requestProperty] = mrt };

            using var request = new HttpRequestMessage(HttpMethod.Put, templateUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            Authorize(request, bearerToken);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Sauvegarde du modele refusee par {Url} : HTTP {Status}",
                    templateUrl, (int)response.StatusCode);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Extrait une valeur de la reponse en suivant un chemin pointe. Une reponse qui n'est pas du
        /// JSON est rendue telle quelle : un magasin peut tres bien servir le fichier brut.
        /// </summary>
        private static string? Extract(string body, string path)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            if (string.IsNullOrWhiteSpace(path)) return body;

            try
            {
                using var document = JsonDocument.Parse(body);
                var element = document.RootElement;

                foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out var child))
                        return null;
                    element = child;
                }

                return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            }
            catch (JsonException)
            {
                return body;
            }
        }

        /// <summary>
        /// Les directives jointes au modele, si le magasin en fournit. Un magasin qui n'en donne pas
        /// reste parfaitement valide : le designer affichera alors le modele brut.
        /// </summary>
        private RenderRequest? ExtractDirectives(string body)
        {
            var json = Extract(body, _directivesPath);
            if (string.IsNullOrWhiteSpace(json) || json == "null") return null;

            try
            {
                return JsonSerializer.Deserialize<RenderRequest>(json, DirectiveOptions);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Directives illisibles : le modele sera affiche tel quel.");
                return null;
            }
        }

        private static void Authorize(HttpRequestMessage request, string? bearerToken)
        {
            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}
