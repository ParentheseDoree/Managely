/**
 * google-sheets-api.js - API CRUD générique pour Google Sheets
 * Supporte les opérations batch et la gestion automatique des feuilles.
 */
const googleSheetsApi = (() => {
    let config = {
        spreadsheetId: null,
        isInitialized: false
    };

    let initPromise = null;

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
    const logError = (msg, data) => log('error', msg, data);

    const waitForGapiSheets = () => {
        return new Promise((resolve, reject) => {
            let attempts = 0;
            const check = () => {
                if (typeof gapi !== 'undefined' && gapi.client && gapi.client.sheets) {
                    resolve();
                } else if (attempts >= 100) {
                    reject(new Error("GAPI Sheets non disponible"));
                } else {
                    attempts++;
                    setTimeout(check, 100);
                }
            };
            check();
        });
    };

    const setAccessToken = () => {
        const token = window.googleAuthApi?.getAccessToken();
        if (token && gapi.client) {
            gapi.client.setToken({ access_token: token });
            return true;
        }
        return false;
    };

    const initialize = async (spreadsheetId) => {
        if (config.isInitialized && config.spreadsheetId === spreadsheetId) return true;
        if (initPromise) return initPromise;

        config.spreadsheetId = spreadsheetId;

        initPromise = (async () => {
            try {
                logInfo("Initialisation API Sheets...");
                await waitForGapiClient();
                if (!gapi.client.sheets) {
                    await gapi.client.load('sheets', 'v4');
                }
                await waitForGapiSheets();
                config.isInitialized = true;
                logSuccess("API Sheets initialisée");
                return true;
            } catch (error) {
                logError("Erreur init Sheets", { message: error.message });
                initPromise = null;
                return false;
            }
        })();

        return initPromise;
    };

    const waitForGapiClient = () => {
        return new Promise((resolve, reject) => {
            let attempts = 0;
            const check = () => {
                if (typeof gapi !== 'undefined' && gapi.client) resolve();
                else if (attempts >= 100) reject(new Error("GAPI client non disponible"));
                else { attempts++; setTimeout(check, 100); }
            };
            check();
        });
    };

    const ensureReady = async () => {
        if (!config.isInitialized) {
            if (!config.spreadsheetId && window.MANAGELY_CONFIG?.SPREADSHEET_ID) {
                config.spreadsheetId = window.MANAGELY_CONFIG.SPREADSHEET_ID;
            }
            if (!config.spreadsheetId) throw new Error("Spreadsheet ID manquant");
            const success = await initialize(config.spreadsheetId);
            if (!success) throw new Error("Init Sheets échouée");
        }
        await waitForGapiSheets();
        if (!setAccessToken()) throw new Error("Token non disponible");
        return true;
    };

    // =========================================================================
    // GESTION DES FEUILLES
    // =========================================================================

    /**
     * S'assure qu'une feuille existe et a les bons en-têtes.
     */
    const ensureSheet = async (sheetName, headers) => {
        await ensureReady();
        try {
            const spreadsheet = await gapi.client.sheets.spreadsheets.get({
                spreadsheetId: config.spreadsheetId
            });

            const exists = spreadsheet.result.sheets.some(s =>
                s.properties.title === sheetName
            );

            if (!exists) {
                logInfo(`Création de la feuille ${sheetName}...`);
                await gapi.client.sheets.spreadsheets.batchUpdate({
                    spreadsheetId: config.spreadsheetId,
                    resource: {
                        requests: [{
                            addSheet: { properties: { title: sheetName } }
                        }]
                    }
                });
            }

            // Vérifier les en-têtes
            const response = await gapi.client.sheets.spreadsheets.values.get({
                spreadsheetId: config.spreadsheetId,
                range: `${sheetName}!1:1`
            });

            const firstRow = response.result.values?.[0];
            if (!firstRow || firstRow[0] !== headers[0]) {
                await gapi.client.sheets.spreadsheets.values.update({
                    spreadsheetId: config.spreadsheetId,
                    range: `${sheetName}!A1`,
                    valueInputOption: 'RAW',
                    resource: { values: [headers] }
                });
                logSuccess(`En-têtes ${sheetName} créés`);
            }
        } catch (error) {
            logError(`Erreur ensureSheet ${sheetName}`, { message: error.message || error });
        }
    };

    // =========================================================================
    // OPÉRATIONS CRUD GÉNÉRIQUES
    // =========================================================================

    /**
     * Lit les données d'une feuille (sans l'en-tête).
     */
    const readSheet = async (sheetName, range) => {
        await ensureReady();
        try {
            const response = await gapi.client.sheets.spreadsheets.values.get({
                spreadsheetId: config.spreadsheetId,
                range: `${sheetName}!${range}`
            });

            const rows = response.result.values || [];
            // Retirer l'en-tête
            return { rows: rows.length > 1 ? rows.slice(1) : [] };
        } catch (error) {
            logError(`Erreur lecture ${sheetName}`, { message: error.message || error });
            throw error;
        }
    };

    /**
     * Lecture batch de plusieurs plages.
     */
    const batchRead = async (ranges) => {
        await ensureReady();
        try {
            const response = await gapi.client.sheets.spreadsheets.values.batchGet({
                spreadsheetId: config.spreadsheetId,
                ranges: ranges
            });

            const results = {};
            if (response.result.valueRanges) {
                for (const vr of response.result.valueRanges) {
                    const rows = vr.values || [];
                    results[vr.range] = rows.length > 1 ? rows.slice(1) : [];
                }
            }
            return { results };
        } catch (error) {
            logError("Erreur batchRead", { message: error.message || error });
            throw error;
        }
    };

    /**
     * Ajoute une ligne à une feuille.
     */
    const appendRow = async (sheetName, range, values) => {
        await ensureReady();
        try {
            await gapi.client.sheets.spreadsheets.values.append({
                spreadsheetId: config.spreadsheetId,
                range: `${sheetName}!${range}`,
                valueInputOption: 'RAW',
                insertDataOption: 'INSERT_ROWS',
                resource: { values: [values] }
            });
            logSuccess(`Ligne ajoutée à ${sheetName}`);
            return { success: true };
        } catch (error) {
            logError(`Erreur append ${sheetName}`, { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    /**
     * Met à jour une ligne existante.
     */
    const updateRow = async (sheetName, rowIndex, range, values) => {
        await ensureReady();
        try {
            // Calculer la lettre de fin depuis le range (ex: "A:K" -> K)
            const endCol = range.split(':')[1] || 'Z';
            const updateRange = `${sheetName}!A${rowIndex}:${endCol}${rowIndex}`;
            
            await gapi.client.sheets.spreadsheets.values.update({
                spreadsheetId: config.spreadsheetId,
                range: updateRange,
                valueInputOption: 'RAW',
                resource: { values: [values] }
            });
            logSuccess(`Ligne ${rowIndex} de ${sheetName} mise à jour`);
            return { success: true };
        } catch (error) {
            logError(`Erreur update ${sheetName}`, { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    /**
     * Supprime une ligne.
     */
    const deleteRow = async (sheetName, rowIndex) => {
        await ensureReady();
        try {
            const spreadsheet = await gapi.client.sheets.spreadsheets.get({
                spreadsheetId: config.spreadsheetId
            });

            const sheet = spreadsheet.result.sheets.find(s =>
                s.properties.title === sheetName
            );
            if (!sheet) throw new Error(`Feuille ${sheetName} non trouvée`);

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
            logSuccess(`Ligne ${rowIndex} de ${sheetName} supprimée`);
            return { success: true };
        } catch (error) {
            logError(`Erreur delete ${sheetName}`, { message: error.message || error });
            return { success: false, error: error.message };
        }
    };

    const isInitialized = () => config.isInitialized;

    return Object.freeze({
        initialize,
        isInitialized,
        ensureSheet,
        readSheet,
        batchRead,
        appendRow,
        updateRow,
        deleteRow
    });
})();

Object.defineProperty(window, 'googleSheetsApi', {
    value: googleSheetsApi,
    configurable: false,
    writable: false
});
