// =============================================================
//  AdiPAIE — utilitaires JavaScript côté navigateur
//  Emplacement : wwwroot/js/adipaie.js
// =============================================================

window.AdiPAIE = window.AdiPAIE || {};

/**
 * Déclenche le téléchargement d'un fichier directement dans le navigateur,
 * à partir des bytes passés en base64 depuis le serveur Blazor (JSInterop).
 *
 * Appelé via :
 *   jsRuntime.InvokeVoidAsync("AdiPAIE.downloadFile", nomFichier, mimeType, base64)
 *
 * @param {string} fileName  - Nom du fichier affiché dans la boîte de téléchargement
 * @param {string} mimeType  - Type MIME (ex: "application/pdf", "text/html")
 * @param {string} base64    - Contenu du fichier encodé en base64
 */
window.AdiPAIE.downloadFile = function (fileName, mimeType, base64) {
    try {
        // Décoder le base64 en bytes
        var bytes = Uint8Array.from(atob(base64), function (c) {
            return c.charCodeAt(0);
        });

        // Créer un Blob et une URL objet temporaire
        var blob = new Blob([bytes], { type: mimeType });
        var url = URL.createObjectURL(blob);

        // Créer un lien <a> invisible et simuler un clic
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();

        // Nettoyer immédiatement
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (e) {
        console.error('[AdiPAIE] Erreur téléchargement :', e);
    }
};
