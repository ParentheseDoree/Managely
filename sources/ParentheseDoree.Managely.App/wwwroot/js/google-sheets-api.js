/**
 * =============================================================================
 * google-sheets-api.js - API CRUD complète pour Google Sheets
 * =============================================================================
 */

const googleSheetsApi = (() => {
    // Configuration
    let config = {
        spreadsheetId: null,
        sheets: {
            clients: 'Clients',
            prestations: 'Passages'
        },
        isInitialized: false
    };

    // Promesse d'initialisation pour éviter les appels multiples
    let initPromise = null;

    // Logging via browserLogger
    const log = (level, msg, data) => {
        if (window.browserLogger) {
            window.browserLogger[level]("API", msg, data);
        } else {
            const fn = level === 'error' ? console.error : 
                       level === 'warn' ? console.warn : 
                       level === 'success' ? console.log : console.info;
            fn(`[API] ${msg}`, data);
        }
    };

    const logInfo = (msg, data) => log('info', msg, data);
    const logSuccess = (msg, data) => log('success', msg, data);
    const logWarn = (msg, data) => log('warn', msg, data);
    const logError = (msg, data) => log('error', msg, data);

    /**
     * Attendre que GAPI client soit disponible
     */
    const waitForGapiClient = () => {
        return new Promise((resolve, reject) => {
            const maxAttempts = 100; // 10 secondes max
            let attempts = 0;

            const check = () => {
                if (typeof gapi !== 'undefined' && gapi.client) {
                    resolve();
                } else if (attempts >= maxAttempts) {
                    reject(new Error("GAPI client non disponible après 10s"));
                } else {
                    attempts++;
                    setTimeout(check, 100);
                }
            };
            check();
        });
    };

    /**
     * Attendre que GAPI Sheets soit disponible
     */
    const waitForGapiSheets = () => {
        return new Promise((resolve, reject) => {
            const maxAttempts = 100; // 10 secondes max
            let attempts = 0;

            const check = () => {
                if (typeof gapi !== 'undefined' && gapi.client && gapi.client.sheets) {
                    resolve();
                } else if (attempts >= maxAttempts) {
                    reject(new Error("GAPI Sheets non disponible après 10s"));
                } else {
                    attempts++;
                    setTimeout(check, 100);
                }
            };
            check();
        });
    };

    /**
     * Configure le token d'accès pour les requêtes
     */
    const setAccessToken = () => {
        const token = window.googleAuthApi?.getAccessToken();
        if (token && gapi.client) {
            gapi.client.setToken({ access_token: token });
            return true;
        }
        logWarn("Token d'accès non disponible");
        return false;
    };

    /**
     * Initialise l'API avec l'ID du spreadsheet
     */
    const initialize = async (spreadsheetId, sheetsConfig = null) => {
        // Si déjà initialisé avec le même ID, retourner immédiatement
        if (config.isInitialized && config.spreadsheetId === spreadsheetId) {
            return true;
        }

        // Si une initialisation est en cours, attendre sa fin
        if (initPromise) {
            return initPromise;
        }

        config.spreadsheetId = spreadsheetId;
        if (sheetsConfig) {
            config.sheets = { ...config.sheets, ...sheetsConfig };
        }

        // Créer la promesse d'initialisation
        initPromise = (async () => {
            try {
                logInfo("Initialisation de l'API Sheets...");
                
                // Attendre que gapi.client soit disponible
                await waitForGapiClient();
                
                // Charger l'API Sheets si pas déjà fait
                if (!gapi.client.sheets) {
                    logInfo("Chargement de l'API Google Sheets...");
                    await gapi.client.load('sheets', 'v4');
                }
                
                // Attendre que gapi.client.sheets soit vraiment disponible
                await waitForGapiSheets();
                
                config.isInitialized = true;
                logSuccess("API Sheets initialisée");
                return true;
            } catch (error) {
                logError("Erreur d'initialisation Sheets", { message: error.message || error });
                initPromise = null; // Permettre de réessayer
                return false;
            }
        })();

        return initPromise;
    };

    /**
     * S'assure que l'API est prête avant d'exécuter une opération
     */
    const ensureReady = async () => {
        // Si pas encore initialisé, essayer d'initialiser
        if (!config.isInitialized) {
            // Récupérer le spreadsheetId depuis la config globale si pas défini
            if (!config.spreadsheetId && window.MANAGELY_CONFIG?.SPREADSHEET_ID) {
                config.spreadsheetId = window.MANAGELY_CONFIG.SPREADSHEET_ID;
            }

            if (!config.spreadsheetId) {
                throw new Error("API non configurée. Spreadsheet ID manquant.");
            }

            const success = await initialize(config.spreadsheetId);
            if (!success) {
                throw new Error("Impossible d'initialiser l'API Sheets");
            }
        }

        // Attendre que gapi.client.sheets soit vraiment disponible
        try {
            await waitForGapiSheets();
        } catch (error) {
            logError("L'API Google Sheets n'est pas disponible", { message: error.message });
            throw new Error("L'API Google Sheets n'est pas disponible. Rechargez la page.");
        }

        // Vérifier le token
        if (!setAccessToken()) {
            throw new Error("Token d'accès non disponible. Veuillez vous reconnecter.");
        }

        return true;
    };

    // =========================================================================
    // CLIENTS
    // =========================================================================

    const getClients = async () => {
        await ensureReady();

        try {
            logInfo("Chargement des clients...");
            
            const response = await gapi.client.sheets.spreadsheets.values.get({
                spreadsheetId: config.spreadsheetId,
                range: `${config.sheets.clients}!A:I`
            });

            const rows = response.result.values || [];
            
            if (rows.length <= 1) {
                logInfo("Aucun client trouvé");
                return [];
            }

            const clients = rows.slice(1).map((row, index) => ({
                rowIndex: index + 2,
                id: row[0] || '',
                nom: row[1] || '',
                prenom: row[2] || '',
                telephone: row[3] || '',
                email: row[4] || '',
                dateNaissance: row[5] || '',
                adresse: row[6] || '',
                notes: row[7] || '',
                dateCreation: row[8] || ''
            }));
            
            logSuccess(`${clients.length} client(s) chargé(s)`);
            return clients;
        } catch (error) {
            logError("Erreur chargement clients", { message: error.message || error });
            throw error;
        }
    };

    const ensureClientsHeader = async () => {
        try {
            const response = await gapi.client.sheets.spreadsheets.values.get({
                spreadsheetId: config.spreadsheetId,
                range: `${config.sheets.clients}!A1:I1`
            });
            
            const firstRow = response.result.values ? response.result.values[0] : null;
            
            if (!firstRow || firstRow[0] !== 'ID') {
                logInfo("Création de l'en-tête Clients...");
                await gapi.client.sheets.spreadsheets.values.update({
                    spreadsheetId: config.spreadsheetId,
                    range: `${config.sheets.clients}!A1:I1`,
                    valueInputOption: 'RAW',
                    resource: {
                        values: [['ID', 'Nom', 'Prénom', 'Téléphone', 'Email', 'DateNaissance', 'Adresse', 'Notes', 'DateCreation']]
                    }
                });
                logSuccess("En-tête Clients créé");
            }
        } catch (error) {
            logError("Erreur création en-tête Clients", { message: error.message || error });
        }
    };

    const addClient = async (client) => {
        await ensureReady();

        try {
            await ensureClientsHeader();
            
            const newId = 'CLI-' + Date.now();
            const dateCreation = new Date().toLocaleDateString('fr-FR');

            const values = [[
                newId,
                client.nom || '',
                client.prenom || '',
                client.telephone || '',
                client.email || '',
                client.dateNaissance || '',
                client.adresse || '',
                client.notes || '',
                dateCreation
            ]];

            await gapi.client.sheets.spreadsheets.values.append({
                spreadsheetId: config.spreadsheetId,
                range: `${config.sheets.clients}!A:I`,
                valueInputOption: 'RAW',
                insertDataOption: 'INSERT_ROWS',
                resource: { values }
            });

            logSuccess(`Client ajouté: ${client.prenom} ${client.nom}`, { id: newId });
            return { success: true, id: newId };
        } catch (error) {
            logError("Erreur ajout client", { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    const updateClient = async (client) => {
        await ensureReady();

        try {
            const range = `${config.sheets.clients}!A${client.rowIndex}:I${client.rowIndex}`;
            const values = [[
                client.id,
                client.nom || '',
                client.prenom || '',
                client.telephone || '',
                client.email || '',
                client.dateNaissance || '',
                client.adresse || '',
                client.notes || '',
                client.dateCreation || ''
            ]];

            await gapi.client.sheets.spreadsheets.values.update({
                spreadsheetId: config.spreadsheetId,
                range,
                valueInputOption: 'RAW',
                resource: { values }
            });

            logSuccess(`Client modifié: ${client.prenom} ${client.nom}`);
            return { success: true };
        } catch (error) {
            logError("Erreur modification client", { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    const deleteClient = async (rowIndex) => {
        await ensureReady();

        try {
            logInfo(`Suppression client ligne ${rowIndex}...`);
            
            const spreadsheet = await gapi.client.sheets.spreadsheets.get({
                spreadsheetId: config.spreadsheetId
            });

            const sheet = spreadsheet.result.sheets.find(s => 
                s.properties.title === config.sheets.clients
            );

            if (!sheet) throw new Error("Feuille Clients non trouvée");

            await gapi.client.sheets.spreadsheets.batchUpdate({
                spreadsheetId: config.spreadsheetId,
                resource: {
                    requests: [{
                        deleteDimension: {
                            range: {
                                sheetId: sheet.properties.sheetId,
                                dimension: 'ROWS',
                                startIndex: rowIndex - 1,
                                endIndex: rowIndex
                            }
                        }
                    }]
                }
            });

            logSuccess("Client supprimé");
            return { success: true };
        } catch (error) {
            logError("Erreur suppression client", { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    // =========================================================================
    // PRESTATIONS
    // =========================================================================

    const getPrestations = async () => {
        await ensureReady();

        try {
            logInfo("Chargement des prestations...");
            
            const response = await gapi.client.sheets.spreadsheets.values.get({
                spreadsheetId: config.spreadsheetId,
                range: `${config.sheets.prestations}!A:K`
            });

            const rows = response.result.values || [];
            
            if (rows.length <= 1) {
                logInfo("Aucune prestation trouvée");
                return [];
            }

            const prestations = rows.slice(1).map((row, index) => ({
                rowIndex: index + 2,
                id: row[0] || '',
                clientId: row[1] || '',
                clientNom: row[2] || '',
                date: row[3] || '',
                typePrestation: row[4] || '',
                description: row[5] || '',
                prix: parseFloat(row[6]) || 0,
                dureeMinutes: parseInt(row[7]) || 0,
                modePaiement: row[8] || '',
                estPayee: row[9] === 'true' || row[9] === 'Oui' || row[9] === '1',
                notes: row[10] || ''
            }));
            
            logSuccess(`${prestations.length} prestation(s) chargée(s)`);
            return prestations;
        } catch (error) {
            logError("Erreur chargement prestations", { message: error.message || error });
            throw error;
        }
    };

    const ensurePrestationsHeader = async () => {
        try {
            const response = await gapi.client.sheets.spreadsheets.values.get({
                spreadsheetId: config.spreadsheetId,
                range: `${config.sheets.prestations}!A1:K1`
            });
            
            const firstRow = response.result.values ? response.result.values[0] : null;
            
            if (!firstRow || firstRow[0] !== 'ID') {
                logInfo("Création de l'en-tête Prestations...");
                await gapi.client.sheets.spreadsheets.values.update({
                    spreadsheetId: config.spreadsheetId,
                    range: `${config.sheets.prestations}!A1:K1`,
                    valueInputOption: 'RAW',
                    resource: {
                        values: [['ID', 'ClientID', 'ClientNom', 'Date', 'TypePrestation', 'Description', 'Prix', 'DureeMinutes', 'ModePaiement', 'EstPayee', 'Notes']]
                    }
                });
                logSuccess("En-tête Prestations créé");
            }
        } catch (error) {
            logError("Erreur création en-tête Prestations", { message: error.message || error });
        }
    };

    const addPrestation = async (prestation) => {
        await ensureReady();

        try {
            await ensurePrestationsHeader();
            
            const newId = 'PRE-' + Date.now();

            const values = [[
                newId,
                prestation.clientId || '',
                prestation.clientNom || '',
                prestation.date || new Date().toLocaleDateString('fr-FR'),
                prestation.typePrestation || '',
                prestation.description || '',
                prestation.prix || 0,
                prestation.dureeMinutes || 0,
                prestation.modePaiement || '',
                prestation.estPayee ? 'Oui' : 'Non',
                prestation.notes || ''
            ]];

            await gapi.client.sheets.spreadsheets.values.append({
                spreadsheetId: config.spreadsheetId,
                range: `${config.sheets.prestations}!A:K`,
                valueInputOption: 'RAW',
                insertDataOption: 'INSERT_ROWS',
                resource: { values }
            });

            logSuccess(`Prestation ajoutée: ${prestation.typePrestation}`, { id: newId });
            return { success: true, id: newId };
        } catch (error) {
            logError("Erreur ajout prestation", { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    const updatePrestation = async (prestation) => {
        await ensureReady();

        try {
            const range = `${config.sheets.prestations}!A${prestation.rowIndex}:K${prestation.rowIndex}`;
            const values = [[
                prestation.id,
                prestation.clientId || '',
                prestation.clientNom || '',
                prestation.date || '',
                prestation.typePrestation || '',
                prestation.description || '',
                prestation.prix || 0,
                prestation.dureeMinutes || 0,
                prestation.modePaiement || '',
                prestation.estPayee ? 'Oui' : 'Non',
                prestation.notes || ''
            ]];

            await gapi.client.sheets.spreadsheets.values.update({
                spreadsheetId: config.spreadsheetId,
                range,
                valueInputOption: 'RAW',
                resource: { values }
            });

            logSuccess(`Prestation modifiée: ${prestation.typePrestation}`);
            return { success: true };
        } catch (error) {
            logError("Erreur modification prestation", { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    const deletePrestation = async (rowIndex) => {
        await ensureReady();

        try {
            logInfo(`Suppression prestation ligne ${rowIndex}...`);
            
            const spreadsheet = await gapi.client.sheets.spreadsheets.get({
                spreadsheetId: config.spreadsheetId
            });

            const sheet = spreadsheet.result.sheets.find(s => 
                s.properties.title === config.sheets.prestations
            );

            if (!sheet) throw new Error("Feuille Passages non trouvée");

            await gapi.client.sheets.spreadsheets.batchUpdate({
                spreadsheetId: config.spreadsheetId,
                resource: {
                    requests: [{
                        deleteDimension: {
                            range: {
                                sheetId: sheet.properties.sheetId,
                                dimension: 'ROWS',
                                startIndex: rowIndex - 1,
                                endIndex: rowIndex
                            }
                        }
                    }]
                }
            });

            logSuccess("Prestation supprimée");
            return { success: true };
        } catch (error) {
            logError("Erreur suppression prestation", { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    /**
     * Vérifie si l'API est initialisée
     */
    const isInitialized = () => config.isInitialized;

    return Object.freeze({
        initialize,
        isInitialized,
        getClients,
        addClient,
        updateClient,
        deleteClient,
        getPrestations,
        addPrestation,
        updatePrestation,
        deletePrestation
    });
})();

Object.defineProperty(window, 'googleSheetsApi', {
    value: googleSheetsApi,
    configurable: false,
    writable: false
});
