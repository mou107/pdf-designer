using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace API.CORE.Extensions
{
    internal static class AuthExtension
    {
        /// <summary>
        /// Valide le JWT de l'application appelante (issuer, audience et cle HMAC partagee, tous
        /// configures). Le moteur n'emet aucun jeton : il fait confiance a celui qu'il recoit.
        /// Le designer Stimulsoft ne pose pas de header Authorization : le jeton est aussi accepte en
        /// query string (?access_token=) via OnMessageReceived.
        /// Desactivable en dev (Auth:Enabled=false) pour tester sans authentification.
        /// </summary>
        internal static WebApplicationBuilder AddJwtAuth(this WebApplicationBuilder builder)
        {
            var enabled = builder.Configuration.GetValue<bool>("Auth:Enabled");
            if (!enabled)
            {
                builder.Services.AddAuthorization();
                return builder;
            }

            var secretKey = builder.Configuration["Auth:SecretKey"]
                ?? throw new InvalidOperationException("Auth:SecretKey manquant (secret de configuration, jamais commite).");

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Auth:Issuer"],
                        ValidAudience = builder.Configuration["Auth:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(accessToken))
                                context.Token = accessToken;
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            return builder;
        }
    }
}
