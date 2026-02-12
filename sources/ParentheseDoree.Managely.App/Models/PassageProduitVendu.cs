namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Produit vendu lors d'un passage (stocké en JSON dans la cellule produits_vendus_json).
/// </summary>
public class PassageProduitVendu
{
    public string ProduitId { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public int Quantite { get; set; } = 1;
    public decimal PrixUnitaire { get; set; }
    public decimal Total => Quantite * PrixUnitaire;
}
