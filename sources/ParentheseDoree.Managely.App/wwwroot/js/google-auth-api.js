/**
 * =============================================================================
 * google-auth-api.js - API d'authentification Google avec vérification des permissions
 * =============================================================================
 */

const googleAuthApi = (() => {
    // Configuration
    let config = {
        clientId: null,
        scopes: [],
        spreadsheetId: null,
        isInitialized: false
    };

    // État
    let currentUser = null;
    let tokenClient = null;
    let accessToken = null;
    let userPermission = 'none'; // 'none', 'read', 'write'

    // Callback Blazor
    let blazorCallback = null;

    // Clés de stockage session
    const STORAGE_KEYS = {
        ACCESS_TOKEN: 'managely_access_token',
        TOKEN_EXPIRY: 'managely_token_expiry',
        USER_INFO: 'managely_user_info',
        PERMISSION: 'managely_permission'
    };

    // Logging via browserLogger
    const log = (level, msg, data) => {
        if (window.browserLogger) {
            window.browserLogger[level]("AUTH", msg, data);
        } else {
            const fn = level === 'error' ? console.error : 
                       level === 'warn' ? console.warn : 
                       level === 'success' ? console.log : console.info;
            fn(`[AUTH] ${msg}`, data);
        }
    };

    const logDebug = (msg, data) => log('debug', msg, data);
    const logInfo = (msg, data) => log('info', msg, data);
    const logSuccess = (msg, data) => log('success', msg, data);
    const logWarn = (msg, data) => log('warn', msg, data);
    const logError = (msg, data) => log('error', msg, data);

    /**
     * Initialise l'API Google Auth
     */
    const initialize = async (clientId, scopes = [], spreadsheetId = null) => {
        if (config.isInitialized) {
            logInfo("API déjà initialisée");
            return true;
        }

        if (!clientId) {
            logError("Client ID manquant");
            return false;
        }

        config.clientId = clientId;
        config.scopes = scopes;
        // Utiliser le spreadsheetId passé ou celui de la config globale
        config.spreadsheetId = spreadsheetId || window.MANAGELY_CONFIG?.SPREADSHEET_ID;

        try {
            await waitForGis();
            
            tokenClient = google.accounts.oauth2.initTokenClient({
                client_id: config.clientId,
                scope: config.scopes.join(' '),
                callback: handleTokenResponse,
                error_callback: handleTokenError
            });

            if (config.scopes.some(s => s.includes('spreadsheets'))) {
                await waitForGapi();
                await loadGapiClient();
            }

            config.isInitialized = true;
            logSuccess("API initialisée avec succès");
            
            await tryRestoreSession();
            
            return true;
        } catch (error) {
            logError("Erreur d'initialisation", { message: error.message });
            return false;
        }
    };

    const waitForGis = () => {
        return new Promise((resolve, reject) => {
            const maxAttempts = 50;
            let attempts = 0;
            
            const check = () => {
                if (typeof google !== 'undefined' && google.accounts && google.accounts.oauth2) {
                    resolve();
                } else if (attempts >= maxAttempts) {
                    reject(new Error("Google Identity Services non disponible"));
                } else {
                    attempts++;
                    setTimeout(check, 100);
                }
            };
            check();
        });
    };

    const waitForGapi = () => {
        return new Promise((resolve, reject) => {
            const maxAttempts = 50;
            let attempts = 0;
            
            const check = () => {
                if (typeof gapi !== 'undefined') {
                    resolve();
                } else if (attempts >= maxAttempts) {
                    reject(new Error("GAPI non disponible"));
                } else {
                    attempts++;
                    setTimeout(check, 100);
                }
            };
            check();
        });
    };

    const loadGapiClient = () => {
        return new Promise((resolve, reject) => {
            gapi.load('client', {
                callback: async () => {
                    try {
                        await gapi.client.init({
                            discoveryDocs: ['https://sheets.googleapis.com/$discovery/rest?version=v4']
                        });
                        resolve();
                    } catch (err) {
                        reject(err);
                    }
                },
                onerror: () => reject(new Error("Erreur chargement GAPI")),
                timeout: 10000,
                ontimeout: () => reject(new Error("Timeout GAPI"))
            });
        });
    };

    const handleTokenResponse = async (response) => {
        if (response.error) {
            logError("Erreur token OAuth", { error: response.error });
            currentUser = null;
            accessToken = null;
            userPermission = 'none';
            notifyStateChange();
            return;
        }

        accessToken = response.access_token;
        
        // Configurer le token dans GAPI
        if (gapi.client) {
            gapi.client.setToken({ access_token: accessToken });
        }

        // Sauvegarder en session
        sessionStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, accessToken);
        sessionStorage.setItem(STORAGE_KEYS.TOKEN_EXPIRY, Date.now() + (response.expires_in * 1000));

        // Récupérer les infos utilisateur
        await fetchUserInfo();
        
        // Initialiser l'API Sheets avec le spreadsheetId
        if (config.spreadsheetId && window.googleSheetsApi) {
            await window.googleSheetsApi.initialize(config.spreadsheetId);
        }
        
        // Vérifier les permissions sur le spreadsheet
        if (config.spreadsheetId) {
            await checkFilePermission();
        }
        
        // Sauvegarder la permission
        sessionStorage.setItem(STORAGE_KEYS.PERMISSION, userPermission);
        
        logSuccess("Connexion réussie", { user: currentUser?.name, permission: userPermission });
        notifyStateChange();
    };

    const handleTokenError = (error) => {
        logError("Erreur OAuth", { type: error?.type, message: error?.message });
        currentUser = null;
        accessToken = null;
        userPermission = 'none';
        notifyStateChange();
    };

    const fetchUserInfo = async () => {
        if (!accessToken) return null;

        try {
            const response = await fetch('https://www.googleapis.com/oauth2/v3/userinfo', {
                headers: { Authorization: `Bearer ${accessToken}` }
            });

            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const userInfo = await response.json();
            
            currentUser = {
                id: userInfo.sub,
                email: userInfo.email,
                name: userInfo.name,
                givenName: userInfo.given_name,
                familyName: userInfo.family_name,
                picture: userInfo.picture,
                emailVerified: userInfo.email_verified
            };

            sessionStorage.setItem(STORAGE_KEYS.USER_INFO, JSON.stringify(currentUser));
            return currentUser;
        } catch (error) {
            logError("Erreur récupération infos utilisateur", { message: error.message });
            return null;
        }
    };

    /**
     * Vérifie les permissions sur le spreadsheet
     */
    const checkFilePermission = async () => {
        if (!config.spreadsheetId || !gapi.client || !gapi.client.sheets) {
            userPermission = 'none';
            return userPermission;
        }

        try {
            logInfo("Vérification des permissions...");
            
            // Tenter une lecture
            const readResponse = await gapi.client.sheets.spreadsheets.get({
                spreadsheetId: config.spreadsheetId
            });

            if (!readResponse || readResponse.status !== 200) {
                logWarn("Pas d'accès au fichier");
                userPermission = 'none';
                return userPermission;
            }

            // Tenter une écriture fictive pour vérifier les droits d'écriture
            try {
                const testRange = 'Clients!A1';
                const getResponse = await gapi.client.sheets.spreadsheets.values.get({
                    spreadsheetId: config.spreadsheetId,
                    range: testRange
                });

                const currentValue = getResponse.result.values ? getResponse.result.values[0][0] : '';
                
                await gapi.client.sheets.spreadsheets.values.update({
                    spreadsheetId: config.spreadsheetId,
                    range: testRange,
                    valueInputOption: 'RAW',
                    resource: { values: [[currentValue]] }
                });

                logSuccess("Permission: Lecture/Écriture");
                userPermission = 'write';
            } catch (writeError) {
                logInfo("Permission: Lecture seule");
                userPermission = 'read';
            }
        } catch (error) {
            if (error.status === 403 || error.status === 404) {
                logError("Accès refusé au fichier", { status: error.status });
                userPermission = 'none';
            } else {
                logError("Erreur vérification permissions", { message: error.message });
                userPermission = 'none';
            }
        }

        return userPermission;
    };

    const tryRestoreSession = async () => {
        const savedToken = sessionStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN);
        const tokenExpiry = sessionStorage.getItem(STORAGE_KEYS.TOKEN_EXPIRY);
        const savedUser = sessionStorage.getItem(STORAGE_KEYS.USER_INFO);
        const savedPermission = sessionStorage.getItem(STORAGE_KEYS.PERMISSION);

        if (savedToken && tokenExpiry && parseInt(tokenExpiry) > Date.now()) {
            accessToken = savedToken;
            
            if (gapi.client) {
                gapi.client.setToken({ access_token: accessToken });
            }
            
            if (savedUser) {
                currentUser = JSON.parse(savedUser);
            } else {
                await fetchUserInfo();
            }
            
            userPermission = savedPermission || 'none';
            
            // Initialiser l'API Sheets si le token est restauré
            if (config.spreadsheetId && window.googleSheetsApi) {
                await window.googleSheetsApi.initialize(config.spreadsheetId);
            }
            
            logInfo("Session restaurée", { user: currentUser?.name, permission: userPermission });
            notifyStateChange();
        }
    };

    /**
     * Déclenche la connexion Google et vérifie les permissions
     */
    const signIn = () => {
        return new Promise((resolve, reject) => {
            if (!config.isInitialized || !tokenClient) {
                reject(new Error("API non initialisée"));
                return;
            }

            let hasResolved = false;
            let timeoutId = null;

            const safeResolve = (result) => {
                if (hasResolved) return;
                hasResolved = true;
                if (timeoutId) clearTimeout(timeoutId);
                resolve(result);
            };

            const originalCallback = tokenClient.callback;
            tokenClient.callback = async (response) => {
                await handleTokenResponse(response);
                
                if (currentUser) {
                    safeResolve({
                        success: true,
                        user: currentUser,
                        permission: userPermission
                    });
                } else {
                    safeResolve({
                        success: false,
                        error: "Échec de la connexion"
                    });
                }
                
                tokenClient.callback = originalCallback;
            };

            tokenClient.error_callback = (error) => {
                logError("Erreur OAuth callback", { type: error?.type });
                let errorMessage = "La connexion a été annulée.";
                
                if (error?.type === 'popup_closed') {
                    errorMessage = "La fenêtre de connexion a été fermée.";
                } else if (error?.type === 'popup_failed_to_open') {
                    errorMessage = "Impossible d'ouvrir la fenêtre de connexion.";
                }
                
                safeResolve({ success: false, error: errorMessage });
            };

            try {
                logInfo("Ouverture de la fenêtre de connexion...");
                tokenClient.requestAccessToken({ prompt: 'consent' });
                
                timeoutId = setTimeout(() => {
                    if (!hasResolved) {
                        logWarn("Timeout de connexion");
                        safeResolve({ success: false, error: "Timeout de connexion" });
                    }
                }, 120000);
            } catch (error) {
                logError("Erreur ouverture popup", { message: error.message });
                safeResolve({ success: false, error: "Impossible d'ouvrir la fenêtre de connexion" });
            }
        });
    };

    const signOut = () => {
        if (accessToken) {
            google.accounts.oauth2.revoke(accessToken, () => {
                logInfo("Token révoqué");
            });
        }

        accessToken = null;
        currentUser = null;
        userPermission = 'none';
        
        if (gapi.client) {
            gapi.client.setToken(null);
        }
        
        // Nettoyer le sessionStorage
        Object.values(STORAGE_KEYS).forEach(key => sessionStorage.removeItem(key));

        logInfo("Déconnexion effectuée");
        notifyStateChange();
    };

    const getCurrentUser = () => currentUser;
    const isSignedIn = () => currentUser !== null && accessToken !== null;
    const getAccessToken = () => accessToken;
    const getPermission = () => userPermission;

    const registerStateCallback = (dotNetRef, methodName) => {
        blazorCallback = { dotNetRef, methodName };
        logDebug("Callback Blazor enregistré", { methodName });
    };

    const unregisterStateCallback = () => {
        blazorCallback = null;
    };

    const notifyStateChange = () => {
        if (blazorCallback) {
            try {
                blazorCallback.dotNetRef.invokeMethodAsync(
                    blazorCallback.methodName,
                    isSignedIn(),
                    currentUser,
                    userPermission
                );
            } catch (error) {
                logError("Erreur notification Blazor", { message: error.message });
            }
        }
    };

    return Object.freeze({
        initialize,
        signIn,
        signOut,
        getCurrentUser,
        isSignedIn,
        getAccessToken,
        getPermission,
        registerStateCallback,
        unregisterStateCallback
    });
})();

Object.defineProperty(window, 'googleAuthApi', {
    value: googleAuthApi,
    configurable: false,
    writable: false
});
