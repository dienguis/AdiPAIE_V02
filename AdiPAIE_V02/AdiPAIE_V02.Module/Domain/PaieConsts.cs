using System;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using static AdiPAIE_V02.Module.Domain.PaieConsts;

namespace AdiPAIE_V02.Module.Domain
{
    public static class PaieConsts
    {
        public static class ValeursDefaut
        {
            // Sénégal – à paramétrer au besoin
            public const decimal AvantageVehicule_Montant = 20_000m;
        }
        public static class Groupes
        {
            public const string BRUT = "BRUT";
            public const string COT_SOC = "COT_SOC";
            public const string RET_FIS = "RET_FIS";
        }

        public static class Rubriques
        {
            public const string SB = "SB";
            public const string LOGT = "LOGT";
            public const string ANC = "ANC";
            public const string IPRES_RG = "IPRES_RG";
            public const string IPRES_RC = "IPRES_RC";
            public const string CSS_AT = "CSS_AT";
            public const string CSS_AF = "CSS_AF";
            public const string TRIMF = "TRIMF";
            public const string IR = "IR";
            public const string PRET = "PRET";
            public const string CONGE_INDEM = "CONGE_INDEM";
            public const string CONGE_AVANCE_GAIN = "CONGE_AVANCE_G";
            public const string CONGE_AVANCE_RET = "CONGE_AVANCE_R";
  
            public const string SURSAL = "SURSAL";      // NEW
            public const string TRANS = "TRANS";        // NEW (prime transport)
            public const string AV_NAT_VEH = "AV_NAT_VEH";    // NEW (avantage nature véhicule)
            
        }

        // Barème annuel DPP(progressif) — valeurs de ton tableau
        public static readonly (decimal Min, decimal Max, decimal Taux)[] IR_DPP_TRANCHES =
        {
            (       0m,    630_000m,  0m),
            ( 630_001m,  1_500_000m, 20m),
            (1_500_001m, 4_000_000m, 30m),
            (4_000_001m, 8_000_000m, 35m),
            (8_000_001m,13_500_000m, 37m),
            (13_500_001m,50_000_000m,40m),
            (50_000_001m,10_000_000_000m,43m)
        };

        public static class IR
        {
            public const decimal Abattement_TauxPercent = 30m;        // IMAB : 30% annuel
            public const decimal Abattement_PlafondAnnuel = 900_000m; // Plafond annuel IMAB
            public const decimal Abattement_PlafondMensuel = 75_000m; // Plafond mensuel IMAB
            public const bool TronquerBaseAuxMille = true;         // Troncature base IR aux milliers

            // Réduction familiale (par défaut = inactif ; configure via UI)
            public const decimal ReductionFamille_Pourcentage = 0m;       // % du droit progressif annuel
            public const decimal ReductionFamille_MinParPart_Annuel = 0m;  // FCFA / part / an (min)
            public const decimal ReductionFamille_MaxParPart_Annuel = 0m;  // FCFA / part / an (max)

            // Régularisation
            public const bool Regularisation_FinAnnee = true;
            public const bool Regularisation_MoisDepart = true;

            // Code barème IR (DPP) par défaut
            public const string BaremeIR_CodeTemplate = "IR_DPP_{0}";
        }

        public static class Comptes
        {
            public const string SALAIRE = "661100";
            public const string INDEM_LOGT = "663110";
            public const string PRIMES = "663840";
            public const string IPRES_RG_TIER = "431300";
            public const string IPRES_RC_TIER = "431310";
            public const string IRPP = "447100";
            public const string TRIMF = "447200";
            public const string CSS_AT = "612450";
            public const string CSS_AF = "612530";
            public const string PERSONNEL = "421100";
        }

        public static class Taux
        {
            public const decimal IPRES_RG_Salarie = 5.60m;
            public const decimal IPRES_RG_Employeur = 8.40m;
            public const decimal IPRES_RC_Salarie = 2.40m;
            public const decimal IPRES_RC_Employeur = 3.60m;
            public const decimal CSS_AT_Employeur = 3.00m;
            public const decimal CSS_AF_Employeur = 7.00m;
        }

        public static class Plafonds
        {
            public const decimal IPRES_RG = 432000m;
            public const decimal IPRES_RC = 1296000m;
            public const decimal CSS = 63000m;
        }

        public static class Baremes
        {
            // Codes par défaut si rien n’est saisi dans ParametresPaie
            public const string TRIMF_CodeMensuelTemplate = "TRIMF_{0}";
            public const string TRIMF_CodeAnnuelTemplate = "TRIMF_AN_{0}";
            public const int TRIMF_AnneeDefaut = 2025;

            // Seed démo global (toggle si pas de param en base)
            public const bool SEED_DEMO_ACTIF_PAR_DEFAUT = false;

            // ===========================
            // TRIMF Mensuel (par défaut)
            // ===========================
            // (Min, Max, MontantParPart)
            public static readonly (decimal Min, decimal Max, decimal Montant)[] TRIMF_MENSUEL_DEFAULT = new[]
            {
                (         0m,     49_999m,     75m),
                (    50_000m,     83_333m,    300m),
                (    83_334m,    166_666m,    400m),
                (   166_667m,    583_333m,  1_000m),
                (   583_334m,    999_999m,  1_500m),
                ( 1_000_000m, 10_000_000_000m, 3_000m)
            };

            // ===========================
            // TRIMF Annuel (par défaut)
            // ===========================
            // (Min, Max, MontantParPart)
            public static readonly (decimal Min, decimal Max, decimal Montant)[] TRIMF_ANNUEL_DEFAULT = new[]
            {
                (         0m,    599_999m,     900m),
                (   600_000m,    999_999m,   3_600m),
                ( 1_000_000m,  1_999_999m,   4_800m),
                ( 2_000_000m,  6_999_999m,  12_000m),
                ( 7_000_000m, 11_999_999m,  18_000m),
                (12_000_000m, 10_000_000_000m, 36_000m)
            };

 

        }
        public static class Conge
        {
            public const decimal MajorationPourcentageDefaut = 0m; // 0 = pas de majoration, change si convention
            public const bool SuspendreBulletinSalaireMoisDedie_Defaut = true;
            public const bool DeductionAvanceSurMoisRetour_Defaut = true;
        }
    }
}
