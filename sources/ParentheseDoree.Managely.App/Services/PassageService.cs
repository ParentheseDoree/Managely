using System.Globalization;
using System.Text.Json;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion des passages (visites).
/// Les sous-entités (prestations, produits) sont stockées en JSON dans les cellules.
/// </summary>
public sealed class PassageService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "Passages";
    private const string Range = "A:M";
    private const string CacheKey = "passages_all";

    private static readonly string[] Headers =
        ["guid", "client_guid", "date", "prestations_json", "produits_vendus_json",
         "produits_conseilles_json", "note_interne", "total", "mode_paiement",
         "carte_cadeau_guid", "montant_carte_utilisee", "paiements_json", "hachage_integrite"];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PassageService(GoogleSheetsService sheets, CacheService cache, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<Passage>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<Passage>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);
        var list = rows.Select((r, i) => MapFromRow(r, i + 2)).ToList();
        _cache.Set(CacheKey, list);
        return list;
    }

    public async Task<List<Passage>> GetByClientAsync(string clientGuid)
    {
        var all = await GetAllAsync();
        return all.Where(p => p.ClientGuid == clientGuid).OrderByDescending(p => p.Date).ToList();
    }

    public async Task<Passage?> GetByGuidAsync(string guid)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(p => p.Guid == guid);
    }

    /// <summary>
    /// Ajoute un passage. Retourne le GUID du passage créé.
    /// </summary>
    public async Task<string> AddAsync(Passage passage)
    {
        passage.GenererGuid();
        passage.RecalculerTotal();
        passage.MettreAJourIntegrite();
        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, MapToRow(passage));
        if (!success) throw new InvalidOperationException("Échec de l'ajout du passage");
        _cache.Invalidate(CacheKey);
        return passage.Guid;
    }

    /// <summary>
    /// Met à jour un passage. Vérifie l'intégrité avant écriture.
    /// </summary>
    public async Task<bool> UpdateAsync(Passage passage, bool forceWrite = false)
    {
        if (!forceWrite)
        {
            var current = await GetByGuidAsync(passage.Guid);
            if (current != null && !string.IsNullOrEmpty(current.HachageIntegrite)
                && current.HachageIntegrite != passage.HachageIntegrite)
            {
                await _logger.WarnAsync(LogCategory.APP, "Conflit détecté sur le passage", 
                    new { guid = passage.Guid });
                return false; // Conflit détecté
            }
        }

        passage.RecalculerTotal();
        passage.MettreAJourIntegrite();
        var success = await _sheets.UpdateRowAsync(SheetName, passage.RowIndex, Range, MapToRow(passage));
        if (!success) throw new InvalidOperationException("Échec de la mise à jour du passage");
        _cache.Invalidate(CacheKey);
        return true;
    }

    public async Task DeleteAsync(int rowIndex)
    {
        var success = await _sheets.DeleteRowAsync(SheetName, rowIndex);
        if (!success) throw new InvalidOperationException("Échec de la suppression");
        _cache.Invalidate(CacheKey);
    }

    /// <summary>Nombre de passages d'un client.</summary>
    public async Task<int> CountByClientAsync(string clientGuid)
    {
        var passages = await GetByClientAsync(clientGuid);
        return passages.Count;
    }

    /// <summary>Total dépensé par un client.</summary>
    public async Task<decimal> TotalDepenseClientAsync(string clientGuid)
    {
        var passages = await GetByClientAsync(clientGuid);
        return passages.Sum(p => p.Total);
    }

    private static Passage MapFromRow(string[] row, int rowIndex)
    {
        return new Passage
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            ClientGuid = row.ElementAtOrDefault(1) ?? "",
            Date = row.ElementAtOrDefault(2) ?? "",
            Prestations = DeserializeJson<List<PassagePrestation>>(row.ElementAtOrDefault(3)) ?? [],
            ProduitsVendus = DeserializeJson<List<PassageProduitVendu>>(row.ElementAtOrDefault(4)) ?? [],
            ProduitsConseilles = DeserializeJson<List<PassageProduitConseille>>(row.ElementAtOrDefault(5)) ?? [],
            NoteInterne = row.ElementAtOrDefault(6) ?? "",
            Total = decimal.TryParse(row.ElementAtOrDefault(7), NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 0,
            ModePaiement = row.ElementAtOrDefault(8) ?? "",
            CarteCadeauGuid = row.ElementAtOrDefault(9) ?? "",
            MontantCarteUtilisee = decimal.TryParse(row.ElementAtOrDefault(10), NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : 0,
            Paiements = DeserializeJson<List<PaiementDetail>>(row.ElementAtOrDefault(11)) ?? [],
            HachageIntegrite = row.ElementAtOrDefault(12) ?? ""
        };
    }

    private static string[] MapToRow(Passage p)
    {
        return [p.Guid, p.ClientGuid, p.Date,
                SerializeJson(p.Prestations),
                SerializeJson(p.ProduitsVendus),
                SerializeJson(p.ProduitsConseilles),
                p.NoteInterne,
                p.Total.ToString(CultureInfo.InvariantCulture),
                p.ModePaiement, p.CarteCadeauGuid,
                p.MontantCarteUtilisee.ToString(CultureInfo.InvariantCulture),
                SerializeJson(p.Paiements),
                p.HachageIntegrite];
    }

    private static string SerializeJson<T>(T obj)
    {
        try { return JsonSerializer.Serialize(obj, JsonOpts); }
        catch { return "[]"; }
    }

    private static T? DeserializeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return null; }
    }
}
