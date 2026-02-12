namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un mouvement de stock (entrée ou sortie).
/// Permet le suivi historique et comptable de l'inventaire.
/// </summary>
public class MouvementStock
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string ProduitGuid { get; set; } = string.Empty;

    /// <summary>"entree" ou "sortie"</summary>
    public string Type { get; set; } = string.Empty;

    public int Quantite { get; set; }

    /// <summary>Coût unitaire (pour les entrées / réapprovisionnements).</summary>
    public decimal CoutUnitaire { get; set; }

    /// <summary>Montant total du mouvement.</summary>
    public decimal MontantTotal => Quantite * CoutUnitaire;

    public string Date { get; set; } = string.Empty;

    /// <summary>"reapprovisionnement", "vente", "ajustement", "perte"</summary>
    public string Motif { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    // Champ d'affichage
    public string ProduitNom { get; set; } = string.Empty;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();
}
