using System.Globalization;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion des mouvements de stock (entrées/sorties).
/// </summary>
public sealed class MouvementStockService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly ProduitService _produits;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "MouvementsStock";
    private const string Range = "A:I";
    private const string CacheKey = "mouvements_all";

    private static readonly string[] Headers =
        ["guid", "produit_guid", "type", "quantite", "cout_unitaire", "date", "motif", "reference", "note"];

    public MouvementStockService(GoogleSheetsService sheets, CacheService cache,
        ProduitService produits, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _produits = produits;
        _logger = logger;
    }

    public async Task<List<MouvementStock>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<MouvementStock>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);
        var list = rows.Select((r, i) => MapFromRow(r, i + 2)).ToList();
        _cache.Set(CacheKey, list);
        return list;
    }

    public async Task<List<MouvementStock>> GetByProduitAsync(string produitGuid)
    {
        var all = await GetAllAsync();
        return all.Where(m => m.ProduitGuid == produitGuid)
                  .OrderByDescending(m => m.Date)
                  .ToList();
    }

    /// <summary>
    /// Enregistre un réapprovisionnement et met à jour le stock du produit.
    /// </summary>
    public async Task<string> ReapprovisionnerAsync(string produitGuid, int quantite, 
        decimal coutUnitaire, string note = "")
    {
        var mouvement = new MouvementStock
        {
            ProduitGuid = produitGuid,
            Type = "entree",
            Quantite = quantite,
            CoutUnitaire = coutUnitaire,
            Date = DateTime.Now.ToString("dd/MM/yyyy"),
            Motif = "reapprovisionnement",
            Note = note
        };

        return await AjouterMouvementAsync(mouvement);
    }

    /// <summary>
    /// Enregistre une sortie de stock (vente) et met à jour le produit.
    /// </summary>
    public async Task<string> EnregistrerSortieAsync(string produitGuid, int quantite,
        decimal prixVente, string passageGuid = "")
    {
        var mouvement = new MouvementStock
        {
            ProduitGuid = produitGuid,
            Type = "sortie",
            Quantite = quantite,
            CoutUnitaire = prixVente,
            Date = DateTime.Now.ToString("dd/MM/yyyy"),
            Motif = "vente",
            Reference = passageGuid
        };

        return await AjouterMouvementAsync(mouvement);
    }

    /// <summary>
    /// Ajoute un mouvement de stock et met à jour le produit associé.
    /// </summary>
    public async Task<string> AjouterMouvementAsync(MouvementStock mouvement)
    {
        mouvement.GenererGuid();
        if (string.IsNullOrEmpty(mouvement.Date))
            mouvement.Date = DateTime.Now.ToString("dd/MM/yyyy");

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, MapToRow(mouvement));
        if (!success) throw new InvalidOperationException("Échec de l'ajout du mouvement");

        // Mettre à jour le stock du produit
        var produits = await _produits.GetAllAsync(true);
        var produit = produits.FirstOrDefault(p => p.Guid == mouvement.ProduitGuid);
        if (produit != null)
        {
            if (mouvement.Type == "entree")
                produit.Stock += mouvement.Quantite;
            else
                produit.Stock = Math.Max(0, produit.Stock - mouvement.Quantite);

            await _produits.UpdateAsync(produit);
        }

        _cache.Invalidate(CacheKey);
        await _logger.SuccessAsync(LogCategory.API, 
            $"Mouvement {mouvement.Type}: {mouvement.Quantite}x {produit?.Nom ?? mouvement.ProduitGuid}");
        return mouvement.Guid;
    }

    /// <summary>
    /// Total des charges (coûts d'achat) sur une période.
    /// </summary>
    public async Task<decimal> GetTotalChargesAsync(int? mois = null, int? annee = null)
    {
        var all = await GetAllAsync();
        var filtered = all.Where(m => m.Type == "entree" && m.Motif == "reapprovisionnement");

        if (mois.HasValue || annee.HasValue)
        {
            filtered = filtered.Where(m =>
            {
                if (DateTime.TryParseExact(m.Date, ["dd/MM/yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                {
                    if (annee.HasValue && d.Year != annee.Value) return false;
                    if (mois.HasValue && d.Month != mois.Value) return false;
                    return true;
                }
                return false;
            });
        }

        return filtered.Sum(m => m.MontantTotal);
    }

    private static MouvementStock MapFromRow(string[] row, int rowIndex)
    {
        return new MouvementStock
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            ProduitGuid = row.ElementAtOrDefault(1) ?? "",
            Type = row.ElementAtOrDefault(2) ?? "",
            Quantite = int.TryParse(row.ElementAtOrDefault(3), out var q) ? q : 0,
            CoutUnitaire = decimal.TryParse(row.ElementAtOrDefault(4), NumberStyles.Any, CultureInfo.InvariantCulture, out var c) ? c : 0,
            Date = row.ElementAtOrDefault(5) ?? "",
            Motif = row.ElementAtOrDefault(6) ?? "",
            Reference = row.ElementAtOrDefault(7) ?? "",
            Note = row.ElementAtOrDefault(8) ?? ""
        };
    }

    private static string[] MapToRow(MouvementStock m)
    {
        return [m.Guid, m.ProduitGuid, m.Type,
                m.Quantite.ToString(),
                m.CoutUnitaire.ToString(CultureInfo.InvariantCulture),
                m.Date, m.Motif, m.Reference, m.Note];
    }
}
