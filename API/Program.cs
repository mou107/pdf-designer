using API.CORE.Extensions;
using API.CORE.Licensing;
using API.CORE.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logs Serilog (console ; sinks additionnels a configurer par environnement)
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

// Licence Stimulsoft (variable d'environnement en prod ; trial avec filigrane sans cle)
StimulsoftLicense.Register(builder.Configuration);

// Base de donnees (MariaDB, ou InMemory en dev pour le sandbox)
builder.AddDbContext();
builder.Migrate();

// Auth JWT Axiobat (desactivable en dev via Auth:Enabled=false)
builder.AddJwtAuth();

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.AddSwagger();
builder.AddScopedServices();
builder.Services.AddCors();

var app = builder.Build();

// Seeds : un modele standard par type de document
using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seedService.EnsureSeedsAsync();
}

// Prechauffage du moteur Stimulsoft : le 1er rendu est ~15x plus lent (fontes/warmup) ;
// on le fait au demarrage pour que le 1er apercu utilisateur soit rapide.
try
{
    var warm = Stimulsoft.Report.StiReport.CreateNewReport();
    warm.Render(false);
    using var warmStream = new MemoryStream();
    warm.ExportDocument(Stimulsoft.Report.StiExportFormat.Pdf, warmStream);
    Log.Information("Moteur Stimulsoft prechauffe ({Size} octets).", warmStream.Length);
}
catch (Exception ex)
{
    Log.Warning(ex, "Echec du prechauffage du moteur Stimulsoft (non bloquant).");
}

app.MapHealthChecks("/health");
app.AddCors();

if (app.Configuration.GetValue<bool>("Auth:Enabled"))
{
    app.UseAuthentication();
}
app.UseAuthorization();
app.UseMiddlewares();
app.MapControllers();
app.UseExceptionHandler();
app.EnableSwagger();

app.Run();
