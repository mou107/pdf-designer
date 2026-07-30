using API.CORE.Services;

namespace API.CORE.Extensions
{
    internal static class DependencyExtension
    {
        internal static WebApplicationBuilder AddScopedServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<RenderService>();
            // Pont du designer uniquement : le rendu, lui, ne parle a personne.
            builder.Services.AddHttpClient<TemplateStoreClient>();
            return builder;
        }
    }
}
