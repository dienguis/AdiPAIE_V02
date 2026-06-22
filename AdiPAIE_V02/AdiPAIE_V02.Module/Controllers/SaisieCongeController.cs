// =============================================================================
//  SaisieCongeController.cs — V1.8 (juin 2026)
//
//  Action "Saisir un congé" sur le DetailView de Salarie.
//
//  Couvre 2 cas via le popup SaisieCongeRequest :
//
//    A) ALLOCATION DE CONGÉ (le salarié part en congé)
//       → Génère une ligne CONGE_PAYE dans le bulletin
//       → Mode bulletin lu depuis ParametresPaie.ModeBulletinConges
//       → Débite le solde du salarié
//
//    B) RACHAT ICCP (compensation monétaire sans départ physique)
//       → Génère une ligne ICCP dans le bulletin mensuel normal
//       → Débite le solde du salarié (de l'année d'origine)
//
//  Modes de calcul :
//    - AUTO   : utilise BulletinCongeService.CalculerAllocationAuto
//    - MANUEL : utilise directement MontantManuel saisi par RH
//
//  Workflow :
//    1. Validation des champs
//    2. Calcul (auto ou manuel)
//    3. Création de la ligne bulletin (CONGE_PAYE ou ICCP)
//    4. Création d'un MouvementSolde "PriseCongé" sur le SoldeConge
//    5. CommitChanges + message de confirmation
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System;
using System.Linq;

