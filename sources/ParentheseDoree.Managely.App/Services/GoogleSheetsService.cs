using Microsoft.JSInterop;
using ParentheseDoree.Managely.App.Models;

namespace ParentheseDoree.Managely.App.Services;

/// <summary>
/// Service simplifié pour les opérations CRUD sur Google Sheets.
/// Pas de système de cache - appels directs à l'API.
/// </summary>
public sealed class GoogleSheetsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly GoogleAuthService _authService;
    private readonly BrowserLoggerService _logger;

    public GoogleSheetsService(
        IJSRuntime jsRuntime, 
        GoogleAuthService authService,
        BrowserLoggerService logger)
    {
        _jsRuntime = jsRuntime;
        _authService = authService;
        _logger = logger;
    }

    // =========================================================================
    // CLIENTS
    // =========================================================================

    /// <summary>
    /// Récupère tous les clients depuis le Spreadsheet.
    /// </summary>
    public async Task<List<Client>> GetClientsAsync()
    {
        try
        {
            await _logger.InfoAsync(LogCategory.API, "Chargement des clients...");
            
            var result = await _jsRuntime.InvokeAsync<ClientJs[]>("googleSheetsApi.getClients");
            
            var clients = result?.Select(c => new Client
            {
                RowIndex = c.RowIndex,
                Id = c.Id ?? string.Empty,
                Nom = c.Nom ?? string.Empty,
                Prenom = c.Prenom ?? string.Empty,
                Telephone = c.Telephone ?? string.Empty,
                Email = c.Email ?? string.Empty,
                DateNaissance = c.DateNaissance ?? string.Empty,
                Adresse = c.Adresse ?? string.Empty,
                Notes = c.Notes ?? string.Empty,
                DateCreation = c.DateCreation ?? string.Empty
            }).ToList() ?? [];

            await _logger.SuccessAsync(LogCategory.API, $"{clients.Count} client(s) chargé(s)");
            return clients;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur chargement clients", new { error = ex.Message });
            throw new InvalidOperationException("Impossible de récupérer les clients", ex);
        }
    }

    /// <summary>
    /// Ajoute un nouveau client.
    /// </summary>
    public async Task<string> AddClientAsync(Client client)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Vous n'avez pas les droits d'écriture");

        try
        {
            await _logger.InfoAsync(LogCategory.API, "Ajout d'un client...");
            
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>("googleSheetsApi.addClient", new
            {
                nom = client.Nom,
                prenom = client.Prenom,
                telephone = client.Telephone,
                email = client.Email,
                dateNaissance = client.DateNaissance,
                adresse = client.Adresse,
                notes = client.Notes
            });

            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Échec de l'ajout du client");

            await _logger.SuccessAsync(LogCategory.API, $"Client ajouté: {client.NomComplet}");
            return result.Id ?? string.Empty;
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur ajout client", new { error = ex.Message });
            throw new InvalidOperationException("Impossible d'ajouter le client", ex);
        }
    }

    /// <summary>
    /// Met à jour un client existant.
    /// </summary>
    public async Task UpdateClientAsync(Client client)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Vous n'avez pas les droits d'écriture");

        try
        {
            await _logger.InfoAsync(LogCategory.API, $"Modification du client {client.NomComplet}...");
            
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>("googleSheetsApi.updateClient", new
            {
                rowIndex = client.RowIndex,
                id = client.Id,
                nom = client.Nom,
                prenom = client.Prenom,
                telephone = client.Telephone,
                email = client.Email,
                dateNaissance = client.DateNaissance,
                adresse = client.Adresse,
                notes = client.Notes,
                dateCreation = client.DateCreation
            });

            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Échec de la mise à jour du client");

            await _logger.SuccessAsync(LogCategory.API, $"Client modifié: {client.NomComplet}");
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur modification client", new { error = ex.Message });
            throw new InvalidOperationException("Impossible de modifier le client", ex);
        }
    }

    /// <summary>
    /// Supprime un client.
    /// </summary>
    public async Task DeleteClientAsync(int rowIndex)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Vous n'avez pas les droits d'écriture");

        try
        {
            await _logger.InfoAsync(LogCategory.API, $"Suppression du client ligne {rowIndex}...");
            
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>("googleSheetsApi.deleteClient", rowIndex);

            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Échec de la suppression du client");

            await _logger.SuccessAsync(LogCategory.API, "Client supprimé");
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur suppression client", new { error = ex.Message });
            throw new InvalidOperationException("Impossible de supprimer le client", ex);
        }
    }

    // =========================================================================
    // PRESTATIONS
    // =========================================================================

    /// <summary>
    /// Récupère toutes les prestations depuis le Spreadsheet.
    /// </summary>
    public async Task<List<Prestation>> GetPrestationsAsync()
    {
        try
        {
            await _logger.InfoAsync(LogCategory.API, "Chargement des prestations...");
            
            var result = await _jsRuntime.InvokeAsync<PrestationJs[]>("googleSheetsApi.getPrestations");
            
            var prestations = result?.Select(p => new Prestation
            {
                RowIndex = p.RowIndex,
                Id = p.Id ?? string.Empty,
                ClientId = p.ClientId ?? string.Empty,
                ClientNom = p.ClientNom ?? string.Empty,
                Date = p.Date ?? string.Empty,
                TypePrestation = p.TypePrestation ?? string.Empty,
                Description = p.Description ?? string.Empty,
                Prix = p.Prix,
                DureeMinutes = p.DureeMinutes,
                ModePaiement = p.ModePaiement ?? string.Empty,
                EstPayee = p.EstPayee,
                Notes = p.Notes ?? string.Empty
            }).ToList() ?? [];

            await _logger.SuccessAsync(LogCategory.API, $"{prestations.Count} prestation(s) chargée(s)");
            return prestations;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur chargement prestations", new { error = ex.Message });
            throw new InvalidOperationException("Impossible de récupérer les prestations", ex);
        }
    }

    /// <summary>
    /// Ajoute une nouvelle prestation.
    /// </summary>
    public async Task<string> AddPrestationAsync(Prestation prestation)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Vous n'avez pas les droits d'écriture");

        try
        {
            await _logger.InfoAsync(LogCategory.API, "Ajout d'une prestation...");
            
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>("googleSheetsApi.addPrestation", new
            {
                clientId = prestation.ClientId,
                clientNom = prestation.ClientNom,
                date = prestation.Date,
                typePrestation = prestation.TypePrestation,
                description = prestation.Description,
                prix = prestation.Prix,
                dureeMinutes = prestation.DureeMinutes,
                modePaiement = prestation.ModePaiement,
                estPayee = prestation.EstPayee,
                notes = prestation.Notes
            });

            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Échec de l'ajout de la prestation");

            await _logger.SuccessAsync(LogCategory.API, $"Prestation ajoutée: {prestation.TypePrestation}");
            return result.Id ?? string.Empty;
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur ajout prestation", new { error = ex.Message });
            throw new InvalidOperationException("Impossible d'ajouter la prestation", ex);
        }
    }

    /// <summary>
    /// Met à jour une prestation existante.
    /// </summary>
    public async Task UpdatePrestationAsync(Prestation prestation)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Vous n'avez pas les droits d'écriture");

        try
        {
            await _logger.InfoAsync(LogCategory.API, $"Modification de la prestation...");
            
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>("googleSheetsApi.updatePrestation", new
            {
                rowIndex = prestation.RowIndex,
                id = prestation.Id,
                clientId = prestation.ClientId,
                clientNom = prestation.ClientNom,
                date = prestation.Date,
                typePrestation = prestation.TypePrestation,
                description = prestation.Description,
                prix = prestation.Prix,
                dureeMinutes = prestation.DureeMinutes,
                modePaiement = prestation.ModePaiement,
                estPayee = prestation.EstPayee,
                notes = prestation.Notes
            });

            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Échec de la mise à jour de la prestation");

            await _logger.SuccessAsync(LogCategory.API, $"Prestation modifiée: {prestation.TypePrestation}");
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur modification prestation", new { error = ex.Message });
            throw new InvalidOperationException("Impossible de modifier la prestation", ex);
        }
    }

    /// <summary>
    /// Supprime une prestation.
    /// </summary>
    public async Task DeletePrestationAsync(int rowIndex)
    {
        if (!_authService.CanWrite)
            throw new UnauthorizedAccessException("Vous n'avez pas les droits d'écriture");

        try
        {
            await _logger.InfoAsync(LogCategory.API, $"Suppression de la prestation ligne {rowIndex}...");
            
            var result = await _jsRuntime.InvokeAsync<OperationResultJs>("googleSheetsApi.deletePrestation", rowIndex);

            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Échec de la suppression de la prestation");

            await _logger.SuccessAsync(LogCategory.API, "Prestation supprimée");
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(LogCategory.API, "Erreur suppression prestation", new { error = ex.Message });
            throw new InvalidOperationException("Impossible de supprimer la prestation", ex);
        }
    }

    // =========================================================================
    // CLASSES INTERNES POUR LA DÉSÉRIALISATION
    // =========================================================================

    private class ClientJs
    {
        public int RowIndex { get; set; }
        public string? Id { get; set; }
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public string? DateNaissance { get; set; }
        public string? Adresse { get; set; }
        public string? Notes { get; set; }
        public string? DateCreation { get; set; }
    }

    private class PrestationJs
    {
        public int RowIndex { get; set; }
        public string? Id { get; set; }
        public string? ClientId { get; set; }
        public string? ClientNom { get; set; }
        public string? Date { get; set; }
        public string? TypePrestation { get; set; }
        public string? Description { get; set; }
        public decimal Prix { get; set; }
        public int DureeMinutes { get; set; }
        public string? ModePaiement { get; set; }
        public bool EstPayee { get; set; }
        public string? Notes { get; set; }
    }

    private class OperationResultJs
    {
        public bool Success { get; set; }
        public string? Id { get; set; }
        public string? Error { get; set; }
    }
}
