// =============================================================================
// Program.cs - Point d'entrée de l'application Blazor WebAssembly
// =============================================================================

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace ParentheseDoree.Managely.App;

/// <summary>
/// Classe principale contenant le point d'entrée de l'application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Point d'entrée de l'application Blazor WebAssembly.
    /// Configure les services et démarre l'application.
    /// </summary>
    /// <param name="args">Arguments de ligne de commande.</param>
    public static async Task Main(string[] args)
    {
        // Crée un builder pour configurer l'application WebAssembly
        // avec les paramètres par défaut (logging, configuration, etc.)
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Enregistre le composant racine App.razor dans l'élément HTML avec l'id "app"
        // C'est ici que toute l'interface Blazor sera rendue
        builder.RootComponents.Add<App>("#app");

        // Enregistre HeadOutlet pour permettre aux composants de modifier
        // dynamiquement le contenu de la balise <head> (titre, meta, etc.)
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // Configure le HttpClient pour les appels API
        // AddScoped = une instance par "scope" (ici, par onglet/fenêtre du navigateur)
        // BaseAddress = URL de base pour les requêtes HTTP (l'URL de l'application)
        builder.Services.AddScoped(sp =>
            new HttpClient
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            }
        );

        // Construit et démarre l'application de manière asynchrone
        // L'application reste active tant que l'onglet du navigateur est ouvert
        await builder.Build().RunAsync();
    }
}
