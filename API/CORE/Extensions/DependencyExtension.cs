using API.CORE.Middlewares;
using API.CORE.Services;

namespace API.CORE.Extensions
{
    internal static class DependencyExtension
    {
        internal static WebApplicationBuilder AddScopedServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<TenantContext>();
            builder.Services.AddScoped<TemplateService>();
            builder.Services.AddScoped<RenderService>();
            builder.Services.AddSingleton<TemplateStorageService>();
            builder.Services.AddSingleton<SampleDataService>();
            builder.Services.AddScoped<SeedService>();
            builder.Services.AddMemoryCache();
            return builder;
        }

        internal static WebApplication UseMiddlewares(this WebApplication app)
        {
            app.UseMiddleware<TenantMiddleware>();
            return app;
        }
    }
}
