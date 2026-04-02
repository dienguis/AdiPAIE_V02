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
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Demande de recrutement d'un intérimaire.
    ///
    /// Workflow : AC (Attaché Commercial) → N+1 → RH → RFE
    ///
    /// Exigence ELTON :
    ///   - Aucun intérimaire ne peut apparaître dans les effectifs
    ///     sans validation RH (statut InterimaireAffecte).
    ///   - Aucun mouvement n'est effectif sans validation N+1 puis RH.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Demande de recrutement intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Resume")]
    //[NavigationItem("GRH - Intérimaires")]
    [Appearance("Demande_Acceptee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeInterimaireStatut,InterimaireAffecte#",
        FontColor = "Green")]
    [Appearance("Demande_Refusee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeInterimaireStatut,Refusee# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeInterimaireStatut,Annulee#",
        FontColor = "Red", FontStyle = DevExpress.Drawing.DXFontStyle.Strikeout)]
    [Appearance("Demande_EnAttente", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeInterimaireStatut,EnAttenteN1# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeInterimaireStatut,EnAttenteRH# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeInterimaireStatut,EnAttenteRFE#",
        FontColor = "DarkOrange")]
    public class DemandeRecrutementInterim : BaseObject
    {
        public DemandeRecrutementInterim(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = DemandeInterimaireStatut.Brouillon;
            DateDemande = DateTime.Today;
            DateDebut = DateTime.Today.AddDays(7);
            DateFin = DateTime.Today.AddMonths(2);
            try { SaisiPar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            Reference = $"DRI-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
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

        // ── Demandeur (AC) ────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Site / Station demandeur")]
        public StationService Site
        {
            get => site;
            set => SetPropertyValue(nameof(Site), ref site, value);
        }
        StationService site;

        [XafDisplayName("Business Unit (BU)")]
        [DataSourceCriteria("Actif = true AND Station.Oid = '@This.Site.Oid'")]
        public BusinessUnitStation BU
        {
            get => bu;
            set => SetPropertyValue(nameof(BU), ref bu, value);
        }
        BusinessUnitStation bu;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Saisi par")]
        public string SaisiPar
        {
            get => saisiPar;
            set => SetPropertyValue(nameof(SaisiPar), ref saisiPar, value);
        }
        string saisiPar;

        [XafDisplayName("Date de la demande")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateDemande
        {
            get => dateDemande;
            set => SetPropertyValue(nameof(DateDemande), ref dateDemande, value);
        }
        DateTime dateDemande;

        // ── Besoin ────────────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Poste souhaité")]
        [DataSourceCriteria("Actif = true")]
        public PosteInterimaire Poste
        {
            get => poste;
            set => SetPropertyValue(nameof(Poste), ref poste, value);
        }
        PosteInterimaire poste;

        [RuleRequiredField]
        [Size(300)]
        [XafDisplayName("Motif du recours")]
        public string MotifRecours
        {
            get => motifRecours;
            set => SetPropertyValue(nameof(MotifRecours), ref motifRecours, value?.Trim());
        }
        string motifRecours;

        [RuleRequiredField]
        [XafDisplayName("Date de début souhaitée")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }
        DateTime dateDebut;

        [RuleRequiredField]
        [XafDisplayName("Date de fin souhaitée")]
        public DateTime DateFin
        {
            get => dateFin;
            set => SetPropertyValue(nameof(DateFin), ref dateFin, value);
        }
        DateTime dateFin;

        [XafDisplayName("Nombre de postes")]
        [RuleRange("DRI_NbPostes", DefaultContexts.Save, 1, 100)]
        public int NombrePostes
        {
            get => nombrePostes;
            set => SetPropertyValue(nameof(NombrePostes), ref nombrePostes, value);
        }
        int nombrePostes = 1;

        [Size(500)]
        [XafDisplayName("Compétences requises")]
        public string CompetencesRequises
        {
            get => competencesRequises;
            set => SetPropertyValue(nameof(CompetencesRequises), ref competencesRequises, value);
        }
        string competencesRequises;

        // ── Workflow ──────────────────────────────────────────────────
        [XafDisplayName("Statut")]
        public DemandeInterimaireStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        DemandeInterimaireStatut statut;

        [XafDisplayName("N+1 valideur")]
        [DataSourceCriteria("IsActif = true")]
        public Salarie ValideurN1
        {
            get => valideurN1;
            set => SetPropertyValue(nameof(ValideurN1), ref valideurN1, value);
        }
        Salarie valideurN1;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé N+1 par")]
        public string ValideN1Par
        {
            get => valideN1Par;
            set => SetPropertyValue(nameof(ValideN1Par), ref valideN1Par, value);
        }
        string valideN1Par;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation N+1")]
        public DateTime? DateValidationN1
        {
            get => dateValidationN1;
            set => SetPropertyValue(nameof(DateValidationN1), ref dateValidationN1, value);
        }
        DateTime? dateValidationN1;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé RH par")]
        public string ValideRHPar
        {
            get => valideRHPar;
            set => SetPropertyValue(nameof(ValideRHPar), ref valideRHPar, value);
        }
        string valideRHPar;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation RH")]
        public DateTime? DateValidationRH
        {
            get => dateValidationRH;
            set => SetPropertyValue(nameof(DateValidationRH), ref dateValidationRH, value);
        }
        DateTime? dateValidationRH;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Transmis RFE par")]
        public string TransmisRFEPar
        {
            get => transmisRFEPar;
            set => SetPropertyValue(nameof(TransmisRFEPar), ref transmisRFEPar, value);
        }
        string transmisRFEPar;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date transmission RFE")]
        public DateTime? DateTransmissionRFE
        {
            get => dateTransmissionRFE;
            set => SetPropertyValue(nameof(DateTransmissionRFE), ref dateTransmissionRFE, value);
        }
        DateTime? dateTransmissionRFE;

        [Size(500)]
        [XafDisplayName("Motif de refus")]
        public string MotifRefus
        {
            get => motifRefus;
            set => SetPropertyValue(nameof(MotifRefus), ref motifRefus, value?.Trim());
        }
        string motifRefus;

        // ── Intérimaire affecté (après validation) ────────────────────
        [XafDisplayName("Intérimaire affecté")]
        public Interimaire InterimaireAffecte
        {
            get => interimaireAffecte;
            set => SetPropertyValue(nameof(InterimaireAffecte), ref interimaireAffecte, value);
        }
        Interimaire interimaireAffecte;

        [XafDisplayName("Contrat créé")]
        public ContratInterim ContratCree
        {
            get => contratCree;
            set => SetPropertyValue(nameof(ContratCree), ref contratCree, value);
        }
        ContratInterim contratCree;

        // ── Propriétés calculées ──────────────────────────────────────
        [NonPersistent]
        public string DisplayName =>
            $"{Reference} — {Poste?.Libelle ?? "—"} — {Site?.Nom ?? BU?.Libelle ?? "—"} ({DateDebut:MM/yyyy})";

        [NonPersistent]
        [XafDisplayName("Durée (jours)")]
        public int DureeJours => Math.Max(0, (DateFin - DateDebut).Days);

        // ── Méthodes workflow ─────────────────────────────────────────
        public void SoumettreN1()
        {
            if (Statut != DemandeInterimaireStatut.Brouillon)
                throw new UserFriendlyException("La demande doit être au statut Brouillon.");
            Statut = DemandeInterimaireStatut.EnAttenteN1;
        }

        public void ValiderN1()
        {
            if (Statut != DemandeInterimaireStatut.EnAttenteN1)
                throw new UserFriendlyException("La demande n'est pas en attente de validation N+1.");
            DateValidationN1 = DateTime.Now;
            try { ValideN1Par = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            Statut = DemandeInterimaireStatut.EnAttenteRH;
        }

        public void RejeterN1(string motif)
        {
            if (Statut != DemandeInterimaireStatut.EnAttenteN1)
                throw new UserFriendlyException("La demande n'est pas en attente de validation N+1.");
            MotifRefus = motif;
            Statut = DemandeInterimaireStatut.Brouillon;
        }

        public void ValiderRH()
        {
            if (Statut != DemandeInterimaireStatut.EnAttenteRH)
                throw new UserFriendlyException("La demande n'est pas en attente de validation RH.");
            DateValidationRH = DateTime.Now;
            try { ValideRHPar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            Statut = DemandeInterimaireStatut.Acceptee;
        }

        public void RejeterRH(string motif)
        {
            if (Statut != DemandeInterimaireStatut.EnAttenteRH)
                throw new UserFriendlyException("La demande n'est pas en attente de validation RH.");
            MotifRefus = motif;
            Statut = DemandeInterimaireStatut.Refusee;
        }

        public void TransmettreRFE()
        {
            if (Statut != DemandeInterimaireStatut.Acceptee)
                throw new UserFriendlyException("La demande doit être acceptée par le RH avant transmission RFE.");
            DateTransmissionRFE = DateTime.Now;
            try { TransmisRFEPar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            Statut = DemandeInterimaireStatut.EnAttenteRFE;
        }

        public void AffecterInterimaire(Interimaire interimaire, ContratInterim contrat = null)
        {
            if (Statut != DemandeInterimaireStatut.EnAttenteRFE
             && Statut != DemandeInterimaireStatut.Acceptee
             && Statut != DemandeInterimaireStatut.EnCoursAttribution)
                throw new UserFriendlyException(
                    "La demande doit être validée RH avant d'affecter un intérimaire.");
            InterimaireAffecte = interimaire;
            ContratCree = contrat;
            Statut = DemandeInterimaireStatut.InterimaireAffecte;
        }

        public override string ToString() => DisplayName;
    }
}
