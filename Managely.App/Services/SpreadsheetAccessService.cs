using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Managely.App.Services;

/// <summary>
/// Service pour vérifier l'accès de l'utilisateur à un fichier Google Spreadsheet spécifique
/// </summary>
public class SpreadsheetAccessService
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IConfiguration _configuration;

    public SpreadsheetAccessService(
        HttpClient httpClient,
        IAccessTokenProvider tokenProvider,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _configuration = configuration;
    }

    /// <summary>
    /// Vérifie si l'utilisateur connecté a accès au fichier Google Spreadsheet configuré
    /// </summary>
    /// <returns>True si l'accès est autorisé, False sinon</returns>
    public async Task<SpreadsheetAccessResult> CheckAccessAsync()
    {
        var spreadsheetId = _configuration["SpreadsheetId"];
        
        if (string.IsNullOrEmpty(spreadsheetId))
        {
            return new SpreadsheetAccessResult 
            { 
                HasAccess = false, 
                ErrorMessage = "L'identifiant du Spreadsheet n'est pas configuré." 
            };
        }

        try
        {
            var tokenResult = await _tokenProvider.RequestAccessToken(new AccessTokenRequestOptions
            {
                Scopes = new[] { "https://www.googleapis.com/auth/spreadsheets.readonly" }
            });

            if (tokenResult.TryGetToken(out var token))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}?fields=properties.title");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return new SpreadsheetAccessResult 
                    { 
                        HasAccess = true 
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                         response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new SpreadsheetAccessResult 
                    { 
                        HasAccess = false, 
                        ErrorMessage = "Vous n'avez pas les droits d'accès au fichier de données requis. Contactez l'administrateur." 
                    };
                }
                else
                {
                    return new SpreadsheetAccessResult 
                    { 
                        HasAccess = false, 
                        ErrorMessage = $"Erreur lors de la vérification des droits : {response.StatusCode}" 
                    };
                }
            }
            else
            {
                return new SpreadsheetAccessResult 
                { 
                    HasAccess = false, 
                    ErrorMessage = "Impossible d'obtenir le jeton d'accès." 
                };
            }
        }
        catch (Exception ex)
        {
            return new SpreadsheetAccessResult 
            { 
                HasAccess = false, 
                ErrorMessage = $"Erreur de connexion : {ex.Message}" 
            };
        }
    }
}

/// <summary>
/// Résultat de la vérification d'accès au Spreadsheet
/// </summary>
public class SpreadsheetAccessResult
{
    public bool HasAccess { get; set; }
    public string? ErrorMessage { get; set; }
}
