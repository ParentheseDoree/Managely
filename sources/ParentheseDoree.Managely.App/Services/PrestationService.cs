using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion du catalogue de prestations.
/// </summary>
public sealed class PrestationService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "Prestations";
    private const string Range = "A:F";
    private const string CacheKey = "prestations_all";

    private static readonly string[] Headers =
        ["guid", "nom", "categorie", "prix_defaut", "duree_minutes", "actif"];

    public PrestationService(GoogleSheetsService sheets, CacheService cache, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<Prestation>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<Prestation>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);

        var list = rows.Select((r, i) => MapFromRow(r, i + 2)).ToList();
        _cache.Set(CacheKey, list);
        return list;
    }

    public async Task<List<Prestation>> GetActivesAsync()
    {
        var all = await GetAllAsync();
        return all.Where(p => p.Actif).ToList();
    }

    public async Task<string> AddAsync(Prestation p)
    {
        p.GenererGuid();
        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, MapToRow(p));
        if (!success) throw new InvalidOperationException("Échec de l'ajout de la prestation");
        _cache.Invalidate(CacheKey);
        return p.Guid;
    }

    public async Task UpdateAsync(Prestation p)
    {
        var success = await _sheets.UpdateRowAsync(SheetName, p.RowIndex, Range, MapToRow(p));
        if (!success) throw new InvalidOperationException("Échec de la mise à jour");
        _cache.Invalidate(CacheKey);
    }

    public async Task DeleteAsync(int rowIndex)
    {
        var success = await _sheets.DeleteRowAsync(SheetName, rowIndex);
        if (!success) throw new InvalidOperationException("Échec de la suppression");
        _cache.Invalidate(CacheKey);
    }

    private static Prestation MapFromRow(string[] row, int rowIndex)
    {
        return new Prestation
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            Nom = row.ElementAtOrDefault(1) ?? "",
            Categorie = row.ElementAtOrDefault(2) ?? "",
            PrixDefaut = decimal.TryParse(row.ElementAtOrDefault(3), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0,
            DureeMinutes = int.TryParse(row.ElementAtOrDefault(4), out var d) ? d : 0,
            Actif = (row.ElementAtOrDefault(5) ?? "true") != "false"
        };
    }

    private static string[] MapToRow(Prestation p)
    {
        return [p.Guid, p.Nom, p.Categorie,
                p.PrixDefaut.ToString(System.Globalization.CultureInfo.InvariantCulture),
                p.DureeMinutes.ToString(), p.Actif ? "true" : "false"];
    }
}
