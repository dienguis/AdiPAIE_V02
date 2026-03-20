using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.Drawing;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
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
    // ════════════════════════════════════════════════════════
    // DEMANDE DE DÉPLACEMENT
    // ════════════════════════════════════════════════════════

    [DefaultClassOptions]
    [XafDisplayName("Demande de déplacement")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("Action_Forward")]
    [NavigationItem("GRH - Espace salarié")]

    [RuleCriteria("Deplacement_DateRetour_GTE_DateDepart", DefaultContexts.Save,
        "DateRetour >= DateDepart",
        CustomMessageTemplate = "La date de retour doit être >= à la date de départ.")]

    // Couleurs par statut
    [Appearance("Deplacement_Approuvee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,ApprouveeRH#",
        FontColor = "Green", FontStyle = DXFontStyle.Bold)]
    [Appearance("Deplacement_Rejetee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,Rejetee#",
        FontColor = "Red")]
    [Appearance("Deplacement_Traitee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,Traitee#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic)]
    [Appearance("Deplacement_EnAttente", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,EnAttenteN1#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,SoumiseAssistant#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,EnAttenteRH#",
        FontColor = "DarkOrange")]

    // Verrouillage sauf en Brouillon
    [Appearance("Deplacement_Lock",
        Criteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DeplacementStatut,Brouillon#",
        TargetItems = "Salarie;Objet;DateDepart;DateRetour;Destination;MotifDeplacement",
        Enabled = false)]
    public class DemandeDeplacement : BaseObject
    {
        public DemandeDeplacement(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = DeplacementStatut.Brouillon;
            DateDepart = DateTime.Today.AddDays(7);
            DateRetour = DateTime.Today.AddDays(8);
            DateDemande = DateTime.Today;
            try
            {
                var currentUser = Session.FindObject<ApplicationUser>(
                    DevExpress.Data.Filtering.CriteriaOperator.Parse(
                        "UserName = ?", SecuritySystem.CurrentUserName));
                if (currentUser?.Salarie != null)
                    Salarie = currentUser.Salarie;
            }
            catch { }
        }

        // ── Salarié ───────────────────────────────────────────
        [RuleRequiredField]
        [Association("Salarie-Deplacements")]
        [XafDisplayName("Salarié")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Objet et dates ────────────────────────────────────
        [RuleRequiredField]
        [Size(300)]
        [XafDisplayName("Objet de la mission")]
        public string Objet
        {
            get => objet;
            set => SetPropertyValue(nameof(Objet), ref objet, value?.Trim());
        }
        string objet;

        [Size(300)]
        [XafDisplayName("Destination principale")]
        public string Destination
        {
            get => destination;
            set => SetPropertyValue(nameof(Destination), ref destination, value?.Trim());
        }
        string destination;

        [RuleRequiredField]
        [XafDisplayName("Date de départ")]
        [ImmediatePostData]
        public DateTime DateDepart
        {
            get => dateDepart;
            set => SetPropertyValue(nameof(DateDepart), ref dateDepart, value.Date);
        }
        DateTime dateDepart;

        [RuleRequiredField]
        [XafDisplayName("Date de retour")]
        [ImmediatePostData]
        public DateTime DateRetour
        {
            get => dateRetour;
            set => SetPropertyValue(nameof(DateRetour), ref dateRetour, value.Date);
        }
        DateTime dateRetour;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Nombre de jours")]
        public int NombreJours
        {
            get => nombreJours;
            set => SetPropertyValue(nameof(NombreJours), ref nombreJours, value);
        }
        int nombreJours;

        [XafDisplayName("Date de demande")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateDemande
        {
            get => dateDemande;
            set => SetPropertyValue(nameof(DateDemande), ref dateDemande, value);
        }
        DateTime dateDemande;

        [Size(1000)]
        [XafDisplayName("Motif / Justification")]
        public string MotifDeplacement
        {
            get => motifDeplacement;
            set => SetPropertyValue(nameof(MotifDeplacement), ref motifDeplacement, value?.Trim());
        }
        string motifDeplacement;

        // ── Statut ────────────────────────────────────────────
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public DeplacementStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        DeplacementStatut statut;

        // ── Hiérarchie (snapshot à la soumission) ─────────────
        [XafDisplayName("Valideur N+1")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ValideurN1
        {
            get => valideurN1;
            set => SetPropertyValue(nameof(ValideurN1), ref valideurN1, value);
        }
        Salarie valideurN1;

        [XafDisplayName("Date validation N+1")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateValidationN1
        {
            get => dateValidationN1;
            set => SetPropertyValue(nameof(DateValidationN1), ref dateValidationN1, value);
        }
        DateTime? dateValidationN1;

        // ── Traitement assistant / RH / DAF / Comptable ────────
        [Size(100)]
        [XafDisplayName("Traité par (assistant)")]
        [ModelDefault("AllowEdit", "False")]
        public string TraiteParAssistant
        {
            get => traiteParAssistant;
            set => SetPropertyValue(nameof(TraiteParAssistant), ref traiteParAssistant, value);
        }
        string traiteParAssistant;

        [Size(100)]
        [XafDisplayName("Approuvé par (RH)")]
        [ModelDefault("AllowEdit", "False")]
        public string ApprouveParRH
        {
            get => approuveParRH;
            set => SetPropertyValue(nameof(ApprouveParRH), ref approuveParRH, value);
        }
        string approuveParRH;

        [XafDisplayName("Date approbation RH")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateApprobationRH
        {
            get => dateApprobationRH;
            set => SetPropertyValue(nameof(DateApprobationRH), ref dateApprobationRH, value);
        }
        DateTime? dateApprobationRH;

        [Size(100)]
        [XafDisplayName("Validé par (DAF)")]
        [ModelDefault("AllowEdit", "False")]
        public string ValideParDAF
        {
            get => valideParDAF;
            set => SetPropertyValue(nameof(ValideParDAF), ref valideParDAF, value);
        }
        string valideParDAF;

        [XafDisplayName("Date validation DAF")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateValidationDAF
        {
            get => dateValidationDAF;
            set => SetPropertyValue(nameof(DateValidationDAF), ref dateValidationDAF, value);
        }
        DateTime? dateValidationDAF;

        [Size(100)]
        [XafDisplayName("Confirmé par (Comptable)")]
        [ModelDefault("AllowEdit", "False")]
        public string ConfirmeParComptable
        {
            get => confirmeParComptable;
            set => SetPropertyValue(nameof(ConfirmeParComptable), ref confirmeParComptable, value);
        }
        string confirmeParComptable;

        // ── Rejets ────────────────────────────────────────────
        [Size(500)]
        [XafDisplayName("Motif de rejet")]
        [ModelDefault("AllowEdit", "False")]
        public string MotifRejet
        {
            get => motifRejet;
            set => SetPropertyValue(nameof(MotifRejet), ref motifRejet, value?.Trim());
        }
        string motifRejet;

        [Size(100)]
        [XafDisplayName("Rejeté par")]
        [ModelDefault("AllowEdit", "False")]
        public string RejeteParNom
        {
            get => rejeteParNom;
            set => SetPropertyValue(nameof(RejeteParNom), ref rejeteParNom, value);
        }
        string rejeteParNom;

        // ── Totaux ────────────────────────────────────────────
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Total frais (FCFA)")]
        public decimal TotalFrais
        {
            get => totalFrais;
            set => SetPropertyValue(nameof(TotalFrais), ref totalFrais, value);
        }
        decimal totalFrais;

        // ── Numéro d'ordre de mission ─────────────────────────
        [Size(30)]
        [XafDisplayName("N° Ordre de mission")]
        [ModelDefault("AllowEdit", "False")]
        public string NumeroOrdre
        {
            get => numeroOrdre;
            set => SetPropertyValue(nameof(NumeroOrdre), ref numeroOrdre, value);
        }
        string numeroOrdre;

        // ── Document généré ───────────────────────────────────
        [XafDisplayName("Ordre de mission (document)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        public DevExpress.Persistent.BaseImpl.FileData DocumentOrdre
        {
            get => documentOrdre;
            set => SetPropertyValue(nameof(DocumentOrdre), ref documentOrdre, value);
        }
        DevExpress.Persistent.BaseImpl.FileData documentOrdre;

        // ── Collections ───────────────────────────────────────
        [Association("DemandeDeplacement-Circuit"), Aggregated]
        [XafDisplayName("Circuit (itinéraire)")]
        public XPCollection<LigneCircuit> Circuit
            => GetCollection<LigneCircuit>(nameof(Circuit));

        [Association("DemandeDeplacement-Frais"), Aggregated]
        [XafDisplayName("Note de frais")]
        public XPCollection<LigneFraisMission> Frais
            => GetCollection<LigneFraisMission>(nameof(Frais));

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayName =>
            $"{Salarie?.FullName} — {Objet} ({DateDepart:dd/MM/yyyy})";

        // ── Cycle de vie ──────────────────────────────────────

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted) RecalculerNombreJours();
        }

        public void RecalculerNombreJours()
        {
            NombreJours = Math.Max(1, (DateRetour - DateDepart).Days + 1);
        }

        public void RecalculerTotal()
        {
            TotalFrais = Frais.Sum(f => f.Montant);
        }

        // ── Méthodes workflow ─────────────────────────────────

        public void Soumettre()
        {
            if (Statut != DeplacementStatut.Brouillon)
                throw new UserFriendlyException(
                    "Seule une demande en Brouillon peut être soumise.");
            if (Salarie == null)
                throw new UserFriendlyException("Le salarié n'est pas renseigné.");
            if (!Circuit.Any())
                throw new UserFriendlyException(
                    "Veuillez renseigner au moins une étape dans le circuit.");

            var chain = Salarie.GetManagerChain(1);
            if (chain.Count == 0)
            {
                Statut = DeplacementStatut.SoumiseAssistant;
                return;
            }
            ValideurN1 = chain[0];
            Statut = DeplacementStatut.EnAttenteN1;
        }

        public void ValiderN1()
        {
            if (Statut != DeplacementStatut.EnAttenteN1)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de validation N+1.");
            DateValidationN1 = DateTime.Now;
            Statut = DeplacementStatut.SoumiseAssistant;
        }

        public void RejeterN1(string motif = null)
        {
            if (Statut != DeplacementStatut.EnAttenteN1)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de validation N+1.");
            try { RejeteParNom = SecuritySystem.CurrentUserName; } catch { }
            if (!string.IsNullOrWhiteSpace(motif)) MotifRejet = motif;
            Statut = DeplacementStatut.Brouillon;
            ValideurN1 = null;
            DateValidationN1 = null;
        }

        public void SoumettreAuRH()
        {
            if (Statut != DeplacementStatut.SoumiseAssistant)
                throw new UserFriendlyException(
                    "La demande n'est pas en phase assistant RH.");
            if (!Frais.Any())
                throw new UserFriendlyException(
                    "Veuillez renseigner au moins une ligne de frais avant de soumettre au RH.");
            try { TraiteParAssistant = SecuritySystem.CurrentUserName; } catch { }
            Statut = DeplacementStatut.EnAttenteRH;
        }

        public void ApprouverRH()
        {
            if (Statut != DeplacementStatut.EnAttenteRH)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente d'approbation RH.");
            DateApprobationRH = DateTime.Now;
            try { ApprouveParRH = SecuritySystem.CurrentUserName; } catch { }
            Statut = DeplacementStatut.ApprouveeRH;
        }

        public void RejeterRH(string motif = null)
        {
            if (Statut != DeplacementStatut.EnAttenteRH)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente d'approbation RH.");
            try { RejeteParNom = SecuritySystem.CurrentUserName; } catch { }
            if (!string.IsNullOrWhiteSpace(motif)) MotifRejet = motif;
            Statut = DeplacementStatut.SoumiseAssistant;
        }

        public void ValiderDAF()
        {
            if (Statut != DeplacementStatut.ApprouveeRH)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de validation DAF.");
            DateValidationDAF = DateTime.Now;
            try { ValideParDAF = SecuritySystem.CurrentUserName; } catch { }
            Statut = DeplacementStatut.EnAttenteComptable;
        }

        public void ConfirmerComptable()
        {
            if (Statut != DeplacementStatut.EnAttenteComptable)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de confirmation comptable.");
            try { ConfirmeParComptable = SecuritySystem.CurrentUserName; } catch { }
            Statut = DeplacementStatut.Traitee;
        }
    }

    // ════════════════════════════════════════════════════════
    // LIGNE CIRCUIT (itinéraire)
    // ════════════════════════════════════════════════════════

    [XafDisplayName("Étape du circuit")]
    [DefaultProperty(nameof(DisplayEtape))]
    public class LigneCircuit : BaseObject
    {
        public LigneCircuit(Session session) : base(session) { }

        [Association("DemandeDeplacement-Circuit")]
        [Browsable(false)]
        public DemandeDeplacement Demande
        {
            get => demande;
            set => SetPropertyValue(nameof(Demande), ref demande, value);
        }
        DemandeDeplacement demande;

        [XafDisplayName("Ordre")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }
        int ordre;

        [RuleRequiredField]
        [Size(100)]
        [XafDisplayName("Ville de départ")]
        public string VilleDepart
        {
            get => villeDepart;
            set => SetPropertyValue(nameof(VilleDepart), ref villeDepart, value?.Trim());
        }
        string villeDepart;

        [RuleRequiredField]
        [Size(100)]
        [XafDisplayName("Ville d'arrivée")]
        public string VilleArrivee
        {
            get => villeArrivee;
            set => SetPropertyValue(nameof(VilleArrivee), ref villeArrivee, value?.Trim());
        }
        string villeArrivee;

        [XafDisplayName("Distance (km)")]
        [ModelDefault("DisplayFormat", "N0")]
        public int DistanceKm
        {
            get => distanceKm;
            set => SetPropertyValue(nameof(DistanceKm), ref distanceKm, value);
        }
        int distanceKm;

        [Size(200)]
        [XafDisplayName("Moyen de transport")]
        public string MoyenTransport
        {
            get => moyenTransport;
            set => SetPropertyValue(nameof(MoyenTransport), ref moyenTransport, value?.Trim());
        }
        string moyenTransport;

        [NonPersistent]
        public string DisplayEtape =>
            $"{Ordre}. {VilleDepart} → {VilleArrivee}" +
            (DistanceKm > 0 ? $" ({DistanceKm} km)" : "");
    }

    // ════════════════════════════════════════════════════════
    // LIGNE FRAIS DE MISSION
    // ════════════════════════════════════════════════════════

    [XafDisplayName("Ligne de frais")]
    [DefaultProperty(nameof(DisplayFrais))]

    [Appearance("Frais_Modifie", Criteria = "EstModifie = true",
        TargetItems = "Montant", FontColor = "DarkOrange",
        FontStyle = DXFontStyle.Bold)]
    public class LigneFraisMission : BaseObject
    {
        public LigneFraisMission(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            EstModifie = false;
        }

        [Association("DemandeDeplacement-Frais")]
        [Browsable(false)]
        public DemandeDeplacement Demande
        {
            get => demande;
            set => SetPropertyValue(nameof(Demande), ref demande, value);
        }
        DemandeDeplacement demande;

        // ── Catégorie ─────────────────────────────────────────
        [RuleRequiredField]
        [DataSourceCriteria("Actif = true")]
        [XafDisplayName("Catégorie")]
        [ImmediatePostData]
        public CategorieFraisMission Categorie
        {
            get => categorie;
            set
            {
                SetPropertyValue(nameof(Categorie), ref categorie, value);
                if (categorie != null && !IsLoading)
                    InitialiserDepuisCategorie();
            }
        }
        CategorieFraisMission categorie;

        // ── Paramètres de calcul ──────────────────────────────
        [XafDisplayName("Mode de calcul")]
        [ModelDefault("AllowEdit", "False")]
        public FraisCalculMode ModeCalcul
        {
            get => modeCalcul;
            set => SetPropertyValue(nameof(ModeCalcul), ref modeCalcul, value);
        }
        FraisCalculMode modeCalcul;

        [XafDisplayName("Quantité (jours / km / unités)")]
        [ModelDefault("DisplayFormat", "N1")]
        [ImmediatePostData]
        public decimal Quantite
        {
            get => quantite;
            set
            {
                SetPropertyValue(nameof(Quantite), ref quantite, value);
                if (!IsLoading) RecalculerMontant();
            }
        }
        decimal quantite;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Taux unitaire (FCFA)")]
        [ImmediatePostData]
        public decimal TauxUnitaire
        {
            get => tauxUnitaire;
            set
            {
                SetPropertyValue(nameof(TauxUnitaire), ref tauxUnitaire, value);
                if (!IsLoading)
                {
                    EstModifie = true;
                    RecalculerMontant();
                }
            }
        }
        decimal tauxUnitaire;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        [XafDisplayName("Montant (FCFA)")]
        public decimal Montant
        {
            get => montant;
            set => SetPropertyValue(nameof(Montant), ref montant, value);
        }
        decimal montant;

        [Size(300)]
        [XafDisplayName("Observation")]
        public string Observation
        {
            get => observation;
            set => SetPropertyValue(nameof(Observation), ref observation, value?.Trim());
        }
        string observation;

        /// <summary>Vrai si le taux a été modifié manuellement vs le taux de référence.</summary>
        [XafDisplayName("Taux modifié")]
        [ModelDefault("AllowEdit", "False")]
        public bool EstModifie
        {
            get => estModifie;
            set => SetPropertyValue(nameof(EstModifie), ref estModifie, value);
        }
        bool estModifie;

        [NonPersistent]
        public string DisplayFrais =>
            $"{Categorie?.Libelle} : {Montant:N0} FCFA";

        // ── Helpers ───────────────────────────────────────────

        private void InitialiserDepuisCategorie()
        {
            ModeCalcul = Categorie.ModeCalcul;
            TauxUnitaire = Categorie.TauxDefaut;
            EstModifie = false;

            // Quantité par défaut selon le mode
            if (ModeCalcul == FraisCalculMode.TauxJournalier)
                Quantite = Demande?.NombreJours ?? 1;
            else if (ModeCalcul == FraisCalculMode.Forfait)
                Quantite = 1;
            else
                Quantite = 0; // kilométrique — saisie manuelle

            RecalculerMontant();
        }

        public void RecalculerMontant()
        {
            Montant = Math.Round(Quantite * TauxUnitaire, 0);
            Demande?.RecalculerTotal();
        }
    }
}
