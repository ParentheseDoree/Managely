namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Types de toast disponibles.
/// </summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Représente un message toast à afficher.
/// </summary>
public sealed class ToastMessage
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public ToastType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; } = DateTime.Now;
    public int DurationMs { get; set; } = 5000;
}

/// <summary>
/// Service de gestion des toasts Bootstrap.
/// Permet d'afficher des notifications temporaires depuis n'importe quelle page.
/// </summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _toasts = [];

    /// <summary>Événement déclenché quand un toast est ajouté ou retiré.</summary>
    public event Action? OnChange;

    /// <summary>Liste des toasts actifs.</summary>
    public IReadOnlyList<ToastMessage> Toasts => _toasts.AsReadOnly();

    public void ShowSuccess(string message, string title = "Succès")
        => Show(ToastType.Success, message, title);

    public void ShowError(string message, string title = "Erreur")
        => Show(ToastType.Error, message, title, 8000);

    public void ShowWarning(string message, string title = "Attention")
        => Show(ToastType.Warning, message, title, 6000);

    public void ShowInfo(string message, string title = "Information")
        => Show(ToastType.Info, message, title);

    private void Show(ToastType type, string message, string title, int durationMs = 5000)
    {
        var toast = new ToastMessage
        {
            Type = type,
            Message = message,
            Title = title,
            DurationMs = durationMs
        };
        _toasts.Add(toast);
        OnChange?.Invoke();
    }

    /// <summary>Retire un toast par son ID.</summary>
    public void Remove(string id)
    {
        _toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }
}
