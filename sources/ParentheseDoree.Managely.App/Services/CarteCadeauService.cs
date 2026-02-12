using System.Globalization;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion des cartes cadeaux.
/// </summary>
public sealed class CarteCadeauService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "CartesCadeaux";
    private const string Range = "A:I";
    private const string CacheKey = "cartes_all";

    private static readonly string[] Headers =
        ["guid", "client_guid", "type", "montant_initial", "solde_restant",
         "date_creation", "date_expiration", "statut", "origine"];

    public CarteCadeauService(GoogleSheetsService sheets, CacheService cache, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<CarteCadeau>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<CarteCadeau>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);
        var list = rows.Select((r, i) => MapFromRow(r, i + 2)).ToList();
        _cache.Set(CacheKey, list);
        return list;
    }

    public async Task<List<CarteCadeau>> GetByClientAsync(string clientGuid)
    {
        var all = await GetAllAsync();
        return all.Where(c => c.ClientGuid == clientGuid).ToList();
    }

    public async Task<List<CarteCadeau>> GetActivesAsync()
    {
        var all = await GetAllAsync();
        return all.Where(c => c.EstUtilisable).ToList();
    }

    public async Task<string> AddAsync(CarteCadeau carte)
    {
        carte.GenererGuid();
        if (string.IsNullOrEmpty(carte.DateCreation))
            carte.DateCreation = DateTime.Now.ToString("dd/MM/yyyy");
        carte.Statut = carte.StatutCalcule;

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, MapToRow(carte));
        if (!success) throw new InvalidOperationException("Échec de l'ajout de la carte cadeau");
        _cache.Invalidate(CacheKey);
        return carte.Guid;
    }

    public async Task UpdateAsync(CarteCadeau carte)
    {
        carte.Statut = carte.StatutCalcule;
        var success = await _sheets.UpdateRowAsync(SheetName, carte.RowIndex, Range, MapToRow(carte));
        if (!success) throw new InvalidOperationException("Échec de la mise à jour");
        _cache.Invalidate(CacheKey);
    }

    public async Task DeleteAsync(int rowIndex)
    {
        var success = await _sheets.DeleteRowAsync(SheetName, rowIndex);
        if (!success) throw new InvalidOperationException("Échec de la suppression");
        _cache.Invalidate(CacheKey);
    }

    /// <summary>
    /// Utilise un montant sur une carte cadeau. Retourne le montant réellement déduit.
    /// </summary>
    public async Task<decimal> UtiliserAsync(string carteGuid, decimal montant)
    {
        var all = await GetAllAsync(true);
        var carte = all.FirstOrDefault(c => c.Guid == carteGuid);
        if (carte == null || !carte.EstUtilisable) return 0;

        var montantDeduit = Math.Min(montant, carte.SoldeRestant);
        carte.SoldeRestant -= montantDeduit;
        carte.Statut = carte.StatutCalcule;
        await UpdateAsync(carte);
        return montantDeduit;
    }

    private static CarteCadeau MapFromRow(string[] row, int rowIndex)
    {
        return new CarteCadeau
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            ClientGuid = row.ElementAtOrDefault(1) ?? "",
            Type = row.ElementAtOrDefault(2) ?? "achat",
            MontantInitial = decimal.TryParse(row.ElementAtOrDefault(3), NumberStyles.Any, CultureInfo.InvariantCulture, out var mi) ? mi : 0,
            SoldeRestant = decimal.TryParse(row.ElementAtOrDefault(4), NumberStyles.Any, CultureInfo.InvariantCulture, out var sr) ? sr : 0,
            DateCreation = row.ElementAtOrDefault(5) ?? "",
            DateExpiration = row.ElementAtOrDefault(6) ?? "",
            Statut = row.ElementAtOrDefault(7) ?? "active",
            Origine = row.ElementAtOrDefault(8) ?? ""
        };
    }

    private static string[] MapToRow(CarteCadeau c)
    {
        return [c.Guid, c.ClientGuid, c.Type,
                c.MontantInitial.ToString(CultureInfo.InvariantCulture),
                c.SoldeRestant.ToString(CultureInfo.InvariantCulture),
                c.DateCreation, c.DateExpiration, c.Statut, c.Origine];
    }
}
