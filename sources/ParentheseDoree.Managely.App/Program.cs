// =============================================================================
// Program.cs - Point d'entrée de l'application Blazor WebAssembly
// =============================================================================

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ParentheseDoree.Managely.App.Services;

namespace ParentheseDoree.Managely.App;

/// <summary>
/// Classe principale contenant le point d'entrée de l'application.
/// </summary>
public static class Program
{
    // =========================================================================
    // CONFIGURATION GOOGLE OAUTH
    // =========================================================================
    // 
    // Pour obtenir un Client ID Google :
    // 1. Allez sur https://console.cloud.google.com/
    // 2. Créez un nouveau projet ou sélectionnez-en un existant
    // 3. Activez l'API "Google Identity Services" et "Google Sheets API"
    // 4. Allez dans "Identifiants" > "Créer des identifiants" > "ID client OAuth"
    // 5. Type d'application : "Application Web"
    // 6. Ajoutez les origines JavaScript autorisées :
    //    - http://localhost:5000 (développement)
    //    - https://votre-domaine.com (production)
    // 7. Copiez le Client ID ci-dessous
    //
    // =========================================================================
    
    /// <summary>
    /// Google OAuth Client ID.
    /// TODO: Remplacez par votre propre Client ID Google.
    /// </summary>
    private const string GOOGLE_CLIENT_ID = "983794540567-tjp9segjt8on7oji9l4d7blqdpf0sad4.apps.googleusercontent.com";

    /// <summary>
    /// ID du Google Spreadsheet contenant les données.
    /// TODO: Remplacez par l'ID de votre spreadsheet.
    /// </summary>
    private const string SPREADSHEET_ID = "1cPCGXK-jObV1fKYMOQ2fkfzNSU644BtnrsCxiVhZG5g";

    /// <summary>
    /// Scopes OAuth requis pour l'application.
    /// </summary>
    private static readonly string[] GOOGLE_SCOPES =
    [
        "openid",
        "email",
        "profile",
        "https://www.googleapis.com/auth/spreadsheets"  // Accès Google Sheets (lecture/écriture)
    ];

    /// <summary>
    /// Point d'entrée de l'application Blazor WebAssembly.
    /// Configure les services et démarre l'application.
    /// </summary>
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Enregistre le composant racine App.razor dans l'élément HTML avec l'id "app"
        builder.RootComponents.Add<App>("#app");

        // Enregistre HeadOutlet pour la gestion dynamique du <head>
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // Configure le HttpClient
        builder.Services.AddScoped(sp =>
            new HttpClient
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            }
        );

        // Enregistre le service de logging navigateur
        builder.Services.AddScoped<BrowserLoggerService>();

        // Enregistre le service d'authentification Google
        builder.Services.AddScoped(sp =>
            new GoogleAuthService(
                sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
                sp.GetRequiredService<BrowserLoggerService>(),
                GOOGLE_CLIENT_ID,
                SPREADSHEET_ID,
                GOOGLE_SCOPES
            )
        );

        // Enregistre le service Google Sheets (CRUD)
        builder.Services.AddScoped<GoogleSheetsService>();

        // Démarre l'application
        await builder.Build().RunAsync();
    }
}
