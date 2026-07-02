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
                    builder.WithOrigins(origins);
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
