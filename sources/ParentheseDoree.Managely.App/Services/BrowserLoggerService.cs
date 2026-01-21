using Microsoft.JSInterop;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Catégories de log disponibles - doit correspondre aux valeurs JS.
/// </summary>
public static class LogCategory
{
    public const string AUTH = "AUTH";
    public const string API = "API";
    public const string APP = "APP";
}

/// <summary>
/// Niveaux de log disponibles - doit correspondre aux valeurs JS.
/// </summary>
public enum LogLevel
{
    DEBUG = 0,
    INFO = 1,
    SUCCESS = 2,
    WARN = 3,
    ERROR = 4
}

/// <summary>
/// Service de logging qui utilise le logger JavaScript du navigateur.
/// </summary>
public sealed class BrowserLoggerService(IJSRuntime jsRuntime, ILogger<BrowserLoggerService> logger)
{
    private bool _isInitialized = false;

    private async Task<bool> EnsureInitializedAsync()
    { 
        if(_isInitialized) return true;

        try
        {
            _isInitialized = await jsRuntime.InvokeAsync<bool>(
                "eval",
                "typeof window !== 'undefined' && typeof window.browserLogger !== 'undefined'");

            return _isInitialized;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'initialisation du BrowserLoggerService");
            return false;
        }
    }

    private async Task LogAsync(
        string methodName,
        string category, 
        string message, 
        object? data)
    {
        if (!await EnsureInitializedAsync())
            return;

        try
        {
            if (data != null)
                await jsRuntime.InvokeVoidAsync(
                    $"window.browserLogger.{methodName}",
                    category,
                    message,
                    data);
            else
                await jsRuntime.InvokeVoidAsync(
                    $"window.browserLogger.{methodName}",
                    category,
                    message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du log JS [{Category}] {Message}", category, message);
        }
    }

    /// <summary>
    /// Définit le niveau minimum de log affiché dans la console.
    /// </summary>
    /// <param name="level">Niveau minimum (DEBUG=0, INFO=1, SUCCESS=2, WARN=3, ERROR=4)</param>
    public async Task SetMinLevelAsync(LogLevel level)
    {
        if (!await EnsureInitializedAsync())
            return;

        try
        {
            await jsRuntime.InvokeVoidAsync(
                "window.browserLogger.setMinLevel",
                (int)level);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du changement du niveau minimum de log à {Level}", level);
        }
    }

    /// <summary>
    /// Log de niveau DEBUG (0) - Informations de débogage détaillées.
    /// </summary>
    /// <param name="category">Catégorie du log (LogCategory.AUTH, LogCategory.API, ou LogCategory.APP)</param>
    /// <param name="message">Message à afficher</param>
    /// <param name="data">Données supplémentaires (optionnel)</param>
    public async Task DebugAsync(string category, string message, object? data = null)
        => await LogAsync("debug", category, message, data);

    /// <summary>
    /// Log de niveau INFO (1) - Informations générales.
    /// </summary>
    /// <param name="category">Catégorie du log (LogCategory.AUTH, LogCategory.API, ou LogCategory.APP)</param>
    /// <param name="message">Message à afficher</param>
    /// <param name="data">Données supplémentaires (optionnel)</param>
    public async Task InfoAsync(string category, string message, object? data = null)
        => await LogAsync("info", category, message, data);

    /// <summary>
    /// Log de niveau SUCCESS (2) - Opérations réussies.
    /// </summary>
    /// <param name="category">Catégorie du log (LogCategory.AUTH, LogCategory.API, ou LogCategory.APP)</param>
    /// <param name="message">Message à afficher</param>
    /// <param name="data">Données supplémentaires (optionnel)</param>
    public async Task SuccessAsync(string category, string message, object? data = null)
        => await LogAsync("success", category, message, data);

    /// <summary>
    /// Log de niveau WARN (3) - Avertissements.
    /// </summary>
    /// <param name="category">Catégorie du log (LogCategory.AUTH, LogCategory.API, ou LogCategory.APP)</param>
    /// <param name="message">Message à afficher</param>
    /// <param name="data">Données supplémentaires (optionnel)</param>
    public async Task WarnAsync(string category, string message, object? data = null)
        => await LogAsync("warn", category, message, data);

    /// <summary>
    /// Log de niveau ERROR (4) - Erreurs.
    /// </summary>
    /// <param name="category">Catégorie du log (LogCategory.AUTH, LogCategory.API, ou LogCategory.APP)</param>
    /// <param name="message">Message à afficher</param>
    /// <param name="data">Données supplémentaires (optionnel)</param>
    public async Task ErrorAsync(string category, string message, object? data = null)
        => await LogAsync("error", category, message, data);
}
