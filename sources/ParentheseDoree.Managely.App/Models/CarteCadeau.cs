namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente une carte cadeau (fidélité ou achat).
/// </summary>
public class CarteCadeau
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string ClientGuid { get; set; } = string.Empty;

    /// <summary>"fidelite" ou "achat"</summary>
    public string Type { get; set; } = "achat";

    public decimal MontantInitial { get; set; }
    public decimal SoldeRestant { get; set; }
    public string DateCreation { get; set; } = string.Empty;
    public string DateExpiration { get; set; } = string.Empty;

    /// <summary>"active", "utilisee", "expiree"</summary>
    public string Statut { get; set; } = "active";

    /// <summary>Description de l'origine (ex: "Fidélité 10 passages")</summary>
    public string Origine { get; set; } = string.Empty;

    // Champ d'affichage
    public string ClientNom { get; set; } = string.Empty;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();

    /// <summary>Calcule le statut réel de la carte.</summary>
    public string StatutCalcule
    {
        get
        {
            if (SoldeRestant <= 0) return "utilisee";
            if (!string.IsNullOrEmpty(DateExpiration)
                && DateTime.TryParse(DateExpiration, out var exp)
                && exp < DateTime.Today)
                return "expiree";
            return "active";
        }
    }

    public bool EstUtilisable => StatutCalcule == "active" && SoldeRestant > 0;
}
