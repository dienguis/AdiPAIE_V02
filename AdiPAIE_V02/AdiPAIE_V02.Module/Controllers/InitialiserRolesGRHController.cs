using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using System;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Bouton dans ParametresPaie → Administration : crée tous les rôles GRH
    /// et leurs permissions Member en une seule action.
    ///
    /// Idempotent : si un rôle existe déjà, ses permissions sont enrichies
    /// sans duplication ni suppression des permissions existantes.
    ///
    /// V1.5.2 (QW1) — La logique métier est extraite dans la classe statique
    /// <see cref="RolesGRHInitializer"/> pour pouvoir être appelée aussi
    /// depuis l'<see cref="DatabaseUpdate.Updater"/> au démarrage de l'app.
    /// Le controller XAF garde uniquement la responsabilité UI (bouton +
    /// confirmation + toast).
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
                ToolTip = "Crée automatiquement les 8 rôles GRH "
                           + "(Employe, Responsable, AssistantRH, AssistantCommercial, RH, DAF, DG, Comptable) "
                           + "avec toutes leurs permissions. "
                           + "Idempotent — sans écrasement des personnalisations.",
                ConfirmationMessage =
                    "Cette action va créer (ou compléter) les 8 rôles GRH :\n"
                    + "Employe, Responsable, AssistantRH, AssistantCommercial, RH, DAF, "
                    + "DG (Directeur Général), Comptable.\n\n"
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
                var (nbRoles, nbPerms) = RolesGRHInitializer.Initialize(ObjectSpace);

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
    }

    /// <summary>
    /// V1.5.2 (QW1) — Logique pure de création/maj des rôles GRH et de leurs
    /// permissions. Réutilisable depuis :
    ///   - <see cref="InitialiserRolesGRHController"/> (action UI manuelle)
    ///   - <see cref="DatabaseUpdate.Updater"/> (auto-init au démarrage)
    ///
    /// Idempotent : ne supprime jamais une permission existante, ne dédoublonne
    /// pas non plus (les anciens roles enrichis avec de nouvelles permissions
    /// V1.5 lors d'un redéploiement).
    /// </summary>
    public static class RolesGRHInitializer
    {
        /// <summary>
        /// Crée ou complète tous les rôles GRH (Employe, Responsable, AssistantRH,
        /// AssistantCommercial, RH, DAF, DG, Comptable). Commit l'ObjectSpace à la fin.
        ///
        /// V1.7.2 — Ajout du rôle DG (Directeur Général) :
        ///   - Lecture complète sur le métier (paie, congés, entretiens, etc.)
        ///   - Lecture des dashboards stratégiques
        ///   - Validation top-down sur DossierOffboarding (clôture définitive)
        ///   - PAS d'écriture sur les paramètres techniques
        /// </summary>
        /// <returns>(nbRolesCrees, nbPermissionsAjoutees)</returns>
        public static (int nbRoles, int nbPerms) Initialize(IObjectSpace os)
        {
            int nbRoles = 0, nbPerms = 0;

            // ══ EMPLOYE ══════════════════════════════════════════
            var employe = GetOrCreate(os, "Employe", ref nbRoles);
            AddType<DemandeDeplacement>(employe, "rwd");
            AddMember<DemandeDeplacement>(os, employe,
                "Objet;Destination;DateDepart;DateRetour;MotifDeplacement",
                write: true, ref nbPerms);
            AddObject<DemandeDeplacement>(os, employe,
                "Salarie.SystemUser.UserName = CurrentUserName()");

            AddType<CongeDemande>(employe, "rwd");
            AddObject<CongeDemande>(os, employe,
                "Salarie.SystemUser.UserName = CurrentUserName()");

            AddType<DemandeAttestation>(employe, "rwd");
            AddObject<DemandeAttestation>(os, employe,
                "Salarie.SystemUser.UserName = CurrentUserName()");

            AddType<EntretienAnnuel>(employe, "r");
            AddMember<EntretienAnnuel>(os, employe,
                "CommentairesCollaborateur;EvolutionSouhaitee",
                write: true, ref nbPerms);
            AddObject<EntretienAnnuel>(os, employe,
                "Salarie.SystemUser.UserName = CurrentUserName()");

            AddType<LigneCircuit>(employe, "rwcd");
            AddType<LigneFraisMission>(employe, "r");
            AddType<NotificationSalarie>(employe, "r");

            // ══ RESPONSABLE (N+1) ════════════════════════════════
            var responsable = GetOrCreate(os, "Responsable", ref nbRoles);
            AddType<DemandeDeplacement>(responsable, "rw");
            AddMember<DemandeDeplacement>(os, responsable,
                "DateValidationN1;ValideurN1;MotifRejet;RejeteParNom",
                write: true, ref nbPerms);
            AddMember<LigneFraisMission>(os, responsable,
                "TauxUnitaire;Montant;Quantite",
                write: true, ref nbPerms);
            AddObject<DemandeDeplacement>(os, responsable,
                "ValideurN1.SystemUser.UserName = CurrentUserName()");

            AddType<CongeDemande>(responsable, "rw");
            AddMember<CongeDemande>(os, responsable,
                "DateValidationN1;ValideurN1;MotifRejetN1",
                write: true, ref nbPerms);
            AddObject<CongeDemande>(os, responsable,
                "ValideurN1.SystemUser.UserName = CurrentUserName()");

            AddType<DemandeAttestation>(responsable, "rw");
            AddMember<DemandeAttestation>(os, responsable,
                "DateValidationN1;ValideurN1;DateValidationN2;ValideurN2;MotifRejet",
                write: true, ref nbPerms);

            AddType<EntretienAnnuel>(responsable, "rw");
            AddMember<EntretienAnnuel>(os, responsable,
                "NoteGlobaleManager;NoteGlobaleService;CommentairesHierarchie;"
                + "CommentaireManager;EstEnSituationEncadrement;ConclusionGenerale;"
                + "PromotionProposee;AugmentationProposee;FormationIdentifiee;"
                + "DateValidationN1;DateValidationN2;MotifRejetN2",
                write: true, ref nbPerms);
            AddObject<EntretienAnnuel>(os, responsable,
                "Evaluateur.SystemUser.UserName = CurrentUserName()");

            AddType<EntretienLigne>(responsable, "rw");
            AddType<EntretienMission>(responsable, "rw");
            AddType<EntretienManagement>(responsable, "rw");
            AddType<LigneFraisMission>(responsable, "rw");

            // ══ ASSISTANT RH ═════════════════════════════════════
            var assistant = GetOrCreate(os, "AssistantRH", ref nbRoles);
            AddType<DemandeDeplacement>(assistant, "rw");
            AddMember<DemandeDeplacement>(os, assistant,
                "TraiteParAssistant;NumeroOrdre;DocumentOrdre",
                write: true, ref nbPerms);
            AddType<LigneFraisMission>(assistant, "rwcd");
            AddType<LigneCircuit>(assistant, "rw");
            AddType<CategorieFraisMission>(assistant, "r");
            AddType<NotificationSalarie>(assistant, "rc");

            // V1.5 — Workflow Mouvements Intérim : 1ʳᵉ étape de validation
            AddType<DemandeMouvementInterim>(assistant, "rw");
            AddMember<DemandeMouvementInterim>(os, assistant,
                "AssistantRHValidationUser;AssistantRHDate;AssistantRHCommentaire",
                write: true, ref nbPerms);

            // V1.7.2 — Élargissement AssistantRH aux modules opérationnels
            //   Intérim : voir fiches, contrats, mouvements, sociétés, alertes
            AddType<Interimaire>(assistant, "rwc"); nbPerms += 3;
            AddType<ContratInterim>(assistant, "rwc"); nbPerms += 3;
            AddType<MouvementInterimaire>(assistant, "rwc"); nbPerms += 3;
            AddType<AlerteInterimaire>(assistant, "rw"); nbPerms += 2;
            AddType<SocieteInterim>(assistant, "r"); nbPerms += 1;
            AddType<DemandeRecrutementInterim>(assistant, "rwc"); nbPerms += 3;
            AddType<FormationInterimaire>(assistant, "rwcd"); nbPerms += 4;
            AddType<EvaluationInterimaire>(assistant, "rwcd"); nbPerms += 4;

            //   Formation/Évaluation : organiser les sessions, voir entretiens
            AddType<SessionFormation>(assistant, "rwcd"); nbPerms += 4;
            AddType<CampagneEvaluation>(assistant, "r"); nbPerms += 1;
            AddType<EntretienAnnuel>(assistant, "r"); nbPerms += 1;
            AddType<CritereEvaluationRef>(assistant, "r"); nbPerms += 1;
            AddType<DemandeAttestation>(assistant, "rwc"); nbPerms += 3;

            //   🔒 SÉCURITÉ STRICTE V1.7.2 :
            //   AssistantRH N'A PAS d'accès direct à Salarie.
            //   - Il ne doit pas pouvoir lister les salariés
            //   - Il ne doit pas pouvoir ouvrir une fiche salarié
            //   - Il ne doit pas pouvoir sélectionner un salarié dans un dropdown
            //   Toutes les infos salaire (Salaire, Indemnités, Échelon, etc.)
            //   restent réservées EXCLUSIVEMENT à RH, DAF et DG.
            //
            //   Pour ses propres congés/attestations/déplacements, le user
            //   cumule le rôle Employe qui filtre par CurrentUserName via
            //   Object Permission (donc accès à sa SEULE fiche salarié).

            // Navigation permissions (menus Formation + Intérim)
            AddNavFormationEtInterim(assistant);
            nbPerms += 20;

            // ══ ASSISTANT COMMERCIAL (V1.5) ══════════════════════
            var assistantCom = GetOrCreate(os, "AssistantCommercial", ref nbRoles);
            AddType<DemandeMouvementInterim>(assistantCom, "rwc");
            AddObject<DemandeMouvementInterim>(os, assistantCom,
                "Initiateur.Email = CurrentUserName() "
                + "AND Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,Brouillon#");
            AddType<Interimaire>(assistantCom, "r");
            AddType<ContratInterim>(assistantCom, "r");
            AddType<StationService>(assistantCom, "r");
            AddType<PosteInterimaire>(assistantCom, "r");
            AddType<MouvementInterimaire>(assistantCom, "r");
            AddType<NotificationSalarie>(assistantCom, "rc");

            // ══ RH ═══════════════════════════════════════════════
            var rh = GetOrCreate(os, "RH", ref nbRoles);
            // V1.8 — Ajout 'c' (Create) + 'd' (Delete) : le RH doit pouvoir
            // créer ses propres demandes (congé, déplacement, attestation)
            // sans cumuler le rôle Employé. Cf. ticket "décombiner RH+Employé".
            AddType<DemandeDeplacement>(rh, "rwcd");
            AddMember<DemandeDeplacement>(os, rh,
                "ApprouveParRH;DateApprobationRH;MotifRejet;RejeteParNom;DocumentOrdre",
                write: true, ref nbPerms);
            AddType<LigneFraisMission>(rh, "rwcd");
            AddType<LigneCircuit>(rh, "rw");

            // V1.8 — Idem pour les congés : RH peut créer sa propre demande
            AddType<CongeDemande>(rh, "rwcd");
            AddMember<CongeDemande>(os, rh,
                "DateReprise;StatutRH;CommentairesRH",
                write: true, ref nbPerms);

            AddType<DemandeAttestation>(rh, "rwcd");

            // V1.5 — Workflow Mouvements Intérim
            AddType<DemandeMouvementInterim>(rh, "rwcd");

            AddType<EntretienAnnuel>(rh, "rw");
            AddMember<EntretienAnnuel>(os, rh,
                "DateCloture;CloturePar;ScoreGlobal;NotesDecisionRH",
                write: true, ref nbPerms);
            AddType<EntretienLigne>(rh, "rw");
            AddType<EntretienMission>(rh, "rw");
            AddType<EntretienManagement>(rh, "rw");
            AddType<CampagneEvaluation>(rh, "rwcd");
            AddType<NotificationSalarie>(rh, "rwcd");
            AddType<DossierDocument>(rh, "rwcd");
            AddType<DossierSalarie>(rh, "rwcd");

            // V1.6.2 — Permissions Bulletin + Salarie pour RH (manquaient)
            // Sans ces accès, la "Consultation bulletins" RH n'affichait que
            // Statut + NetAPayer (Salarie navigation hidden, BrutFiscal/Social hidden).
            // RH a besoin du contexte salarié complet pour gérer la paie.
            // IMPORTANT : après ces ajouts, le user RH doit se DECONNECTER puis
            // se RECONNECTER (XAF cache les permissions au login).
            // NB: AddType est idempotent côté XAF — re-runs sans effet si déjà présent.
            // On incrémente nbPerms pour avoir un compteur visible (premier run).
            AddType<Salarie>(rh, "rw");      nbPerms += 2; // Read + Write
            AddType<Bulletin>(rh, "rwc");    nbPerms += 3; // Read + Write + Create
            // V1.8 — Ajout 'c' (Create) + 'd' (Delete) sur BulletinLigne :
            // sans Create, le bouton "New" est masqué dans la grille Lignes
            // → RH ne peut pas ajouter manuellement une rubrique (ex: avantage
            // en nature sur un bulletin de congé personnalisé).
            AddType<BulletinLigne>(rh, "rwcd"); nbPerms += 4;
            AddType<PeriodePaie>(rh, "rw");  nbPerms += 2;
            AddType<Conjoint>(rh, "rwcd");   nbPerms += 4; // Famille (TRIMF)
            AddType<Enfant>(rh, "rwcd");     nbPerms += 4; // Famille V1.6
            // V1.7 — Annuaire famille hiérarchique
            AddType<AdiPAIE_V02.Module.NonPersistent.FamilleAnnuaire>(rh, "r");
            nbPerms += 1;
            // V1.7 — Provision congés annuelle
            AddType<AdiPAIE_V02.Module.NonPersistent.ProvisionConges>(rh, "r");
            nbPerms += 1;

            // V1.7.2 — Navigation permissions (menus principaux)
            AddNavManagerMenus(rh);
            nbPerms += 30;

            // V1.8 — Le masquage de "Mon espace" pour RH/DAF/DG est fait
            // au runtime par HideEspaceSalarieController (le Deny déclaratif
            // ne fonctionne pas en XAF Blazor quand Employé a Allow).

            // ══ DAF ══════════════════════════════════════════════
            var daf = GetOrCreate(os, "DAF", ref nbRoles);
            // V1.8 — DAF peut créer/éditer ses propres demandes de déplacement
            // (avant : "r" lecture seule → le DAF devait demander au RH de
            // saisir sa demande pour lui). Validation DAF reste possible
            // via les champs ValideParDAF/DateValidationDAF.
            AddType<DemandeDeplacement>(daf, "rwcd");
            AddMember<DemandeDeplacement>(os, daf,
                "ValideParDAF;DateValidationDAF",
                write: true, ref nbPerms);

            // V1.5 — Workflow Mouvements Intérim : DAF approuve si OptionApprobationDAF
            AddType<DemandeMouvementInterim>(daf, "rw");
            AddMember<DemandeMouvementInterim>(os, daf,
                "DAFValidationUser;DAFDate;DAFCommentaire",
                write: true, ref nbPerms);

            AddObject<DemandeDeplacement>(os, daf,
                "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+"
                + "DeplacementStatut,ApprouveeRH#");
            AddType<LigneFraisMission>(daf, "r");
            AddType<LigneCircuit>(daf, "r");

            // V1.7.2 — DAF voit aussi les rubriques pour validation
            AddType<Rubrique>(daf, "r"); nbPerms += 1;
            AddType<RubriqueTypeRef>(daf, "r"); nbPerms += 1;
            AddType<Salarie>(daf, "r"); nbPerms += 1;
            AddType<Bulletin>(daf, "r"); nbPerms += 1;
            AddType<BulletinLigne>(daf, "r"); nbPerms += 1;
            AddType<Pret>(daf, "r"); nbPerms += 1;
            // V1.8 — DAF peut créer/éditer ses propres demandes de congé
            // (avant : "r" lecture seule)
            AddType<CongeDemande>(daf, "rwcd"); nbPerms += 4;
            // Idem pour les attestations
            AddType<DemandeAttestation>(daf, "rwcd"); nbPerms += 4;
            AddType<TreiziemeMois>(daf, "r"); nbPerms += 1;
            AddType<Gratification>(daf, "rw"); nbPerms += 2;
            AddType<AdiPAIE_V02.Module.NonPersistent.ProvisionTreiziemeMois>(daf, "r"); nbPerms += 1;
            AddType<AdiPAIE_V02.Module.NonPersistent.RapportGratification>(daf, "r"); nbPerms += 1;
            AddType<AdiPAIE_V02.Module.BusinessObjects.RH.DossierOffboarding>(daf, "rw"); nbPerms += 2;

            // V1.7.2 — Navigation permissions (menus principaux)
            AddNavManagerMenus(daf);
            nbPerms += 30;

            // ══ DG (Directeur Général) ═══════════════════════════
            // V1.7.2 — Profil de SUPERVISION et VALIDATION STRATÉGIQUE.
            // Le DG ne gère pas l'opérationnel (RH/DAF s'en occupent) mais
            // a une vision LECTURE complète sur les sujets sensibles et peut
            // valider/approuver certains workflows critiques au plus haut niveau.
            //
            // Accès :
            //   ✅ Lecture : tous les salariés, contrats, bulletins, congés,
            //                entretiens, prêts, attestations, dossiers de départ
            //   ✅ Lecture : 13ième mois, gratifications, provisions
            //   ✅ Lecture : dashboards stratégiques (DRH + DAF)
            //   ✅ Validation : signature finale dossiers offboarding
            //                   (top-down après validation DAF)
            //   ❌ PAS d'écriture sur les paramètres techniques (rubriques,
            //      barèmes, plan comptable)
            //   ❌ PAS d'accès aux fonctions admin système
            var dg = GetOrCreate(os, "DG", ref nbRoles);
            // Lecture sur les éléments métier essentiels
            AddType<Salarie>(dg, "r");                   nbPerms += 1;
            AddType<ContratSalarie>(dg, "r");            nbPerms += 1;
            AddType<Bulletin>(dg, "r");                  nbPerms += 1;
            AddType<BulletinLigne>(dg, "r");             nbPerms += 1;
            AddType<PeriodePaie>(dg, "r");               nbPerms += 1;
            AddType<Conjoint>(dg, "r");                  nbPerms += 1;
            AddType<Enfant>(dg, "r");                    nbPerms += 1;
            // V1.8 — DG peut créer/éditer ses propres demandes personnelles
            // (congé, attestation, déplacement) sans cumuler le rôle Employé.
            // Lecture sur les autres salariés (vue 360°) conservée.
            AddType<CongeDemande>(dg, "rwcd");           nbPerms += 4;
            AddType<SoldeConge>(dg, "r");                nbPerms += 1;
            AddType<Pret>(dg, "r");                      nbPerms += 1;
            AddType<DemandeAttestation>(dg, "rwcd");     nbPerms += 4;
            AddType<EntretienAnnuel>(dg, "r");           nbPerms += 1;
            AddType<DemandeDeplacement>(dg, "rwcd");     nbPerms += 4;
            AddType<DemandeMouvementInterim>(dg, "r");   nbPerms += 1;

            // V1.7 — Annuaire famille hiérarchique
            AddType<AdiPAIE_V02.Module.NonPersistent.FamilleAnnuaire>(dg, "r");
            nbPerms += 1;
            // V1.7 — Provision congés annuelle
            AddType<AdiPAIE_V02.Module.NonPersistent.ProvisionConges>(dg, "r");
            nbPerms += 1;

            // V1.7.2 — 13ième mois + Gratifications + Provisions + Reporting
            AddType<TreiziemeMois>(dg, "r");             nbPerms += 1;
            AddType<Gratification>(dg, "r");             nbPerms += 1;
            AddType<AdiPAIE_V02.Module.NonPersistent.ProvisionTreiziemeMois>(dg, "r");
            nbPerms += 1;
            AddType<AdiPAIE_V02.Module.NonPersistent.RapportGratification>(dg, "r");
            nbPerms += 1;

            // Validation top-down : signature finale sur dossier offboarding
            // après validation DAF (au moment de la clôture définitive).
            AddType<AdiPAIE_V02.Module.BusinessObjects.RH.DossierOffboarding>(dg, "rw");
            nbPerms += 2;

            // Notifications (visibles mais non éditables)
            AddType<NotificationSalarie>(dg, "r");       nbPerms += 1;

            // V1.7.2 — Navigation permissions (menus principaux)
            // Sans ces permissions, le DG ne voit que "Mon espace" (rôle Employe).
            AddNavManagerMenus(dg);
            nbPerms += 30;

            // ══ COMPTABLE ════════════════════════════════════════
            var comptable = GetOrCreate(os, "Comptable", ref nbRoles);
            AddType<DemandeDeplacement>(comptable, "r");
            AddMember<DemandeDeplacement>(os, comptable,
                "ConfirmeParComptable",
                write: true, ref nbPerms);
            AddObject<DemandeDeplacement>(os, comptable,
                "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+"
                + "DeplacementStatut,EnAttenteComptable#");
            AddType<LigneFraisMission>(comptable, "r");
            AddType<LigneCircuit>(comptable, "r");

            // ── Commit ────────────────────────────────────────────
            os.CommitChanges();

            return (nbRoles, nbPerms);
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers (statiques pour réutilisation Updater + Controller)
        // ════════════════════════════════════════════════════════════════

        /// <summary>Récupère ou crée un rôle par son nom.</summary>
        private static PermissionPolicyRole GetOrCreate(
            IObjectSpace os, string nom, ref int nbRoles)
        {
            var role = os.FirstOrDefault<PermissionPolicyRole>(r => r.Name == nom);
            if (role == null)
            {
                role = os.CreateObject<PermissionPolicyRole>();
                role.Name = nom;
                nbRoles++;
            }
            return role;
        }

        /// <summary>
        /// Type Permissions selon code compact : r=Read, w=Write, c=Create,
        /// d=Delete, n=Navigate. Navigate accordé automatiquement avec Read.
        /// </summary>
        private static void AddType<T>(PermissionPolicyRole role, string ops)
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
            if (ops.Contains("r") || ops.Contains("n"))
                role.AddTypePermission<T>(SecurityOperations.Navigate,
                    SecurityPermissionState.Allow);
        }

        /// <summary>
        /// V1.7.2 — Helper pour ajouter une permission Navigation sur un menu.
        /// Indispensable pour que les rôles métier (DG/DAF/RH) VOIENT leurs menus
        /// dans la sidebar. Sans ces permissions, l'utilisateur ne voit que son
        /// espace personnel (Mon espace) même s'il a Read sur les Types.
        ///
        /// Path format : "Application/NavigationItems/Items/<MenuId>[/Items/<SubMenuId>]"
        /// </summary>
        private static void AddNav(PermissionPolicyRole role, string path)
        {
            role.AddNavigationPermission(path, SecurityPermissionState.Allow);
        }

        /// <summary>
        /// V1.8 — Refuse explicitement un item de menu pour un rôle.
        /// Utilisé pour masquer un menu à un rôle, MÊME quand un autre rôle
        /// combiné l'autoriserait (Deny gagne sur Allow en XAF).
        ///
        /// Cas d'usage : RH + Employé combinés → on ne veut pas montrer
        /// "Mon espace > Mes bulletins" au RH (il a déjà la vue 360°
        /// via le menu "Paie > Consultation bulletins").
        /// </summary>
        private static void AddNavDeny(PermissionPolicyRole role, string path)
        {
            role.AddNavigationPermission(path, SecurityPermissionState.Deny);
        }

        /// <summary>
        /// V1.8 — Tentative initiale via Navigation Permissions Deny.
        /// CONSTAT : Allow gagne sur Deny en XAF Blazor pour les Navigation
        /// Permissions des sub-items quand un autre rôle (Employé) a Allow.
        /// Le masquage est donc fait via HideEspaceSalarieController côté
        /// runtime (modification du Model.NavigationItems après login).
        ///
        /// Méthode laissée en place mais ne fait plus rien — supprimable
        /// à terme. Les rôles RH/DAF/DG n'appellent plus cette méthode.
        /// </summary>
        private static void DenyEspaceSalarie(PermissionPolicyRole role, ref int nbPerms)
        {
            // Plus utilisée — masquage fait via HideEspaceSalarieController.
            // Body vide pour éviter de polluer la BDD avec des Deny inutiles.
        }

        /// <summary>
        /// V1.7.2 — Bloc de navigation permissions standard pour les rôles
        /// "managers métier" (DG, DAF, RH). Accorde l'accès aux menus principaux
        /// de SunuPaie. Les sub-items hérités sont aussi accessibles.
        /// </summary>
        private static void AddNavManagerMenus(PermissionPolicyRole role)
        {
            // Menu PAIE
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/Paie_SalariesPaie");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/Paie_ConsultationBulletins");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/PeriodePaie_ListView");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/RH_Prets");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/RH_EcheancesPrets");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/SimulationSursalaire_ListView");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/Paie_TreiziemeMois");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/Paie_ProvisionTreiziemeMois");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/Paie_Gratifications");
            AddNav(role, @"Application/NavigationItems/Items/GRH - Paie/Items/Paie_RapportGratifications");

            // Menu RESSOURCES HUMAINES
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/RH_Salaries");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/ContratSalarie_ListView");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/RH_AnnuaireFamille");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/RH_Avancements");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/RH_HistoriquePostes");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/RH_Offboarding");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/RH_Dossiers");
            AddNav(role, @"Application/NavigationItems/Items/Ressources humaines/Items/NotificationSalarie_ListView");

            // Menu CONGÉS
            AddNav(role, @"Application/NavigationItems/Items/GRH_Conges");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Conges/Items/GRH_DemandesConge");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Conges/Items/GRH_SoldesConges");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Conges/Items/GRH_ProvisionConges");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Conges/Items/GRH_PlanningConges");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Conges/Items/JourFerie_ListView");

            // Menu TABLEAUX DE BORD (dashboards stratégiques)
            // V1.7.2 — Le vrai ID en XAF est "GRH - Tableaux de Bord" (cf. xafml).
            // "Tableaux de Bord" sans préfixe est un groupe legacy masqué.
            AddNav(role, @"Application/NavigationItems/Items/GRH - Tableaux de Bord");

            // Menu MISSIONS / DÉPLACEMENTS
            AddNav(role, @"Application/NavigationItems/Items/GRH_Missions");

            // Menu FORMATION / ÉVALUATION
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation");

            // Menu INTÉRIMAIRES (lecture pour DG/DAF, plus pour autres)
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires");
        }

        /// <summary>
        /// V1.7.2 — Navigation permissions ciblées sur les menus
        /// Formation/Évaluation et Intérimaires (avec sous-menus).
        /// Utilisé pour les rôles "opérationnels" qui n'ont PAS accès aux
        /// menus Paie/Tableaux de Bord (AssistantRH par exemple).
        /// </summary>
        private static void AddNavFormationEtInterim(PermissionPolicyRole role)
        {
            // Menu FORMATION / ÉVALUATION
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation/Items/GRH_Campagnes");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation/Items/GRH_Entretiens");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation/Items/GRH_Criteres");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation/Items/GRH_SessionsFormation");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Evaluation/Items/GRH_Attestations");

            // Menu INTÉRIMAIRES
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Fiches");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Demandes");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Contrats");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Mouvements");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_DemandesMouvement");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Alertes");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Formations");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Evaluations");
            AddNav(role, @"Application/NavigationItems/Items/GRH_Interimaires/Items/GRH_Int_Societes");
        }

        /// <summary>
        /// V1.7.2 — SÉCURITÉ PAIE : masque les champs salariaux sensibles
        /// pour les rôles non habilités à voir les informations de paie.
        ///
        /// RÈGLE MÉTIER ELTON (validée 2026-05) :
        ///   Toutes les informations salariales (montants, indemnités, échelons,
        ///   catégorie, parts fiscales, sursalaire, AV nature) sont EXCLUSIVEMENT
        ///   réservées aux rôles RH, DAF et DG.
        ///
        /// Les autres rôles (AssistantRH, Responsable, AssistantCommercial,
        /// Comptable) peuvent voir l'EXISTENCE d'un Salarié et son identité
        /// (Matricule, FullName, Email, Téléphone) mais PAS les éléments paie.
        ///
        /// Champs verrouillés sur Salarie :
        ///   - SalaireBase, IndemniteLogement, Sursalaire, PrimeTransport,
        ///     AvantageVehicule, NombrePartsFiscales
        ///   - Echelon, Categories (révèlent indirectement le niveau salarial)
        ///   - SalaireBaseAffichage (champ calculé qui dépend de SalaireBase)
        /// </summary>
        private static void DenyChampsPaieSensibles<T>(
            IObjectSpace os, PermissionPolicyRole role, ref int nbPerms)
            where T : class
        {
            string[] champsSensibles = {
                "SalaireBase",
                "SalaireBaseAffichage",
                "IndemniteLogement",
                "Sursalaire",
                "PrimeTransport",
                "AvantageVehicule",
                "NombrePartsFiscales",
                "Echelon",
                "Categories"
            };

            var typePerm = GetOrCreateTypePerm<T>(os, role);
            foreach (var champ in champsSensibles)
            {
                bool existe = false;
                foreach (var mp in typePerm.MemberPermissions)
                {
                    if (mp.Members == champ) { existe = true; break; }
                }
                if (existe) continue;

                var mpDeny = os.CreateObject<PermissionPolicyMemberPermissionsObject>();
                mpDeny.Members = champ;
                mpDeny.ReadState = SecurityPermissionState.Deny;
                mpDeny.WriteState = SecurityPermissionState.Deny;
                typePerm.MemberPermissions.Add(mpDeny);
                nbPerms++;
            }
        }

        /// <summary>Member Permissions sur plusieurs champs (séparés par ;).</summary>
        private static void AddMember<T>(
            IObjectSpace os, PermissionPolicyRole role,
            string members, bool write, ref int nbPerms)
            where T : class
        {
            var typePerm = GetOrCreateTypePerm<T>(os, role);

            foreach (var m in members.Split(';'))
            {
                var member = m.Trim();
                if (string.IsNullOrEmpty(member)) continue;

                bool existe = false;
                foreach (var mp in typePerm.MemberPermissions)
                {
                    if (mp.Members == member) { existe = true; break; }
                }
                if (existe) continue;

                var mp2 = os.CreateObject<PermissionPolicyMemberPermissionsObject>();
                mp2.Members = member;
                mp2.ReadState = SecurityPermissionState.Allow;
                mp2.WriteState = write
                    ? SecurityPermissionState.Allow
                    : SecurityPermissionState.Deny;
                typePerm.MemberPermissions.Add(mp2);
                nbPerms++;
            }
        }

        /// <summary>Filtre objet (criteria) sur un type.</summary>
        private static void AddObject<T>(
            IObjectSpace os, PermissionPolicyRole role, string criteria)
            where T : class
        {
            var typePerm = GetOrCreateTypePerm<T>(os, role);
            foreach (var op in typePerm.ObjectPermissions)
                if (op.Criteria == criteria) return;

            var obj = os.CreateObject<PermissionPolicyObjectPermissionsObject>();
            obj.Criteria = criteria;
            obj.ReadState = SecurityPermissionState.Allow;
            obj.WriteState = SecurityPermissionState.Allow;
            typePerm.ObjectPermissions.Add(obj);
        }

        private static PermissionPolicyTypePermissionObject GetOrCreateTypePerm<T>(
            IObjectSpace os, PermissionPolicyRole role) where T : class
        {
            var typeName = typeof(T).FullName;
            foreach (var tp in role.TypePermissions)
                if (tp.TargetType?.FullName == typeName) return tp;

            var newTp = os.CreateObject<PermissionPolicyTypePermissionObject>();
            newTp.TargetType = typeof(T);
            role.TypePermissions.Add(newTp);
            return newTp;
        }
    }
}
