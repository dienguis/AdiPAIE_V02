// =============================================================================
//  SaisieSoldeInitialController.cs — V1.8 (juin 2026)
//
//  Action "Saisir solde initial de congés" sur le DetailView de Salarie.
//
//  Cas d'usage : mise en production. Le RH ouvre la fiche du salarié,
//  clique sur le bouton, saisit dans un popup :
//    - Type de congé (CPAYE, etc.)
//    - Année
//    - Jours reportés (cumul actuel — peut être négatif)
//    - Date d'arrêté du solde
//    - Flag "à vérifier" (pour les 16 lignes bleues du fichier Excel)
//    - Source + Commentaire
//
//  Le controller crée/met à jour un SoldeConge actif et trace l'origine
//  via les 3 champs V1.8 (SoldeArreteAu, SoldeAVerifier, SourceInitialisation).
//
//  Accessible aux rôles RH, DAF, DG (à configurer dans
//  InitialiserRolesGRHController si besoin de restriction stricte).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class SaisieSoldeInitialController
        : ObjectViewController<DetailView, Salarie>
    {
        readonly PopupWindowShowAction saisieSoldeAction;

        public SaisieSoldeInitialController()
        {
            saisieSoldeAction = new PopupWindowShowAction(this,
                "Salarie_SaisirSoldeInitialConge", PredefinedCategory.Edit)
            {
                Caption = "Saisir solde initial de congés",
                ImageName = "BO_List",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Initialiser le solde de congés du salarié à partir " +
                          "des données connues du RH (fichier Excel Planning Congés). " +
                          "À utiliser à la mise en production puis pour " +
                          "régulariser les soldes à vérifier.",
                AcceptButtonCaption = "Enregistrer le solde",
                CancelButtonCaption = "Annuler"
            };
            saisieSoldeAction.CustomizePopupWindowParams += OnCustomizePopup;
            saisieSoldeAction.Execute += OnExecute;
        }

        // ─────────────────────────────────────────────────────────────
        //  Construction du popup avec pré-remplissage intelligent
        //  V1.8 — Ajout AdditionalObjectSpaces pour requêter CongeType
        //  (entité persistente) depuis un NonPersistentObjectSpace.
        // ─────────────────────────────────────────────────────────────
        void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var salarie = (Salarie)View.CurrentObject;

            // OS non-persistent pour le DTO SaisieSoldeInitialRequest
            var osNp = (DevExpress.ExpressApp.NonPersistentObjectSpace)
                Application.CreateObjectSpace(typeof(SaisieSoldeInitialRequest));

            // OS persistent additionnel pour requêter CongeType
            var osP = Application.CreateObjectSpace(typeof(CongeType));
            osNp.AdditionalObjectSpaces.Add(osP);

            var request = osNp.CreateObject<SaisieSoldeInitialRequest>();

            // Pré-remplir avec le type CPAYE par défaut (le plus courant)
            request.TypeConge = osP.GetObjectsQuery<CongeType>()
                .ToList()
                .FirstOrDefault(t => string.Equals(t.Code, "CPAYE",
                    StringComparison.OrdinalIgnoreCase));

            request.Annee = DateTime.Today.Year;
            request.SoldeArreteAu = DateTime.Today;

            var detailView = Application.CreateDetailView(osNp, request);
            detailView.Caption = $"Solde initial — {salarie?.FullName ?? "salarié"}";

            e.View = detailView;
        }

        // ─────────────────────────────────────────────────────────────
        //  Au clic "Enregistrer" : crée ou met à jour le SoldeConge
        // ─────────────────────────────────────────────────────────────
        void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            try
            {
                var request = e.PopupWindowViewCurrentObject as SaisieSoldeInitialRequest;
                if (request == null)
                {
                    ShowError("Formulaire introuvable. Réessayez.");
                    return;
                }

                if (request.TypeConge == null)
                {
                    ShowError("Type de congé obligatoire.");
                    return;
                }

                var salarieRaw = (Salarie)View.CurrentObject;
                if (salarieRaw == null)
                {
                    ShowError("Salarié introuvable.");
                    return;
                }

                // Travailler dans un OS persistant dédié pour faire le commit
                using var os = Application.CreateObjectSpace(typeof(SoldeConge));
                var salarie = os.GetObjectByKey<Salarie>(salarieRaw.Oid);
                var typeConge = os.GetObjectByKey<CongeType>(request.TypeConge.Oid);
                if (salarie == null || typeConge == null)
                {
                    ShowError("Impossible de relocaliser le salarié ou le type de congé.");
                    return;
                }

                // Chercher le solde existant (Salarie + Type + Année + Actif)
                var solde = os.GetObjectsQuery<SoldeConge>()
                    .FirstOrDefault(s => s.Salarie.Oid == salarie.Oid
                                      && s.TypeConge.Oid == typeConge.Oid
                                      && s.Annee == request.Annee
                                      && s.Statut == DomainEnums.SoldeCongeStatut.Actif);

                bool creation = false;
                if (solde == null)
                {
                    solde = os.CreateObject<SoldeConge>();
                    solde.Salarie = salarie;
                    solde.TypeConge = typeConge;
                    solde.Annee = request.Annee;
                    solde.Statut = DomainEnums.SoldeCongeStatut.Actif;
                    creation = true;
                }

                // Appliquer les valeurs saisies
                solde.JoursReportes        = request.JoursReportes;
                solde.SoldeArreteAu        = request.SoldeArreteAu ?? DateTime.Today;
                solde.SoldeAVerifier       = request.SoldeAVerifier;
                solde.SourceInitialisation = !string.IsNullOrWhiteSpace(request.Source)
                    ? request.Source
                    : "Saisie manuelle RH";

                // Commentaire stocké via un MouvementSolde de type Initialisation
                if (!string.IsNullOrWhiteSpace(request.Commentaire))
                {
                    try
                    {
                        MouvementSolde.Creer(os, solde,
                            DomainEnums.MouvementSoldeType.Initialisation,
                            0m,  // 0 : on ne change pas le compteur, c'est une trace
                            reference: $"INIT-{request.Annee}",
                            commentaire: request.Commentaire);
                    }
                    catch { /* facultatif */ }
                }

                os.CommitChanges();

                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ Solde {(creation ? "créé" : "mis à jour")} pour {salarie.FullName} : " +
                    $"{request.JoursReportes:N1} j de {typeConge.Libelle} " +
                    $"(année {request.Annee}, arrêté au {(request.SoldeArreteAu ?? DateTime.Today):dd/MM/yyyy})" +
                    (request.SoldeAVerifier ? "\n⚠️ Marqué 'à vérifier'" : ""),
                    InformationType.Success, 6000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError($"Erreur saisie solde : {ex.Message}");
            }
        }

        void ShowError(string message)
        {
            Application.ShowViewStrategy?.ShowMessage(
                $"❌ {message}",
                InformationType.Error, 6000, InformationPosition.Top);
        }
    }
}
