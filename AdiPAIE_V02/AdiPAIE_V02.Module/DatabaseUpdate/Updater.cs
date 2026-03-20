using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Properties;
using AdiPAIE_V02.Module.Reports;
using AdiPAIE_V02.Module.Services;
using AdiPAIE_V02.Module.Utils;
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

            //Security Implementatation 10/09/2025 ADIENG
            var role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Employe")
                ?? ObjectSpace.CreateObject<PermissionPolicyRole>();
            role.Name = "Employe";

            // Bulletins : navigation + lecture OK, pas d’édition
            role.AddTypePermission<Bulletin>(SecurityOperations.Navigate, SecurityPermissionState.Allow);
            role.AddTypePermission<Bulletin>(SecurityOperations.Read, SecurityPermissionState.Allow);
            role.AddTypePermission<Bulletin>(SecurityOperations.Write, SecurityPermissionState.Deny);
            role.AddTypePermission<Bulletin>(SecurityOperations.Delete, SecurityPermissionState.Deny);
            role.AddTypePermission<Bulletin>(SecurityOperations.Create, SecurityPermissionState.Deny);
            // Filtre d’objets : uniquement mes bulletins (login = email)
            role.AddObjectPermission<Bulletin>(
                SecurityOperations.Read,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            // Même logique pour les prêts/échéances si tu veux les exposer aux salariés :
            role.AddTypePermission<Pret>(SecurityOperations.Navigate, SecurityPermissionState.Allow);
            role.AddTypePermission<Pret>(SecurityOperations.Read, SecurityPermissionState.Allow);
            role.AddObjectPermission<Pret>(
                SecurityOperations.Read,
                "Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            role.AddTypePermission<PretEcheance>(SecurityOperations.Navigate, SecurityPermissionState.Allow);
            role.AddTypePermission<PretEcheance>(SecurityOperations.Read, SecurityPermissionState.Allow);

            role.AddObjectPermission<PretEcheance>(
                SecurityOperations.Read,
                "Pret.Salarie.Email = CurrentUserName()",
                SecurityPermissionState.Allow);

            ObjectSpace.CommitChanges();

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

            // 3) Dashboards (même ObjectSpace, un seul commit)
            CreateOrUpdateDashboard(os, "Paie — Suivi mensuel", Resources.Dash_Paie_SuiviMensuelXml);
            CreateOrUpdateDashboard(os, "Paie — IR & TRIMF", Resources.Dash_Paie_FiscaliteXml);

            os.CommitChanges();

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

#if !RELEASE
            var adminRole = CreateAdminRole();

            UserManager userManager = ObjectSpace.ServiceProvider.GetRequiredService<UserManager>();

            //if (TenantName != null)
            //{
            //    var defaultRole = CreateDefaultRole();

            //    string userName = $"User@{TenantName}";
            //    if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, userName) == null)
            //    {
            //        string EmptyPassword = "";
            //        _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, userName, EmptyPassword, (user) =>
            //        {
            //            user.Roles.Add(defaultRole);
            //        });
            //    }
            //}
            //       string adminUserName = TenantName != null ? $"Admin@{TenantName}" : "Admin";


            //if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, adminUserName) == null)
            //{
            //    string EmptyPassword = "";
            //    _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, adminUserName, EmptyPassword, (user) =>
            //    {
            //        user.Roles.Add(adminRole);
            //    });
            //}

         // ── Utilisateur Admin ─────────────────────────────
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
#endif
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

        private static DashboardData CreateOrUpdateDashboard(IObjectSpace os, string title, string xml)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title obligatoire.", nameof(title));

            var dash = os.FindObject<DashboardData>(
                CriteriaOperator.FromLambda<DashboardData>(d => d.Title == title));

            if (dash == null)
            {
                dash = os.CreateObject<DashboardData>();
                dash.Title = title.Trim();
                dash.Content = xml ?? string.Empty;
            }
            else
            {
                if (!string.Equals(dash.Content, xml ?? string.Empty, StringComparison.Ordinal))
                    dash.Content = xml ?? string.Empty;
            }
            return dash; // pas de Commit ici
        }
    }
}
