namespace API.CORE.Licensing
{
    /// <summary>
    /// Active la licence Stimulsoft depuis la configuration (variable d'environnement en prod).
    /// Sans cle : mode trial (filigrane sur les rendus) — suffisant pour le POC.
    /// </summary>
    public static class StimulsoftLicense
    {
        public static void Register(IConfiguration configuration)
        {
            var key = configuration["Stimulsoft:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(key))
                Stimulsoft.Base.StiLicense.Key = key;
        }
    }
}
