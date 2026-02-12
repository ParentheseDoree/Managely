namespace ParentheseDoree.Managely.App.Models;

/// <summary>
/// Paire clé-valeur pour la configuration du centre.
/// </summary>
public class Parametres
{
    public int RowIndex { get; set; }
    public string Cle { get; set; } = string.Empty;
    public string Valeur { get; set; } = string.Empty;
}
