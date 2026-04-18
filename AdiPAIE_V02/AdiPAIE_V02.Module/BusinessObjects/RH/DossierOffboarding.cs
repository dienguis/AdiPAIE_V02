using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Dossier de départ (offboarding) — suivi complet de la procédure de sortie
    /// et calcul du solde de tout compte.
    ///
    /// Workflow : Initié → EnCours → SoldeCalculé → ValidéRH → ValidéDAF → Clôturé
    ///
    /// Calcul solde de tout compte (droit sénégalais) :
    ///   - Congés non pris : SoldeDisponible × (SalaireBase / 26)
    ///   - Indemnité de préavis (si licenciement)
    ///   - Indemnité de licenciement (si licenciement > 1 an)
    ///   - Dernier salaire au prorata
    ///   - Primes diverses
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Dossier de départ")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Audit")]
    //[NavigationItem("Ressources humaines")]
    [Appearance("Offboarding_Cloture", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+OffboardingStatut,Cloture#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [Appearance("Offboarding_ValideRH", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+OffboardingStatut,ValideRH#",
        FontColor = "#1B6C2A")]
    [RuleCriteria("Offboarding_DateSortie_Valide", DefaultContexts.Save,
        "DateSortie >= DateDebut",
        CustomMessageTemplate = "La date de sortie doit être après la date de début de procédure.")]
    public class DossierOffboarding : BaseObject
    {
        public DossierOffboarding(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = OffboardingStatut.Initie;
            DateDebut = DateTime.Today;
            DateSortie = DateTime.Today;
            Reference = $"OFF-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
            try { InitiePar = SecuritySystem.CurrentUserName; } catch { }
        }

        // ── Référence ─────────────────────────────────────────────────
        [Size(30)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Référence")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value);
        }
        string reference;

        // ── Salarié ───────────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        [DataSourceCriteria("IsActif = true")]
        [ImmediatePostData]
        public Salarie Salarie
        {
            get => salarie;
            set
            {
                SetPropertyValue(nameof(Salarie), ref salarie, value);
                if (!IsLoading && value != null)
                    PreRemplirDepuisSalarie();
            }
        }
        Salarie salarie;

        // ── Motif et dates ────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Motif de départ")]
        public MotifDepart? MotifDepart
        {
            get => motifDepart;
            set => SetPropertyValue(nameof(MotifDepart), ref motifDepart, value);
        }
        MotifDepart? motifDepart;

        [Size(500)]
        [XafDisplayName("Précision motif")]
        [VisibleInListView(false)]
        public string PrecisionMotif
        {
            get => precisionMotif;
            set => SetPropertyValue(nameof(PrecisionMotif), ref precisionMotif, value?.Trim());
        }
        string precisionMotif;

        [XafDisplayName("Date d'initiation")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }
        DateTime dateDebut;

        [RuleRequiredField]
        [XafDisplayName("Date de sortie effective")]
        public DateTime DateSortie
        {
            get => dateSortie;
            set => SetPropertyValue(nameof(DateSortie), ref dateSortie, value);
        }
        DateTime dateSortie;

        [XafDisplayName("Durée préavis (jours)")]
        [VisibleInListView(false)]
        public int DureePreavis
        {
            get => dureePreavis;
            set => SetPropertyValue(nameof(DureePreavis), ref dureePreavis, value);
        }
        int dureePreavis;

        // ── Snapshot rémunération ─────────────────────────────────────
        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Salaire de base (snapshot)")]
        [VisibleInListView(false)]
        public decimal SalaireBase
        {
            get => salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value);
        }
        decimal salaireBase;

        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Indemnité logement (snapshot)")]
        [VisibleInListView(false)]
        public decimal IndemniteLogement
        {
            get => indemniteLogement;
            set => SetPropertyValue(nameof(IndemniteLogement), ref indemniteLogement, value);
        }
        decimal indemniteLogement;

        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Ancienneté (années)")]
        [VisibleInListView(false)]
        public int AncienneteAnnees
        {
            get => ancienneteAnnees;
            set => SetPropertyValue(nameof(AncienneteAnnees), ref ancienneteAnnees, value);
        }
        int ancienneteAnnees;

        [ModelDefault("DisplayFormat", "N2")]
        [XafDisplayName("Congés disponibles (jours)")]
        [VisibleInListView(false)]
        public decimal CongesDisponibles
        {
            get => congesDisponibles;
            set => SetPropertyValue(nameof(CongesDisponibles), ref congesDisponibles, value);
        }
        decimal congesDisponibles;

        // ── Calcul solde de tout compte ───────────────────────────────
        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Indemnité congés non pris (FCFA)")]
        [VisibleInListView(false)]
        public decimal IndemniteCongés
        {
            get => indemniteCongés;
            set => SetPropertyValue(nameof(IndemniteCongés), ref indemniteCongés, value);
        }
        decimal indemniteCongés;

        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Indemnité de préavis (FCFA)")]
        [VisibleInListView(false)]
        public decimal IndemnitePreavis
        {
            get => indemnitePreavis;
            set => SetPropertyValue(nameof(IndemnitePreavis), ref indemnitePreavis, value);
        }
        decimal indemnitePreavis;

        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Indemnité de licenciement (FCFA)")]
        [VisibleInListView(false)]
        public decimal IndemniteLicenciement
        {
            get => indemniteLicenciement;
            set => SetPropertyValue(nameof(IndemniteLicenciement), ref indemniteLicenciement, value);
        }
        decimal indemniteLicenciement;

        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Salaire du mois prorata (FCFA)")]
        [VisibleInListView(false)]
        public decimal SalaireProrata
        {
            get => salaireProrata;
            set => SetPropertyValue(nameof(SalaireProrata), ref salaireProrata, value);
        }
        decimal salaireProrata;

        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("Autres éléments (FCFA)")]
        [VisibleInListView(false)]
        public decimal AutresElements
        {
            get => autresElements;
            set => SetPropertyValue(nameof(AutresElements), ref autresElements, value);
        }
        decimal autresElements;

        [NonPersistent]
        [ModelDefault("DisplayFormat", "N0")]
        [XafDisplayName("TOTAL SOLDE DE TOUT COMPTE (FCFA)")]
        public decimal TotalSoldeToutCompte =>
            IndemniteCongés + IndemnitePreavis + IndemniteLicenciement
            + SalaireProrata + AutresElements;

        // ── Statut ────────────────────────────────────────────────────
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public OffboardingStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        OffboardingStatut statut;

        // ── Traçabilité ───────────────────────────────────────────────
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Initié par")]
        [VisibleInListView(false)]
        public string InitiePar
        {
            get => initiePar;
            set => SetPropertyValue(nameof(InitiePar), ref initiePar, value);
        }
        string initiePar;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé RH par")]
        [VisibleInListView(false)]
        public string ValideRHPar
        {
            get => valideRHPar;
            set => SetPropertyValue(nameof(ValideRHPar), ref valideRHPar, value);
        }
        string valideRHPar;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation RH")]
        [VisibleInListView(false)]
        public DateTime? DateValidationRH
        {
            get => dateValidationRH;
            set => SetPropertyValue(nameof(DateValidationRH), ref dateValidationRH, value);
        }
        DateTime? dateValidationRH;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé DAF par")]
        [VisibleInListView(false)]
        public string ValideDAFPar
        {
            get => valideDAFPar;
            set => SetPropertyValue(nameof(ValideDAFPar), ref valideDAFPar, value);
        }
        string valideDAFPar;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation DAF")]
        [VisibleInListView(false)]
        public DateTime? DateValidationDAF
        {
            get => dateValidationDAF;
            set => SetPropertyValue(nameof(DateValidationDAF), ref dateValidationDAF, value);
        }
        DateTime? dateValidationDAF;

        [Size(1000)]
        [XafDisplayName("Observations")]
        [VisibleInListView(false)]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value);
        }
        string observations;

        // ── Document solde de tout compte ─────────────────────────────
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Document solde de tout compte (.pdf)")]
        [VisibleInListView(false)]
        public FileData DocumentSolde
        {
            get => documentSolde;
            set => SetPropertyValue(nameof(DocumentSolde), ref documentSolde, value);
        }
        FileData documentSolde;

        // ── Propriétés calculées ──────────────────────────────────────
        [NonPersistent]
        public string DisplayName =>
            $"{Salarie?.FullName ?? "—"} — {MotifDepart} ({DateSortie:dd/MM/yyyy})";

        // ── Méthodes ──────────────────────────────────────────────────
        private void PreRemplirDepuisSalarie()
        {
            if (Salarie == null) return;
            SalaireBase = Salarie.SalaireBase;
            IndemniteLogement = Salarie.IndemniteLogement;
            AncienneteAnnees = Salarie.Anciennete;

            // Congés disponibles — chercher le solde actif de l'année en cours
            try
            {
                var annee = DateTime.Today.Year;
                var solde = new DevExpress.Xpo.XPQuery<SoldeConge>(Session)
                    .Where(s => s.Salarie.Oid == Salarie.Oid
                             && s.Statut == SoldeCongeStatut.Actif
                             && s.Annee == annee)
                    .Sum(s => s.SoldeDisponible);
                CongesDisponibles = solde;
            }
            catch { CongesDisponibles = 0; }
        }

        public void CalculerSoldeToutCompte()
        {
            // 1. Indemnité congés non pris = jours × (salaire / 26)
            var tauxJournalier = SalaireBase > 0 ? SalaireBase / 26m : 0m;
            IndemniteCongés = Math.Round(CongesDisponibles * tauxJournalier, 0);

            // 2. Indemnité de préavis (si licenciement)
            if (MotifDepart == Domain.DomainEnums.MotifDepart.Licenciement
             || MotifDepart == Domain.DomainEnums.MotifDepart.RuptureConventionnelle)
                IndemnitePreavis = Math.Round((SalaireBase + IndemniteLogement)
                    / 30m * DureePreavis, 0);
            else
                IndemnitePreavis = 0;

            // 3. Indemnité de licenciement (droit sénégalais)
            // Code du Travail SN : 25% salaire/an jusqu'à 5 ans,
            //                      30% de 6 à 10 ans, 40% au-delà
            if ((MotifDepart == Domain.DomainEnums.MotifDepart.Licenciement
              || MotifDepart == Domain.DomainEnums.MotifDepart.RuptureConventionnelle)
             && AncienneteAnnees >= 1)
            {
                var base_ = SalaireBase + IndemniteLogement;
                decimal indemnite = 0;
                for (int an = 1; an <= AncienneteAnnees; an++)
                {
                    if (an <= 5) indemnite += base_ * 0.25m;
                    else if (an <= 10) indemnite += base_ * 0.30m;
                    else indemnite += base_ * 0.40m;
                }
                IndemniteLicenciement = Math.Round(indemnite, 0);
            }
            else IndemniteLicenciement = 0;

            // 4. Prorata salaire du mois
            var joursSortie = DateSortie.Day;
            SalaireProrata = Math.Round(
                (SalaireBase + IndemniteLogement) / 30m * joursSortie, 0);

            Statut = OffboardingStatut.SoldeCalcule;
        }

        public override string ToString() => DisplayName;
    }
}
