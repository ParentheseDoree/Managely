using System.Globalization;
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
    /// Applique le formatage automatique sur Nom, Prénom et Téléphone.
    /// - Nom : MAJUSCULES (NOM-NOM pour composés)
    /// - Prénom : Première lettre majuscule (Prenom-Prenom pour composés)
    /// - Téléphone : XX XX XX XX XX
    /// </summary>
    public void AppliquerFormatage()
    {
        Nom = FormaterNom(Nom);
        Prenom = FormaterPrenom(Prenom);
        NumeroTelephone = FormaterTelephone(NumeroTelephone);
    }

    /// <summary>NOM en MAJUSCULES, tirets sans espaces.</summary>
    private static string FormaterNom(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return nom;
        nom = nom.Trim();
        // Séparer par tiret, mettre chaque partie en majuscules, rejoindre
        var parts = nom.Split(['-', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Join("-", parts.Select(p => p.ToUpper()));
    }

    /// <summary>Prénom avec majuscule initiale, tirets sans espaces pour composés.</summary>
    private static string FormaterPrenom(string prenom)
    {
        if (string.IsNullOrWhiteSpace(prenom)) return prenom;
        prenom = prenom.Trim();
        var parts = prenom.Split(['-', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Join("-", parts.Select(p =>
            p.Length <= 1 ? p.ToUpper() : char.ToUpper(p[0]) + p[1..].ToLower()));
    }

    /// <summary>Téléphone formaté en XX XX XX XX XX.</summary>
    private static string FormaterTelephone(string tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return tel;
        // Ne garder que les chiffres
        var digits = new string(tel.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return tel;
        // Gérer le +33
        if (digits.StartsWith("33") && digits.Length == 11)
            digits = "0" + digits[2..];
        // Formater par paires
        if (digits.Length == 10)
            return $"{digits[..2]} {digits[2..4]} {digits[4..6]} {digits[6..8]} {digits[8..10]}";
        return tel.Trim(); // Retourner tel quel si longueur inattendue
    }

    /// <summary>Supprime les accents d'une chaîne (pour la recherche).</summary>
    public static string SupprimerAccents(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (UnicodeCategory.NonSpacingMark != CharUnicodeInfo.GetUnicodeCategory(c))
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

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
