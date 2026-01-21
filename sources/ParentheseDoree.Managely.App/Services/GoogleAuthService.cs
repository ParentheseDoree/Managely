using Microsoft.JSInterop;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Niveau de permission sur le fichier Google Spreadsheet.
/// </summary>
public enum PermissionLevel
{
    /// <summary>Aucun accès au fichier.</summary>
    None,
    /// <summary>Accès en lecture seule.</summary>
    Read,
    /// <summary>Accès en lecture et écriture.</summary>
    Write
}

/// <summary>
/// Service d'authentification Google pour Blazor WebAssembly.
/// Gère l'interaction avec l'API JavaScript googleAuthApi et vérifie les permissions sur le spreadsheet.
/// </summary>
public sealed class GoogleAuthService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly BrowserLoggerService _logger;
    private readonly string _clientId;
    private readonly string[] _scopes;
    private readonly string _spreadsheetId;
    
    private bool _isInitialized;
    private DotNetObjectReference<GoogleAuthService>? _dotNetRef;
    
    /// <summary>
    /// Événement déclenché lors d'un changement d'état d'authentification.
    /// </summary>
    public event Action<bool, GoogleUser?>? OnAuthStateChanged;

    /// <summary>
    /// Utilisateur actuellement connecté (null si non connecté).
    /// </summary>
    public GoogleUser? CurrentUser { get; private set; }

    /// <summary>
    /// Niveau de permission actuel sur le spreadsheet.
    /// </summary>
    public PermissionLevel Permission { get; private set; } = PermissionLevel.None;

    /// <summary>
    /// Indique si un utilisateur est actuellement connecté.
    /// </summary>
    public bool IsSignedIn => CurrentUser != null;

    /// <summary>
    /// Indique si l'utilisateur peut lire les données.
    /// </summary>
    public bool CanRead => Permission == PermissionLevel.Read || Permission == PermissionLevel.Write;

    /// <summary>
    /// Indique si l'utilisateur peut modifier les données.
    /// </summary>
    public bool CanWrite => Permission == PermissionLevel.Write;

    /// <summary>
    /// Crée une nouvelle instance du service d'authentification Google.
    /// </summary>
    public GoogleAuthService(
        IJSRuntime jsRuntime,
        BrowserLoggerService logger,
        string clientId,
        string spreadsheetId,
        string[]? scopes = null)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _clientId = clientId;
        _spreadsheetId = spreadsheetId;
        _scopes = scopes ?? [];
    }

    /// <summary>
    /// Initialise le service d'authentification Google.
    /// Doit être appelé avant toute autre opération.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;

        try
        {
            var apiAvailable = await WaitForApiAsync();
            if (!apiAvailable)
            {
                await _logger.ErrorAsync(LogCategory.AUTH, "API Google Auth non disponible");
                return false;
            }

            var success = await _jsRuntime.InvokeAsync<bool>(
                "googleAuthApi.initialize",
                _clientId,
                _scopes,
                _spreadsheetId);

            if (!success)
            {
                await _logger.ErrorAsync(LogCategory.AUTH, "Échec d'initialisation de l'API Google");
                return false;
            }

            _dotNetRef = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync(
                "googleAuthApi.registerStateCallback",
                _dotNetRef,
                nameof(OnAuthStateChangedCallback));

            await RefreshUserStateAsync();

            _isInitialized = true;
            await _logger.SuccessAsync(LogCategory.AUTH, "Service Google Auth initialisé");
            
            return true;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.AUTH, "Erreur d'initialisation", new { error = ex.Message });
            return false;
        }
    }

    /// <summary>
    /// Attendre que l'API JavaScript soit disponible.
    /// </summary>
    private async Task<bool> WaitForApiAsync()
    {
        const int maxAttempts = 50;
        const int delayMs = 100;

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var available = await _jsRuntime.InvokeAsync<bool>(
                    "eval",
                    "typeof window.googleAuthApi !== 'undefined'");

                if (available) return true;
            }
            catch
            {
                // Ignorer les erreurs et réessayer
            }

            await Task.Delay(delayMs);
        }

        return false;
    }

    /// <summary>
    /// Déclenche le processus de connexion Google.
    /// Vérifie également les permissions sur le spreadsheet.
    /// </summary>
    public async Task<(GoogleUser? User, PermissionLevel Permission)> SignInAsync()
    {
        if (!_isInitialized)
        {
            var initialized = await InitializeAsync();
            if (!initialized) return (null, PermissionLevel.None);
        }

        try
        {
            await _logger.InfoAsync(LogCategory.AUTH, "Tentative de connexion...");
            
            var result = await _jsRuntime.InvokeAsync<SignInResultJs>("googleAuthApi.signIn");
            
            if (result is { Success: true, User: not null })
            {
                CurrentUser = new GoogleUser
                {
                    Id = result.User.Id ?? string.Empty,
                    Email = result.User.Email ?? string.Empty,
                    Name = result.User.Name ?? string.Empty,
                    GivenName = result.User.GivenName ?? string.Empty,
                    FamilyName = result.User.FamilyName ?? string.Empty,
                    Picture = result.User.Picture ?? string.Empty,
                    EmailVerified = result.User.EmailVerified
                };

                Permission = result.Permission?.ToLower() switch
                {
                    "write" => PermissionLevel.Write,
                    "read" => PermissionLevel.Read,
                    _ => PermissionLevel.None
                };

                await _logger.SuccessAsync(LogCategory.AUTH, "Connexion réussie", 
                    new { user = CurrentUser.Name, permission = Permission.ToString() });
                
                return (CurrentUser, Permission);
            }

            await _logger.WarnAsync(LogCategory.AUTH, "Connexion échouée", 
                new { error = result?.Error ?? "Erreur inconnue" });
            
            return (null, PermissionLevel.None);
        }
        catch (JSException ex)
        {
            await _logger.ErrorAsync(LogCategory.AUTH, "Erreur de connexion", new { error = ex.Message });
            return (null, PermissionLevel.None);
        }
    }

    /// <summary>
    /// Déconnecte l'utilisateur actuel.
    /// </summary>
    public async Task SignOutAsync()
    {
        if (!_isInitialized) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("googleAuthApi.signOut");
            CurrentUser = null;
            Permission = PermissionLevel.None;
            await _logger.InfoAsync(LogCategory.AUTH, "Déconnexion effectuée");
        }
        catch (JSException ex)
        {
            await _logger.ErrorAsync(LogCategory.AUTH, "Erreur de déconnexion", new { error = ex.Message });
        }
    }

    /// <summary>
    /// Récupère le token d'accès actuel.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (!_isInitialized || !IsSignedIn) return null;

        try
        {
            return await _jsRuntime.InvokeAsync<string?>("googleAuthApi.getAccessToken");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Rafraîchit l'état de l'utilisateur depuis l'API JS.
    /// </summary>
    private async Task RefreshUserStateAsync()
    {
        try
        {
            var isSignedIn = await _jsRuntime.InvokeAsync<bool>("googleAuthApi.isSignedIn");
            
            if (isSignedIn)
            {
                var userJs = await _jsRuntime.InvokeAsync<GoogleUserJs?>("googleAuthApi.getCurrentUser");
                if (userJs != null)
                {
                    CurrentUser = new GoogleUser
                    {
                        Id = userJs.Id ?? string.Empty,
                        Email = userJs.Email ?? string.Empty,
                        Name = userJs.Name ?? string.Empty,
                        GivenName = userJs.GivenName ?? string.Empty,
                        FamilyName = userJs.FamilyName ?? string.Empty,
                        Picture = userJs.Picture ?? string.Empty,
                        EmailVerified = userJs.EmailVerified
                    };
                }

                var permissionStr = await _jsRuntime.InvokeAsync<string?>("googleAuthApi.getPermission");
                Permission = permissionStr?.ToLower() switch
                {
                    "write" => PermissionLevel.Write,
                    "read" => PermissionLevel.Read,
                    _ => PermissionLevel.None
                };
            }
            else
            {
                CurrentUser = null;
                Permission = PermissionLevel.None;
            }
        }
        catch
        {
            CurrentUser = null;
            Permission = PermissionLevel.None;
        }
    }

    /// <summary>
    /// Callback appelé par JavaScript lors d'un changement d'état d'authentification.
    /// </summary>
    [JSInvokable]
    public void OnAuthStateChangedCallback(bool isSignedIn, GoogleUserJs? userJs, string? permission)
    {
        if (isSignedIn && userJs != null)
        {
            CurrentUser = new GoogleUser
            {
                Id = userJs.Id ?? string.Empty,
                Email = userJs.Email ?? string.Empty,
                Name = userJs.Name ?? string.Empty,
                GivenName = userJs.GivenName ?? string.Empty,
                FamilyName = userJs.FamilyName ?? string.Empty,
                Picture = userJs.Picture ?? string.Empty,
                EmailVerified = userJs.EmailVerified
            };

            Permission = permission?.ToLower() switch
            {
                "write" => PermissionLevel.Write,
                "read" => PermissionLevel.Read,
                _ => PermissionLevel.None
            };
        }
        else
        {
            CurrentUser = null;
            Permission = PermissionLevel.None;
        }

        OnAuthStateChanged?.Invoke(isSignedIn, CurrentUser);
    }

    /// <summary>
    /// Libère les ressources utilisées par le service.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("googleAuthApi.unregisterStateCallback");
            }
            catch
            {
                // Ignorer les erreurs lors du cleanup
            }
            
            _dotNetRef.Dispose();
            _dotNetRef = null;
        }
    }

    // Classes internes pour la désérialisation JSON
    private class SignInResultJs
    {
        public bool Success { get; set; }
        public GoogleUserJs? User { get; set; }
        public string? Permission { get; set; }
        public string? Error { get; set; }
    }

    public class GoogleUserJs
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? Picture { get; set; }
        public bool EmailVerified { get; set; }
    }
}
