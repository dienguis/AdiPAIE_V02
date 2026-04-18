using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using System.Collections.Generic;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Masque les colonnes superflues dans les ListViews "Mon espace"
    /// pour un affichage plus aéré (salarié connecté uniquement).
    ///
    /// RH / Admin : ne touche à rien (voit toutes les colonnes).
    /// </summary>
    public class EspaceSalarieColumnsController : ViewController<ListView>
    {
        // ── Colonnes VISIBLES par vue (tout le reste sera masqué) ──
        private static readonly Dictionary<string, HashSet<string>> _colonnesParVue
            = new Dictionary<string, HashSet<string>>
        {
            // Demande de déplacement
            ["DemandeDeplacement_ListView"] = new HashSet<string>
            {
                "Objet", "Statut", "DateDepart", "DateRetour", "NombreJours", "Destination"
            },
            // Demande de congé
            ["CongeDemande_ListView"] = new HashSet<string>
            {
                "Type", "DateDebut", "DateFin", "DureeJours", "Statut", "Motif"
            },
            // Bulletins
            ["Bulletin_ListView"] = new HashSet<string>
            {
                "Periode", "Mois", "Annee", "NetAPayer", "Statut"
            },
            // Demande d'attestation
            ["DemandeAttestation_ListView"] = new HashSet<string>
            {
                "Nature", "DateDemande", "DateSouhaitee", "Statut"
            },
            // Solde de congés
            ["SoldeConge_ListView"] = new HashSet<string>
            {
                "TypeConge", "Annee", "JoursAcquis", "JoursPris",
                "JoursEnAttente", "SoldeDisponible", "Statut"
            },
            // Entretien annuel
            ["EntretienAnnuel_ListView"] = new HashSet<string>
            {
                "Campagne", "Evaluateur", "DatePlanifiee", "Statut", "ScoreGlobal"
            },
        };

        protected override void OnActivated()
        {
            base.OnActivated();

            // Seulement pour les salariés connectés (pas admin pur)
            if (!EspaceSalarieHelper.EstSalarieConnecte(ObjectSpace))
                return;

            // Récupérer l'ID de la vue courante
            var viewId = View.Id;
            if (viewId == null)
                return;

            // Chercher si cette vue a un mapping de colonnes
            if (!_colonnesParVue.TryGetValue(viewId, out var colonnesVisibles))
                return;

            // Masquer les colonnes non essentielles
            foreach (IModelColumn col in View.Model.Columns)
            {
                if (!colonnesVisibles.Contains(col.PropertyName))
                    col.Index = -1;
            }

        }
    }
}
