namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un ajustement financier manuel.
/// Permet de corriger les comptes ou ajouter des dépenses non liées aux passages.
/// </summary>
public class AjustementFinancier
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// "depense", "recette", "ajustement", "perte", "charge"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Montant (positif = recette, négatif = dépense).</summary>
    public decimal Montant { get; set; }

    public string Description { get; set; } = string.Empty;
    public string Categorie { get; set; } = string.Empty;

    /// <summary>"prestation", "produit", "carte_cadeau", "autre"</summary>
    public string CategorieComptable { get; set; } = "autre";

    public string Note { get; set; } = string.Empty;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();

    /// <summary>Indique si c'est une recette.</summary>
    public bool EstRecette => Montant > 0;
}
