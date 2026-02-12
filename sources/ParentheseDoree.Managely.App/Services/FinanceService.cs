using System.Globalization;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service de calcul financier exhaustif.
/// Prend en compte : prestations, produits vendus, cartes cadeaux (vendues/utilisées),
/// charges (achats produits), ajustements manuels, pertes.
/// </summary>
public sealed class FinanceService
{
    private readonly PassageService _passages;
    private readonly CarteCadeauService _cartes;
    private readonly ClientService _clients;
    private readonly AjustementService _ajustements;
    private readonly MouvementStockService _mouvements;
    private readonly BrowserLoggerService _logger;

    public FinanceService(PassageService passages, CarteCadeauService cartes,
        ClientService clients, AjustementService ajustements,
        MouvementStockService mouvements, BrowserLoggerService logger)
    {
        _passages = passages;
        _cartes = cartes;
        _clients = clients;
        _ajustements = ajustements;
        _mouvements = mouvements;
        _logger = logger;
    }

    /// <summary>
    /// Statistiques pour un mois/année spécifiques (pour les KPIs en haut).
    /// </summary>
    public async Task<DashboardStats> GetStatsAsync(int mois, int annee)
    {
        var passages = await _passages.GetAllAsync();
        var clients = await _clients.GetAllAsync();
        var cartes = await _cartes.GetAllAsync();

        var today = DateTime.Now.ToString("dd/MM/yyyy");

        var passagesPeriode = passages.Where(p => EstDansPeriode(p.Date, mois, annee)).ToList();
        var passagesAujourdhui = passages.Where(p => p.Date == today).ToList();

        // CA cumulé de l'année sélectionnée
        var passagesAnnee = passages.Where(p => TryParseDate(p.Date, out var d) && d.Year == annee).ToList();

        return new DashboardStats
        {
            TotalClients = clients.Count,
            TotalPassages = passages.Count,
            PassagesMoisEnCours = passagesPeriode.Count,
            PassagesAujourdhui = passagesAujourdhui.Count,
            CaMoisEnCours = passagesPeriode.Sum(p => p.Total),
            CaAujourdhui = passagesAujourdhui.Sum(p => p.Total),
            CaTotal = passagesAnnee.Sum(p => p.Total),
            CartesActives = cartes.Count(c => c.EstUtilisable),
            AnniversairesMois = clients.Count(c => EstMoisAnniv(c.MoisAnniversaire, mois))
        };
    }

