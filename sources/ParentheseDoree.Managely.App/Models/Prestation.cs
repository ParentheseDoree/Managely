namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un type de prestation/soin dans le catalogue.
/// Correspond à une ligne dans la feuille "Prestations" du Spreadsheet.
/// </summary>
public class Prestation
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Categorie { get; set; } = string.Empty;
    public decimal PrixDefaut { get; set; }
    public int DureeMinutes { get; set; }
    public bool Actif { get; set; } = true;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();
}
