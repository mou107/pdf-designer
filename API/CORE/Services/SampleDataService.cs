namespace API.CORE.Services
{
    /// <summary>
    /// Donnees exemples et JSON Schemas par type de document (SEED/, copies dans le repertoire de sortie).
    /// Source de verite du dictionnaire de donnees des templates .mrt.
    /// </summary>
    public class SampleDataService
    {
        private readonly string _seedRoot;

        public SampleDataService(IWebHostEnvironment env)
        {
            _seedRoot = Path.Combine(env.ContentRootPath, "SEED");
        }

        public async Task<string> GetSampleDataAsync(string docType)
        {
            var path = Path.Combine(_seedRoot, "sample-data", $"{docType}.json");
            if (!File.Exists(path))
                path = Path.Combine(_seedRoot, "sample-data", "Quote.json");
            return await File.ReadAllTextAsync(path);
        }

        public async Task<string?> GetSchemaAsync(string docType)
        {
            var path = Path.Combine(_seedRoot, "schemas", $"{docType}.schema.json");
            return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        }
    }
}
