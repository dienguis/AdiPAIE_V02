// AdiPAIE_V02.Module/Services/AuditService.cs
// Service helper pour l'enregistrement d'audit centralisé
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Méthodes statiques pour enregistrer facilement des entrées d'audit
    /// depuis n'importe quel contrôleur ou service.
    /// </summary>
    public static class AuditService
    {
        /// <summary>
        /// Enregistre une action d'audit et commit immédiatement.
        /// Utilise un ObjectSpace distinct pour ne pas perturber
        /// la transaction en cours.
        /// </summary>
        public static void Enregistrer(
            XafApplication application,
            string nomEntite,
            string action,
            string objectId,
            string objectLabel,
            string details = null,
            string ancienStatut = null,
            string nouveauStatut = null)
        {
            if (application == null) return;

            try
            {
                using var os = application.CreateObjectSpace(typeof(AuditEntry));
                var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)os).Session;

                string user;
                try { user = SecuritySystem.CurrentUserName; }
                catch { user = "(système)"; }

                AuditEntry.Enregistrer(
                    session, user, nomEntite, action,
                    objectId, objectLabel,
                    details, ancienStatut, nouveauStatut);

                os.CommitChanges();
            }
            catch
            {
                // L'audit ne doit jamais bloquer le flux métier
            }
        }

        /// <summary>
        /// Enregistre une action d'audit à partir d'une Session XPO existante.
        /// Ne fait PAS de commit — l'appelant doit gérer le commit.
        /// </summary>
        public static void EnregistrerDansSession(
            Session session,
            string nomEntite,
            string action,
            string objectId,
            string objectLabel,
            string details = null,
            string ancienStatut = null,
            string nouveauStatut = null)
        {
            if (session == null) return;

            try
            {
                string user;
                try { user = SecuritySystem.CurrentUserName; }
                catch { user = "(système)"; }

                AuditEntry.Enregistrer(
                    session, user, nomEntite, action,
                    objectId, objectLabel,
                    details, ancienStatut, nouveauStatut);
            }
            catch
            {
                // L'audit ne doit jamais bloquer le flux métier
            }
        }
    }
}
