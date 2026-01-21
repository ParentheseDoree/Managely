/**
 * Script de restauration d'URL pour le routing SPA sur GitHub Pages
 * 
 * Fonctionnement :
 * 1. Quand un utilisateur accède directement à une route (ex: /login),
 *    GitHub Pages retourne 404.html qui stocke l'URL et redirige vers /
 * 2. Ce script détecte la redirection stockée et restaure l'URL originale
 *    via history.replaceState, permettant au router Blazor de prendre le relais
 */
(function() {
    var redirect = sessionStorage.getItem('spa-redirect');
    if (redirect) {
        sessionStorage.removeItem('spa-redirect');
        // Ne pas remplacer si c'est déjà la page d'accueil
        var basePath = '/__BASE_PATH__';
        if (redirect !== basePath && redirect !== basePath.slice(0, -1)) {
            history.replaceState(null, null, redirect);
        }
    }
})();