    /// <summary>
    /// Bilan financier complet pour un mois/année.
    /// </summary>
    public async Task<BilanFinancier> GetBilanAsync(int mois, int annee)
    {
        var passages = await _passages.GetAllAsync();
        var cartes = await _cartes.GetAllAsync();
        // On exclut la catégorie "produit" des ajustements car les charges d'achat produits
        // sont déjà calculées via MouvementsStock (évite le double comptage)
        var (recettesAjust, depensesAjust) = await _ajustements.GetTotauxAsync(mois, annee, excludeCategorie: "produit");
        var charges = await _mouvements.GetTotalChargesAsync(mois, annee);

        var passagesPeriode = passages.Where(p => EstDansPeriode(p.Date, mois, annee)).ToList();

        var caPrestations = passagesPeriode.Sum(p => p.Prestations.Sum(pr => pr.Prix));
        var caProduits = passagesPeriode.Sum(p =>
            p.ProduitsVendus.Sum(pv => pv.Quantite * pv.PrixUnitaire));

        // Cartes cadeaux vendues dans la période
        var cartesVenduesPeriode = cartes.Where(c =>
            c.Type == "achat" && EstDansPeriode(c.DateCreation, mois, annee)).ToList();
        var caCartesVendues = cartesVenduesPeriode.Sum(c => c.MontantInitial);

        // Calculer l'utilisation des cartes/bons avec distinction par type
        var carteFideliteGuids = cartes.Where(c => c.Type == "fidelite").Select(c => c.Guid).ToHashSet();
        var bonFideliteGuids = cartes.Where(c => c.Type == "bon_fidelite").Select(c => c.Guid).ToHashSet();

        decimal cartesAchatUtilisees = 0;
        decimal cartesFideliteUtilisees = 0;
        decimal bonsFideliteUtilises = 0;

        foreach (var p in passagesPeriode)
        {
            if (p.Paiements.Count > 0)
            {
                foreach (var pay in p.Paiements.Where(pay => pay.Mode == "Carte cadeau"))
                {
                    if (carteFideliteGuids.Contains(pay.CarteCadeauGuid))
                        cartesFideliteUtilisees += pay.Montant;
                    else if (bonFideliteGuids.Contains(pay.CarteCadeauGuid))
                        bonsFideliteUtilises += pay.Montant;
                    else
                        cartesAchatUtilisees += pay.Montant;
                }
            }
            else if (!string.IsNullOrEmpty(p.CarteCadeauGuid) && p.MontantCarteUtilisee > 0)
            {
                if (carteFideliteGuids.Contains(p.CarteCadeauGuid))
                    cartesFideliteUtilisees += p.MontantCarteUtilisee;
                else if (bonFideliteGuids.Contains(p.CarteCadeauGuid))
                    bonsFideliteUtilises += p.MontantCarteUtilisee;
                else
                    cartesAchatUtilisees += p.MontantCarteUtilisee;
            }
        }

        // Bons anniversaire émis ce mois
        var bonsEmis = cartes.Where(c =>
            c.Type == "bon_fidelite" && EstDansPeriode(c.DateCreation, mois, annee))
            .Sum(c => c.MontantInitial);

        return new BilanFinancier
        {
            Mois = mois,
            Annee = annee,
            CaPrestations = caPrestations,
            CaProduits = caProduits,
            CaCartesVendues = caCartesVendues,
            MontantCartesAchatUtilisees = cartesAchatUtilisees,
            MontantCartesFideliteUtilisees = cartesFideliteUtilisees,
            MontantBonsFideliteUtilises = bonsFideliteUtilises,
            BonsAnniversaireEmis = bonsEmis,
            ChargesAchatsProduits = charges,
            RecettesAjustements = recettesAjust,
            DepensesAjustements = depensesAjust,
            NbPassages = passagesPeriode.Count
        };
    }

    /// <summary>
    /// Revenus mensuels détaillés par catégorie pour une année donnée (pour le graphique stacked).
    /// </summary>
    public async Task<RevenusMensuelsDetail> GetRevenusMensuelsAsync(int annee)
    {
        var passages = await _passages.GetAllAsync();
        var cartes = await _cartes.GetAllAsync();

        var detail = new RevenusMensuelsDetail();

        foreach (var p in passages)
        {
            if (TryParseDate(p.Date, out var date) && date.Year == annee)
            {
                detail.Prestations[date.Month] += p.Prestations.Sum(pr => pr.Prix);
                detail.Produits[date.Month] += p.ProduitsVendus.Sum(pv => pv.Quantite * pv.PrixUnitaire);
            }
        }

        foreach (var c in cartes)
        {
            if (TryParseDate(c.DateCreation, out var date) && date.Year == annee)
            {
                if (c.Type == "achat")
                    detail.CartesVendues[date.Month] += c.MontantInitial;
                else if (c.Type is "fidelite" or "bon_fidelite")
                    detail.Fidelite[date.Month] += c.MontantInitial;
            }
        }

        return detail;
    }

    /// <summary>
    /// Répartition du CA par type (prestations / produits / cartes vendues / fidélité) pour un mois/année.
    /// </summary>
    public async Task<RepartitionCA> GetRepartitionAsync(int mois, int annee)
    {
        var passages = await _passages.GetAllAsync();
        var cartes = await _cartes.GetAllAsync();

        var filtered = passages.Where(p => EstDansPeriode(p.Date, mois, annee)).ToList();

        var totalPrestations = filtered.Sum(p => p.Prestations.Sum(pr => pr.Prix));
        var totalProduits = filtered.Sum(p =>
            p.ProduitsVendus.Sum(pv => pv.Quantite * pv.PrixUnitaire));

        var cartesVendues = cartes
            .Where(c => c.Type == "achat" && EstDansPeriode(c.DateCreation, mois, annee))
            .Sum(c => c.MontantInitial);

        var cartesFidelite = cartes
            .Where(c => c.Type == "fidelite" && EstDansPeriode(c.DateCreation, mois, annee))
            .Sum(c => c.MontantInitial);

        var bonsAnniv = cartes
            .Where(c => c.Type == "bon_fidelite" && EstDansPeriode(c.DateCreation, mois, annee))
            .Sum(c => c.MontantInitial);

        return new RepartitionCA
        {
            Prestations = totalPrestations,
            Produits = totalProduits,
            CartesVendues = cartesVendues,
            CartesFidelite = cartesFidelite,
            BonsAnniversaire = bonsAnniv
        };
    }

