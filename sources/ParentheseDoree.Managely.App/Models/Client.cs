namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un client du centre esthétique.
/// Correspond à une ligne dans la feuille "Clients" du Google Spreadsheet.
/// </summary>
public class Client
{
    /// <summary>
    /// Numéro de ligne dans le Spreadsheet (1-based, hors en-tête).
    /// Utilisé pour les opérations de mise à jour et suppression.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Identifiant unique du client.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Nom de famille du client.
    /// </summary>
    public string Nom { get; set; } = string.Empty;

    /// <summary>
    /// Prénom du client.
    /// </summary>
    public string Prenom { get; set; } = string.Empty;

    /// <summary>
    /// Numéro de téléphone du client.
    /// </summary>
    public string Telephone { get; set; } = string.Empty;

    /// <summary>
    /// Adresse email du client.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Date de naissance du client.
    /// </summary>
    public string DateNaissance { get; set; } = string.Empty;

    /// <summary>
    /// Adresse postale du client.
    /// </summary>
    public string Adresse { get; set; } = string.Empty;

    /// <summary>
    /// Notes ou commentaires sur le client.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Date de création de la fiche client.
    /// </summary>
    public string DateCreation { get; set; } = string.Empty;

    /// <summary>
    /// Retourne le nom complet du client.
    /// </summary>
    public string NomComplet => $"{Prenom} {Nom}".Trim();
}
