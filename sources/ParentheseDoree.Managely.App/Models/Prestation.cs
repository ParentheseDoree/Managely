namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente une prestation ou un passage effectué pour un client.
/// Correspond à une ligne dans la feuille "Passages" du Google Spreadsheet.
/// </summary>
public class Prestation
{
    /// <summary>
    /// Numéro de ligne dans le Spreadsheet (1-based, hors en-tête).
    /// Utilisé pour les opérations de mise à jour et suppression.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Identifiant unique de la prestation.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant du client associé.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Nom du client (pour affichage).
    /// </summary>
    public string ClientNom { get; set; } = string.Empty;

    /// <summary>
    /// Date de la prestation.
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// Type ou nom de la prestation/soin réalisé.
    /// </summary>
    public string TypePrestation { get; set; } = string.Empty;

    /// <summary>
    /// Description détaillée des soins réalisés.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Prix de la prestation en euros.
    /// </summary>
    public decimal Prix { get; set; }

    /// <summary>
    /// Durée de la prestation en minutes.
    /// </summary>
    public int DureeMinutes { get; set; }

    /// <summary>
    /// Mode de paiement utilisé (CB, Espèces, Chèque, etc.).
    /// </summary>
    public string ModePaiement { get; set; } = string.Empty;

    /// <summary>
    /// Indique si la prestation a été payée.
    /// </summary>
    public bool EstPayee { get; set; }

    /// <summary>
    /// Notes ou commentaires sur la prestation.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}
