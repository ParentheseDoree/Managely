using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ParentheseDoree.Managely.App.Services;

namespace ParentheseDoree.Managely.App;

public static class Program
{
    private const string GOOGLE_CLIENT_ID = "983794540567-tjp9segjt8on7oji9l4d7blqdpf0sad4.apps.googleusercontent.com";
    private const string SPREADSHEET_ID = "1cPCGXK-jObV1fKYMOQ2fkfzNSU644BtnrsCxiVhZG5g";

    private static readonly string[] GOOGLE_SCOPES =
    [
        "openid",
        "email",
        "profile",
        "https://www.googleapis.com/auth/spreadsheets"
    ];

    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // HttpClient
        builder.Services.AddScoped(sp =>
            new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        // Logger
        builder.Services.AddScoped<BrowserLoggerService>();

        // Cache
        builder.Services.AddSingleton<CacheService>();

        // Auth Google
        builder.Services.AddScoped(sp =>
            new GoogleAuthService(
                sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
                sp.GetRequiredService<BrowserLoggerService>(),
                GOOGLE_CLIENT_ID,
                SPREADSHEET_ID,
                GOOGLE_SCOPES));

        // Service Sheets générique
        builder.Services.AddScoped<GoogleSheetsService>();

        // Services métier
        builder.Services.AddScoped<ClientService>();
        builder.Services.AddScoped<PrestationService>();
        builder.Services.AddScoped<ProduitService>();
        builder.Services.AddScoped<PassageService>();
        builder.Services.AddScoped<CarteCadeauService>();
        builder.Services.AddScoped<FideliteService>();
        builder.Services.AddScoped<MouvementStockService>();
        builder.Services.AddScoped<AjustementService>();
        builder.Services.AddScoped<FinanceService>();

        await builder.Build().RunAsync();
    }
}
