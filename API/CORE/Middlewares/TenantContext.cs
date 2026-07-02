namespace API.CORE.Middlewares
{
    /// <summary>
    /// Contexte multi-tenant de la requete courante (societeId / userId issus du JWT Axiobat).
    /// </summary>
    public class TenantContext
    {
        public string SocieteId { get; set; } = string.Empty;
        public string? UserId { get; set; }
    }

    /// <summary>
    /// Resout societeId/userId : claims JWT -> header Ocelot X-Societe-Id -> query string -> valeur dev.
    /// </summary>
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public TenantMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context, TenantContext tenant)
        {
            var societeId = context.User.FindFirst("societeId")?.Value
                ?? context.Request.Headers["X-Societe-Id"].FirstOrDefault()
                ?? context.Request.Query["societeId"].FirstOrDefault()
                ?? _configuration["Auth:DevSocieteId"];

            var userId = context.User.FindFirst("userId")?.Value
                ?? context.Request.Headers["X-User-Id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(societeId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "societeId introuvable (claim JWT, header X-Societe-Id ou query)." });
                return;
            }

            tenant.SocieteId = societeId;
            tenant.UserId = userId;

            await _next(context);
        }
    }
}
