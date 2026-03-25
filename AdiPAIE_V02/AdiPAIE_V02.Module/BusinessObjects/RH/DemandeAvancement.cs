using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Exceptions;
using DevExpress.Drawing;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Demande d'avancement ou de promotion d'un salarié.
    ///
    /// Workflow :
    ///   RH → Soumettre → DG → Approuver → RH → Appliquer (MAJ fiche salarié)
    ///
    /// À l'application :
    ///   - Fonction, Département, Échelon, SalaireBase, IndemniteLogement
    ///     sont mis à jour automatiquement sur la fiche salarié.
    ///   - Un HistoriquePoste est créé pour tracer l'ancienne situation.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Demande d'avancement")]
    [DefaultProperty(nameof(DisplayAvancement))]
    [ImageName("BO_Employee")]
    [NavigationItem("GRH - Administration")]
    [Appearance("AvApprouve", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,ApprouveDG#",
        FontColor = "#1B6C2A", FontStyle = DXFontStyle.Bold)]
    [Appearance("AvApplique", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,Applique#",
        FontColor = "#1F4E79", FontStyle = DXFontStyle.Italic)]
    [Appearance("AvRejete", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AvancementStatut,Rejete#",
        FontColor = "Red", FontStyle = DXFontStyle.Italic)]
    public class DemandeAvancement : BaseObject
    {
        public DemandeAvancement(Session session) : base(session) { }

        // ── Référence ─────────────────────────────────────────
        string reference;
        [Size(50)]
        [XafDisplayName("Référence")]
        [ModelDefault("AllowEdit", "False")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim());
        }

        // ── Salarié ───────────────────────────────────────────
        [Association("Salarie-Avancements")]
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        [ImmediatePostData]
        public Salarie Salarie
        {
            get => salarie;
            set
            {
                SetPropertyValue(nameof(Salarie), ref salarie, value);
                if (!IsLoading) ChargerSituationActuelle();
            }
        }
        Salarie salarie;

        TypeAvancement typeAvancement;
        [XafDisplayName("Type d'avancement")]
        public TypeAvancement TypeAvancement
        {
            get => typeAvancement;
            set => SetPropertyValue(nameof(TypeAvancement), ref typeAvancement, value);
        }

        DateTime dateEffet;
        [RuleRequiredField]
        [XafDisplayName("Date d'effet")]
        public DateTime DateEffet
        {
            get => dateEffet;
            set => SetPropertyValue(nameof(DateEffet), ref dateEffet, value);
        }

        string motif;
        [Size(1000)]
        [XafDisplayName("Motif / Justification")]
        public string Motif
        {
            get => motif;
            set => SetPropertyValue(nameof(Motif), ref motif, value?.Trim());
        }

        // ═══════════════════════════════════════════════════════
        // SITUATION ACTUELLE (snapshot au moment de la demande)
        // ═══════════════════════════════════════════════════════
        [Association("FonctionActuelle-Avancements")]
        [XafDisplayName("Fonction actuelle")]
        [ModelDefault("AllowEdit", "False")]
        public Fonction FonctionActuelle
        {
            get => fonctionActuelle;
            set => SetPropertyValue(nameof(FonctionActuelle), ref fonctionActuelle, value);
        }
        Fonction fonctionActuelle;

        [Association("DeptActuel-Avancements")]
        [XafDisplayName("Département actuel")]
        [ModelDefault("AllowEdit", "False")]
        public Departement DepartementActuel
        {
            get => departementActuel;
            set => SetPropertyValue(nameof(DepartementActuel), ref departementActuel, value);
        }
        Departement departementActuel;

        [Association("EchelonActuel-Avancements")]
        [XafDisplayName("Échelon actuel")]
        [ModelDefault("AllowEdit", "False")]
        public Echelons EchelonActuel
        {
            get => echelonActuel;
            set => SetPropertyValue(nameof(EchelonActuel), ref echelonActuel, value);
        }
        Echelons echelonActuel;

        decimal salaireActuel;
        [XafDisplayName("Salaire actuel (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal SalaireActuel
        {
            get => salaireActuel;
            set => SetPropertyValue(nameof(SalaireActuel), ref salaireActuel, value);
        }

        decimal indemniteActuelle;
        [XafDisplayName("Indemnité logement actuelle (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal IndemniteActuelle
        {
            get => indemniteActuelle;
            set => SetPropertyValue(nameof(IndemniteActuelle), ref indemniteActuelle, value);
        }

        // ═══════════════════════════════════════════════════════
        // NOUVELLE SITUATION (proposée)
        // ═══════════════════════════════════════════════════════
        [Association("NouvelleFonction-Avancements")]
        [XafDisplayName("Nouvelle fonction")]
        public Fonction NouvelleFonction
        {
            get => nouvelleFonction;
            set => SetPropertyValue(nameof(NouvelleFonction), ref nouvelleFonction, value);
        }
        Fonction nouvelleFonction;

        [Association("NouveauDept-Avancements")]
        [XafDisplayName("Nouveau département")]
        public Departement NouveauDepartement
        {
            get => nouveauDepartement;
            set => SetPropertyValue(nameof(NouveauDepartement), ref nouveauDepartement, value);
        }
        Departement nouveauDepartement;

        [Association("NouvelEchelon-Avancements")]
        [XafDisplayName("Nouvel échelon")]
        [ImmediatePostData]
        public Echelons NouvelEchelon
        {
            get => nouvelEchelon;
            set
            {
                SetPropertyValue(nameof(NouvelEchelon), ref nouvelEchelon, value);
                if (!IsLoading && nouvelEchelon != null)
                {
                    NouveauSalaireBase = nouvelEchelon.SalaireBase;
                    NouvelleIndemnite = nouvelEchelon.IdemniteLogement;
                }
            }
        }
        Echelons nouvelEchelon;

        decimal nouveauSalaireBase;
        [XafDisplayName("Nouveau salaire de base (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal NouveauSalaireBase
        {
            get => nouveauSalaireBase;
            set => SetPropertyValue(nameof(NouveauSalaireBase), ref nouveauSalaireBase, value);
        }

        decimal nouvelleIndemnite;
        [XafDisplayName("Nouvelle indemnité logement (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal NouvelleIndemnite
        {
            get => nouvelleIndemnite;
            set => SetPropertyValue(nameof(NouvelleIndemnite), ref nouvelleIndemnite, value);
        }

        // ── Indicateurs d'impact ──────────────────────────────
        [NonPersistent]
        [XafDisplayName("Variation salaire (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        public decimal VariationSalaire =>
            NouveauSalaireBase - SalaireActuel;

        [NonPersistent]
        [XafDisplayName("Variation salaire (%)")]
        [ModelDefault("DisplayFormat", "N1")]
        public decimal VariationSalairePercent =>
            SalaireActuel > 0
                ? Math.Round(VariationSalaire / SalaireActuel * 100, 1)
                : 0;

        // ═══════════════════════════════════════════════════════
        // STATUT ET WORKFLOW
        // ═══════════════════════════════════════════════════════
        AvancementStatut statut;
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public AvancementStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        DateTime? dateSoumission;
        [XafDisplayName("Date de soumission")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateSoumission
        {
            get => dateSoumission;
            set => SetPropertyValue(nameof(DateSoumission), ref dateSoumission, value);
        }

        DateTime? dateDecisionDG;
        [XafDisplayName("Date décision DG")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateDecisionDG
        {
            get => dateDecisionDG;
            set => SetPropertyValue(nameof(DateDecisionDG), ref dateDecisionDG, value);
        }

        DateTime? dateApplication;
        [XafDisplayName("Date d'application")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateApplication
        {
            get => dateApplication;
            set => SetPropertyValue(nameof(DateApplication), ref dateApplication, value);
        }

        [Association("DG-Avancements")]
        [XafDisplayName("Approuvé par")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ApprovePar
        {
            get => approvePar;
            set => SetPropertyValue(nameof(ApprovePar), ref approvePar, value);
        }
        Salarie approvePar;

        string motifRejet;
        [Size(500)]
        [XafDisplayName("Motif de rejet")]
        public string MotifRejet
        {
            get => motifRejet;
            set => SetPropertyValue(nameof(MotifRejet), ref motifRejet, value?.Trim());
        }

        string appliquePar;
        [Size(100)]
        [XafDisplayName("Appliqué par")]
        [ModelDefault("AllowEdit", "False")]
        public string AppliquePar
        {
            get => appliquePar;
            set => SetPropertyValue(nameof(AppliquePar), ref appliquePar, value);
        }

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayAvancement =>
            $"{Reference} — {Salarie?.FullName} ({TypeAvancement}) — {Statut}";

        // ── Collection inverse HistoriquePoste ───────────────
        [Association("Avancement-Historique"), Aggregated]
        [XafDisplayName("Historiques générés")]
        [Browsable(false)]
        public XPCollection<HistoriquePoste> Historiques
            => GetCollection<HistoriquePoste>(nameof(Historiques));

        // ── Transitions ───────────────────────────────────────
        public void Soumettre()
        {
            if (Statut != AvancementStatut.Brouillon)
                throw new UserFriendlyException(
                    "Seule une demande en Brouillon peut être soumise.");
            Statut = AvancementStatut.SoumisRH;
            DateSoumission = DateTime.Now;
        }

        public void ApprouverDG(Salarie dg)
        {
            if (Statut != AvancementStatut.SoumisRH)
                throw new UserFriendlyException(
                    "La demande doit être soumise au RH pour être approuvée.");
            Statut = AvancementStatut.ApprouveDG;
            DateDecisionDG = DateTime.Now;
            ApprovePar = dg;
        }

        public void Rejeter(string motif)
        {
            if (Statut == AvancementStatut.Applique)
                throw new UserFriendlyException(
                    "Un avancement déjà appliqué ne peut pas être rejeté.");
            if (string.IsNullOrWhiteSpace(motif))
                throw new UserFriendlyException(
                    "Veuillez saisir un motif de rejet.");
            Statut = AvancementStatut.Rejete;
            MotifRejet = motif;
        }

        public void Annuler()
        {
            if (Statut == AvancementStatut.Applique)
                throw new UserFriendlyException(
                    "Un avancement déjà appliqué ne peut pas être annulé.");
            Statut = AvancementStatut.Annule;
        }

        // ── Helpers ───────────────────────────────────────────
        /// <summary>
        /// Charge la situation actuelle du salarié comme snapshot.
        /// </summary>
        public void ChargerSituationActuelle()
        {
            if (Salarie == null) return;
            FonctionActuelle = Salarie.Fonction;
            DepartementActuel = Salarie.Departement;
            EchelonActuel = Salarie.Echelon;
            SalaireActuel = Salarie.SalaireBase;
            IndemniteActuelle = Salarie.IndemniteLogement;
            // Pré-remplir la nouvelle situation avec les valeurs actuelles
            NouvelleFonction = Salarie.Fonction;
            NouveauDepartement = Salarie.Departement;
            NouvelEchelon = Salarie.Echelon;
            NouveauSalaireBase = Salarie.SalaireBase;
            NouvelleIndemnite = Salarie.IndemniteLogement;
        }

        // ── Cycle de vie ──────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = AvancementStatut.Brouillon;
            DateEffet = DateTime.Today;
        }
    }
}
