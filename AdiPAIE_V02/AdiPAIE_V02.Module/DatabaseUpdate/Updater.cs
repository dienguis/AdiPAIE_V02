using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Properties;
using AdiPAIE_V02.Module.Reports;
using AdiPAIE_V02.Module.Services;
using AdiPAIE_V02.Module.Utils;
// using DevExpress.DashboardCommon; // retiré — plus de dashboards programmatiques
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Dashboards;
using DevExpress.ExpressApp.MultiTenancy;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Security.Strategy;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Updating;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.BaseImpl.MultiTenancy;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using Microsoft.Extensions.DependencyInjection;
using static AdiPAIE_V02.Module.Domain.DomainEnums;


namespace AdiPAIE_V02.Module.DatabaseUpdate
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Updating.ModuleUpdater
    public class Updater : ModuleUpdater
    {
        public Updater(IObjectSpace objectSpace, Version currentDBVersion) :
            base(objectSpace, currentDBVersion)
        {
        }

        public override void UpdateDatabaseAfterUpdateSchema()
        {
            base.UpdateDatabaseAfterUpdateSchema();

            // ═══════════════════════════════════════════════════════
            // Rôle Employe — Permissions espace salarié (idempotent)
            // ═══════════════════════════════════════════════════════
            var role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Employe")
                ?? ObjectSpace.CreateObject<PermissionPolicyRole>();
            role.Name = "Employe";

            // Nettoyer les permissions existantes pour éviter les doublons
            // à chaque redémarrage de l’application
            while (role.TypePermissions.Count > 0)
                role.TypePermissions.Remove(role.TypePermissions[0]);

            // Helper : Navigate + Read
            const string NavRead = SecurityOperations.Navigate + ";" + SecurityOperations.Read;
            // Helper : Navigate + CRUD complet
            const string NavCrud = SecurityOperations.Navigate + ";" + SecurityOperations.Read + ";"
                + SecurityOperations.Write + ";" + SecurityOperations.Create + ";" + SecurityOperations.Delete;
            // Helper : Navigate + Read + Write + Create (sans Delete)
            const string NavRwc = SecurityOperations.Navigate + ";" + SecurityOperations.Read + ";"
                + SecurityOperations.Write + ";" + SecurityOperations.Create;

            // ── Bulletins : lecture seule de ses propres bulletins ──
            role.AddTypePermissionsRecursively<Bulletin>(NavRead, SecurityPermissionState.Allow);
            role.AddObjectPermission<Bulletin>(
                SecurityOperations.Read,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── Prêts : lecture seule ──
            role.AddTypePermissionsRecursively<Pret>(NavRead, SecurityPermissionState.Allow);
            role.AddObjectPermission<Pret>(
                SecurityOperations.Read,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            role.AddTypePermissionsRecursively<PretEcheance>(NavRead, SecurityPermissionState.Allow);
            role.AddObjectPermission<PretEcheance>(
                SecurityOperations.Read,
                "Pret.Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── Congés : créer, modifier, supprimer ses propres demandes ──
            // Type-level : autorise Navigate + Read + Write + Create + Delete
            role.AddTypePermissionsRecursively<CongeDemande>(NavCrud, SecurityPermissionState.Allow);
            // Object-level : Read + Write + Delete uniquement (Create n’est pas valide ici)
            const string RWD = SecurityOperations.Read + ";" + SecurityOperations.Write + ";" + SecurityOperations.Delete;
            role.AddObjectPermission<CongeDemande>(
                RWD,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // Types de congé : lecture seule (référentiel)
            role.AddTypePermissionsRecursively<CongeType>(NavRead, SecurityPermissionState.Allow);

            // ── Soldes de congé : lecture seule ──
            role.AddTypePermissionsRecursively<SoldeConge>(NavRead, SecurityPermissionState.Allow);
            role.AddObjectPermission<SoldeConge>(
                SecurityOperations.Read,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── Demandes d’attestation : créer et modifier ──
            role.AddTypePermissionsRecursively<DemandeAttestation>(NavRwc, SecurityPermissionState.Allow);
            const string RW = SecurityOperations.Read + ";" + SecurityOperations.Write;
            role.AddObjectPermission<DemandeAttestation>(
                RW,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── Entretiens annuels : lecture seule ──
            role.AddTypePermissionsRecursively<EntretienAnnuel>(NavRead, SecurityPermissionState.Allow);
            role.AddObjectPermission<EntretienAnnuel>(
                SecurityOperations.Read,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── Déplacements : créer et modifier ──
            role.AddTypePermissionsRecursively<DemandeDeplacement>(NavRwc, SecurityPermissionState.Allow);
            role.AddObjectPermission<DemandeDeplacement>(
                RW,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── Référentiels déplacement : lookups nécessaires ──
            role.AddTypePermissionsRecursively<VilleSenegal>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<LigneCircuit>(NavCrud, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<LigneFraisMission>(NavCrud, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<CategorieFraisMission>(NavRead, SecurityPermissionState.Allow);

            // ── ParametresPaie : lecture (pour clé API Google Maps, etc.) ──
            role.AddTypePermissionsRecursively<ParametresPaie>(NavRead, SecurityPermissionState.Allow);

            // ── Salarié : lecture de sa propre fiche ──
            role.AddTypePermissionsRecursively<Salarie>(NavRead, SecurityPermissionState.Allow);
            role.AddObjectPermission<Salarie>(
                SecurityOperations.Read,
                "Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // ── NotificationSalarie : le salarié doit pouvoir créer une notif
            //    quand il soumet une demande de congé (pour alerter le N+1) ──
            role.AddTypePermissionsRecursively<NotificationSalarie>(
                SecurityOperations.Navigate + ";" + SecurityOperations.Create + ";" + SecurityOperations.Write,
                SecurityPermissionState.Allow);

            // ── FileData : pour les pièces jointes (justificatifs congé, etc.) ──
            role.AddTypePermissionsRecursively<FileData>(NavRwc, SecurityPermissionState.Allow);

            // ═══════════════════════════════════════════════════════
            // Navigation Permissions — rendre le menu "Mon espace" visible
            // ═══════════════════════════════════════════════════════
            // Nettoyer les NavigationPermissions existantes (idempotent)
            while (role.NavigationPermissions.Count > 0)
                role.NavigationPermissions.Remove(role.NavigationPermissions[0]);

            // Groupe parent
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie", SecurityPermissionState.Allow);
            // Éléments enfants
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie/Items/GRH_MesBulletins", SecurityPermissionState.Allow);
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie/Items/GRH_MesConges", SecurityPermissionState.Allow);
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie/Items/GRH_MesSoldesConges", SecurityPermissionState.Allow);
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie/Items/GRH_MesAttestations", SecurityPermissionState.Allow);
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie/Items/GRH_MesEntretiens", SecurityPermissionState.Allow);
            role.AddNavigationPermission(@"Application/NavigationItems/Items/GRH_EspaceSalarie/Items/GRH_MesDeplacements", SecurityPermissionState.Allow);

            // ═══════════════════════════════════════════════════════
            // Denied Actions — Actions RH interdites pour un employé
            // L'employé ne peut que créer, sauvegarder et soumettre.
            // ═══════════════════════════════════════════════════════
            // Nettoyer les ActionPermissions existantes (idempotent)
            while (role.ActionPermissions.Count > 0)
                role.ActionPermissions.Remove(role.ActionPermissions[0]);

            var deniedActions = new[]
            {
                // ── Bulletins ──
                "CloturerBulletin",
                "ReouvrirBulletin",
                "ValiderBulletin",
                "ValiderEtEnvoyer",
                "RenvoyerBulletin",
                "Bulletin_RecalculerMaintenant",
                "NormalizeBulletinLines",
                "ReloadModeleDefaults",
                "EnvoyerBulletinEmail",
                "ExporterBulletinsMoisCourant",
                "ExporterBulletinsSelection",
                "GenererEcritureComptable",
                "GenerateEcritureFromBulletin",
                "CreateBulletinsForPeriod",
                "BulkBulletin_OuvrirParams",
                // ── Congés ──
                "Conge_Accorder",
                "Conge_Accorder_Detail",
                "Conge_Refuser",
                "Conge_ModifierDates",
                "Conge_Annuler",
                // ── Déplacements ──
                "Deplacement_InitialiserFrais",
                "Deplacement_SoumettreRH",
                "Deplacement_ApprouverRH",
                "Deplacement_RejeterRH",
                "Deplacement_GenererOrdre",
                "Deplacement_GenererEtatFrais",
                "Deplacement_ValiderDAF",
                "Deplacement_ConfirmerComptable",
                // "Deplacement_CalculerDistances", // L'employé a besoin de calculer les distances
                // ── Attestations ──
                "Demande_PrendreEnCharge",
                "Demande_Traiter",
                "Demande_Rejeter",
                "Attestation_Generer",
                // ── Entretiens ──
                "Entretien_Planifier",
                "Entretien_LancerEvaluation",
                "Entretien_Cloturer",
                "Entretien_RecalculerScore",
                "Entretien_GenererFiche",
                // ── Avancements ──
                "Avancement_PreRemplir",
                "Avancement_Soumettre",
                "Avancement_Approuver",
                "Avancement_Rejeter",
                "Avancement_Appliquer",
                "Avancement_Annuler",
                // ── Salarié (fiche) ──
                "Salarie.Active",
                "Salarie.Desactive",
                "Salarie.RegenererModele",
                "OpenDossierRH",
                "EnvoyerClePDF",
            };

            foreach (var actionId in deniedActions)
            {
                role.CreateActionPermissionObject(actionId);
            }

            ObjectSpace.CommitChanges();

            // ═══════════════════════════════════════════════════════
            // Rôle RH — AllowAllByDefault + DENY navigation Comptabilité / Mon espace
            // Pas IsAdministrative, mais accès CRUD complet sur toutes les données
            // sauf la navigation vers les menus interdits.
            // ═══════════════════════════════════════════════════════
            // Chercher TOUS les rôles RH existants pour éviter les doublons
            var allRolesRH = ObjectSpace.GetObjectsQuery<PermissionPolicyRole>()
                .Where(r => r.Name == "RH")
                .ToList();

            PermissionPolicyRole roleRH;
            if (allRolesRH.Count == 0)
            {
                roleRH = ObjectSpace.CreateObject<PermissionPolicyRole>();
                roleRH.Name = "RH";
            }
            else
            {
                // Prendre le premier, supprimer les doublons éventuels
                roleRH = allRolesRH[0];
                for (int i = 1; i < allRolesRH.Count; i++)
                {
                    // Migrer les utilisateurs du doublon vers le rôle principal
                    var duplicate = allRolesRH[i];
                    var usersOnDuplicate = ObjectSpace.GetObjectsQuery<ApplicationUser>()
                        .Where(u => u.Roles.Any(r => r.Oid == duplicate.Oid))
                        .ToList();
                    foreach (var u in usersOnDuplicate)
                    {
                        if (!u.Roles.Contains(roleRH))
                            u.Roles.Add(roleRH);
                        u.Roles.Remove(duplicate);
                    }
                    ObjectSpace.Delete(duplicate);
                }
            }
            roleRH.IsAdministrative = false;
            roleRH.PermissionPolicy = SecurityPermissionPolicy.AllowAllByDefault;

            // Nettoyer les NavigationPermissions existantes (idempotent)
            while (roleRH.NavigationPermissions.Count > 0)
                roleRH.NavigationPermissions.Remove(roleRH.NavigationPermissions[0]);

            // DENY navigation vers Comptabilité et Mon espace
            roleRH.AddNavigationPermission(
                @"Application/NavigationItems/Items/Comptabilite",
                SecurityPermissionState.Deny);
            roleRH.AddNavigationPermission(
                @"Application/NavigationItems/Items/GRH_EspaceSalarie",
                SecurityPermissionState.Deny);

            // S'assurer que les utilisateurs RH n'ont PAS le rôle Default
            // (qui contient des Deny explicites pouvant bloquer AllowAllByDefault)
            var usersRH = ObjectSpace.GetObjectsQuery<ApplicationUser>()
                .Where(u => u.Roles.Any(r => r.Name == "RH"))
                .ToList();
            var roleDefault = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Default");
            if (roleDefault != null)
            {
                foreach (var userRH in usersRH)
                {
                    if (userRH.Roles.Contains(roleDefault))
                        userRH.Roles.Remove(roleDefault);
                }
            }

            ObjectSpace.CommitChanges();

            // ═══════════════════════════════════════════════════════
            // Rôle RH_Manager — Accès lecture aux Tableaux de Bord RH
            // (Module Dashboards — Étape 2 / squelette).
            // Permissions étendues progressivement aux Tableaux 1 → 6
            // (Étape 4). Idempotent : crée ou met à jour, fusionne les
            // doublons éventuels.
            // ═══════════════════════════════════════════════════════
            var allRolesRHM = ObjectSpace.GetObjectsQuery<PermissionPolicyRole>()
                .Where(r => r.Name == "RH_Manager")
                .ToList();

            PermissionPolicyRole roleRHM;
            if (allRolesRHM.Count == 0)
            {
                roleRHM = ObjectSpace.CreateObject<PermissionPolicyRole>();
                roleRHM.Name = "RH_Manager";
            }
            else
            {
                roleRHM = allRolesRHM[0];
                for (int i = 1; i < allRolesRHM.Count; i++)
                {
                    var duplicate = allRolesRHM[i];
                    var usersOnDup = ObjectSpace.GetObjectsQuery<ApplicationUser>()
                        .Where(u => u.Roles.Any(r => r.Oid == duplicate.Oid))
                        .ToList();
                    foreach (var u in usersOnDup)
                    {
                        if (!u.Roles.Contains(roleRHM))
                            u.Roles.Add(roleRHM);
                        u.Roles.Remove(duplicate);
                    }
                    ObjectSpace.Delete(duplicate);
                }
            }
            roleRHM.IsAdministrative = false;
            roleRHM.PermissionPolicy = SecurityPermissionPolicy.DenyAllByDefault;

            // Reset idempotent des permissions pour éviter les doublons à
            // chaque démarrage de l'application.
            while (roleRHM.TypePermissions.Count > 0)
                roleRHM.TypePermissions.Remove(roleRHM.TypePermissions[0]);
            while (roleRHM.NavigationPermissions.Count > 0)
                roleRHM.NavigationPermissions.Remove(roleRHM.NavigationPermissions[0]);

            const string NavRead_RHM =
                SecurityOperations.Navigate + ";" + SecurityOperations.Read;

            // ── Entrée de menu « Tableaux de Bord RH » (DashboardsRHMenu) ──
            roleRHM.AddTypePermissionsRecursively<DashboardsRHMenu>(
                NavRead_RHM, SecurityPermissionState.Allow);

            // ── Entités sources des dashboards (lecture seule) ──
            //    Les permissions seront étendues étape 4.1 → 4.6 selon les
            //    besoins précis de chaque tableau (filtres sur sites,
            //    départements, périodes, etc.).
            roleRHM.AddTypePermissionsRecursively<Salarie>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<ContratSalarie>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Interimaire>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<ContratInterim>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<MouvementInterimaire>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Departement>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Categories>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Fonction>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Echelons>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Site>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<StationService>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<Bulletin>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<BulletinLigne>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<PeriodePaie>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<CongeDemande>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<CongeType>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<SoldeConge>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<DossierOffboarding>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<HistoriquePoste>(NavRead_RHM, SecurityPermissionState.Allow);
            roleRHM.AddTypePermissionsRecursively<DossierDisciplinaire>(NavRead_RHM, SecurityPermissionState.Allow);

            // ── ApplicationUser (lecture pour la jointure salarié ↔ user) ──
            roleRHM.AddTypePermissionsRecursively<ApplicationUser>(
                SecurityOperations.Read, SecurityPermissionState.Allow);

            // ═══════════════════════════════════════════════════════════════
            //  Étendre l'accès aux dashboards aux rôles RH et DAF (Étape 7.SEC)
            //  Pour le rôle RH (AllowAllByDefault) : les permissions sont déjà
            //  ouvertes mais on ajoute explicitement le menu pour la clarté.
            //  Pour DAF (créé par InitialiserRolesGRHController) : on ajoute
            //  les permissions Read sur les sources des dashboards.
            // ═══════════════════════════════════════════════════════════════
            GrantDashboardAccessToExistingRole("RH");
            GrantDashboardAccessToExistingRole("DAF");

            // ═══════════════════════════════════════════════════════════════
            //  V1.1 Sprint 1D — Permissions Read sur UniteOrganisationnelle
            //  uniquement (BusinessUnitType est DEPRECATED — plus de seed
            //  ni de migration ni de permission). Voir CHANGELOG Sprint 1D.
            // ═══════════════════════════════════════════════════════════════
            GrantUniteOrganisationnelleReadAccess();

            // ═══════════════════════════════════════════════════════════════
            //  V1.1 Sprint 1B — Seed démo COMPLET pour tester les dashboards
            //  Appel conditionné par appsettings.json :
            //    "Dashboards": { "SeedDemoData": true }
            //  Par défaut TRUE en dev, à passer à FALSE en prod après wipe.
            //  Le seeder est idempotent (ne recrée pas ce qui existe déjà).
            //  Tous les codes/matricules sont préfixés "DEMO_" → suppression
            //  ciblée via le Controller "Vider données démo" sans risque
            //  pour les seeds réels (rubriques de paie, paramètres, etc.).
            // ═══════════════════════════════════════════════════════════════
            if (IsDemoSeedEnabled())
            {
                try
                {
                    DemoDataSeeder.EnsureAll(ObjectSpace);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DemoDataSeeder] Échec non bloquant : {ex.Message}");
                }
            }

            ObjectSpace.CommitChanges();

            // ═══════════════════════════════════════════════════════
            // Fix ONE-SHOT : Supprimer le ModelDifference pour DemandeAvancement_DetailView
            // Un layout personnalisé en base a supprimé le champ Salarié.
            // En nettoyant le nœud, XAF régénère le layout par défaut.
            // → COMMENTEZ cette ligne après le premier lancement réussi
            //   pour ne pas écraser de futures personnalisations du layout.
            // ═══════════════════════════════════════════════════════
            // ResetDemandeAvancementDetailView(); // Désactivé — le fix [Aggregated] retiré de Salarie.Avancements résout le problème

            //Security Implementatation 10/09/2025
            //
            //ADIENG TEST DATAT 28/08/2025 TOP


            EnsureUniqueIndexPeriodePaie_CompanyYearMonth();
            var os = ObjectSpace;
            var xpOs = (XPObjectSpace)ObjectSpace;
            var session = (Session)xpOs.Session;
            EnsureUniqueOneOpenPeriodIndex(session);

            // 1) S'assurer qu'on a des Paramètres Paie (singleton "soft")
            var p = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
            if (p == null)
            {
                p = os.CreateObject<ParametresPaie>(); // valeurs par défaut définies dans la classe
                os.CommitChanges();                    // pour le rendre visible tout de suite dans l'UI
            }

            // 1b) Auto-remplir les paramètres Power BI depuis la connection string de l'app
            //     (uniquement si les champs sont encore vides — ne jamais écraser)
            try
            {
                var appConnStr = session.ConnectionString
                    ?? session.Connection?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(appConnStr))
                {
                    PowerBIConfigService.AutoFillFromAppConnectionString(os, appConnStr);
                    if (os.IsModified) os.CommitChanges();
                }
            }
            catch { /* Ne pas bloquer le démarrage si l'auto-fill échoue */ }

            // 2) Seed conditionnel : seulement si l’option UI est cochée
            if (p.ActiverSeedDemo)
            {
                // IMPORTANT : SeedDemoData doit être idempotent (ne rien dupliquer)
                SeedDemoData(os);

                // Injecte (si manquants) TRIMF Mensuel & Annuel à partir des constantes
                EnsureTrimfSeed(os, p);
                Seed_IR_Reductions_Table(ObjectSpace);
                //os.CommitChanges();

                // ===========================
                // RÉFÉRENTIEL : Groupes / Types / Rubriques (version unifiée)
                // ===========================

                // >>> Comptes (mini plan comptable) — inchangé
                var c661100 = EnsureCompte(os, "661100", "Appointements & salaires");
                var c663110 = EnsureCompte(os, "663110", "Indemnités de logement");
                var c663840 = EnsureCompte(os, "661200", "Primes (transport/panier)");
                var c431300 = EnsureCompte(os, "432100", "IPRES - Régime Général (tiers)");
                var c431310 = EnsureCompte(os, "432200", "IPRES - Régime Cadre (tiers)");
                var c447100 = EnsureCompte(os, "447100", "IRPP");
                var c447200 = EnsureCompte(os, "447200", "TRIMF");
                var c612450 = EnsureCompte(os, "631100", "CSS - Accident de travail");
                var c612530 = EnsureCompte(os, "631200", "CSS - Allocation familiale");
                var c421100 = EnsureCompte(os, "422000", "Personnel - Rémunérations dues");
                var c664100 = EnsureCompte(os, "664100", "CFCE");
                var c272800 = EnsureCompte(os, "272800", "Prêts");
                var c421000 = EnsureCompte(os, "421000", "Avance sur Salaire");

                // ==================
                // Groupes d'impression
                // ==================
                var gSalaireBrut = EnsureGroupe(os, code: "BRUT", libelle: "Salaire brut (1)", order: 1);
                var gCotSocial = EnsureGroupe(os, code: "COT_SOC", libelle: "Total Cotisations Sociales", order: 2);
                var gRetFiscal = EnsureGroupe(os, code: "RET_FIS", libelle: "Total Retenues Fiscales", order: 3);
                var gAutre = EnsureGroupe(os, code: "RET_AUTRE", libelle: "Total Autres Retenues", order: 4); // [FIX] un seul groupe "Autres retenues"

                // ==================
                // Types de rubrique
                // ==================
                var tBrute = EnsureTypeRef(os, "BRUTE", "Éléments bruts", gSalaireBrut, RubriqueTypeCalcul.Gain, SensAssiette.Plus, true, true);
                var tIndImpos = EnsureTypeRef(os, "INDEM_IMPOSA", "Indemnités imposables", gSalaireBrut, RubriqueTypeCalcul.Gain, SensAssiette.Plus, true, true);
                var tAvNatureImpos = EnsureTypeRef(os, "AvNatImpos", "Av Nature Impos", gSalaireBrut, RubriqueTypeCalcul.Gain, SensAssiette.Plus, true, false);
                var tIndNonImp = EnsureTypeRef(os, "INDEM_NON_IMPOSA", "Indemnités non imposables", gSalaireBrut, RubriqueTypeCalcul.Gain, SensAssiette.Plus, false, true);
                var tCotSoc = EnsureTypeRef(os, "COTSOC", "Cotisations sociales", gCotSocial, RubriqueTypeCalcul.Retenue, SensAssiette.Moins, false, false);
                var tCotFis = EnsureTypeRef(os, "COTFISC", "Cotisation fiscales", gRetFiscal, RubriqueTypeCalcul.Retenue, SensAssiette.Moins, false, false);
                var tRetenue = EnsureTypeRef(os, "RETENUE", "Retenues", gAutre, RubriqueTypeCalcul.Retenue, SensAssiette.Moins, false, false); // [FIX] rattaché au bon groupe

                // ==================
                // Rubriques clés
                // ==================
                var rSB = EnsureRubrique(os, "SB", "Salaire de base", tBrute,
                                         ordre: 1, canon: RubriqueCanonique.SalaireDeBase,
                                         debitDefaut: c661100, creditDefaut: c421100);
                var r13 = EnsureRubrique(os, "13EME", "13e mois", tBrute,
                                         ordre: 5, debitDefaut: c661100, creditDefaut: c421100);
                var rSURSAL = EnsureRubrique(os, "SURSAL", "Sursalaire", tBrute,
                                         ordre: 20, canon: RubriqueCanonique.Sursalaire,
                                         debitDefaut: c661100, creditDefaut: c421100);
                var rCong = EnsureRubrique(os, "CONGE_PAYE", "Indemnité congés payés", tIndImpos,
                                         ordre: 23, debitDefaut: c661100, creditDefaut: c421100);
                var rANC = EnsureRubrique(os, "ANC", "Prime d'ancienneté", tBrute,
                                         ordre: 30, canon: RubriqueCanonique.PrimeAnciennete,
                                         debitDefaut: c661100, creditDefaut: c421100);
                var rLOGT = EnsureRubrique(os, "LOGT", "Indemnité de logement", tIndImpos,
                                         ordre: 60, canon: RubriqueCanonique.IndemniteLogement,
                                         debitDefaut: c663110, creditDefaut: c421100);

                var rPrimeGen = EnsureRubrique(os, "PRIME_GEN", "Prime générique", tIndImpos, ordre: 80, debitDefaut: c661100, creditDefaut: c421100);
                var rIndemGenNonImp = EnsureRubrique(os, "INDEM_GEN_NON_IMP", "Indemnité générique Non Imposable", tIndNonImp, ordre: 81, debitDefaut: c661100, creditDefaut: c421100);
                var rIndemGen = EnsureRubrique(os, "INDEM_GEN_IMP", "Indemnité générique Imposable", tIndImpos, ordre: 82, debitDefaut: c661100, creditDefaut: c421100);
                var rIKM = EnsureRubrique(os, "TRANS", "Prime de transport", tIndNonImp, ordre: 83, canon: RubriqueCanonique.PrimeTransport, debitDefaut: c661100, creditDefaut: c421100);

                var rHS25 = EnsureRubrique(os, "HS25", "Heures sup 25%", tBrute, ordre: 120, taux1: 25m, debitDefaut: c661100, creditDefaut: c421100);
                var rHS50 = EnsureRubrique(os, "HS50", "Heures sup 50%", tBrute, ordre: 121, taux1: 50m, debitDefaut: c661100, creditDefaut: c421100);
                var rHS100 = EnsureRubrique(os, "HS100", "Heures sup 100%", tBrute, ordre: 122, taux1: 100m, debitDefaut: c661100, creditDefaut: c421100);

                var rAVVeh = EnsureRubrique(os, "AV_NAT_VEH", "Avantage en nature - véhicule", tAvNatureImpos,
                                         ordre: 180, canon: RubriqueCanonique.AvantageNatureVehicule, debitDefaut: c661100, creditDefaut: c421100);
                var rAVTel = EnsureRubrique(os, "AV_TEL", "Avantage en nature - téléphone", tBrute, ordre: 182, debitDefaut: c661100, creditDefaut: c421100);

                var rIPRG = EnsureRubrique(os, "IPRES_RG", "RETENUE IPRES RG", tCotSoc,
                                         ordre: 200, canon: RubriqueCanonique.IPRES_RG,
                                         creditDefaut: c431300, taux1: 5.60m, taux2: 8.40m, plafond: 432000m);

                var rIPRC = EnsureRubrique(os, "IPRES_RC", "RETENUE IPRES RC", tCotSoc,
                                         ordre: 210, canon: RubriqueCanonique.IPRES_RC,
                                         creditDefaut: c431310, taux1: 2.40m, taux2: 3.60m, plafond: 1296000m);

                var rCSSAT = EnsureRubrique(os, "CSS_AT", "CSS - Assu. accident travail", tCotSoc,
                                         ordre: 220, canon: RubriqueCanonique.CSS_AccidentTravail,
                                         debitDefaut: c612450, taux2: 3.00m, plafond: 63000m);

                var rCSSAF = EnsureRubrique(os, "CSS_AF", "CSS - Allocation familiale", tCotSoc,
                                         ordre: 230, canon: RubriqueCanonique.CSS_AllocationFamiliale,
                                         debitDefaut: c612530, taux2: 7.00m, plafond: 63000m);

                var rTRIMF = EnsureRubrique(os, "TRIMF", "RETENUE TRIMF", tCotFis,
                                         ordre: 300, canon: RubriqueCanonique.TRIMF,
                                         creditDefaut: c447200);

                var rIR = EnsureRubrique(os, "IR", "RETENUE IMPÔTS", tCotFis,
                                         ordre: 310, canon: RubriqueCanonique.IRPP,
                                         creditDefaut: c447100);
                var rCFCE = EnsureRubrique(os, "CFCE", "RETENUE CFCE", tCotFis,
                                         ordre: 311, canon: RubriqueCanonique.CFCE,
                                         creditDefaut: c664100, taux2: 3.00m);

                // [FIX] Créer PRET / AVANCE_SAL UNE SEULE FOIS (avec comptes par défaut)
                var rPret = EnsureRubrique(os, "PRET", "RETENUE Prêt", tRetenue,
                                           ordre: 500, canon: RubriqueCanonique.RemboursementPret,
                                           creditDefaut: c272800);
                var rAvance = EnsureRubrique(os, "AVANCE_SAL", "RETENUE Avance sur Salaire", tRetenue,
                                           ordre: 501, canon: RubriqueCanonique.RemboursementAvance,
                                           creditDefaut: c421000);

                // [REMOVE] doublon via EnsureReferentielNoyau (créait à nouveau RETENUE/PRET/AVANCE sans comptes)
                // Rubrique rPret, rAvance; // [REMOVE]
                // EnsureReferentielNoyau(ObjectSpace, out rPret, out rAvance); // [REMOVE]

                // Pointe ParametresPaie sur les rubriques par défaut si vides
                if (p.RubriqueRetenuePretDefaut == null) p.RubriqueRetenuePretDefaut = rPret;   // [FIX]
                if (p.RubriqueRetenueAvanceDefaut == null) p.RubriqueRetenueAvanceDefaut = rAvance; // [FIX]
                ObjectSpace.CommitChanges();

                // — types de prêts/avances + backfill
                SeedPretTypes(ObjectSpace, p);
                ObjectSpace.CommitChanges();

                foreach (var s in os.GetObjectsQuery<Salarie>())
                {
                    bool shouldHaveVehicle = (s.AvantageVehicule > 0m) || s.PossedeVehicule;
                    if (s.PossedeVehicule != shouldHaveVehicle)
                        s.PossedeVehicule = shouldHaveVehicle;
                }
                os.CommitChanges();
            }

            // Dashboards RH — supprimés (approche DashboardObjectDataSource incompatible Blazor)
            // Les tableaux de bord seront recréés via des pages Blazor manuelles.

            // Normaliser tous les libellés existants (one-shot)
            var rubs = ObjectSpace.GetObjectsQuery<Rubrique>().ToList();
            foreach (var r in rubs)
            {
                var norm = TextCaseFr.ToTitleCaseFrPreserveAcronyms (r.Libelle); // même logique que LibelleTitre
                if (!string.Equals(r.Libelle, norm, StringComparison.Ordinal))
                {
                    r.Libelle = norm;
                }
            }
            if (ObjectSpace.IsModified) ObjectSpace.CommitChanges();

            // ═══════════════════════════════════════════════════════
            // Centre d'imports — assure qu'un singleton existe
            // (utilisé comme conteneur pour les actions d'import en masse)
            // ═══════════════════════════════════════════════════════
            if (ObjectSpace.GetObjectsCount(typeof(CentreImports), null) == 0)
            {
                var ci = ObjectSpace.CreateObject<CentreImports>();
                ci.Note = "Centre regroupant les actions d'import en masse "
                        + "(salariés, conjoints, comptes bancaires, ...).";
                ObjectSpace.CommitChanges();
            }

            // ═══════════════════════════════════════════════════════
            // Centre des constantes paie — assure qu'un singleton existe
            // ═══════════════════════════════════════════════════════
            if (ObjectSpace.GetObjectsCount(typeof(CentreConstantesPaie), null) == 0)
            {
                ObjectSpace.CreateObject<CentreConstantesPaie>();
                ObjectSpace.CommitChanges();
            }

            // ═══════════════════════════════════════════════════════
            // Sites — référentiel paramétrable (personnel interne)
            // Idempotent : crée uniquement les sites manquants
            // ═══════════════════════════════════════════════════════
            (string code, string nom, string ville)[] sitesParDefaut =
            {
                ("SIEGE",     "Siège",      "Dakar"),
                ("DEPOT_CDB", "Dépôt CDB",  "Dakar"),
                ("DEPOT_HANN","Dépôt Hann", "Dakar"),
            };
            foreach (var (code, nom, ville) in sitesParDefaut)
            {
                var existing = ObjectSpace.FirstOrDefault<Site>(s => s.Code == code);
                if (existing == null)
                {
                    var site = ObjectSpace.CreateObject<Site>();
                    site.Code = code;
                    site.Nom = nom;
                    site.Ville = ville;
                    site.Actif = true;
                }
            }
            if (ObjectSpace.IsModified) ObjectSpace.CommitChanges();

            //ADIENG TEST DATAT 28/08/2025 END

            if (!ObjectSpace.CanInstantiate(typeof(ApplicationUser)))
            {
                return;
            }

//#if !RELEASE
//            if (TenantName == null)
//            {
//                _ = CreateTenant("company1.com", "AdiPAIE_V02_company1");
//                _ = CreateTenant("company2.com", "AdiPAIE_V02_company2");
//                ObjectSpace.CommitChanges();
//            }
//#endif

            // ═══════════════════════════════════════════════════════
            // V1.7 — Seed Admin + rôle Administrators
            // Anciennement entouré de #if !RELEASE → Admin n'était JAMAIS
            // créé en production, conduisant à "Login failed for 'Admin'"
            // sur une base fraîche. Maintenant exécuté dans TOUS les modes
            // (DEBUG + RELEASE). Le bloc est idempotent : si Admin existe
            // déjà, FindUserByName retourne non-null et on ne recrée rien.
            // ═══════════════════════════════════════════════════════
            var adminRole = CreateAdminRole();

            UserManager userManager = ObjectSpace.ServiceProvider.GetRequiredService<UserManager>();

            // ── Utilisateur Admin (mot de passe vide au 1er lancement) ──
            string adminUserName = "Admin";
            if (userManager.FindUserByName<ApplicationUser>(
                    ObjectSpace, adminUserName) == null)
            {
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(
                    ObjectSpace, adminUserName, EmptyPassword, (user) =>
                    {
                        user.Roles.Add(adminRole);
                    });
            }

            ObjectSpace.CommitChanges();
          //ADIENG 27/08/2025 DEBUT  FIXER LES RUBRIQUES ESSENTIEL
          //  CreateReports();

         //ObjectSpace.CommitChanges();

            foreach (var r in rubs)
            {
                var norm = TextCaseFr.ToTitleCaseFrPreserveAcronyms(r.Libelle);
                if (!string.Equals(r.Libelle, norm, StringComparison.Ordinal))
                    r.Libelle = norm;
            }
            if (ObjectSpace.IsModified) ObjectSpace.CommitChanges();

            //ADIENG 27/08/2025 FIN

            // ═══════════════════════════════════════════════════════
            // V1.4.3 — Auto-seed du rapport BulletinPaie
            // Si le ReportDataV2 "BulletinPaie" est absent (déploiement
            // sur DB neuve, restore partiel, suppression accidentelle),
            // on le crée à partir du REPX embarqué dans l'assembly.
            // Idempotent : si déjà présent en DB, on ne touche pas.
            // ═══════════════════════════════════════════════════════
            SeedBulletinReportIfMissing();

            // ═══════════════════════════════════════════════════════
            // QW6 (V1.5.1) — Index SQL pour performance dashboards
            // Idempotent : NOT EXISTS check avant CREATE INDEX.
            // ═══════════════════════════════════════════════════════
            EnsurePerformanceIndexes();

            // ═══════════════════════════════════════════════════════
            // QW1 (V1.5.2) — Auto-init des rôles GRH au démarrage
            // Idempotent — délègue à RolesGRHInitializer.Initialize.
            // Si des rôles existent déjà, leurs permissions sont juste
            // enrichies (les V1.5 DemandeMouvementInterim notamment).
            // ═══════════════════════════════════════════════════════
            EnsureRolesGRHInitialized();
        }

        /// <summary>
        /// QW1 (V1.5.2) — Création/maj des rôles GRH au démarrage.
        /// Délègue à <see cref="Controllers.RolesGRHInitializer.Initialize"/>.
        /// Non-bloquant : un échec ne crash pas l'app.
        /// </summary>
        private void EnsureRolesGRHInitialized()
        {
            try
            {
                var (nbRoles, nbPerms) =
                    Controllers.RolesGRHInitializer.Initialize(ObjectSpace);
                if (nbRoles > 0 || nbPerms > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[V1.5.2 RolesGRH] Auto-init : {nbRoles} rôle(s), "
                        + $"{nbPerms} permission(s).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[V1.5.2 RolesGRH] Auto-init non bloquant : {ex.Message}");
            }
        }

        /// <summary>
        /// QW6 (V1.5.1) — Crée les index manquants pour accélérer les
        /// dashboards et requêtes fréquentes. Idempotent via NOT EXISTS.
        /// </summary>
        private void EnsurePerformanceIndexes()
        {
            try
            {
                var session = ((DevExpress.ExpressApp.Xpo.XPObjectSpace)ObjectSpace).Session;

                // Bulletin : filtré par Annee + Statut (dashboards N°4, N°6, etc.)
                ExecSqlIfIndexMissing(session,
                    "IX_Bulletin_Annee_Statut",
                    "Bulletin",
                    "CREATE INDEX IX_Bulletin_Annee_Statut ON Bulletin(Annee, Statut, GCRecord)");

                // Bulletin : filtré pour Espace Salarié (DatePublication)
                ExecSqlIfIndexMissing(session,
                    "IX_Bulletin_Salarie_DatePub",
                    "Bulletin",
                    "CREATE INDEX IX_Bulletin_Salarie_DatePub ON Bulletin(Salarie, DatePublication)");

                // ContratInterim : filtré par Statut (alertes fin mission)
                ExecSqlIfIndexMissing(session,
                    "IX_ContratInterim_Statut",
                    "ContratInterim",
                    "CREATE INDEX IX_ContratInterim_Statut ON ContratInterim(Statut, DateFinReelle)");

                // MouvementInterimaire : filtré par Date + Type (Dashboard N°3)
                ExecSqlIfIndexMissing(session,
                    "IX_MouvementInterim_Date_Type",
                    "MouvementInterimaire",
                    "CREATE INDEX IX_MouvementInterim_Date_Type ON MouvementInterimaire(DateMouvement, TypeMouvement)");

                // Bulletin : unique sur Salarie+Annee+Mois (évite doublons)
                ExecSqlIfIndexMissing(session,
                    "UX_Bulletin_Salarie_Annee_Mois",
                    "Bulletin",
                    "CREATE UNIQUE INDEX UX_Bulletin_Salarie_Annee_Mois "
                    + "ON Bulletin(Salarie, Annee, Mois) "
                    + "WHERE GCRecord IS NULL");

                // V1.7.1 — Salarie.Email unique (filtré : NULL et vide tolérés en multiple,
                // soft-deletes ignorés). Bloque les doublons même sur imports SQL directs.
                // La normalisation (trim + lowercase) est faite dans Salarie.OnSaving.
                ExecSqlIfIndexMissing(session,
                    "UX_Salarie_Email",
                    "Salarie",
                    "CREATE UNIQUE INDEX UX_Salarie_Email "
                    + "ON Salarie(Email) "
                    + "WHERE Email IS NOT NULL AND Email <> '' AND GCRecord IS NULL");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[V1.5.1 Indexes] Création index non bloquante : {ex.Message}");
            }
        }

        private static void ExecSqlIfIndexMissing(
            DevExpress.Xpo.Session session, string indexName, string tableName, string createSql)
        {
            try
            {
                // Pattern : IF NOT EXISTS (SELECT...) CREATE INDEX...
                // Les noms d'index/table sont hardcodés en compile-time → safe.
                var guardedSql =
                    $"IF NOT EXISTS (SELECT 1 FROM sys.indexes "
                    + $"WHERE name = '{indexName}' "
                    + $"AND object_id = OBJECT_ID('{tableName}')) "
                    + $"BEGIN {createSql} END";

                session.ExecuteNonQuery(guardedSql);
                System.Diagnostics.Debug.WriteLine(
                    $"[V1.5.1 Indexes] Vérification/création index '{indexName}' OK.");
            }
            catch (Exception ex)
            {
                // Non bloquant : si la table n'existe pas encore (1ʳᵉ run avant
                // schéma complet), on laisse passer
                System.Diagnostics.Debug.WriteLine(
                    $"[V1.5.1 Indexes] '{indexName}' : {ex.Message}");
            }
        }


        /// <summary>
        /// V1.4.3 — Crée le ReportDataV2 "BulletinPaie" depuis la ressource
        /// embarquée Reports/BulletinPaie.repx si aucun rapport actif ne porte
        /// ce nom. Le design custom peut continuer à être édité ensuite via
        /// le designer XAF — le filet de sécurité garantit juste qu'un rapport
        /// existe en DB pour Publier / Imprimer / Télécharger.
        /// </summary>
        private void SeedBulletinReportIfMissing()
        {
            const string ReportName = "BulletinPaie";
            const string ResourceName = "AdiPAIE_V02.Module.Reports.BulletinPaie.repx";

            try
            {
                // Idempotence : ne pas écraser le rapport custom existant
                // (XPO filtre automatiquement les soft-deletes via GCRecord)
                var existing = ObjectSpace.FirstOrDefault<DevExpress.Persistent.BaseImpl.ReportDataV2>(
                    r => r.DisplayName == ReportName);
                if (existing != null) return;

                // Charger le REPX embarqué
                var asm = typeof(Updater).Assembly;
                using var stream = asm.GetManifestResourceStream(ResourceName);
                if (stream == null)
                {
                    // Ressource absente → on log et on continue (le rapport peut
                    // être créé manuellement par RH via le designer XAF)
                    System.Diagnostics.Debug.WriteLine(
                        $"[V1.4.3 Seed] Ressource '{ResourceName}' introuvable — skip auto-seed.");
                    return;
                }

                using var ms = new System.IO.MemoryStream();
                stream.CopyTo(ms);
                var repxBytes = ms.ToArray();

                var rd = ObjectSpace.CreateObject<DevExpress.Persistent.BaseImpl.ReportDataV2>();
                rd.DisplayName = ReportName;
                rd.IsInplaceReport = true;
                // DataTypeName en lecture seule — l'info de type est dans le REPX (Content)
                rd.Content = repxBytes;

                ObjectSpace.CommitChanges();

                System.Diagnostics.Debug.WriteLine(
                    $"[V1.4.3 Seed] ReportDataV2 '{ReportName}' créé depuis ressource embarquée ({repxBytes.Length} octets).");
            }
            catch (Exception ex)
            {
                // Non bloquant : si le seed échoue, l'app démarre quand même
                System.Diagnostics.Debug.WriteLine(
                    $"[V1.4.3 Seed] Échec seed BulletinPaie : {ex.Message}");
            }
        }

        // Copie de la méthode utilitaire utilisée par LibelleTitre (sans dépendre d'une instance)

        //private void CreateReports()
        //{
        //    var name = "Bulletin A4 (simple)";
        //    var old = ObjectSpace.FirstOrDefault<ReportDataV2>(r => r.DisplayName == name);
        //    if (old != null) ObjectSpace.Delete(old); // pour être 100% sûr de repartir propre

        //    var rpt = ReportTemplates.CreateBulletinA4_Simple(); // version SANS paramètre
        //    ReportsV2Helper.SaveToReportsV2(ObjectSpace, rpt, name, typeof(Bulletin));
        //}

        public override void UpdateDatabaseBeforeUpdateSchema()
        {
            base.UpdateDatabaseBeforeUpdateSchema();
            //if(CurrentDBVersion < new Version("1.1.0.0") && CurrentDBVersion > new Version("0.0.0.0")) {
            //    RenameColumn("DomainObject1Table", "OldColumnName", "NewColumnName");
            //}
        }

        Tenant CreateTenant(string tenantName, string databaseName)
        {
            var tenant = ObjectSpace.FirstOrDefault<Tenant>(t => t.Name == tenantName);
            if (tenant == null)
            {
                tenant = ObjectSpace.CreateObject<Tenant>();
                tenant.Name = tenantName;
                tenant.ConnectionString = $"Data Source=(localdb)\\mssqllocaldb;Integrated Security=SSPI;Pooling=false;Initial Catalog={databaseName}";
            }
            return tenant;
        }

        PermissionPolicyRole CreateAdminRole()
        {
            PermissionPolicyRole adminRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Administrators");
            if (adminRole == null)
            {
                adminRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
                adminRole.Name = "Administrators";
                adminRole.IsAdministrative = true;
            }
            return adminRole;
        }

        PermissionPolicyRole CreateDefaultRole()
        {
            PermissionPolicyRole defaultRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(role => role.Name == "Default");
            if (defaultRole == null)
            {
                defaultRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
                defaultRole.Name = "Default";

                defaultRole.AddObjectPermissionFromLambda<ApplicationUser>(SecurityOperations.Read, cm => cm.Oid == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddNavigationPermission(@"Application/NavigationItems/Items/Default/Items/MyDetails", SecurityPermissionState.Allow);
                defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "ChangePasswordOnFirstLogon", cm => cm.Oid == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "StoredPassword", cm => cm.Oid == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<PermissionPolicyRole>(SecurityOperations.Read, SecurityPermissionState.Deny);
                defaultRole.AddObjectPermission<ModelDifference>(SecurityOperations.ReadWriteAccess, "UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
                defaultRole.AddObjectPermission<ModelDifferenceAspect>(SecurityOperations.ReadWriteAccess, "Owner.UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<ModelDifference>(SecurityOperations.Create, SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<ModelDifferenceAspect>(SecurityOperations.Create, SecurityPermissionState.Allow);
            }
            return defaultRole;
        }

        //Guid? TenantId
        //{
        //    get
        //    {
        //        return ObjectSpace.ServiceProvider.GetRequiredService<ITenantProvider>().TenantId;
        //    }
        //}
        //string TenantName
        //{
        //    get
        //    {
        //        return ObjectSpace.ServiceProvider.GetRequiredService<ITenantProvider>().TenantName;
        //    }
        //}

        // ===========================
        // SEED : 10 salariés + modèles (partiel)
        // ===========================
        private void SeedDemoData(IObjectSpace os)
        {
            // --- Référentiels de base ---
            var conv = EnsureConvention(os, "CONV_PET", "CONVENTION PETROLE");
            var catAgent = EnsureCategorie(os, conv, "Non Cadre");
            var catCadre = EnsureCategorie(os, conv, "Cadre");

            // (échelons alimentés depuis tableau)

            var rows = new[] {
    new { Code="1A",    Descriptif="Employés",                                         Montant=133638m, Statut="NC", IdemLogement=105000m },
    new { Code="1ère",  Descriptif="Employés",                                         Montant=136113m, Statut="NC", IdemLogement=120000m },
    new { Code="2",     Descriptif="Employés",                                         Montant=142897m, Statut="NC", IdemLogement=105000m },
    new { Code="2émeA", Descriptif="Employés",                                         Montant=157186m, Statut="NC", IdemLogement=120000m },
    new { Code="2émeB", Descriptif="Employés",                                         Montant=159261m, Statut="NC", IdemLogement=120000m },
    new { Code="3B",    Descriptif="Employés",                                         Montant=148533m, Statut="NC", IdemLogement=105000m },
    new { Code="3èA",   Descriptif="Employés",                                         Montant=144784m, Statut="NC", IdemLogement=105000m },
    new { Code="3ème",  Descriptif="Employés",                                         Montant=163389m, Statut="NC", IdemLogement=120000m },
    new { Code="4ème",  Descriptif="Employés",                                         Montant=150607m, Statut="NC", IdemLogement=105000m },
    new { Code="4émeA", Descriptif="Employés",                                         Montant=162993m, Statut="NC", IdemLogement=120000m },
    new { Code="4émeB", Descriptif="Employés",                                         Montant=164329m, Statut="NC", IdemLogement=120000m },
    new { Code="4émeC", Descriptif="Employés",                                         Montant=165668m, Statut="NC", IdemLogement=120000m },
    new { Code="5ème",  Descriptif="Employés",                                         Montant=167002m, Statut="NC", IdemLogement=120000m },
    new { Code="6ème",  Descriptif="Employés",                                         Montant=189698m, Statut="NC", IdemLogement=120000m },
    new { Code="7ème",  Descriptif="Employés",                                         Montant=180451m, Statut="NC", IdemLogement=105000m },
    new { Code="7èmeA", Descriptif="Employés",                                         Montant=198496m, Statut="NC", IdemLogement=120000m },
    new { Code="7èmeB", Descriptif="EMPLOYE",                                          Montant=239832m, Statut="NC", IdemLogement=120000m },
    new { Code="7èmeC", Descriptif="EMPLOYE",                                          Montant=255041m, Statut="NC", IdemLogement=120000m },
    new { Code="8ème",  Descriptif="EMPLOYE",                                          Montant=232135m, Statut="NC", IdemLogement=105000m },

    new { Code="AM1",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=260662m, Statut="NC", IdemLogement=120000m },
    new { Code="AM1A",  Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=219413m, Statut="NC", IdemLogement=105000m },
    new { Code="AM1B",  Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=239506m, Statut="NC", IdemLogement=105000m },
    new { Code="AM2",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=284534m, Statut="NC", IdemLogement=120000m },
    new { Code="AM3",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=291835m, Statut="NC", IdemLogement=120000m },
    new { Code="AM4",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=309761m, Statut="NC", IdemLogement=120000m },
    new { Code="AM5",   Descriptif="AGENTS DE MAITRISE, TECHNICIENS ET ASSIMILES",     Montant=314599m, Statut="NC", IdemLogement=120000m },

    new { Code="C1",    Descriptif="INGENIEURS ET CADRES",                              Montant=268743m, Statut="C",  IdemLogement=140000m },
    new { Code="C2",    Descriptif="INGENIEURS ET CADRES",                              Montant=289902m, Statut="C",  IdemLogement=140000m },
    new { Code="C3",    Descriptif="INGENIEURS ET CADRES",                              Montant=310429m, Statut="C",  IdemLogement=140000m },
    new { Code="D1",    Descriptif="INGENIEURS ET CADRES",                              Montant=335377m, Statut="C",  IdemLogement=280000m },
    new { Code="D2",    Descriptif="INGENIEURS ET CADRES",                              Montant=361904m, Statut="C",  IdemLogement=280000m },
    new { Code="D3",    Descriptif="DIRECTEURS",                                        Montant=390958m, Statut="C",  IdemLogement=280000m },
    new { Code="E",     Descriptif="DIRECTEUR GENERAL",                                 Montant=422380m, Statut="C",  IdemLogement=1330000m },

    new { Code="P1A",   Descriptif="INGENIEURS ET CADRES C1",                           Montant=316424m, Statut="C",  IdemLogement=155000m },
    new { Code="P1B",   Descriptif="INGENIEURS ET CADRES",                              Montant=322618m, Statut="C",  IdemLogement=155000m },
    new { Code="P2A",   Descriptif="INGENIEURS ET CADRES C2",                           Montant=334837m, Statut="C",  IdemLogement=155000m },
    new { Code="P2B",   Descriptif="INGENIEURS ET CADRES",                              Montant=346691m, Statut="C",  IdemLogement=155000m },
    new { Code="P3A",   Descriptif="INGENIEURS ET CADRES C3",                           Montant=358546m, Statut="C",  IdemLogement=155000m },
    new { Code="P3B",   Descriptif="DIRECTEURS D1",                                     Montant=387360m, Statut="C",  IdemLogement=295000m },
    new { Code="P4A",   Descriptif="DIRECTEURS D2",                                     Montant=417999m, Statut="C",  IdemLogement=295000m },
    new { Code="P4B",   Descriptif="DIRECTEURS D3",                                     Montant=451556m, Statut="C",  IdemLogement=295000m },
    new { Code="P5",    Descriptif="DIRECTEUR GENERAL",                                 Montant=487849m, Statut="C",  IdemLogement=1345000m },
};

            foreach (var r in rows)
            {
                var isCadre = (r.Statut ?? "").Trim().Equals("C", StringComparison.OrdinalIgnoreCase);
                var cat = isCadre ? catCadre : catAgent;

                var e = os.GetObjectsQuery<Echelons>()
                          .FirstOrDefault(x => x.Categories == cat && x.Code == r.Code);
                if (e == null)
                {
                    e = os.CreateObject<Echelons>();
                    e.Categories = cat;
                    e.Code = r.Code;
                }
                e.Libelle = r.Descriptif?.Trim();
                e.SalaireBase = r.Montant;
                e.IdemniteLogement = r.IdemLogement;
            }

            var echelonPool = os.GetObjectsQuery<Echelons>().ToList();

            // Groupes/Types/Rubriques principaux sont gérés plus haut (section unifiée)
            // -> rien à refaire ici
            // IR : barème DPP annuel (idempotent)
            var dpp = new (decimal Min, decimal Max, decimal Taux)[] {
                (0m, 630_000m, 0m),
                (630_001m, 1_500_000m, 20m),
                (1_500_001m, 4_000_000m, 30m),
                (4_000_001m, 8_000_000m, 35m),
                (8_000_001m, 13_500_000m, 37m),
                (13_500_001m, 50_000_000m, 40m),
                (50_000_001m, 10_000_000_000m, 43m)
            };
            var annee = DateTime.Today.Year;
            EnsureBaremeIR(os, $"IR_DPP_{annee}", dpp, new DateTime(annee, 1, 1), new DateTime(annee, 12, 31));

            // (bloc de création salariés démo commenté chez toi — on le laisse tel quel)
        }

        // ===========================
        // Helpers de création
        // ===========================
        private Convention EnsureConvention(IObjectSpace os, string code, string libelle)
        {
            var obj = os.GetObjectsQuery<Convention>().FirstOrDefault(c => c.CodeConvention == code);
            if (obj == null)
            {
                obj = os.CreateObject<Convention>();
                obj.CodeConvention = code; obj.NomConvention = libelle;
            }
            return obj;
        }

        private Categories EnsureCategorie(IObjectSpace os, Convention conv, string libelle)
        {
            var obj = os.GetObjectsQuery<Categories>()
                .FirstOrDefault(c => c.Convention == conv && c.Intitule == libelle);
            if (obj == null)
            {
                obj = os.CreateObject<Categories>();
                obj.Convention = conv; obj.Intitule = libelle;
            }
            return obj;
        }

        private Echelons EnsureEchelon(IObjectSpace os, Categories cat, string code, string libelle, decimal salaireBase, decimal indemLogt)
        {
            var obj = os.GetObjectsQuery<Echelons>()
                .FirstOrDefault(e => e.Categories == cat && e.Code == code);
            if (obj == null)
            {
                obj = os.CreateObject<Echelons>();
                obj.Categories = cat;
                obj.Code = code;
                obj.Libelle = libelle;
                obj.SalaireBase = salaireBase;
                obj.IdemniteLogement = indemLogt;
            }
            return obj;
        }
        private GroupeImpressionRef EnsureGroupe(IObjectSpace os, string code, string libelle, int order)
        {
            static string Normalize(string s)
            {
                s = (s ?? "").Trim();
                var oneSpace = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
                return oneSpace.ToUpperInvariant();
            }

            GroupeImpressionRef obj = null;

            // 🔎 1) Priorité au Code (unique)
            if (!string.IsNullOrWhiteSpace(code))
                obj = os.GetObjectsQuery<GroupeImpressionRef>().FirstOrDefault(g => g.Code == code);

            // 🔎 2) Fallback : NormalizedKey du libellé
            if (obj == null)
            {
                var norm = Normalize(libelle);
                obj = os.FindObject<GroupeImpressionRef>(
                    new DevExpress.Data.Filtering.BinaryOperator(nameof(GroupeImpressionRef.NormalizedKey), norm)
                );
            }

            // ➕ 3) Création si absent
            if (obj == null)
            {
                obj = os.CreateObject<GroupeImpressionRef>();
                obj.Code = code?.Trim().ToUpperInvariant();
                obj.Libelle = libelle;
                obj.Actif = true;
                obj.OrdreGroupe = order;
                return obj;
            }

            // ♻️ 4) Mise à jour douce
            if (string.IsNullOrWhiteSpace(obj.Code) && !string.IsNullOrWhiteSpace(code))
                obj.Code = code.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(obj.Libelle))
                obj.Libelle = libelle;
            if (!obj.OrdreGroupe.HasValue)
                obj.OrdreGroupe = order;
            // Ne force pas Actif si l’admin l’a désactivé volontairement

            return obj;
        }


        // >>> MODIF: cherche par Code + renseigne Code & Libellé
        private RubriqueTypeRef EnsureTypeRef(IObjectSpace os, string code, string libelle, GroupeImpressionRef grp,
            RubriqueTypeCalcul calc, SensAssiette sens, bool bf, bool bs)
        {
            var obj = os.GetObjectsQuery<RubriqueTypeRef>().FirstOrDefault(t => t.Code == code);
            if (obj == null)
            {
                obj = os.CreateObject<RubriqueTypeRef>();
                obj.Code = code; obj.Libelle = libelle; obj.Groupe = grp;
                obj.DefaultTypeCalcul = calc; obj.DefaultSens = sens;
                obj.BruteFiscal = bf; obj.BruteSocial = bs; obj.Actif = true;
            }
            else
            {
                // mise à jour soft
                obj.Groupe = grp;
                obj.DefaultTypeCalcul = calc; obj.DefaultSens = sens;
                obj.BruteFiscal = bf; obj.BruteSocial = bs; obj.Actif = true;
                if (string.IsNullOrWhiteSpace(obj.Libelle)) obj.Libelle = libelle;
            }
            return obj;
        }

        private PlanComptable EnsureCompte(IObjectSpace os, string code, string libelle)
        {
            var c = os.GetObjectsQuery<PlanComptable>().FirstOrDefault(p => p.Code == code);
            if (c == null)
            {
                c = os.CreateObject<PlanComptable>();
                c.Code = code; c.Intitule = libelle;
            }
            else if (string.IsNullOrWhiteSpace(c.Intitule))
            {
                c.Intitule = libelle;
            }
            return c;
        }

        private Rubrique EnsureRubrique(
            IObjectSpace os,
            string code,
            string libelle,
            RubriqueTypeRef typeRef,
            int ordre,
            RubriqueCanonique? canon = null,
            PlanComptable debitDefaut = null,
            PlanComptable creditDefaut = null,
            decimal? taux1 = null,
            decimal? taux2 = null,
            decimal? plafond = null)
        {
            var r = os.GetObjectsQuery<Rubrique>().FirstOrDefault(x => x.Code == code);
            if (r == null)
            {
                r = os.CreateObject<Rubrique>();
                r.Code = code; r.Libelle = libelle; r.TypeRef = typeRef;
                r.OrdreAffichage = ordre;
                r.Actif = true;
            }

            // Mise à jour courtoise
            r.TypeRef = typeRef;
            r.OrdreAffichage = r.OrdreAffichage ?? ordre;
            if (string.IsNullOrWhiteSpace(r.Libelle)) r.Libelle = libelle;

            if (canon.HasValue) r.Canonique = canon.Value;

            if (debitDefaut != null) r.CompteDebitDefaut = debitDefaut;
            if (creditDefaut != null) r.CompteCreditDefaut = creditDefaut;

            r.Taux1 = taux1;
            r.Taux2 = taux2;
            r.Plafond = plafond;

            return r;
        }

        // [DEPRECATED] : wrappers pour compat descendante (à supprimer si plus référencés)
        //private GroupeImpressionRef EnsureGroupeCore(IObjectSpace os, string libelle, int order)
        //    => EnsureGroupe(os, code: null, libelle: libelle, order: order); // [DEPRECATED]

        //private RubriqueTypeRef EnsureTypeRefCore(
        //    IObjectSpace os, string code, string libelle, GroupeImpressionRef grp,
        //    RubriqueTypeCalcul calc, SensAssiette sens, bool brutFiscal, bool brutSocial)
        //    => EnsureTypeRef(os, code, libelle, grp, calc, sens, brutFiscal, brutSocial); // [DEPRECATED]

        //private Rubrique EnsureRubriqueCore(
        //    IObjectSpace os, string code, string libelle, RubriqueTypeRef typeRef,
        //    int ordre, RubriqueCanonique? canon = null)
        //    => EnsureRubrique(os, code, libelle, typeRef, ordre, canon); // [DEPRECATED]

        // ===========================
        // RBAC Dashboards (Étape 7.SEC)
        // ===========================
        /// <summary>
        /// Étend les permissions d'un rôle existant (RH, DAF) pour qu'il
        /// puisse accéder au module Tableaux de Bord RH. Idempotent : si
        /// le rôle n'existe pas, ne fait rien (sera traité au prochain
        /// démarrage si l'utilisateur l'a créé entre temps).
        ///
        /// Permissions ajoutées :
        ///   - Navigate + Read sur DashboardsRHMenu (l'entrée de menu)
        ///   - Read sur les entités sources des 6 dashboards
        /// </summary>
        private void GrantDashboardAccessToExistingRole(string roleName)
        {
            var role = ObjectSpace.GetObjectsQuery<PermissionPolicyRole>()
                .Where(r => r.Name == roleName)
                .FirstOrDefault();
            if (role == null) return;   // rôle pas encore créé : skip

            const string NavRead =
                SecurityOperations.Navigate + ";" + SecurityOperations.Read;

            // Menu d'entrée (toujours utile, même si AllowAllByDefault)
            role.AddTypePermissionsRecursively<DashboardsRHMenu>(
                NavRead, SecurityPermissionState.Allow);

            // Entités sources (Read) — utilisées par les services dashboards
            role.AddTypePermissionsRecursively<Salarie>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<ContratSalarie>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<Interimaire>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<ContratInterim>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<MouvementInterimaire>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<Departement>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<Categories>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<Site>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<StationService>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<Bulletin>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<BulletinLigne>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<CongeDemande>(NavRead, SecurityPermissionState.Allow);
            role.AddTypePermissionsRecursively<CongeType>(NavRead, SecurityPermissionState.Allow);
        }

        // ===========================
        // V1.1 — Lecture du flag SeedDemoData (sans dépendance NuGet)
        // ===========================
        /// <summary>
        /// Détermine si le seed démo doit être créé au démarrage.
        /// Sources lues dans cet ordre (priorité décroissante) :
        ///   1. Variable d'environnement <c>DASHBOARDS_SEED_DEMO</c>
        ///   2. Bloc "Dashboards":"SeedDemoData" dans appsettings.json (parse simple)
        ///   3. Défaut : TRUE (utile en dev)
        /// En prod : positionner DASHBOARDS_SEED_DEMO=false dans les variables
        /// système OU mettre <c>"SeedDemoData": false</c> dans appsettings.json.
        /// Pas de dépendance Microsoft.Extensions.Configuration → évite d'ajouter
        /// un nouveau package NuGet au projet Module.
        /// </summary>
        private static bool IsDemoSeedEnabled()
        {
            try
            {
                // Priorité 1 : variable d'environnement
                var env = Environment.GetEnvironmentVariable("DASHBOARDS_SEED_DEMO");
                if (!string.IsNullOrWhiteSpace(env))
                    return env.Equals("true", StringComparison.OrdinalIgnoreCase);

                // Priorité 2 : appsettings.json (parsing simple par regex)
                var basePath = System.IO.Directory.GetCurrentDirectory();
                var path     = System.IO.Path.Combine(basePath, "appsettings.json");
                if (System.IO.File.Exists(path))
                {
                    var content = System.IO.File.ReadAllText(path);
                    // Recherche tolérante : "SeedDemoData": false (avec/sans espaces)
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            content,
                            @"""SeedDemoData""\s*:\s*false",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return false;
                }

                // Défaut
                return true;
            }
            catch { return true; }   // fail open en dev
        }

        // ===========================
        // V1.1 — Référentiel BusinessUnitType
        // ===========================

        /// <summary>
        /// Définition des 4 types initiaux. La métier peut en ajouter
        /// d'autres via l'écran XAF — cette liste sert uniquement au seed
        /// du premier démarrage. Les modifications manuelles ne sont PAS
        /// écrasées (idempotent : on ne crée que ce qui manque).
        /// </summary>
        private static readonly (string Code, string Libelle, CouleurPalette Palette, int Ordre)[] BUTypeSeeds =
        {
            ("BOUTIQUE",     "Boutique",     CouleurPalette.OrangeElton, 0),
            ("PISTE",        "Piste",        CouleurPalette.NavyElton,   1),
            ("E_SERVICE",    "E-Service",    CouleurPalette.BleuClair,   2),
            ("ESPACE_AUTO",  "Espace Auto",  CouleurPalette.Vert,        3)
        };

        /// <summary>
        /// Idempotent — crée les 4 types initiaux s'ils n'existent pas.
        /// Ne touche pas aux types ajoutés manuellement par le métier.
        /// </summary>
        private void EnsureBusinessUnitTypesSeed()
        {
            foreach (var seed in BUTypeSeeds)
            {
                var existing = ObjectSpace.GetObjectsQuery<BusinessUnitType>()
                    .Where(t => t.Code == seed.Code)
                    .FirstOrDefault();
                if (existing == null)
                {
                    var t = ObjectSpace.CreateObject<BusinessUnitType>();
                    t.Code    = seed.Code;
                    t.Libelle = seed.Libelle;
                    t.Palette = seed.Palette;
                    t.Ordre   = seed.Ordre;
                    t.Actif   = true;
                }
            }
        }

        /// <summary>
        /// Migration douce — pour chaque BusinessUnitStation sans Type,
        /// lui assigne le bon Type en matchant son Libelle (case-insensitive,
        /// trim). Si aucun Type ne matche, laisse Type=null (à corriger
        /// manuellement par le métier ensuite).
        /// </summary>
        private void MigrateBUsToTypes()
        {
            var allTypes = ObjectSpace.GetObjectsQuery<BusinessUnitType>().ToList();
            if (allTypes.Count == 0) return;

            var bus = ObjectSpace.GetObjectsQuery<BusinessUnitStation>()
                .Where(b => b.Type == null)
                .ToList();

            foreach (var bu in bus)
            {
                if (string.IsNullOrWhiteSpace(bu.Libelle)) continue;

                var libelleNormalise = bu.Libelle.Trim();

                // Match exact insensible à la casse sur le Libelle
                var match = allTypes.FirstOrDefault(t =>
                    string.Equals(t.Libelle, libelleNormalise,
                        StringComparison.OrdinalIgnoreCase));

                // Fallback : match insensible à la casse + variantes "espace auto" / "espaceauto"
                if (match == null)
                {
                    var libelleSansEspace = libelleNormalise.Replace(" ", "");
                    match = allTypes.FirstOrDefault(t =>
                        string.Equals(t.Libelle.Replace(" ", ""), libelleSansEspace,
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t.Code.Replace("_", ""), libelleSansEspace,
                            StringComparison.OrdinalIgnoreCase));
                }

                if (match != null) bu.Type = match;
            }
        }

        /// <summary>
        /// V1.1 Sprint 1D — Étend les permissions des rôles dashboards
        /// pour lire UniteOrganisationnelle (nouveau modèle).
        /// </summary>
        private void GrantUniteOrganisationnelleReadAccess()
        {
            const string NavRead =
                SecurityOperations.Navigate + ";" + SecurityOperations.Read;

            foreach (var roleName in new[] { "RH_Manager", "RH", "DAF" })
            {
                var role = ObjectSpace.GetObjectsQuery<PermissionPolicyRole>()
                    .Where(r => r.Name == roleName)
                    .FirstOrDefault();
                if (role == null) continue;

                role.AddTypePermissionsRecursively<UniteOrganisationnelle>(
                    NavRead, SecurityPermissionState.Allow);
            }
        }

        /// <summary>
        /// [DEPRECATED V1.1 Sprint 1D] Conservée pour ne pas casser le code
        /// si elle est appelée ailleurs. N'est plus invoquée par défaut au
        /// démarrage. Sera supprimée définitivement quand le BO BusinessUnitType
        /// sera retiré.
        /// </summary>
        [System.Obsolete("Remplacée par GrantUniteOrganisationnelleReadAccess en V1.1 Sprint 1D")]
        private void GrantBusinessUnitTypeReadAccess()
        {
            const string NavRead =
                SecurityOperations.Navigate + ";" + SecurityOperations.Read;

            foreach (var roleName in new[] { "RH_Manager", "RH", "DAF" })
            {
                var role = ObjectSpace.GetObjectsQuery<PermissionPolicyRole>()
                    .Where(r => r.Name == roleName)
                    .FirstOrDefault();
                if (role == null) continue;

                role.AddTypePermissionsRecursively<BusinessUnitType>(
                    NavRead, SecurityPermissionState.Allow);

                // V1.1 Sprint 1B — RBAC sur la nouvelle entité UniteOrganisationnelle
                role.AddTypePermissionsRecursively<UniteOrganisationnelle>(
                    NavRead, SecurityPermissionState.Allow);
            }
        }

        // ===========================
        // SEEDs spécifiques
        // ===========================
        private void EnsureTrimfSeed(IObjectSpace os, ParametresPaie p)
        {
            var an = p.ResolveAnnee(null);
            var codeMensuel = p.ResolveCodeTRIMFMensuel(an);
            var codeAnnuel = p.ResolveCodeTRIMFAnnuel(an);

            var trMens = PaieConsts.Baremes.TRIMF_MENSUEL_DEFAULT;
            var trAnn = PaieConsts.Baremes.TRIMF_ANNUEL_DEFAULT;

            EnsureBaremeTRIMF(os, codeMensuel, TrimfNature.Mensuel, trMens);
            EnsureBaremeTRIMF(os, codeAnnuel, TrimfNature.Annuel, trAnn);
        }

        private void Seed_IR_Reductions_Table(IObjectSpace os)
        {
            void Ensure(decimal parts, decimal tauxPct, decimal minA, decimal maxA)
            {
                var row = os.GetObjectsQuery<IRReductionFamille>()
                            .FirstOrDefault(r => r.NbrePart == parts);
                if (row == null)
                {
                    row = os.CreateObject<IRReductionFamille>();
                    row.NbrePart = parts;
                }
                row.Taux = tauxPct;
                row.MinAnnuel = minA;
                row.MaxAnnuel = maxA;
                row.Actif = true;
            }

            Ensure(1.0m, 0m, 0m, 0m);
            Ensure(1.5m, 10m, 100000m, 300000m);
            Ensure(2.0m, 15m, 200000m, 650000m);
            Ensure(2.5m, 20m, 300000m, 1100000m);
            Ensure(3.0m, 25m, 400000m, 1650000m);
            Ensure(3.5m, 30m, 500000m, 2030000m);
            Ensure(4.0m, 35m, 600000m, 2490000m);
            Ensure(4.5m, 40m, 700000m, 2755000m);
            Ensure(5.0m, 45m, 800000m, 3180000m);
        }

        private void EnsureBaremeTRIMF(
            IObjectSpace os,
            string code,
            TrimfNature nature,
            (decimal Min, decimal Max, decimal Montant)[] tranches)
        {
            var b = os.GetObjectsQuery<BaremeTRIMF>().FirstOrDefault(x => x.Code == code);
            if (b == null)
            {
                b = os.CreateObject<BaremeTRIMF>();
                b.Code = code;
                b.Nature = nature;
                b.Actif = true;
            }

            if (!b.Tranches.Any())
            {
                int ordre = 0;
                foreach (var (min, max, montant) in tranches)
                {
                    var t = os.CreateObject<BaremeTRIMFTranche>();
                    t.Bareme = b;
                    t.Ordre = ++ordre;
                    t.MontantMin = min;
                    t.MontantMax = max;
                    t.Montant = montant;
                }
            }
        }

        private BaremeIR EnsureBaremeIR(IObjectSpace os, string code, (decimal Min, decimal Max, decimal Taux)[] tranches, DateTime? debut = null, DateTime? fin = null)
        {
            var b = os.GetObjectsQuery<BaremeIR>().FirstOrDefault(x => x.Code == code);
            if (b == null)
            {
                b = os.CreateObject<BaremeIR>();
                b.Code = code;
            }
            b.Actif = true;
            b.DateDebut = debut;
            b.DateFin = fin;

            foreach (var t in b.Tranches.ToList()) t.Delete();
            int ordre = 0;
            foreach (var t in tranches)
            {
                var tr = os.CreateObject<BaremeIRTranche>();
                tr.Bareme = b;
                tr.MontantMin = t.Min;
                tr.MontantMax = t.Max;
                tr.Taux = t.Taux;
                ordre++;
            }
            return b;
        }

        private void EnsureUniqueIndexPeriodePaie_CompanyYearMonth()
        {
            var os = (XPObjectSpace)ObjectSpace;
            var session = (Session)os.Session;

            var ci = session.GetClassInfo(typeof(PeriodePaie));
            string table = ci.TableName;                              // ex: PeriodePaie
            string colCmp = ci.FindMember("Company")?.MappingField ?? "Company";
            string colAnnee = ci.FindMember("Annee")?.MappingField ?? "Annee";
            string colMois = ci.FindMember("Mois")?.MappingField ?? "Mois";
            const string indexName = "UX_PeriodePaie_Company_Annee_Mois";

            string sql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = '{indexName}' AND object_id = OBJECT_ID('[dbo].[{table}]')
)
BEGIN
    CREATE UNIQUE INDEX [{indexName}]
    ON [dbo].[{table}] ([{colCmp}], [{colAnnee}], [{colMois}]);
END";
            session.ExecuteNonQuery(sql);
        }

        private void EnsureUniqueOneOpenPeriodIndex(Session session)
        {
            var ci = session.GetClassInfo(typeof(PeriodePaie));
            string table = ci.TableName;                         // ex: PeriodePaie
            var mCompany = ci.FindMember("Company");
            var mStatut = ci.FindMember("Statut");

            // Nom des colonnes réelles en base
            string colCmp = mCompany?.MappingField ?? "Company";
            string colStatut = mStatut?.MappingField ?? "Statut";

            // Nom de l’index
            const string indexName = "UX_PeriodePaie_OneOpen";

            // Valeur numérique de l’énum "Ouverte"
            int opened = (int)DomainEnums.PeriodePaieStatut.Ouverte;

            // Si Company existe → unicité par Company ; sinon unicité globale (sur Statut).
            bool hasCompany = (mCompany != null);
            string keyCols = hasCompany ? $"[{colCmp}]" : $"[{colStatut}]";

            string sql = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = '{indexName}' AND object_id = OBJECT_ID('[dbo].[{table}]')
)
BEGIN
    CREATE UNIQUE INDEX [{indexName}]
    ON [dbo].[{table}] ({keyCols})
    WHERE ([{colStatut}] = {opened});
END";
            session.ExecuteNonQuery(sql);
        }
        private void EnsureParametresPaie(IObjectSpace os)
        {
            if (os.GetObjectsCount(typeof(ParametresPaie), null) != 0) return;
            var p = os.CreateObject<ParametresPaie>();
            os.CommitChanges();
        }

        // === Unique filtered index: one ACTIVE model per Salarie, ignore soft-deleted rows ===
        private void EnsureBulletinModeleFilteredUniqueIndex(Session session)
        {
            const string table = "BulletinModele";
            const string indexName = "UX_BulletinModele_Salarie_Actif_Filtered";
            string sql = $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Salarie_ModeleActif' AND object_id = OBJECT_ID('[dbo].[' + '{table}' + ']'))
BEGIN
    DROP INDEX [UX_Salarie_ModeleActif] ON [dbo].[{table}];
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('[dbo].[{table}]'))
BEGIN
    CREATE UNIQUE INDEX [{indexName}]
    ON [dbo].[{table}] ([Salarie])
    WHERE [Actif] = 1 AND [GCRecord] IS NULL;
END";
            session.ExecuteNonQuery(sql);
        }

        private PretType EnsurePretType(IObjectSpace os, string code, string lib, PretNature nat, Rubrique rub, PretAmortissement amort = PretAmortissement.PrincipalConstant, decimal? tx = null)
        {
            var t = os.GetObjectsQuery<PretType>().FirstOrDefault(x => x.Code == code);
            if (t == null) { t = os.CreateObject<PretType>(); t.Code = code; }
            t.Libelle = lib; t.Nature = nat; t.RubriqueRetenue = rub; t.AmortissementDefaut = amort; t.TauxInteretDefaut = tx; t.Actif = true;
            return t;
        }

        private void SeedPretTypes(IObjectSpace os, ParametresPaie p)
        {
            var rubPret = p.ResolveRubriqueRetenue(DomainEnums.PretNature.Pret);
            var rubAv = p.ResolveRubriqueRetenue(DomainEnums.PretNature.AvanceSalaire);

            EnsurePretType(os, "PRT_EQUIP", "Prêt équipement", PretNature.Pret, rubPret, PretAmortissement.PrincipalConstant, 0m);
            EnsurePretType(os, "PRT_VEHI", "Prêt véhicule", PretNature.Pret, rubPret, PretAmortissement.PrincipalConstant, 0m);
            EnsurePretType(os, "PRT_LOGT", "Prêt logement", PretNature.Pret, rubPret, PretAmortissement.AnnuiteConstante, 3.00m);
            EnsurePretType(os, "AV_SAL_STD", "Avance sur salaire", PretNature.AvanceSalaire, rubAv, PretAmortissement.PrincipalConstant, 0m);

            foreach (var pr in os.GetObjectsQuery<Pret>().Where(x => x.TypePret == null).ToList())
            {
                pr.TypePret = (pr.Nature == PretNature.AvanceSalaire)
                    ? os.GetObjectsQuery<PretType>().FirstOrDefault(x => x.Code == "AV_SAL_STD")
                    : os.GetObjectsQuery<PretType>().FirstOrDefault(x => x.Code == "PRT_EQUIP");
                if (pr.RubriqueRetenue == null)
                    pr.RubriqueRetenue = pr.GetRubriqueRetenueEffective();
            }
        }

        // LoadDashboardXml et CreateOrUpdateDashboard supprimés (ancien code dashboard)

        /// <summary>
        /// Supprime le nœud DemandeAvancement_DetailView des ModelDifferences
        /// stockées en base. Cela force XAF à régénérer le layout par défaut
        /// (qui inclura le champ Salarié supprimé par erreur lors d'une
        /// personnalisation du layout dans le Model Editor).
        /// Idempotent : si le nœud n'existe pas, rien n'est modifié.
        /// </summary>
        private void ResetDemandeAvancementDetailView()
        {
            try
            {
                var xpOs = ObjectSpace as XPObjectSpace;
                if (xpOs == null) return;

                var session = xpOs.Session;
                var diffs = new XPQuery<ModelDifference>(session)
                    .ToList();

                bool modified = false;
                foreach (var diff in diffs)
                {
                    foreach (var aspect in diff.Aspects)
                    {
                        if (string.IsNullOrEmpty(aspect.Xml)) continue;
                        if (!aspect.Xml.Contains("DemandeAvancement_DetailView")) continue;

                        try
                        {
                            var doc = new System.Xml.XmlDocument();
                            doc.LoadXml(aspect.Xml);

                            // Chercher et supprimer les nœuds DemandeAvancement_DetailView
                            var nodes = doc.SelectNodes(
                                "//*[@Id='DemandeAvancement_DetailView']");
                            if (nodes != null && nodes.Count > 0)
                            {
                                foreach (System.Xml.XmlNode node in nodes)
                                    node.ParentNode?.RemoveChild(node);

                                aspect.Xml = doc.OuterXml;
                                modified = true;
                            }
                        }
                        catch
                        {
                            // Parsing XML échoue — on skip cet aspect
                        }
                    }
                }

                if (modified)
                    ObjectSpace.CommitChanges();
            }
            catch
            {
                // Ne pas bloquer le démarrage si le nettoyage échoue
            }
        }
    }
}
