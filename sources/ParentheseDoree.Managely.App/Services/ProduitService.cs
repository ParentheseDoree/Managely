using ParentheseDoree.Managely.App.Models;
using System.Globalization;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion de l'inventaire produits.
/// </summary>
public sealed class ProduitService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "Produits";
    private const string Range = "A:I";
    private const string CacheKey = "produits_all";

    private static readonly string[] Headers =
        ["guid", "nom", "marque", "prix_vente", "prix_achat", "stock", "seuil_alerte", "categorie", "actif"];

    public ProduitService(GoogleSheetsService sheets, CacheService cache, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<Produit>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<Produit>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);
        var list = rows.Select((r, i) => MapFromRow(r, i + 2)).ToList();
        _cache.Set(CacheKey, list);
        return list;
    }

    public async Task<List<Produit>> GetActifsAsync()
    {
        var all = await GetAllAsync();
        return all.Where(p => p.Actif).ToList();
    }

    public async Task<List<Produit>> GetAlertesStockAsync()
    {
        var all = await GetAllAsync();
        return all.Where(p => p.Actif && p.EstEnAlerte).ToList();
    }

    public async Task<string> AddAsync(Produit p)
    {
        p.GenererGuid();
        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, MapToRow(p));
        if (!success) throw new InvalidOperationException("Échec de l'ajout du produit");
        _cache.Invalidate(CacheKey);
        return p.Guid;
    }

    public async Task UpdateAsync(Produit p)
    {
        var success = await _sheets.UpdateRowAsync(SheetName, p.RowIndex, Range, MapToRow(p));
        if (!success) throw new InvalidOperationException("Échec de la mise à jour du produit");
        _cache.Invalidate(CacheKey);
    }

    public async Task DeleteAsync(int rowIndex)
    {
        var success = await _sheets.DeleteRowAsync(SheetName, rowIndex);
        if (!success) throw new InvalidOperationException("Échec de la suppression");
        _cache.Invalidate(CacheKey);
    }

    /// <summary>
    /// Décrémente le stock d'un produit par son GUID.
    /// </summary>
    public async Task DecrementerStockAsync(string produitGuid, int quantite)
    {
        var all = await GetAllAsync(true);
        var produit = all.FirstOrDefault(p => p.Guid == produitGuid);
        if (produit == null) return;

        produit.Stock = Math.Max(0, produit.Stock - quantite);
        await UpdateAsync(produit);
    }

    private static Produit MapFromRow(string[] row, int rowIndex)
    {
        return new Produit
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            Nom = row.ElementAtOrDefault(1) ?? "",
            Marque = row.ElementAtOrDefault(2) ?? "",
            PrixVente = decimal.TryParse(row.ElementAtOrDefault(3), NumberStyles.Any, CultureInfo.InvariantCulture, out var pv) ? pv : 0,
            PrixAchat = decimal.TryParse(row.ElementAtOrDefault(4), NumberStyles.Any, CultureInfo.InvariantCulture, out var pa) ? pa : 0,
            Stock = int.TryParse(row.ElementAtOrDefault(5), out var s) ? s : 0,
            SeuilAlerte = int.TryParse(row.ElementAtOrDefault(6), out var sa) ? sa : 5,
            Categorie = row.ElementAtOrDefault(7) ?? "",
            Actif = (row.ElementAtOrDefault(8) ?? "true") != "false"
        };
    }

    private static string[] MapToRow(Produit p)
    {
        return [p.Guid, p.Nom, p.Marque,
                p.PrixVente.ToString(CultureInfo.InvariantCulture),
                p.PrixAchat.ToString(CultureInfo.InvariantCulture),
                p.Stock.ToString(), p.SeuilAlerte.ToString(),
                p.Categorie, p.Actif ? "true" : "false"];
    }
}
