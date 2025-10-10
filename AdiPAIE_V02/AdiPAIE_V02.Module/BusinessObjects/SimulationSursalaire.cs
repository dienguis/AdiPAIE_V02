using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using Microsoft.Graph.Models.Security;
using System;
using System.ComponentModel;
using System.Text;

using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [ImageName("BO_Calculator")]
    public class SimulationSursalaire : BaseObject
    {
        public SimulationSursalaire(Session session) : base(session) { }

        // --- Liens & méta -----------------------------------------------------------------

        private Salarie _salarie;
        [Association("Salarie-Simulations")]
        [ImmediatePostData]
        public Salarie Salarie
        {
            get => _salarie;
            set
            {
                if (SetPropertyValue(nameof(Salarie), ref _salarie, value) && value != null)
                    InitialiserDepuisSalarie();
            }
        }

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        [Size(128)]
        public string CreatedBy { get; set; }

        protected override void OnSaving()
        {
            base.OnSaving();
                       if (string.IsNullOrWhiteSpace(CreatedBy))
                CreatedBy = SecuritySystem.CurrentUserName;
        }

        // --- Entrées ----------------------------------------------------------------------

        private int _annee;
        [ImmediatePostData]
        public int Annee
        {
            get => _annee;
            set => SetPropertyValue(nameof(Annee), ref _annee, value);
        }

        private int _mois;
        [ImmediatePostData]
        public int Mois
        {
            get => _mois;
            set => SetPropertyValue(nameof(Mois), ref _mois, value);
        }

        private decimal _netCible;
        [ImmediatePostData]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal NetCible
        {
            get => _netCible;
            set => SetPropertyValue(nameof(NetCible), ref _netCible, value);
        }

        // --- « Fixes » (lecture seule dans l’UI) -----------------------------------------

        private decimal _salaireBase;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal SalaireBase
        {
            get => _salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref _salaireBase, value);
        }

        private decimal _indemniteLogement;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal IndemniteLogement
        {
            get => _indemniteLogement;
            set => SetPropertyValue(nameof(IndemniteLogement), ref _indemniteLogement, value);
        }

        private decimal _primeAnciennete;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal PrimeAnciennete
        {
            get => _primeAnciennete;
            set => SetPropertyValue(nameof(PrimeAnciennete), ref _primeAnciennete, value);
        }

        private decimal _primeTransport;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal PrimeTransport
        {
            get => _primeTransport;
            set => SetPropertyValue(nameof(PrimeTransport), ref _primeTransport, value);
        }

        private decimal _avantageVehicule;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal AvantageVehicule
        {
            get => _avantageVehicule;
            set => SetPropertyValue(nameof(AvantageVehicule), ref _avantageVehicule, value);
        }

        private decimal _partsFiscales;
        [ModelDefault("DisplayFormat", "N1")]
        [ModelDefault("AllowEdit", "False")]
        public decimal PartsFiscales
        {
            get => _partsFiscales;
            set => SetPropertyValue(nameof(PartsFiscales), ref _partsFiscales, value);
        }

        [DevExpress.ExpressApp.Model.ModelDefault("AllowEdit", "False")]
        [DevExpress.ExpressApp.DC.XafDisplayName("Parts TRIMF (depuis salarié)")]
        public decimal PartsTRIMF_FromSalarie => Salarie?.TrimfParts ?? 0m;

        // --- Résultats --------------------------------------------------------------------

        private decimal _sursalaireCalcule;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("➤ SURSALAIRE CALCULÉ")]
        public decimal SursalaireCalcule
        {
            get => _sursalaireCalcule;
            set => SetPropertyValue(nameof(SursalaireCalcule), ref _sursalaireCalcule, value);
        }

        private decimal _brutFiscalEstime;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal BrutFiscalEstime
        {
            get => _brutFiscalEstime;
            set => SetPropertyValue(nameof(BrutFiscalEstime), ref _brutFiscalEstime, value);
        }

        private decimal _brutSocialEstime;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal BrutSocialEstime
        {
            get => _brutSocialEstime;
            set => SetPropertyValue(nameof(BrutSocialEstime), ref _brutSocialEstime, value);
        }

        private decimal _totalCotisationsSociales;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal TotalCotisationsSociales
        {
            get => _totalCotisationsSociales;
            set => SetPropertyValue(nameof(TotalCotisationsSociales), ref _totalCotisationsSociales, value);
        }

        private decimal _trimfEstime;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal TRIMFEstime
        {
            get => _trimfEstime;
            set => SetPropertyValue(nameof(TRIMFEstime), ref _trimfEstime, value);
        }

        private decimal _irppEstime;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal IRPPEstime
        {
            get => _irppEstime;
            set => SetPropertyValue(nameof(IRPPEstime), ref _irppEstime, value);
        }

        private decimal _netObtenu;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        [Appearance("Highlight_NetObtenu", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
        public decimal NetObtenu
        {
            get => _netObtenu;
            set => SetPropertyValue(nameof(NetObtenu), ref _netObtenu, value);
        }

        private decimal _ecartNet;
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal EcartNet
        {
            get => _ecartNet;
            set => SetPropertyValue(nameof(EcartNet), ref _ecartNet, value);
        }

        private string _detailCalcul;
        [Size(SizeAttribute.Unlimited)]
        [ModelDefault("AllowEdit", "False")]
        public string DetailCalcul
        {
            get => _detailCalcul;
            set => SetPropertyValue(nameof(DetailCalcul), ref _detailCalcul, value);
        }

        // --- Méthodes ---------------------------------------------------------------------

        private void InitialiserDepuisSalarie()
        {
            if (Salarie == null) return;

            SalaireBase = Salarie.Echelon?.SalaireBase ?? Salarie.SalaireBase;
            IndemniteLogement = Salarie.Echelon?.IdemniteLogement ?? Salarie.IndemniteLogement;

            var dateSimul = new DateTime(Annee > 0 ? Annee : DateTime.Today.Year,
                                         Mois > 0 ? Mois : DateTime.Today.Month, 1);
            var anc = AncienneteHelper.NombreAnnee(Salarie.DateEmbauche, dateSimul);
            PrimeAnciennete = (anc >= 2 && anc <= 25)
                ? Math.Round(SalaireBase * anc / 100m, 0, MidpointRounding.AwayFromZero)
                : 0m;

            PrimeTransport = Salarie.PrimeTransport;
            AvantageVehicule = Salarie.AvantageVehicule;
            PartsFiscales = Salarie.NombrePartsFiscales;

            if (Annee == 0) Annee = DateTime.Today.Year;
            if (Mois == 0) Mois = DateTime.Today.Month;
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Annee = DateTime.Today.Year;
            Mois = DateTime.Today.Month;
        }
    }
    public static class SimulationSursalaireHelper
    {
        private const int MAX_ITERATIONS = 20;
        private const decimal TOLERANCE = 100m; // 100 FCFA

        /// <summary>
        /// Calcule le sursalaire nécessaire pour atteindre le net cible et alimente l'objet SimulationSursalaire.
        /// </summary>
 
        public static void CalculerSursalaire(SimulationSursalaire sim)
        {
            if (sim?.Salarie == null)
                throw new ArgumentException("Salarié obligatoire.");

            if (sim.NetCible <= 0)
            {
                sim.SursalaireCalcule = 0m;
                sim.DetailCalcul = "Net cible invalide (doit être > 0)";
                return;
            }

            var session = sim.Session;
            var sb = new StringBuilder();
            sb.AppendLine("=== SIMULATION DE SURSALAIRE (discrète) ===");
            sb.AppendLine($"Période : {sim.Mois:D2}/{sim.Annee} | Net cible : {sim.NetCible:N0} FCFA");
            sb.AppendLine();

            // 1) bornes entières en FCFA
            long target = (long)Math.Round(sim.NetCible, 0, MidpointRounding.AwayFromZero);
            long lo = 0;
            long hi = Math.Max(1, target * 4); // borne large

            // 2) bisection sur la fonction entière f(s) = Net(s)
            ResultatCalcul best = null;
            long bestS = 0;

            while (lo <= hi)
            {
                long mid = (lo + hi) / 2;
                var res = CalculerNetAvecSursalaire_Discret(session, sim, mid);

                if (best == null || res.NetObtenu > best.NetObtenu || (res.NetObtenu == best.NetObtenu && mid < bestS))
                {
                    best = res; bestS = mid;
                }

                if (res.NetObtenu == target)
                {
                    best = res; bestS = mid;
                    break; // trouvé pile
                }
                else if (res.NetObtenu < target)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            // 3) si pas "pile", on veut le plus petit sursalaire tel que Net >= target
            //    On inspecte autour de la borne haute finale pour trouver le premier >= target.
            long candidate = Math.Max(0, Math.Min(lo, hi) + 0);
            long start = Math.Max(0, candidate - 1000);
            long end = candidate + 1000;

            ResultatCalcul chosenRes = null;
            long chosenS = -1;

            for (long s = start; s <= end; s++)
            {
                var r = CalculerNetAvecSursalaire_Discret(session, sim, s);
                if (r.NetObtenu >= target)
                {
                    chosenRes = r; chosenS = s;
                    break;
                }
                // garde le meilleur < target au cas où tout est en dessous (rare si bornes OK)
                if (chosenRes == null || r.NetObtenu > chosenRes.NetObtenu)
                {
                    chosenRes = r; chosenS = s;
                }
            }

            // 4) finalise
            if (chosenRes == null)
            {
                sim.DetailCalcul = "Calcul impossible (aucun résultat).";
                return;
            }

            // arrondi FCFA (déjà entier)
            var surFinal = (decimal)chosenS;

            // un dernier passage pour figer les totaux affichés
            var final = CalculerNetAvecSursalaire_Discret(session, sim, chosenS);

            AffecterResultats(sim, surFinal, final);

            // trace
            sb.AppendLine($"Sursalaire retenu : {surFinal:N0} FCFA");
            sb.AppendLine($"Net obtenu       : {final.NetObtenu:N0} FCFA (écart {(final.NetObtenu - sim.NetCible):N0})");
            sim.DetailCalcul = sb.ToString();
        }

        private static void AffecterResultats(SimulationSursalaire sim, decimal sursalaire, ResultatCalcul res)
        {
            sim.SursalaireCalcule = sursalaire;
            sim.BrutFiscalEstime = res.BrutFiscal;
            sim.BrutSocialEstime = res.BrutSocial;
            sim.TotalCotisationsSociales = res.CotisationsSociales;
            sim.TRIMFEstime = res.TRIMF;
            sim.IRPPEstime = res.IRPP;
            sim.NetObtenu = res.NetObtenu;
            sim.EcartNet = res.NetObtenu - sim.NetCible;
        }

        /// <summary>
        /// Calcule le net pour un sursalaire donné (simulation complète alignée à ta logique).
        /// </summary>
        private static ResultatCalcul CalculerNetAvecSursalaire(Session session, SimulationSursalaire sim, decimal sursalaire)
        {
            var r = new ResultatCalcul();

            // 1) Gains
            decimal salaireBase = sim.SalaireBase;
            decimal indemLogement = sim.IndemniteLogement;
            decimal primeAnc = sim.PrimeAnciennete;
            decimal transport = sim.PrimeTransport;
            decimal avVehicule = sim.AvantageVehicule;

            decimal totalGains = salaireBase + indemLogement + primeAnc + sursalaire + transport + avVehicule;

            // 2) Brut fiscal & social (règles issues de ton moteur)
            r.BrutFiscal = salaireBase + indemLogement + primeAnc + sursalaire + avVehicule;
            r.BrutSocial = salaireBase + indemLogement + primeAnc + sursalaire + transport;

            // 3) Cotisations sociales
            r.CotisationsSociales = CalculerCotisationsSociales(session, sim.Salarie, r.BrutSocial);

            // 4) TRIMF
           
            var partsTrimf = sim.Salarie?.TrimfParts ?? 0m;
            r.TRIMF = CalculerTRIMF_MensuelActif(session, r.BrutFiscal, partsTrimf, sim.Annee, sim.Mois);


            // 5) IRPP
            r.IRPP = CalculerIRPP(session, r.BrutFiscal, sim.PartsFiscales, sim.Annee, sim.Mois);

            // 6) Net
            r.NetObtenu = totalGains - (r.CotisationsSociales + r.TRIMF + r.IRPP);

            return r;
        }


        private static ResultatCalcul CalculerNetAvecSursalaire_Discret(Session session, SimulationSursalaire sim, long sursalaireFCFA)
        {
            // on travaille en entier FCFA (long/decimal à 0 décimales)
            var s = (decimal)sursalaireFCFA;

            var r = new ResultatCalcul();

            // Gains
            decimal salaireBase = sim.SalaireBase;
            decimal indemLogement = sim.IndemniteLogement;
            decimal primeAnc = sim.PrimeAnciennete;
            decimal transport = sim.PrimeTransport;
            decimal avVehicule = sim.AvantageVehicule;

            // total gains (pas d’arrondi ici)
            decimal totalGains = salaireBase + indemLogement + primeAnc + s + transport + avVehicule;

            // Assiettes (comme ton moteur)
            r.BrutFiscal = salaireBase + indemLogement + primeAnc + s + avVehicule;
            r.BrutSocial = salaireBase + indemLogement + primeAnc + s + transport;

            // Cotisations (arronder chaque rubrique à 0, AwayFromZero — déjà fait dans tes calc)
            r.CotisationsSociales = CalculerCotisationsSociales(session, sim.Salarie, r.BrutSocial);

            // TRIMF (barème mensuel actif) — parts lues sur Salarié.TrimfParts
            var partsTrimf = sim.Salarie?.TrimfParts ?? 0m;
            r.TRIMF = CalculerTRIMF_MensuelActif(session, r.BrutFiscal, partsTrimf, sim.Annee, sim.Mois);

            // IRPP (avec abattement & réduction famille) — déjà arrondi à 0 / mois
            r.IRPP = CalculerIRPP(session, r.BrutFiscal, sim.PartsFiscales, sim.Annee, sim.Mois);

            // Net (FCFA entier)
            r.NetObtenu = totalGains - (r.CotisationsSociales + r.TRIMF + r.IRPP);

            // Force à 0 décimales (sécurité)
            r.NetObtenu = Math.Round(r.NetObtenu, 0, MidpointRounding.AwayFromZero);

            return r;
        }

        // ===== Détails de calcul (reprise de tes patterns) =====================

        private static decimal CalculerCotisationsSociales(Session session, Salarie salarie, decimal brutSocial)
        {
            decimal total = 0m;

            // IPRES RG
            var ipresRG = TrouverRubrique(session, RubriqueCanonique.IPRES_RG);
            if (ipresRG != null)
            {
                decimal plafond = ipresRG.Plafond ?? 0m;
                decimal baseCalc = plafond > 0 ? Math.Min(brutSocial, plafond) : brutSocial;
                decimal taux = ipresRG.Taux1 ?? 0m; // Part salariale
                total += Math.Round(baseCalc * taux / 100m, 0, MidpointRounding.AwayFromZero);
            }

            // IPRES RC (cadres)
            if (EstCadre(salarie))
            {
                var ipresRC = TrouverRubrique(session, RubriqueCanonique.IPRES_RC);
                if (ipresRC != null)
                {
                    decimal plafond = ipresRC.Plafond ?? 0m;
                    decimal baseCalc = plafond > 0 ? Math.Min(brutSocial, plafond) : brutSocial;
                    decimal taux = ipresRC.Taux1 ?? 0m;
                    total += Math.Round(baseCalc * taux / 100m, 0, MidpointRounding.AwayFromZero);
                }
            }

            // CSS Accident du Travail (part salariale si applicable dans ton paramétrage)
            var cssAT = TrouverRubrique(session, RubriqueCanonique.CSS_AccidentTravail);
            if (cssAT != null)
            {
                decimal plafond = cssAT.Plafond ?? 0m;
                decimal baseCalc = plafond > 0 ? Math.Min(brutSocial, plafond) : brutSocial;
                decimal taux = cssAT.Taux1 ?? 0m;
                total += Math.Round(baseCalc * taux / 100m, 0, MidpointRounding.AwayFromZero);
            }

            return total;
        }

           private static decimal CalculerTRIMF_MensuelActif(Session session, decimal brutFiscal, decimal parts, int annee, int mois)
        {
            if (parts <= 0) return 0m;

            // Fin du mois (beaucoup de barèmes sont “valides au dernier jour du mois”)
            var dateRef = new DateTime(annee, mois, 1).AddMonths(1).AddDays(-1);

            // 1) Essayer via ParametresPaie.CodeBaremeTRIMF_Mensuel (si renseigné)
            var pp = new XPQuery<ParametresPaie>(session).FirstOrDefault();
            string code = pp?.CodeBaremeTRIMF_Mensuel;

            BaremeTRIMF bareme = null;

            if (!string.IsNullOrWhiteSpace(code))
            {
                bareme = new XPQuery<BaremeTRIMF>(session)
                    .Where(b =>
                        b.Actif
                        && b.Nature == TrimfNature.Mensuel
                        && b.Code == code
                        && (b.DateDebut == null || b.DateDebut <= dateRef)
                        && (b.DateFin == null || dateRef <= b.DateFin))
                    .OrderByDescending(b => b.DateDebut ?? new DateTime(1900, 1, 1))
                    .FirstOrDefault();
            }

            // 2) Fallback : prendre le dernier barème Mensuel Actif qui couvre dateRef
            if (bareme == null)
            {
                bareme = new XPQuery<BaremeTRIMF>(session)
                    .Where(b =>
                        b.Actif
                        && b.Nature == TrimfNature.Mensuel
                        && (b.DateDebut == null || b.DateDebut <= dateRef)
                        && (b.DateFin == null || dateRef <= b.DateFin))
                    .OrderByDescending(b => b.DateDebut ?? new DateTime(1900, 1, 1))
                    .FirstOrDefault();
            }

            if (bareme == null)
                return 0m; // => vérifier code/date/Nature dans la base

            // 3) Normaliser les tranches : MontantMax <= 0 => borne ouverte (∞)
            var tranches = bareme.Tranches
                .OrderBy(t => t.MontantMin)
                .Select(t => new
                {
                    Min = t.MontantMin,
                    Max = (t.MontantMax <= 0 ? decimal.MaxValue : t.MontantMax),
                    Montant = t.Montant,
                    Ordre = t.Ordre
                })
                .ToList();

            if (tranches.Count == 0)
                return 0m;

            // 4) Trouver la tranche (bornes inclusives) ; si trou → fallback = dernière tranche
            var tranche = tranches.FirstOrDefault(t => brutFiscal >= t.Min && brutFiscal <= t.Max)
                       ?? tranches.Last();

            // 5) Montant par part × parts
            var trimf = Math.Round(tranche.Montant * parts, 0, MidpointRounding.AwayFromZero);
            return trimf;
        }

        private static decimal CalculerIRPP(Session session, decimal brutFiscalMensuel, decimal parts, int annee, int mois)
        {
            if (parts <= 0) parts = 1m;

            var parametres = new XPQuery<ParametresPaie>(session).FirstOrDefault();
            var bareme = TrouverBaremeIR(session, annee);
            if (bareme == null) return 0m;

            // Abattement 30% (plafonné)
            decimal pctAbatt = (parametres?.R_IR_Abattement_TauxPercent ?? 30m) / 100m;
            decimal plafondMensuel = parametres?.R_IR_Abattement_PlafondMensuel ?? 75_000m;
            decimal plafondAnnuel = parametres?.R_IR_Abattement_PlafondAnnuel ?? 900_000m;

            decimal revenuAnnuelTheo = brutFiscalMensuel * 12m;
            decimal abattAnnuelTheo = revenuAnnuelTheo * pctAbatt;
            decimal imabMensuel = (abattAnnuelTheo >= plafondAnnuel)
                ? plafondMensuel
                : Math.Round(abattAnnuelTheo / 12m, 0, MidpointRounding.AwayFromZero);

            decimal dppAnnuelTheo = Math.Max(0m, (brutFiscalMensuel - imabMensuel) * 12m);

            // Progressif
            decimal impotProgressif = 0m;
            foreach (var t in bareme.Tranches.OrderBy(t => t.MontantMin))
            {
                if (dppAnnuelTheo <= t.MontantMin) break;
                decimal sup = Math.Min(dppAnnuelTheo, t.MontantMax);
                decimal assiette = Math.Max(0m, sup - t.MontantMin);
                if (assiette <= 0) continue;

                impotProgressif += Math.Round(assiette * (t.Taux / 100m), 0, MidpointRounding.AwayFromZero);

                if (dppAnnuelTheo <= t.MontantMax) break;
            }

            // Réduction famille
            var (rfPct, rfMin, rfMax) = ResolveReductionFamille(session, parts);
            decimal reduction = Math.Round(impotProgressif * rfPct, 0, MidpointRounding.AwayFromZero);
            if (rfMin > 0 && reduction < rfMin) reduction = rfMin;
            if (rfMax > 0 && reduction > rfMax) reduction = rfMax;

            decimal impotAnnuelNet = Math.Max(0m, impotProgressif - reduction);
            decimal impotMensuel = Math.Round(impotAnnuelNet / 12m, 0, MidpointRounding.AwayFromZero);

            return impotMensuel;
        }

        // ===== Helpers d’accès aux données ====================================

        private static bool EstCadre(Salarie salarie)
        {
            var lib = salarie?.Categories?.Intitule ?? salarie?.Echelon?.Categories?.Intitule;
            return lib != null && lib.IndexOf("cadre", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Rubrique TrouverRubrique(Session session, RubriqueCanonique canonique)
        {
            return new XPQuery<Rubrique>(session)
                .FirstOrDefault(r => r.Actif && r.Canonique == canonique);
        }

   
        private static BaremeIR TrouverBaremeIR(Session session, int annee)
        {
            var parametres = new XPQuery<ParametresPaie>(session).FirstOrDefault();
            var code = parametres?.ResolveCodeBaremeIRAnnuel(annee) ?? $"IR_DPP_{annee}";

            return new XPQuery<BaremeIR>(session)
                .FirstOrDefault(b => b.Actif && b.Code == code);
        }

        private static (decimal pct, decimal minAnnuel, decimal maxAnnuel) ResolveReductionFamille(Session session, decimal parts)
        {
            var rf = new XPQuery<IRReductionFamille>(session)
                .FirstOrDefault(r => r.Actif && r.NbrePart == parts);

            if (rf != null)
                return (rf.Taux / 100m, rf.MinAnnuel, rf.MaxAnnuel);

            var parametres = new XPQuery<ParametresPaie>(session).FirstOrDefault();
            decimal pct = (parametres?.R_IR_ReductionFamille_Pourcentage ?? 0m) / 100m;
            decimal minA = (parametres?.R_IR_ReductionFamille_MinParPart_Annuel ?? 0m) * parts;
            decimal maxA = (parametres?.R_IR_ReductionFamille_MaxParPart_Annuel ?? 0m) * parts;

            return (pct, minA, maxA);
        }

        // ===== DTO interne =====================================================

        private sealed class ResultatCalcul
        {
            public decimal BrutFiscal { get; set; }
            public decimal BrutSocial { get; set; }
            public decimal CotisationsSociales { get; set; }
            public decimal TRIMF { get; set; }
            public decimal IRPP { get; set; }
            public decimal NetObtenu { get; set; }
        }
    }

}