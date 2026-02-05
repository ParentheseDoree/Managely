using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Managely.App.Services;

namespace Managely.App
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // Configuration de l'authentification Google OAuth 2.0
            builder.Services.AddOidcAuthentication(options =>
            {
                var googleConfig = builder.Configuration.GetSection("Google");
                options.ProviderOptions.Authority = googleConfig["Authority"];
                options.ProviderOptions.ClientId = googleConfig["ClientId"];
                options.ProviderOptions.RedirectUri = googleConfig["RedirectUri"];
                options.ProviderOptions.PostLogoutRedirectUri = googleConfig["PostLogoutRedirectUri"];
                options.ProviderOptions.ResponseType = googleConfig["ResponseType"];
                
                // Scopes pour Google
                options.ProviderOptions.DefaultScopes.Clear();
                var scopes = googleConfig.GetSection("DefaultScopes").Get<string[]>();
                if (scopes != null)
                {
                    foreach (var scope in scopes)
                    {
                        options.ProviderOptions.DefaultScopes.Add(scope);
                    }
                }
            });

            // Service de vérification d'accès au Spreadsheet
            builder.Services.AddScoped<SpreadsheetAccessService>();

            await builder.Build().RunAsync();
        }
    }
}