    /// <summary>
    /// Top prestations filtrées par mois/année.
    /// </summary>
    public async Task<List<(string Nom, int Count, decimal Total)>> GetTopPrestationsAsync(
        int mois, int annee, int top = 10)
    {
        var passages = await _passages.GetAllAsync();
        var filtered = passages.Where(p => EstDansPeriode(p.Date, mois, annee)).ToList();

        return filtered
            .SelectMany(p => p.Prestations)
            .Where(p => !string.IsNullOrEmpty(p.Nom))
            .GroupBy(p => p.Nom)
            .Select(g => (Nom: g.Key, Count: g.Count(), Total: g.Sum(p => p.Prix)))
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Top produits vendus filtrés par mois/année.
    /// </summary>
    public async Task<List<(string Nom, int Quantite, decimal Total)>> GetTopProduitsAsync(
        int mois, int annee, int top = 10)
    {
        var passages = await _passages.GetAllAsync();
        var filtered = passages.Where(p => EstDansPeriode(p.Date, mois, annee)).ToList();

        return filtered
            .SelectMany(p => p.ProduitsVendus)
            .Where(p => !string.IsNullOrEmpty(p.Nom))
            .GroupBy(p => p.Nom)
            .Select(g => (
                Nom: g.Key,
                Quantite: g.Sum(p => p.Quantite),
                Total: g.Sum(p => p.Quantite * p.PrixUnitaire)
            ))
            .OrderByDescending(x => x.Quantite)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Répartition par mode de paiement pour un mois/année.
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetRepartitionPaiementsAsync(int mois, int annee)
    {
        var passages = await _passages.GetAllAsync();
        var filtered = passages.Where(p => EstDansPeriode(p.Date, mois, annee)).ToList();

        return filtered
            .Where(p => !string.IsNullOrEmpty(p.ModePaiement))
            .GroupBy(p => p.ModePaiement)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Total));
    }

