using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Espace salarie : consultation seule des bulletins.
    ///
    ///   Salarie connecte  -> desactive les controleurs RH et masque
    ///                        les actions non autorisees. DetailView en lecture seule.
    ///   RH / Admin        -> aucune restriction.
    ///
    /// Approche : desactiver les controleurs RH + masquer par categorie/ID
    /// sans toucher aux actions framework XAF internes.
    /// </summary>
    public class BulletinEspaceSalarieReadOnlyController
        : ObjectViewController<ObjectView, Bulletin>
    {
        private const string ReasonKey = "EspaceSalarieReadOnly";

        private readonly List<Controller> _controlleursDesactives = new List<Controller>();

        // Actions explicitement masquees (en plus de celles des controleurs desactives)
        private static readonly HashSet<string> ActionsMasquees = new HashSet<string>
        {
            "New", "Delete", "Save", "SaveAndClose", "SaveAndNew",
            "ImprimerBulletin",
            "GenererEcritureComptable",
            "GenerateEcritureFromBulletin",
            "Workflows_OuvrirDiagrammes",
            "NormalizeBulletinLines",
            "ChargerDepuisModele",
            "RemplacerDepuisModele",
            "RemplacerLignesModeleSeulement",
            // V1.4.3 — actions RH publication à masquer côté salarié
            "PublierBulletin",
            "DepublierBulletin",
            "RenotifierBulletin",
            "ValiderBulletin",
            "ValiderEtEnvoyer",
            "RenvoyerBulletin",
            "EnvoyerClePDF",
            "EnvoyerBulletinEmail",
            "Bulletin_RecalculerMaintenant",
            "CloturerBulletin",
            "ReouvrirBulletin"
        };

        // Categories entieres a masquer
        private static readonly HashSet<string> CategoriesMasquees = new HashSet<string>
        {
            "Edit", "RecordEdit", "Reports", "Save", "ObjectsCreation"
        };

        // Actions a PRESERVER meme si leur categorie est masquee
        private static readonly HashSet<string> ActionsProtegees = new HashSet<string>
        {
            "Bulletin_Telecharger"
        };

        protected override void OnActivated()
        {
            base.OnActivated();

            // V1.8 — Bug corrigé : on utilisait EstSalarieConnecte qui retournait
            // true pour TOUT user lié à un Salarie (même les managers RH/DAF/DG/Admin
            // qui ont aussi un Salarie associé via leur email). Conséquence : le
            // DetailView du Bulletin passait en lecture seule pour ces managers,
            // masquant TOUS les boutons Edit (Recharger bulletin, Recalculer
            // cotisations, Ajouter une ligne, etc.).
            //
            // Maintenant on utilise DoitRestreindreEspaceSalarie qui exclut les
            // rôles managers (RH/DAF/DG/Admin) de la restriction. Un salarié pur
            // (sans rôle manager) reste bien restreint.
            if (!EspaceSalarieHelper.DoitRestreindreEspaceSalarie(ObjectSpace))
                return;

            // ---- 1. Desactiver les controleurs RH entiers ---
            DesactiverControleur<BulletinCloturePretController>();
            DesactiverControleur<BulletinValiderEnvoyerController>();
            DesactiverControleur<ExportComptableBatchController>();
            DesactiverControleur<BulletinEmailController>();
            DesactiverControleur<BulletinPrintController>();
            DesactiverControleur<ExportComptableController>();
            DesactiverControleur<GenerateEcritureController>();

            // ---- 2. Masquer par categorie et par ID ---
            foreach (var controller in Frame.Controllers)
            {
                foreach (ActionBase action in controller.Actions)
                {
                    // Ne jamais toucher aux actions protegees
                    if (ActionsProtegees.Contains(action.Id))
                        continue;

                    if (ActionsMasquees.Contains(action.Id)
                        || CategoriesMasquees.Contains(action.Category ?? ""))
                    {
                        action.Active[ReasonKey] = false;
                    }
                }
            }

            // ---- 3. DetailView en lecture seule ---
            if (View is DetailView dv)
            {
                dv.AllowEdit[ReasonKey] = false;
            }
        }

        protected override void OnDeactivated()
        {
            foreach (var ctrl in _controlleursDesactives)
                ctrl.Active.RemoveItem(ReasonKey);
            _controlleursDesactives.Clear();

            foreach (var controller in Frame.Controllers)
                foreach (ActionBase action in controller.Actions)
                    action.Active.RemoveItem(ReasonKey);

            if (View is DetailView dv)
                dv.AllowEdit.RemoveItem(ReasonKey);

            base.OnDeactivated();
        }

        private void DesactiverControleur<T>() where T : Controller
        {
            var ctrl = Frame.GetController<T>();
            if (ctrl != null)
            {
                ctrl.Active[ReasonKey] = false;
                _controlleursDesactives.Add(ctrl);
            }
        }
    }
}
