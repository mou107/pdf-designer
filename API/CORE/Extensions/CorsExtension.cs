namespace API.CORE.Extensions
{
    public static class CorsExtension
    {
        public static WebApplication AddCors(this WebApplication app)
        {
            var origins = app.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();

            app.UseCors(builder =>
            {
                if (origins.Length > 0)
                    builder.SetIsOriginAllowed(origin =>
                        origins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                        || (app.Environment.IsDevelopment() && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback));
                else
                    builder.SetIsOriginAllowed(_ => true);

                builder.AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            return app;
        }
    }
}
