namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un produit en inventaire.
/// Correspond à une ligne dans la feuille "Produits" du Spreadsheet.
/// </summary>
public class Produit
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Marque { get; set; } = string.Empty;
    public decimal PrixVente { get; set; }
    public decimal PrixAchat { get; set; }
    public int Stock { get; set; }
    public int SeuilAlerte { get; set; } = 5;
    public string Categorie { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;

    public bool EstEnAlerte => Stock <= SeuilAlerte;
    public bool EstEnRupture => Stock <= 0;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();
}
