using System.Globalization;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion des ajustements financiers manuels.
/// </summary>
public sealed class AjustementService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "Ajustements";
    private const string Range = "A:H";
    private const string CacheKey = "ajustements_all";

    private static readonly string[] Headers =
        ["guid", "date", "type", "montant", "description", "categorie", "categorie_comptable", "note"];

    public AjustementService(GoogleSheetsService sheets, CacheService cache, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<AjustementFinancier>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<AjustementFinancier>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);
        var list = rows.Select((r, i) => MapFromRow(r, i + 2)).ToList();
        _cache.Set(CacheKey, list);
        return list;
    }

    public async Task<string> AddAsync(AjustementFinancier ajustement)
    {
        ajustement.GenererGuid();
        if (string.IsNullOrEmpty(ajustement.Date))
            ajustement.Date = DateTime.Now.ToString("dd/MM/yyyy");

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, MapToRow(ajustement));
        if (!success) throw new InvalidOperationException("Échec de l'ajout de l'ajustement");
        _cache.Invalidate(CacheKey);
        return ajustement.Guid;
    }

    public async Task UpdateAsync(AjustementFinancier ajustement)
    {
        var success = await _sheets.UpdateRowAsync(SheetName, ajustement.RowIndex, Range, MapToRow(ajustement));
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
    /// Total des ajustements pour un mois/année donnés.
    /// <param name="excludeCategorie">Catégorie comptable à exclure (ex: "produit" pour éviter le double comptage avec MouvementsStock).</param>
    /// </summary>
    public async Task<(decimal Recettes, decimal Depenses)> GetTotauxAsync(
        int? mois = null, int? annee = null, string? excludeCategorie = null)
    {
        var all = await GetAllAsync();
        var filtered = all.AsEnumerable();

        // Exclure une catégorie comptable si demandé (évite le double comptage)
        if (!string.IsNullOrEmpty(excludeCategorie))
            filtered = filtered.Where(a => a.CategorieComptable != excludeCategorie);

        if (mois.HasValue || annee.HasValue)
        {
            filtered = filtered.Where(a =>
            {
                if (DateTime.TryParseExact(a.Date, ["dd/MM/yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    if (annee.HasValue && d.Year != annee.Value) return false;
                    if (mois.HasValue && d.Month != mois.Value) return false;
                    return true;
                }
                return false;
            });
        }

        var list = filtered.ToList();
        var recettes = list.Where(a => a.Montant > 0).Sum(a => a.Montant);
        var depenses = list.Where(a => a.Montant < 0).Sum(a => Math.Abs(a.Montant));
        return (recettes, depenses);
    }

    private static AjustementFinancier MapFromRow(string[] row, int rowIndex)
    {
        return new AjustementFinancier
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            Date = row.ElementAtOrDefault(1) ?? "",
            Type = row.ElementAtOrDefault(2) ?? "",
            Montant = decimal.TryParse(row.ElementAtOrDefault(3), NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : 0,
            Description = row.ElementAtOrDefault(4) ?? "",
            Categorie = row.ElementAtOrDefault(5) ?? "",
            CategorieComptable = row.ElementAtOrDefault(6) ?? "autre",
            Note = row.ElementAtOrDefault(7) ?? ""
        };
    }

    private static string[] MapToRow(AjustementFinancier a)
    {
        return [a.Guid, a.Date, a.Type,
                a.Montant.ToString(CultureInfo.InvariantCulture),
                a.Description, a.Categorie, a.CategorieComptable, a.Note];
    }
}
