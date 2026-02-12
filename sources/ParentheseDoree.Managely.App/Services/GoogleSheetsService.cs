using Microsoft.JSInterop;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service bas-niveau générique pour les opérations CRUD sur Google Sheets.
/// Tous les services métier utilisent ce service pour accéder aux données.
/// </summary>
public sealed class GoogleSheetsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly GoogleAuthService _authService;
    private readonly BrowserLoggerService _logger;

    public GoogleSheetsService(
        IJSRuntime jsRuntime,
        GoogleAuthService authService,
        BrowserLoggerService logger)
    {
        _jsRuntime = jsRuntime;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Lit toutes les lignes d'une feuille (hors en-tête).
    /// Retourne une liste de tableaux de chaînes.
    /// </summary>
    public async Task<List<string[]>> ReadSheetAsync(string sheetName, string range)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<SheetDataJs>("googleSheetsApi.readSheet", sheetName, range);
            if (result?.Rows == null) return [];
            return result.Rows.Select(r => r ?? []).ToList();
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, $"Erreur lecture {sheetName}", new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Lit plusieurs plages en un seul appel batch.
    /// </summary>
    public async Task<Dictionary<string, List<string[]>>> BatchReadAsync(string[] ranges)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<BatchReadResultJs>("googleSheetsApi.batchRead", (object)ranges);
            var dict = new Dictionary<string, List<string[]>>();
            if (result?.Results != null)
            {
                foreach (var kvp in result.Results)
                    dict[kvp.Key] = kvp.Value?.Select(r => r ?? []).ToList() ?? [];
            }
            return dict;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur batch read", new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Ajoute une ligne à une feuille. Retourne true si succès.
    /// </summary>
    public async Task<bool> AppendRowAsync(string sheetName, string range, string[] values)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Droits d'écriture requis");

        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>(
                "googleSheetsApi.appendRow", sheetName, range, values);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, $"Erreur ajout {sheetName}", new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Met à jour une ligne existante.
    /// </summary>
    public async Task<bool> UpdateRowAsync(string sheetName, int rowIndex, string range, string[] values)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Droits d'écriture requis");

        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>(
                "googleSheetsApi.updateRow", sheetName, rowIndex, range, values);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, $"Erreur MAJ {sheetName} ligne {rowIndex}", new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Supprime une ligne.
    /// </summary>
    public async Task<bool> DeleteRowAsync(string sheetName, int rowIndex)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Droits d'écriture requis");

        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>(
                "googleSheetsApi.deleteRow", sheetName, rowIndex);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, $"Erreur suppression {sheetName} ligne {rowIndex}", new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// S'assure qu'une feuille existe avec les en-têtes spécifiés.
    /// </summary>
    public async Task EnsureSheetAsync(string sheetName, string[] headers)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("googleSheetsApi.ensureSheet", sheetName, headers);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, $"Erreur ensureSheet {sheetName}", new { error = ex.Message });
        }
    }

    // Classes internes pour la désérialisation
    private class SheetDataJs
    {
        public string[][]? Rows { get; set; }
    }

    private class BatchReadResultJs
    {
        public Dictionary<string, string[][]>? Results { get; set; }
    }

    private class OperationResultJs
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
