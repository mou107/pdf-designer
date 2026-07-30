using API.CORE.Extensions;
using API.CORE.Licensing;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logs Serilog (console ; sinks additionnels a configurer par environnement)
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

// Licence Stimulsoft (variable d'environnement en prod ; trial avec filigrane sans cle)
StimulsoftLicense.Register(builder.Configuration);

// Ni base de donnees ni stockage disque : le moteur est sans etat. Le modele (.mrt), les donnees et les
// directives arrivent dans la requete de rendu ; les modeles sont stockes par l'application appelante.

// Seule exception a l'absence d'etat : le designer web. Il dialogue avec le serveur action par action
// (deplacer un objet, ouvrir un dialogue, previsualiser) et retrouve entre deux appels le rapport en
// cours d'edition dans un cache serveur — sans lui, chaque modification s'interrompt sur « Cache not
// specified! ». C'est un etat de session d'edition, pas un stockage de modeles : la source de verite
// reste le magasin appelant, ou le .mrt n'est ecrit qu'a l'enregistrement.
// Ce cache est local au processus (StiServerCacheMode.ObjectCache) : derriere plusieurs instances, il
// faut des sessions collantes ou un cache distribue, sinon une action peut atterrir sur une instance qui
// ignore l'edition en cours.
builder.Services.AddMemoryCache();

// Auth JWT de l'application appelante (desactivable en dev via Auth:Enabled=false)
builder.AddJwtAuth();

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.AddSwagger();
builder.AddScopedServices();
builder.Services.AddCors();

var app = builder.Build();

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
app.MapControllers();
app.UseExceptionHandler();
app.EnableSwagger();

app.Run();
