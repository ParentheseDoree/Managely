using System.Security.Cryptography;
using System.Text;

namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un client du centre esthétique.
/// Correspond à une ligne dans la feuille "Clients" du Google Spreadsheet.
/// </summary>
public class Client
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string MoisAnniversaire { get; set; } = string.Empty;
    public string NumeroTelephone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Adresse { get; set; } = string.Empty;
    public string DateCreation { get; set; } = string.Empty;
    public string DateModification { get; set; } = string.Empty;
    public string HachageIntegrite { get; set; } = string.Empty;

    public string NomComplet => $"{Prenom} {Nom}".Trim();

    /// <summary>
    /// Génère un nouveau GUID pour le client.
    /// </summary>
    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();

    /// <summary>
    /// Calcule le hachage d'intégrité basé sur les données du client.
    /// </summary>
    public string CalculerHachage()
    {
        var data = $"{Nom}|{Prenom}|{MoisAnniversaire}|{NumeroTelephone}|{Email}|{Adresse}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes)[..16];
    }

    /// <summary>
    /// Met à jour le hachage d'intégrité et la date de modification.
    /// </summary>
    public void MettreAJourIntegrite()
    {
        DateModification = DateTime.UtcNow.ToString("o");
        HachageIntegrite = CalculerHachage();
    }

    /// <summary>
    /// Vérifie si les données ont été modifiées depuis le dernier hachage.
    /// Retourne true si les données sont intègres (pas de conflit).
    /// </summary>
    public bool VerifierIntegrite()
    {
        if (string.IsNullOrEmpty(HachageIntegrite)) return true;
        return CalculerHachage() == HachageIntegrite;
    }
}
