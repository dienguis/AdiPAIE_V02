using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using System;
using System.Collections.Generic;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Bouton dans ParametresPaie → Administration :
    /// crée tous les rôles GRH et leurs permissions Member en une seule action.
    ///
    /// Idempotent : si un rôle existe déjà, ses permissions sont enrichies
    /// sans duplication ni suppression des permissions existantes.
    /// </summary>
    public class InitialiserRolesGRHController
        : ObjectViewController<DetailView, ParametresPaie>
    {
        readonly SimpleAction initialiserRolesAction;

        public InitialiserRolesGRHController()
        {
            initialiserRolesAction = new SimpleAction(this,
                "Admin_InitialiserRolesGRH", PredefinedCategory.Edit)
            {
                Caption = "Init. rôles GRH",
                ImageName = "BO_Security_Permission",
                PaintStyle = DevExpress.ExpressApp.Templates.ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Crée automatiquement les 7 rôles GRH "
                           + "(Employe, Responsable, AssistantRH, AssistantCommercial, RH, DAF, Comptable) "
                           + "avec toutes leurs permissions. "
                           + "Idempotent — sans écrasement des personnalisations.",
                ConfirmationMessage =
                    "Cette action va créer (ou compléter) les 7 rôles GRH :\n"
                    + "Employe, Responsable, AssistantRH, AssistantCommercial, RH, DAF, Comptable.\n\n"
                    + "Les rôles existants ne seront pas écrasés.\n"
                    + "Continuer ?"
            };
            initialiserRolesAction.Execute += InitialiserRolesAction_Execute;
        }

        void InitialiserRolesAction_Execute(
            object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var os = ObjectSpace;
                int nbRoles = 0, nbPerms = 0;

                // ══ EMPLOYE ══════════════════════════════════════════
                var employe = GetOrCreate(os, "Employe", ref nbRoles);
                // Peut lire/créer/modifier ses propres objets GRH
                AddType<DemandeDeplacement>(employe, "rwd");
                AddMember<DemandeDeplacement>(employe,
                    "Objet;Destination;DateDepart;DateRetour;MotifDeplacement",
                    write: true, ref nbPerms);
                AddObject<DemandeDeplacement>(employe,
                    "Salarie.SystemUser.UserName = CurrentUserName()");

                AddType<CongeDemande>(employe, "rwd");
                AddObject<CongeDemande>(employe,
                    "Salarie.SystemUser.UserName = CurrentUserName()");

                AddType<DemandeAttestation>(employe, "rwd");
                AddObject<DemandeAttestation>(employe,
                    "Salarie.SystemUser.UserName = CurrentUserName()");

                AddType<EntretienAnnuel>(employe, "r");
                AddMember<EntretienAnnuel>(employe,
                    "CommentairesCollaborateur;EvolutionSouhaitee",
                    write: true, ref nbPerms);
                AddObject<EntretienAnnuel>(employe,
                    "Salarie.SystemUser.UserName = CurrentUserName()");

                AddType<LigneCircuit>(employe, "rwcd");
                AddType<LigneFraisMission>(employe, "r");
                AddType<NotificationSalarie>(employe, "r");

                // ══ RESPONSABLE (N+1) ════════════════════════════════
                var responsable = GetOrCreate(os, "Responsable", ref nbRoles);
                AddType<DemandeDeplacement>(responsable, "rw");
                AddMember<DemandeDeplacement>(responsable,
                    "DateValidationN1;ValideurN1;MotifRejet;RejeteParNom",
                    write: true, ref nbPerms);
                AddMember<LigneFraisMission>(responsable,
                    "TauxUnitaire;Montant;Quantite",
                    write: true, ref nbPerms);
                // N+1 voit les demandes dont il est le valideur
                AddObject<DemandeDeplacement>(responsable,
                    "ValideurN1.SystemUser.UserName = CurrentUserName()");

                AddType<CongeDemande>(responsable, "rw");
                AddMember<CongeDemande>(responsable,
                    "DateValidationN1;ValideurN1;MotifRejetN1",
                    write: true, ref nbPerms);
                AddObject<CongeDemande>(responsable,
                    "ValideurN1.SystemUser.UserName = CurrentUserName()");

                AddType<DemandeAttestation>(responsable, "rw");
                AddMember<DemandeAttestation>(responsable,
                    "DateValidationN1;ValideurN1;DateValidationN2;ValideurN2;MotifRejet",
                    write: true, ref nbPerms);

                AddType<EntretienAnnuel>(responsable, "rw");
                AddMember<EntretienAnnuel>(responsable,
                    "NoteGlobaleManager;NoteGlobaleService;CommentairesHierarchie;"
                    + "CommentaireManager;EstEnSituationEncadrement;ConclusionGenerale;"
                    + "PromotionProposee;AugmentationProposee;FormationIdentifiee;"
                    + "DateValidationN1;DateValidationN2;MotifRejetN2",
                    write: true, ref nbPerms);
                AddObject<EntretienAnnuel>(responsable,
                    "Evaluateur.SystemUser.UserName = CurrentUserName()");

                AddType<EntretienLigne>(responsable, "rw");
                AddType<EntretienMission>(responsable, "rw");
                AddType<EntretienManagement>(responsable, "rw");
                AddType<LigneFraisMission>(responsable, "rw");

                // ══ ASSISTANT RH ═════════════════════════════════════
                var assistant = GetOrCreate(os, "AssistantRH", ref nbRoles);
                AddType<DemandeDeplacement>(assistant, "rw");
                AddMember<DemandeDeplacement>(assistant,
                    "TraiteParAssistant;NumeroOrdre;DocumentOrdre",
                    write: true, ref nbPerms);
                AddType<LigneFraisMission>(assistant, "rwcd");
                AddType<LigneCircuit>(assistant, "rw");
                AddType<CategorieFraisMission>(assistant, "r");
                AddType<NotificationSalarie>(assistant, "rc");

                // V1.5 — Workflow Mouvements Intérim : 1ʳᵉ étape de validation
                AddType<DemandeMouvementInterim>(assistant, "rw");
                AddMember<DemandeMouvementInterim>(assistant,
                    "AssistantRHValidationUser;AssistantRHDate;AssistantRHCommentaire",
                    write: true, ref nbPerms);

                // ══ ASSISTANT COMMERCIAL (V1.5) ══════════════════════
                // Initie les demandes de mouvement intérim pour les stations
                // qui sont sous sa responsabilité (collection Salarie.StationsGerees).
                // Voit ses propres demandes (filtre Initiateur = currentUser).
                var assistantCom = GetOrCreate(os, "AssistantCommercial", ref nbRoles);
                AddType<DemandeMouvementInterim>(assistantCom, "rwc"); // pas de delete
                AddObject<DemandeMouvementInterim>(assistantCom,
                    "Initiateur.Email = CurrentUserName() "
                    + "AND Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,Brouillon#");
                // Lecture seule sur les intérimaires & contrats (pour lookup et autocomplétion)
                AddType<Interimaire>(assistantCom, "r");
                AddType<ContratInterim>(assistantCom, "r");
                AddType<StationService>(assistantCom, "r");
                AddType<PosteInterimaire>(assistantCom, "r");
                AddType<MouvementInterimaire>(assistantCom, "r");
                AddType<NotificationSalarie>(assistantCom, "rc");

                // ══ RH ═══════════════════════════════════════════════
                var rh = GetOrCreate(os, "RH", ref nbRoles);
                AddType<DemandeDeplacement>(rh, "rw");
                AddMember<DemandeDeplacement>(rh,
                    "ApprouveParRH;DateApprobationRH;MotifRejet;RejeteParNom;DocumentOrdre",
                    write: true, ref nbPerms);
                AddType<LigneFraisMission>(rh, "rwcd");
                AddType<LigneCircuit>(rh, "rw");

                AddType<CongeDemande>(rh, "rw");
                AddMember<CongeDemande>(rh,
                    "DateReprise;StatutRH;CommentairesRH",
                    write: true, ref nbPerms);

                AddType<DemandeAttestation>(rh, "rwcd");

                // V1.5 — Workflow Mouvements Intérim : RH peut valider à toutes
                // les étapes (court-circuit possible si Assistant RH absent)
                AddType<DemandeMouvementInterim>(rh, "rwcd");

                AddType<EntretienAnnuel>(rh, "rw");
                AddMember<EntretienAnnuel>(rh,
                    "DateCloture;CloturePar;ScoreGlobal;NotesDecisionRH",
                    write: true, ref nbPerms);
                AddType<EntretienLigne>(rh, "rw");
                AddType<EntretienMission>(rh, "rw");
                AddType<EntretienManagement>(rh, "rw");
                AddType<CampagneEvaluation>(rh, "rwcd");
                AddType<NotificationSalarie>(rh, "rwcd");
                AddType<DossierDocument>(rh, "rwcd");
                AddType<DossierSalarie>(rh, "rwcd");

                // ══ DAF ══════════════════════════════════════════════
                var daf = GetOrCreate(os, "DAF", ref nbRoles);
                AddType<DemandeDeplacement>(daf, "r");
                AddMember<DemandeDeplacement>(daf,
                    "ValideParDAF;DateValidationDAF",
                    write: true, ref nbPerms);

                // V1.5 — Workflow Mouvements Intérim : DAF approuve si
                // OptionApprobationDAF=true sur la demande
                AddType<DemandeMouvementInterim>(daf, "rw");
                AddMember<DemandeMouvementInterim>(daf,
                    "DAFValidationUser;DAFDate;DAFCommentaire",
                    write: true, ref nbPerms);
                // DAF voit toutes les demandes approuvées par RH
                AddObject<DemandeDeplacement>(daf,
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+"
                    + "DeplacementStatut,ApprouveeRH#");
                AddType<LigneFraisMission>(daf, "r");
                AddType<LigneCircuit>(daf, "r");

                // ══ COMPTABLE ════════════════════════════════════════
                var comptable = GetOrCreate(os, "Comptable", ref nbRoles);
                AddType<DemandeDeplacement>(comptable, "r");
                AddMember<DemandeDeplacement>(comptable,
                    "ConfirmeParComptable",
                    write: true, ref nbPerms);
                AddObject<DemandeDeplacement>(comptable,
                    "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+"
                    + "DeplacementStatut,EnAttenteComptable#");
                AddType<LigneFraisMission>(comptable, "r");
                AddType<LigneCircuit>(comptable, "r");

                // ── Commit ────────────────────────────────────────────
                os.CommitChanges();

                Application.ShowViewStrategy?.ShowMessage(
                    $"Initialisation terminée : {nbRoles} rôle(s) créé(s), "
                    + $"{nbPerms} permission(s) ajoutée(s). "
                    + "Relancez l'application pour que les droits soient actifs.",
                    InformationType.Success, 6000, InformationPosition.Top);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy?.ShowMessage(
                    $"Erreur lors de l'initialisation : {ex.Message}",
                    InformationType.Error, 8000, InformationPosition.Top);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>Récupère ou crée un rôle par son nom.</summary>
        private PermissionPolicyRole GetOrCreate(
            IObjectSpace os, string nom, ref int nbRoles)
        {
            var role = os.FirstOrDefault<PermissionPolicyRole>(
                r => r.Name == nom);
            if (role == null)
            {
                role = os.CreateObject<PermissionPolicyRole>();
                role.Name = nom;
                nbRoles++;
            }
            return role;
        }

        /// <summary>
        /// Ajoute les Type Permissions selon un code compact :
        /// r=Read, w=Write, c=Create, d=Delete, n=Navigate
        /// </summary>
        private void AddType<T>(PermissionPolicyRole role, string ops)
            where T : class
        {
            if (ops.Contains("r"))
                role.AddTypePermission<T>(SecurityOperations.Read,
                    SecurityPermissionState.Allow);
            if (ops.Contains("w"))
                role.AddTypePermission<T>(SecurityOperations.Write,
                    SecurityPermissionState.Allow);
            if (ops.Contains("c"))
                role.AddTypePermission<T>(SecurityOperations.Create,
                    SecurityPermissionState.Allow);
            if (ops.Contains("d"))
                role.AddTypePermission<T>(SecurityOperations.Delete,
                    SecurityPermissionState.Allow);
            // Navigate toujours accordé si Read est accordé
            if (ops.Contains("r") || ops.Contains("n"))
                role.AddTypePermission<T>(SecurityOperations.Navigate,
                    SecurityPermissionState.Allow);
        }

        /// <summary>
        /// Ajoute des Member Permissions sur plusieurs champs (séparés par ;).
        /// </summary>
        private void AddMember<T>(PermissionPolicyRole role,
            string members, bool write, ref int nbPerms)
            where T : class
        {
            var typeName = typeof(T).FullName;
            var typePerm = GetOrCreateTypePerm<T>(role);

            foreach (var m in members.Split(';'))
            {
                var member = m.Trim();
                if (string.IsNullOrEmpty(member)) continue;

                // Vérifie si la permission membre existe déjà
                bool existe = false;
                foreach (var mp in typePerm.MemberPermissions)
                {
                    if (mp.Members == member) { existe = true; break; }
                }
                if (existe) continue;

                var mp2 = ObjectSpace.CreateObject<PermissionPolicyMemberPermissionsObject>();
                mp2.Members = member;
                mp2.ReadState = SecurityPermissionState.Allow;
                mp2.WriteState = write
                    ? SecurityPermissionState.Allow
                    : SecurityPermissionState.Deny;
                typePerm.MemberPermissions.Add(mp2);
                nbPerms++;
            }
        }

        /// <summary>Ajoute un filtre objet (criteria) sur un type.</summary>
        private void AddObject<T>(PermissionPolicyRole role, string criteria)
            where T : class
        {
            var typePerm = GetOrCreateTypePerm<T>(role);
            // N'ajoute pas si un critère identique existe déjà
            foreach (var op in typePerm.ObjectPermissions)
                if (op.Criteria == criteria) return;

            var obj = ObjectSpace.CreateObject<PermissionPolicyObjectPermissionsObject>();
            obj.Criteria = criteria;
            obj.ReadState = SecurityPermissionState.Allow;
            obj.WriteState = SecurityPermissionState.Allow;
            typePerm.ObjectPermissions.Add(obj);
        }

        private PermissionPolicyTypePermissionObject GetOrCreateTypePerm<T>(
            PermissionPolicyRole role) where T : class
        {
            var typeName = typeof(T).FullName;
            foreach (var tp in role.TypePermissions)
                if (tp.TargetType?.FullName == typeName) return tp;

            var newTp = ObjectSpace.CreateObject<PermissionPolicyTypePermissionObject>();
            newTp.TargetType = typeof(T);
            role.TypePermissions.Add(newTp);
            return newTp;
        }
    }
}
