const browserLogger = (() => {
    // Catégories de logs
    const LogCategory = Object.freeze({
        AUTH: "Auth",
        API: "API",
        APP: "App"
    });

    // Niveaux de logs
    const LogLevel = Object.freeze({
        DEBUG: 0,
        INFO: 1,
        SUCCESS: 2,
        WARN: 3,
        ERROR: 4
    });

    // Styles CSS pour chaque niveau de log
    const LogStyle = Object.freeze({
        DEBUG: "color:#9E9E9E;",
        INFO: "color:#2196F3;",
        SUCCESS: "color:#4CAF50;font-weight:bold;",
        WARN: "color:#FF9800;font-weight:bold;",
        ERROR: "color:#F44336;font-weight:bold;"
    });

    // Emojis pour chaque niveau de log
    const LogEmoji = Object.freeze({
        DEBUG: "🔍",
        INFO: "ℹ️",
        SUCCESS: "✅",
        WARN: "⚠️",
        ERROR: "❌"
    });

    // Couleurs principales pour chaque catégorie
    const CategoryColor = Object.freeze({
        AUTH: "#9C27B0",
        API: "#00BCD4",
        APP: "#607D8B"
    });

    // Configuration globale du logger
    const LoggerConfig = {
        minLevel: LogLevel.DEBUG
    };

    // Récupérer le nom du niveau à partir de sa valeur
    const getLevelName = (level) => Object.keys(LogLevel).find(key => LogLevel[key] === level);

    // Clonage sécurisé des données pour affichage
    const cloneDataSafe = (data) => {
        try {
            return structuredClone ? structuredClone(data) : JSON.parse(JSON.stringify(data));
        } catch {
            return data;
        }
    };

    // Fonction principale de log
    const log = (level, category, message, data) => {
        if (typeof message !== "string" || level < LoggerConfig.minLevel) return;

        const levelName = getLevelName(level) || "INFO";
        const categoryName = LogCategory[category] || "App";
        const categoryStyle = `color:${CategoryColor[category] || "#607D8B"};font-weight:bold;`;
        const timestamp = new Date().toLocaleTimeString();
        const formattedMessage = `%c[${timestamp}]%c %c${categoryName}%c ${LogEmoji[levelName] || "❔"}`;
        const styles = ["color:#888;", "color:inherit;", categoryStyle, LogStyle[levelName] || ""];

        if (data !== undefined) {
            console.groupCollapsed(`${formattedMessage} ${message}`, ...styles);
            const safeData = cloneDataSafe(data);

            switch (level) {
                case LogLevel.ERROR:
                    console.error(safeData);
                    break;
                case LogLevel.WARN:
                    console.warn(safeData);
                    break;
                case LogLevel.DEBUG:
                    console.debug(safeData);
                    break;
                default:
                    console.log(safeData);
            }

            console.groupEnd();
        } else {
            console.log(`${formattedMessage} ${message}`, ...styles);
        }
    };

    // Définir le niveau minimum de log
    const setMinLevel = (level) => {
        if (typeof level === 'number' && level >= 0 && level <= 4) {
            LoggerConfig.minLevel = level;
        }
    };

    // Interface publique
    return Object.freeze({
        debug: (category, message, data) => log(LogLevel.DEBUG, category, message, data),
        info: (category, message, data) => log(LogLevel.INFO, category, message, data),
        success: (category, message, data) => log(LogLevel.SUCCESS, category, message, data),
        warn: (category, message, data) => log(LogLevel.WARN, category, message, data),
        error: (category, message, data) => log(LogLevel.ERROR, category, message, data),
        setMinLevel
    });
})();

Object.defineProperty(window, "browserLogger", {
    value: browserLogger,
    configurable: false,
    writable: false
});
