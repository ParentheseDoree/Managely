using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Représente un passage (visite) complet d'un client au centre.
/// Contient les prestations réalisées, produits vendus/conseillés et notes.
/// </summary>
public class Passage
{
    public int RowIndex { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string ClientGuid { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;

    /// <summary>Prestations réalisées (sérialisées en JSON).</summary>
    public List<PassagePrestation> Prestations { get; set; } = [];

    /// <summary>Produits vendus lors du passage (sérialisés en JSON).</summary>
    public List<PassageProduitVendu> ProduitsVendus { get; set; } = [];

    /// <summary>Produits conseillés mais non vendus (sérialisés en JSON).</summary>
    public List<PassageProduitConseille> ProduitsConseilles { get; set; } = [];

    public string NoteInterne { get; set; } = string.Empty;
    public decimal Total { get; set; }

    /// <summary>Détail des paiements (sérialisé en JSON pour multi-paiement).</summary>
    public List<PaiementDetail> Paiements { get; set; } = [];

    /// <summary>Mode de paiement principal (rétrocompatibilité).</summary>
    public string ModePaiement { get; set; } = string.Empty;
    public string CarteCadeauGuid { get; set; } = string.Empty;
    public decimal MontantCarteUtilisee { get; set; }

    /// <summary>Hachage d'intégrité pour la détection de conflits.</summary>
    public string HachageIntegrite { get; set; } = string.Empty;

    // Champs calculés / affichage
    public string ClientNom { get; set; } = string.Empty;

    public void GenererGuid() => Guid = System.Guid.NewGuid().ToString();

    /// <summary>Recalcule le total du passage.</summary>
    public void RecalculerTotal()
    {
        Total = Prestations.Sum(p => p.Prix)
              + ProduitsVendus.Sum(p => p.Total);
    }

    /// <summary>Résumé textuel des prestations.</summary>
    public string ResumePrestations =>
        Prestations.Count == 0
            ? "Aucune prestation"
            : string.Join(", ", Prestations.Select(p => p.Nom));

    /// <summary>Résumé des modes de paiement.</summary>
    public string ResumePaiements
    {
        get
        {
            if (Paiements.Count > 0)
                return string.Join(" + ", Paiements.Select(p => $"{p.Montant:N2}€ {p.Mode}"));
            return string.IsNullOrEmpty(ModePaiement) ? "—" : ModePaiement;
        }
    }

    /// <summary>Calcule le hachage d'intégrité.</summary>
    public string CalculerHachage()
    {
        var data = $"{ClientGuid}|{Date}|{Total}|{ModePaiement}|{NoteInterne}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes)[..16];
    }

    public void MettreAJourIntegrite()
    {
        HachageIntegrite = CalculerHachage();
    }

    public bool VerifierIntegrite()
    {
        if (string.IsNullOrEmpty(HachageIntegrite)) return true;
        return CalculerHachage() == HachageIntegrite;
    }
}

/// <summary>
/// Détail d'un paiement (support multi-paiement).
/// </summary>
public class PaiementDetail
{
    public string Mode { get; set; } = string.Empty;
    public decimal Montant { get; set; }
    public string CarteCadeauGuid { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}
