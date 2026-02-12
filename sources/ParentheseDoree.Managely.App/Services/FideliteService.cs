using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Logique de fidélité : tous les 10 passages, génère une carte cadeau automatique.
/// </summary>
public sealed class FideliteService
{
    private readonly PassageService _passages;
    private readonly CarteCadeauService _cartes;
    private readonly BrowserLoggerService _logger;

    public FideliteService(PassageService passages, CarteCadeauService cartes, BrowserLoggerService logger)
    {
        _passages = passages;
        _cartes = cartes;
        _logger = logger;
    }

    /// <summary>
    /// Vérifie si un client a atteint un palier de fidélité après un passage.
    /// Si oui, crée automatiquement une carte cadeau.
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
        var derniereCarte = cartes
            .Where(c => c.Type == "fidelite")
            .OrderByDescending(c => c.DateCreation)
            .FirstOrDefault();

        if (derniereCarte != null)
        {
            // Vérifier si la carte a déjà été créée pour ce palier (même nombre de passages)
            if (derniereCarte.Origine.Contains($"{nbPassages} passages"))
                return null;
        }

        // Calculer la valeur : 10% du total des 10 derniers passages
        var derniers10 = passagesClient
            .OrderByDescending(p => p.Date)
            .Take(10)
            .ToList();

        var totalDerniers = derniers10.Sum(p => p.Total);
        var valeurCarte = Math.Round(totalDerniers * 0.10m, 2);

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
            Origine = $"Fidélité {nbPassages} passages - 10% de {totalDerniers:N2}€"
        };

        await _cartes.AddAsync(carte);
        await _logger.SuccessAsync(LogCategory.APP,
            $"Carte fidélité générée: {valeurCarte:N2}€ pour {nbPassages} passages");

        return carte;
    }

    /// <summary>
    /// Retourne le statut fidélité d'un client.
    /// </summary>
    public async Task<(int NbPassages, int PassagesAvantProchain, decimal TotalDerniers)> GetStatutAsync(string clientGuid)
    {
        var passagesClient = await _passages.GetByClientAsync(clientGuid);
        var nbPassages = passagesClient.Count;
        var passagesAvant = nbPassages == 0 ? 10 : (10 - (nbPassages % 10)) % 10;
        if (passagesAvant == 0) passagesAvant = 10;

        var derniers = passagesClient
            .OrderByDescending(p => p.Date)
            .Take(nbPassages % 10 == 0 ? 10 : nbPassages % 10)
            .Sum(p => p.Total);

        return (nbPassages, passagesAvant, derniers);
    }
}
