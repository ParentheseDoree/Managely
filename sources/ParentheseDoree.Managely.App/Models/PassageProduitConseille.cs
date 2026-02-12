namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Produit conseillé (non vendu) lors d'un passage.
/// Peut être un produit du catalogue OU un texte libre.
/// </summary>
public class PassageProduitConseille
{
    /// <summary>ID du produit catalogue (vide si texte libre).</summary>
    public string ProduitId { get; set; } = string.Empty;

    /// <summary>Nom du produit (catalogue ou saisi manuellement).</summary>
    public string Nom { get; set; } = string.Empty;

    public decimal PrixIndicatif { get; set; }
    public string Commentaire { get; set; } = string.Empty;

    /// <summary>Indique si c'est un texte libre (pas issu du catalogue).</summary>
    public bool EstTexteLibre => string.IsNullOrEmpty(ProduitId);
}
