/**
 * Script de restauration d'URL pour le routing SPA sur GitHub Pages
 * 
 * Fonctionnement :
 * 1. Quand un utilisateur accède directement à une route (ex: /Managely/login),
 *    GitHub Pages retourne 404.html qui stocke l'URL et redirige vers /Managely/
 * 2. Ce script détecte la redirection stockée et restaure l'URL originale
 *    via history.replaceState, permettant au router Blazor de prendre le relais
 */
(function() {
    var redirect = sessionStorage.getItem('spa-redirect');
    if (redirect) {
        sessionStorage.removeItem('spa-redirect');
        var basePath = '/__BASE_PATH__';
        var basePathWithoutSlash = basePath.slice(0, -1);
        
        // Sécurité : ne restaurer que si l'URL fait partie de l'application
        // (doit commencer par le basePath du repo)
        var isValidRedirect = redirect.startsWith(basePath) || redirect.startsWith(basePathWithoutSlash);
        var isNotHomePage = redirect !== basePath && redirect !== basePathWithoutSlash;
        
        if (isValidRedirect && isNotHomePage) {
            history.replaceState(null, null, redirect);
        }
    }
})();
