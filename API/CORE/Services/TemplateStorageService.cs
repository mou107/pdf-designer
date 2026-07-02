namespace API.CORE.Services
{
    /// <summary>
    /// Lecture/ecriture des fichiers .mrt sur disque.
    /// Schema : {TemplatesRoot}/{societeId|_global}/templates/{docType}/{templateId}_v{version}.mrt
    /// (pattern imite du bloc Publishing du webapi Axiobat).
    /// </summary>
    public class TemplateStorageService
    {
        public const string GlobalFolder = "_global";
        private readonly string _root;

        public TemplateStorageService(IConfiguration configuration)
        {
            _root = configuration["Storage:TemplatesRoot"] ?? "./_data";
            Directory.CreateDirectory(_root);
        }

        public string Root => _root;

        public string BuildRelativePath(string? societeId, string docType, string templateId, int version)
            => Path.Combine(societeId ?? GlobalFolder, "templates", docType, $"{templateId}_v{version}.mrt");

        public string GetAbsolutePath(string relativePath) => Path.Combine(_root, relativePath);

        public bool Exists(string relativePath) => File.Exists(GetAbsolutePath(relativePath));

        public async Task<string> ReadAsync(string relativePath)
            => await File.ReadAllTextAsync(GetAbsolutePath(relativePath));

        public async Task WriteAsync(string relativePath, string content)
        {
            var absolute = GetAbsolutePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllTextAsync(absolute, content);
        }

        public void Copy(string fromRelative, string toRelative)
        {
            var to = GetAbsolutePath(toRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(GetAbsolutePath(fromRelative), to, overwrite: true);
        }
    }
}