namespace AdiPAIE_V02.Module.Controllers
{
    public sealed class SaisieCongeController
        : ObjectViewController<DetailView, Salarie>
    {
        readonly PopupWindowShowAction saisieCongeAction;

        public SaisieCongeController()
        {
            saisieCongeAction = new PopupWindowShowAction(this,
                "Salarie_SaisirConge", PredefinedCategory.Edit)
            {
                Caption = "Saisir un congé",
                ImageName = "Action_GrantPermission",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Saisir une allocation de congé (le salarié part en " +
                          "congé) ou un rachat ICCP (compensation monétaire " +
                          "sans départ physique). Calcule automatiquement le " +
                          "montant depuis l'historique 12 mois, ou accepte une " +
                          "saisie manuelle pour les cas rétroactifs.",
                AcceptButtonCaption = "Générer la ligne de bulletin",
                CancelButtonCaption = "Annuler"
            };
            saisieCongeAction.CustomizePopupWindowParams += OnCustomizePopup;
            saisieCongeAction.Execute += OnExecute;
        }

        // ─────────────────────────────────────────────────────────────
        //  Construction du popup avec pré-remplissage intelligent
        //  V1.8 — Pour requêter des entités persistentes (CongeType,
        //  Bulletin) depuis un NonPersistentObjectSpace, on doit ajouter
        //  l'OS persistent à AdditionalObjectSpaces. Sans ça XAF lève
        //  une ArgumentException "Cannot handle the type".
        // ─────────────────────────────────────────────────────────────
        void OnCustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var salarie = (Salarie)View.CurrentObject;

            // OS non-persistent pour le DTO SaisieCongeRequest
            var osNp = (DevExpress.ExpressApp.NonPersistentObjectSpace)
                Application.CreateObjectSpace(typeof(SaisieCongeRequest));

            // OS persistent additionnel pour requêter CongeType, Bulletin, etc.
            var osP = Application.CreateObjectSpace(typeof(CongeType));
            osNp.AdditionalObjectSpaces.Add(osP);

            var request = osNp.CreateObject<SaisieCongeRequest>();

            // Pré-remplissage par défaut — query sur l'OS persistent additionnel
            request.TypeConge = osP.GetObjectsQuery<CongeType>()
                .ToList()
                .FirstOrDefault(t => string.Equals(t.Code, "CPAYE",
                    StringComparison.OrdinalIgnoreCase));

            request.TypeOperation = TypeOperationConge.AllocationConge;
            request.AnneeOrigineSolde = DateTime.Today.Year;
            request.AnneeBulletin = DateTime.Today.Year;
            request.MoisBulletin = DateTime.Today.Month;
            request.NbJours = 24m;
            request.DateDebut = DateTime.Today;
            request.DateFin = DateTime.Today.AddDays(23);

            // Détection automatique du mode : AUTO si historique ≥ 12 mois
            if (salarie != null)
            {
                int nbBulletins = 0;
                try
                {
                    nbBulletins = osP.GetObjectsQuery<Bulletin>()
                        .Where(b => b.Salarie != null && b.Salarie.Oid == salarie.Oid)
                        .Count();
                }
                catch { }

                request.ModeCalcul = nbBulletins >= 12
                    ? ModeCalculIndemnite.Auto
                    : ModeCalculIndemnite.Manuel;
            }

            var detailView = Application.CreateDetailView(osNp, request);
            detailView.Caption = $"Saisir un congé — {salarie?.FullName ?? "salarié"}";

            e.View = detailView;
        }

        // ─────────────────────────────────────────────────────────────
        //  Au clic "Générer" : calcule + crée la ligne bulletin
        // ─────────────────────────────────────────────────────────────
        void OnExecute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            try
            {
                var request = e.PopupWindowViewCurrentObject as SaisieCongeRequest;
                if (request == null) { ShowError("Formulaire introuvable."); return; }

                if (request.TypeConge == null)
                { ShowError("Type de congé obligatoire."); return; }
                if (request.NbJours <= 0)
                { ShowError("Le nombre de jours doit être > 0."); return; }
                if (request.ModeCalcul == ModeCalculIndemnite.Manuel
                    && request.MontantManuel <= 0)
                { ShowError("En mode MANUEL, le montant doit être > 0."); return; }

                var salarieRaw = (Salarie)View.CurrentObject;
                if (salarieRaw == null) { ShowError("Salarié introuvable."); return; }

                // OS persistant dédié pour le commit
                using var os = Application.CreateObjectSpace(typeof(Bulletin));
                var salarie = os.GetObjectByKey<Salarie>(salarieRaw.Oid);
                var typeConge = os.GetObjectByKey<CongeType>(request.TypeConge.Oid);
                if (salarie == null || typeConge == null)
                { ShowError("Impossible de relocaliser salarié/type de congé."); return; }

                // ─── 1. Calcul du montant ──────────────────────────────
                decimal montant;
                string detailsCalcul;
                if (request.ModeCalcul == ModeCalculIndemnite.Auto)
                {
                    var res = BulletinCongeService.CalculerAllocationAuto(
                        os, salarie, request.NbJours,
                        request.AnneeBulletin, request.MoisBulletin);

                    if (!res.Succes || res.Montant <= 0)
                    {
                        ShowError("Calcul AUTO impossible — vérifiez que le " +
                                  "salarié a au moins 1 bulletin sur les 12 " +
                                  "derniers mois. Passez en mode MANUEL si " +
                                  "saisie rétroactive.");
                        return;
                    }

                    montant = res.Montant;
                    detailsCalcul = res.FormuleUtilisee
                        + (res.HistoriqueComplet ? "" : $"\n  ⚠️ Historique partiel : {res.NbMoisHistoriqueTrouves}/12 mois");
                }
                else
                {
                    montant = request.MontantManuel;
                    detailsCalcul = $"Saisie manuelle (montant repris " +
                                    $"ancien système / décision RH).";
                }

                // ─── 2. Mode bulletin (Unique / Séparé) ────────────────
                var parametres = ParametresPaie.TryGet(os);
                var modeBulletin = parametres?.ModeBulletinConges
                    ?? DomainEnums.ModeBulletinConges.BulletinUnique;

                // ─── 3. V1.8 — Bascule en bulletin de congé (si demandé)
                //    Supprime les rubriques de salaire normal AVANT d'ajouter
                //    l'indemnité de congé. Uniquement pour AllocationConge.
                int nbLignesSupprimees = 0;
                if (request.TypeOperation == TypeOperationConge.AllocationConge
                    && request.BasculerEnBulletinDeConge)
                {
                    // Chercher le bulletin existant (sera créé par CreerLigneAllocationConge
                    // s'il n'existe pas — dans ce cas, rien à nettoyer)
                    var bulletinExistant = os.GetObjectsQuery<Bulletin>()
                        .FirstOrDefault(b => b.Salarie.Oid == salarie.Oid
                                          && b.Annee == request.AnneeBulletin
                                          && b.Mois == request.MoisBulletin);

                    if (bulletinExistant != null)
                    {
                        nbLignesSupprimees = BulletinCongeService
                            .NettoyerRubriquesSalaireMensuel(os, bulletinExistant);
                    }
                }

                // ─── 4. Génération de la ligne ─────────────────────────
                BulletinCongeGenerationResult result;
                if (request.TypeOperation == TypeOperationConge.AllocationConge)
                {
                    result = BulletinCongeService.CreerLigneAllocationConge(
                        os, salarie,
                        request.AnneeBulletin, request.MoisBulletin,
                        request.NbJours, montant, request.Motif,
                        modeBulletin, detailsCalcul);
                }
                else // RachatICCP
                {
                    result = BulletinCongeService.CreerLigneRachatICCP(
                        os, salarie,
                        request.AnneeBulletin, request.MoisBulletin,
                        request.NbJours, montant, request.Motif,
                        request.AnneeOrigineSolde, detailsCalcul);
                }

                if (!result.Succes)
                {
                    ShowError(result.Message);
                    return;
                }

                // ─── 4. Débit du solde de congés ───────────────────────
                try
                {
                    var solde = SoldeCongeCalculService.GetOrCreateSolde(
                        os, salarie, typeConge, request.AnneeOrigineSolde);

                    MouvementSolde.Creer(os, solde,
                        DomainEnums.MouvementSoldeType.PriseCongé,
                        request.NbJours,
                        reference: $"BULL-{request.AnneeBulletin}-{request.MoisBulletin:D2}",
                        commentaire:
                            (request.TypeOperation == TypeOperationConge.AllocationConge
                                ? "Congé pris"
                                : "Rachat ICCP")
                            + $" — {montant:N0} FCFA"
                            + (string.IsNullOrWhiteSpace(request.Motif)
                                ? ""
                                : $" — {request.Motif}"));
                }
                catch (Exception exSolde)
                {
                    // Non bloquant — la ligne bulletin est créée même si le
                    // débit solde échoue (à régulariser manuellement)
                    Application.ShowViewStrategy?.ShowMessage(
                        $"⚠️ Ligne bulletin créée mais débit solde a échoué : {exSolde.Message}",
                        InformationType.Warning, 8000, InformationPosition.Top);
                }

                os.CommitChanges();

                // ─── 5. Message de confirmation ────────────────────────
                string opLabel = request.TypeOperation == TypeOperationConge.AllocationConge
                    ? "Allocation de congé"
                    : "Rachat ICCP";

                string ligneBascule = nbLignesSupprimees > 0
                    ? $"\n  • ⚠️ Bulletin basculé en bulletin de congé : " +
                      $"{nbLignesSupprimees} rubrique(s) de salaire supprimée(s)" +
                      $" (les cotisations seront recalculées sur la nouvelle base)"
                    : "";

                Application.ShowViewStrategy?.ShowMessage(
                    $"✅ {opLabel} générée pour {salarie.FullName}\n" +
                    $"  • Bulletin : {request.MoisBulletin:D2}/{request.AnneeBulletin}\n" +
                    $"  • Jours    : {request.NbJours:N1}\n" +
                    $"  • Montant  : {montant:N0} FCFA\n" +
                    $"  • Rubrique : {result.CodeRubrique}\n" +
                    $"  • Détails  : {detailsCalcul}" +
                    ligneBascule,
                    InformationType.Success, 12000, InformationPosition.Top);

                View.Refresh();
            }
            catch (Exception ex)
            {
                ShowError($"Erreur saisie congé : {ex.Message}");
            }
        }

        void ShowError(string message)
        {
            Application.ShowViewStrategy?.ShowMessage(
                $"❌ {message}",
                InformationType.Error, 8000, InformationPosition.Top);
        }
    }
}
