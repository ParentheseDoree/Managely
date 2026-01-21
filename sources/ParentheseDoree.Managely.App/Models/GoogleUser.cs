namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente les informations d'un utilisateur connecté via Google OAuth.
/// </summary>
public sealed class GoogleUser
{
    /// <summary>
    /// Identifiant unique Google de l'utilisateur (sub claim).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Adresse email de l'utilisateur.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nom complet de l'utilisateur.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Prénom de l'utilisateur.
    /// </summary>
    public string GivenName { get; set; } = string.Empty;

    /// <summary>
    /// Nom de famille de l'utilisateur.
    /// </summary>
    public string FamilyName { get; set; } = string.Empty;

    /// <summary>
    /// URL de la photo de profil de l'utilisateur.
    /// </summary>
    public string Picture { get; set; } = string.Empty;

    /// <summary>
    /// Indique si l'email de l'utilisateur est vérifié.
    /// </summary>
    public bool EmailVerified { get; set; }
}
