using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de gestion des clients avec détection de conflits.
/// </summary>
public sealed class ClientService
{
    private readonly GoogleSheetsService _sheets;
    private readonly CacheService _cache;
    private readonly BrowserLoggerService _logger;

    private const string SheetName = "Clients";
    private const string Range = "A:J";
    private const string CacheKey = "clients_all";

    private static readonly string[] Headers =
        ["guid", "nom", "prenom", "mois_anniversaire", "numero_telephone", "email", "adresse", "date_creation", "date_modification", "hachage_integrite"];

    public ClientService(GoogleSheetsService sheets, CacheService cache, BrowserLoggerService logger)
    {
        _sheets = sheets;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<Client>> GetAllAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Get<List<Client>>(CacheKey);
            if (cached != null) return cached;
        }

        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var rows = await _sheets.ReadSheetAsync(SheetName, Range);

        var clients = rows.Select((row, i) => MapFromRow(row, i + 2)).ToList();
        _cache.Set(CacheKey, clients);
        return clients;
    }

    public async Task<Client?> GetByGuidAsync(string guid)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(c => c.Guid == guid);
    }

    public async Task<string> AddAsync(Client client)
    {
        client.GenererGuid();
        client.DateCreation = DateTime.UtcNow.ToString("o");
        client.MettreAJourIntegrite();

        var values = MapToRow(client);
        await _sheets.EnsureSheetAsync(SheetName, Headers);
        var success = await _sheets.AppendRowAsync(SheetName, Range, values);
        if (!success) throw new InvalidOperationException("Échec de l'ajout du client");

        _cache.Invalidate(CacheKey);
        await _logger.SuccessAsync(LogCategory.API, $"Client ajouté: {client.NomComplet}");
        return client.Guid;
    }

    /// <summary>
    /// Met à jour un client avec détection de conflit.
    /// Retourne false si un conflit est détecté (sauf si forceWrite=true).
    /// </summary>
    public async Task<bool> UpdateAsync(Client client, bool forceWrite = false)
    {
        if (!forceWrite)
        {
            // Recharger depuis le serveur pour vérifier l'intégrité
            var current = await GetByGuidAsync(client.Guid);
            if (current != null && !string.IsNullOrEmpty(current.HachageIntegrite)
                && current.HachageIntegrite != client.HachageIntegrite)
            {
                await _logger.WarnAsync(LogCategory.APP, "Conflit détecté sur le client",
                    new { guid = client.Guid, nom = client.NomComplet });
                return false; // Conflit
            }
        }

        client.MettreAJourIntegrite();
        var values = MapToRow(client);
        var success = await _sheets.UpdateRowAsync(SheetName, client.RowIndex, Range, values);
        if (!success) throw new InvalidOperationException("Échec de la mise à jour du client");

        _cache.Invalidate(CacheKey);
        await _logger.SuccessAsync(LogCategory.API, $"Client modifié: {client.NomComplet}");
        return true;
    }

    public async Task DeleteAsync(int rowIndex)
    {
        var success = await _sheets.DeleteRowAsync(SheetName, rowIndex);
        if (!success) throw new InvalidOperationException("Échec de la suppression du client");

        _cache.Invalidate(CacheKey);
        await _logger.SuccessAsync(LogCategory.API, "Client supprimé");
    }

    /// <summary>
    /// Retourne les clients dont le mois d'anniversaire correspond.
    /// </summary>
    public async Task<List<Client>> GetAnniversairesMoisAsync(int mois)
    {
        var all = await GetAllAsync();
        var moisStr = mois.ToString("D2");
        var moisNoms = new[] { "", "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
            "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre" };
        var moisNom = mois >= 1 && mois <= 12 ? moisNoms[mois] : "";

        return all.Where(c =>
            c.MoisAnniversaire.Equals(moisStr, StringComparison.OrdinalIgnoreCase) ||
            c.MoisAnniversaire.Equals(moisNom, StringComparison.OrdinalIgnoreCase) ||
            c.MoisAnniversaire.Equals(mois.ToString(), StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    /// <summary>
    /// Recherche de clients par texte avec tolérance (fuzzy matching simple).
    /// </summary>
    public async Task<List<Client>> RechercherAsync(string terme)
    {
        if (string.IsNullOrWhiteSpace(terme)) return await GetAllAsync();

        var all = await GetAllAsync();
        var t = terme.Trim().ToLower();

        // Score-based search: exact contains first, then fuzzy
        return all
            .Select(c => new
            {
                Client = c,
                Score = CalculerScoreRecherche(c, t)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Client)
            .ToList();
    }

    private static int CalculerScoreRecherche(Client c, string terme)
    {
        int score = 0;
        var nom = c.NomComplet.ToLower();
        var tel = c.NumeroTelephone.ToLower();
        var email = c.Email.ToLower();

        // Correspondance exacte dans le nom complet
        if (nom.Contains(terme)) score += 100;
        // Correspondance dans le téléphone
        if (tel.Contains(terme)) score += 90;
        // Correspondance dans l'email
        if (email.Contains(terme)) score += 80;
        // Commence par
        if (c.Nom.StartsWith(terme, StringComparison.OrdinalIgnoreCase)) score += 50;
        if (c.Prenom.StartsWith(terme, StringComparison.OrdinalIgnoreCase)) score += 50;

        // Recherche fuzzy (distance de Levenshtein simplifiée)
        if (score == 0)
        {
            var distNom = DistanceLevenshtein(c.Nom.ToLower(), terme);
            var distPrenom = DistanceLevenshtein(c.Prenom.ToLower(), terme);
            var minDist = Math.Min(distNom, distPrenom);
            var seuil = Math.Max(2, terme.Length / 3); // Tolérance proportionnelle
            if (minDist <= seuil) score += (seuil - minDist + 1) * 10;
        }

        return score;
    }

    /// <summary>
    /// Distance de Levenshtein entre deux chaînes.
    /// </summary>
    private static int DistanceLevenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static Client MapFromRow(string[] row, int rowIndex)
    {
        return new Client
        {
            RowIndex = rowIndex,
            Guid = row.ElementAtOrDefault(0) ?? "",
            Nom = row.ElementAtOrDefault(1) ?? "",
            Prenom = row.ElementAtOrDefault(2) ?? "",
            MoisAnniversaire = row.ElementAtOrDefault(3) ?? "",
            NumeroTelephone = row.ElementAtOrDefault(4) ?? "",
            Email = row.ElementAtOrDefault(5) ?? "",
            Adresse = row.ElementAtOrDefault(6) ?? "",
            DateCreation = row.ElementAtOrDefault(7) ?? "",
            DateModification = row.ElementAtOrDefault(8) ?? "",
            HachageIntegrite = row.ElementAtOrDefault(9) ?? ""
        };
    }

    private static string[] MapToRow(Client c)
    {
        return [c.Guid, c.Nom, c.Prenom, c.MoisAnniversaire, c.NumeroTelephone,
                c.Email, c.Adresse, c.DateCreation, c.DateModification, c.HachageIntegrite];
    }
}