    private static bool EstDansPeriode(string date, int mois, int annee)
    {
        return TryParseDate(date, out var d) && d.Month == mois && d.Year == annee;
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

    private static bool TryParseDate(string date, out DateTime result)
    {
        return DateTime.TryParseExact(date, ["dd/MM/yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}

public class DashboardStats
{
    public int TotalClients { get; set; }
    public int TotalPassages { get; set; }
    public int PassagesMoisEnCours { get; set; }
    public int PassagesAujourdhui { get; set; }
    public decimal CaMoisEnCours { get; set; }
    public decimal CaAujourdhui { get; set; }
    /// <summary>CA total de l'année sélectionnée</summary>
    public decimal CaTotal { get; set; }
    public int CartesActives { get; set; }
    public int AnniversairesMois { get; set; }
}

/// <summary>
/// Bilan financier complet pour une période.
/// 
/// LOGIQUE FINANCIÈRE COMPLÈTE :
/// ─────────────────────────────
/// CA passages = CaPrestations + CaProduits  (valeur totale des services rendus)
///   → Correspond exactement au total affiché sur la page Passages
///
/// Parmi ce CA, une partie a été payée par cartes cadeaux :
///   - MontantCartesAchatUtilisees : payé par cartes achetées (argent déjà encaissé à la vente)
///   - MontantCartesFideliteUtilisees : payé par cartes fidélité (services offerts gratuitement)
///
/// Encaissements réels sur la période :
///   = (CA passages - toutes cartes utilisées) + cartes vendues + ajustements recettes
///   = argent réellement reçu des clients
///
/// Pertes fidélité = services offerts via cartes fidélité (coût réel pour l'entreprise)
/// </summary>
public class BilanFinancier
{
    public int Mois { get; set; }
    public int Annee { get; set; }
    public int NbPassages { get; set; }

    // ═══ CA DES PASSAGES (= ce qui est affiché sur la page Passages) ═══
    /// <summary>CA prestations (somme brute, quel que soit le mode de paiement).</summary>
    public decimal CaPrestations { get; set; }
    /// <summary>CA produits vendus (somme brute, quel que soit le mode de paiement).</summary>
    public decimal CaProduits { get; set; }

    /// <summary>CA total des passages = Prestations + Produits. Correspond au total de la page Passages.</summary>
    public decimal CaPassages => CaPrestations + CaProduits;

    // ═══ CARTES CADEAUX ═══
    /// <summary>Montant des cartes cadeaux ACHAT vendues sur la période (argent encaissé).</summary>
    public decimal CaCartesVendues { get; set; }

    /// <summary>Montant payé par cartes cadeaux ACHAT sur les passages de la période.</summary>
    public decimal MontantCartesAchatUtilisees { get; set; }

    /// <summary>Montant payé par cartes FIDÉLITÉ (10 passages) sur les passages de la période.</summary>
    public decimal MontantCartesFideliteUtilisees { get; set; }

    /// <summary>Montant payé par bons ANNIVERSAIRE sur les passages de la période.</summary>
    public decimal MontantBonsFideliteUtilises { get; set; }

    /// <summary>Montant total des bons anniversaire émis sur la période (pour info).</summary>
    public decimal BonsAnniversaireEmis { get; set; }

    /// <summary>Total de tous les paiements par cartes/bons (achat + fidélité + bons anniv).</summary>
    public decimal MontantCartesTotalUtilisees => MontantCartesAchatUtilisees + MontantCartesFideliteUtilisees + MontantBonsFideliteUtilises;

    // ═══ CHARGES ═══
    /// <summary>Coûts d'achat des produits (réapprovisionnements).</summary>
    public decimal ChargesAchatsProduits { get; set; }

    // ═══ AJUSTEMENTS MANUELS ═══
    public decimal RecettesAjustements { get; set; }
    public decimal DepensesAjustements { get; set; }

    // ═══ TOTAUX ═══

    /// <summary>
    /// Encaissements réels = argent effectivement reçu sur la période.
    /// = CA passages payés en espèces/CB/chèque/virement
    /// + argent des ventes de cartes cadeaux
    /// + ajustements recettes
    /// 
    /// On déduit MontantCartesAchatUtilisees car cet argent a déjà été compté dans CaCartesVendues.
    /// On déduit MontantCartesFideliteUtilisees car aucun argent n'a jamais été reçu pour ça.
    /// </summary>
    public decimal TotalEncaissements => CaPrestations + CaProduits + CaCartesVendues
                                        - MontantCartesAchatUtilisees
                                        - MontantCartesFideliteUtilisees
                                        - MontantBonsFideliteUtilises
                                        + RecettesAjustements;

    /// <summary>Total des charges et dépenses réelles.</summary>
    public decimal TotalDepenses => ChargesAchatsProduits + DepensesAjustements;

    /// <summary>
    /// Bénéfice net = Encaissements - Dépenses.
    /// Les pertes fidélité sont déjà prises en compte (déduites des encaissements).
    /// </summary>
    public decimal Benefice => TotalEncaissements - TotalDepenses;

    /// <summary>CA réel encaissé (hors ajustements).</summary>
    public decimal CaReel => CaPrestations + CaProduits + CaCartesVendues
                            - MontantCartesAchatUtilisees - MontantCartesFideliteUtilisees - MontantBonsFideliteUtilises;
}

/// <summary>Répartition du CA par catégorie.</summary>
public class RepartitionCA
{
    public decimal Prestations { get; set; }
    public decimal Produits { get; set; }
    public decimal CartesVendues { get; set; }
    public decimal CartesFidelite { get; set; }
    public decimal BonsAnniversaire { get; set; }
    public decimal Total => Prestations + Produits + CartesVendues + CartesFidelite + BonsAnniversaire;
    public bool HasData => Total > 0;
}

/// <summary>Revenus mensuels détaillés par catégorie (pour graphique stacked bar).</summary>
public class RevenusMensuelsDetail
{
    public Dictionary<int, decimal> Prestations { get; } = Enumerable.Range(1, 12).ToDictionary(m => m, _ => 0m);
    public Dictionary<int, decimal> Produits { get; } = Enumerable.Range(1, 12).ToDictionary(m => m, _ => 0m);
    public Dictionary<int, decimal> CartesVendues { get; } = Enumerable.Range(1, 12).ToDictionary(m => m, _ => 0m);
    public Dictionary<int, decimal> Fidelite { get; } = Enumerable.Range(1, 12).ToDictionary(m => m, _ => 0m);
}
