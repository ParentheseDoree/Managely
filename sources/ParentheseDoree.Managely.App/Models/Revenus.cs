namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente une entrée de revenus.
/// Types: "prestation", "produit", "carte_vendue", "carte_utilisee"
/// </summary>
public class Revenus
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Montant { get; set; }
    public string PassageGuid { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();
}
