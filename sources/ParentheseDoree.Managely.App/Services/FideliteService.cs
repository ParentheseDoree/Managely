using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Logique de fidélité :
/// 1) Tous les 10 passages → carte fidélité automatique (10% des PRESTATIONS des 10 derniers passages)
/// 2) Mois d'anniversaire → bon fidélité de 15€ si >60€ de prestations cabine dans le mois
/// 
/// IMPORTANT : Seules les PRESTATIONS comptent (pas les produits vendus).
/// Les bons fidélité sont de type "bon_fidelite" (distincts des cartes cadeaux "achat" et des cartes "fidelite").
/// </summary>
public sealed class FideliteService
{
    private readonly PassageService _passages;
    private readonly CarteCadeauService _cartes;
    private readonly ClientService _clients;
    private readonly BrowserLoggerService _logger;

    public FideliteService(PassageService passages, CarteCadeauService cartes,
        ClientService clients, BrowserLoggerService logger)
    {
        _passages = passages;
        _cartes = cartes;
        _clients = clients;
        _logger = logger;
    }

    /// <summary>
    /// Vérifie si un client a atteint un palier de fidélité après un passage.
    /// Règle : tous les 10 passages → 10% du total des PRESTATIONS (pas produits) des 10 derniers passages.
    /// Retourne la carte créée ou null.
    /// </summary>
    public async Task<CarteCadeau?> VerifierEtGenererAsync(string clientGuid)
    {
        var passagesClient = await _passages.GetByClientAsync(clientGuid);
        var nbPassages = passagesClient.Count;

        // Vérifier si on est sur un multiple de 10
        if (nbPassages == 0 || nbPassages % 10 != 0)
            return null;

        // Vérifier qu'on n'a pas déjà généré une carte pour ce palier
        var cartes = await _cartes.GetByClientAsync(clientGuid);
        var dejaGenere = cartes.Any(c =>
            c.Type == "fidelite" && c.Origine.Contains($"{nbPassages} passages"));
        if (dejaGenere)
            return null;

        // Calculer la valeur : 10% du total des PRESTATIONS (pas produits) des 10 derniers passages
        var derniers10 = passagesClient
            .OrderByDescending(p => p.Date)
            .Take(10)
            .ToList();

        var totalPrestationsDerniers = derniers10.Sum(p => p.Prestations.Sum(pr => pr.Prix));
        var valeurCarte = Math.Round(totalPrestationsDerniers * 0.10m, 2);

        if (valeurCarte <= 0)
            return null;

        var carte = new CarteCadeau
        {
            ClientGuid = clientGuid,
            Type = "fidelite",
            MontantInitial = valeurCarte,
            SoldeRestant = valeurCarte,
            DateCreation = DateTime.Now.ToString("dd/MM/yyyy"),
            DateExpiration = DateTime.Now.AddYears(1).ToString("dd/MM/yyyy"),
            Statut = "active",
            Origine = $"Fidélité {nbPassages} passages — 10% de {totalPrestationsDerniers:N2}€ de prestations"
        };

        await _cartes.AddAsync(carte);
        await _logger.SuccessAsync(LogCategory.APP,
            $"Carte fidélité générée: {valeurCarte:N2}€ pour {nbPassages} passages");

        return carte;
    }

    /// <summary>
    /// Vérifie le droit au bon fidélité anniversaire.
    /// Règle : pendant le mois d'anniversaire, le client reçoit automatiquement un bon de 15€.
    /// Ce bon est utilisable uniquement sur un passage dont les prestations cabine dépassent 60€.
    /// Il expire à la fin du mois d'anniversaire.
    /// Génération automatique dès le premier passage du mois d'anniversaire.
    /// </summary>
    public async Task<CarteCadeau?> VerifierBonAnniversaireAsync(string clientGuid)
    {
        var client = await _clients.GetByGuidAsync(clientGuid);
        if (client == null || string.IsNullOrEmpty(client.MoisAnniversaire))
            return null;

        // Vérifier que c'est bien le mois d'anniversaire
        var moisActuel = DateTime.Now.Month;
        if (!EstMoisAnniv(client.MoisAnniversaire, moisActuel))
            return null;

        // Vérifier qu'on n'a pas déjà généré un bon anniversaire ce mois
        var cartes = await _cartes.GetByClientAsync(clientGuid);
        var moisStr = DateTime.Now.ToString("MM/yyyy");
        var dejaGenere = cartes.Any(c =>
            c.Type == "bon_fidelite" && c.Origine.Contains($"Anniversaire {moisStr}"));
        if (dejaGenere)
            return null;

        // Générer le bon fidélité anniversaire de 15€ (valable tout le mois)
        var finMois = new DateTime(DateTime.Now.Year, moisActuel,
            DateTime.DaysInMonth(DateTime.Now.Year, moisActuel));

        var bon = new CarteCadeau
        {
            ClientGuid = clientGuid,
            Type = "bon_fidelite",
            MontantInitial = 15m,
            SoldeRestant = 15m,
            DateCreation = DateTime.Now.ToString("dd/MM/yyyy"),
            DateExpiration = finMois.ToString("dd/MM/yyyy"),
            Statut = "active",
            Origine = $"Anniversaire {moisStr} — Bon 15€ (utilisable si prestations cabine > 60€)"
        };

        await _cartes.AddAsync(bon);
        await _logger.SuccessAsync(LogCategory.APP,
            $"Bon fidélité anniversaire 15€ généré pour {client.NomComplet}");

        return bon;
    }

    /// <summary>
    /// Vérifie si un bon fidélité anniversaire peut être utilisé sur le passage en cours.
    /// Condition : le total des prestations cabine du passage doit être > 60€.
    /// Les produits ne comptent pas.
    /// </summary>
    public bool BonAnniversaireUtilisable(decimal totalPrestationsPassage)
    {
        return totalPrestationsPassage > 60m;
    }

    /// <summary>
    /// Retourne le statut fidélité d'un client.
    /// Le total est calculé sur les PRESTATIONS uniquement.
    /// </summary>
    public async Task<(int NbPassages, int PassagesAvantProchain, decimal TotalPrestationsDerniers)> GetStatutAsync(string clientGuid)
    {
        var passagesClient = await _passages.GetByClientAsync(clientGuid);
        var nbPassages = passagesClient.Count;
        var passagesAvant = nbPassages == 0 ? 10 : (10 - (nbPassages % 10)) % 10;
        if (passagesAvant == 0) passagesAvant = 10;

        var nbToTake = nbPassages % 10 == 0 ? 10 : nbPassages % 10;
        var derniersPrestations = passagesClient
            .OrderByDescending(p => p.Date)
            .Take(nbToTake)
            .Sum(p => p.Prestations.Sum(pr => pr.Prix));

        return (nbPassages, passagesAvant, derniersPrestations);
    }

    private static bool EstMoisAnniv(string moisAnniv, int moisCible)
    {
        if (string.IsNullOrEmpty(moisAnniv)) return false;
        if (int.TryParse(moisAnniv, out var moisNum)) return moisNum == moisCible;

        var moisNoms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["janvier"] = 1, ["février"] = 2, ["mars"] = 3, ["avril"] = 4,
            ["mai"] = 5, ["juin"] = 6, ["juillet"] = 7, ["août"] = 8,
            ["septembre"] = 9, ["octobre"] = 10, ["novembre"] = 11, ["décembre"] = 12
        };
        return moisNoms.TryGetValue(moisAnniv.Trim(), out var m) && m == moisCible;
    }
}
