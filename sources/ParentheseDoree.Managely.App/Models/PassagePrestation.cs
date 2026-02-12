namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Prestation effectuée lors d'un passage (stockée en JSON dans la cellule prestations_json).
/// </summary>
public class PassagePrestation
{
    public string PrestationId { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public decimal Prix { get; set; }
    public int DureeMinutes { get; set; }
}
