using AdiPAIE_V02.Module.BusinessObjects.RH;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp;
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
    /// Session de formation — une occurrence concrète (dates, lieu, formateur).
    /// Rattachée à un PlanFormation et un DomaineFormation.
    ///
    /// Cycle : Planifiée → Confirmée → En cours → Terminée
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Session de formation")]
    [DefaultProperty(nameof(Intitule))]
    [ImageName("BO_Event")]
    [NavigationItem("GRH - Formation")]
    [Appearance("SessionConfirmee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Confirmee#",
        FontColor = "#1B6C2A")]
    [Appearance("SessionTerminee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Terminee#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [Appearance("SessionAnnulee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+SessionFormationStatut,Annulee#",
        FontColor = "Red", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    public class SessionFormation : BaseObject
    {
        public SessionFormation(Session session) : base(session) { }

        // ── Rattachement plan ─────────────────────────────────
        [Association("PlanFormation-Sessions")]
        [XafDisplayName("Plan de formation")]
        public PlanFormation Plan
        {
            get => plan;
            set => SetPropertyValue(nameof(Plan), ref plan, value);
        }
        PlanFormation plan;

        // ── Domaine ───────────────────────────────────────────
        [Association("DomaineFormation-Sessions")]
        [RuleRequiredField]
        [XafDisplayName("Domaine")]
        public DomaineFormation Domaine
        {
            get => domaine;
            set => SetPropertyValue(nameof(Domaine), ref domaine, value);
        }
        DomaineFormation domaine;

        // ── Identification ────────────────────────────────────
        string intitule;
        [RuleRequiredField]
        [Size(200)]
        [XafDisplayName("Intitulé de la formation")]
        public string Intitule
        {
            get => intitule;
            set => SetPropertyValue(nameof(Intitule), ref intitule, value?.Trim());
        }

        string reference;
        [Size(50)]
        [XafDisplayName("Référence")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value?.Trim().ToUpper());
        }

        FormationModalite modalite;
        [XafDisplayName("Modalité")]
        public FormationModalite Modalite
        {
            get => modalite;
            set => SetPropertyValue(nameof(Modalite), ref modalite, value);
        }

        // ── Planification ────────────────────────────────────
        DateTime dateDebut;
        [RuleRequiredField]
        [XafDisplayName("Date de début")]
        [ImmediatePostData]
        public DateTime DateDebut
        {
            get => dateDebut;
            set
            {
                SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
                if (!IsLoading) RecalculerDuree();
            }
        }

        DateTime dateFin;
        [RuleRequiredField]
        [XafDisplayName("Date de fin")]
        [ImmediatePostData]
        public DateTime DateFin
        {
            get => dateFin;
            set
            {
                SetPropertyValue(nameof(DateFin), ref dateFin, value);
                if (!IsLoading) RecalculerDuree();
            }
        }

        int dureeJours;
        [XafDisplayName("Durée (jours)")]
        [ModelDefault("AllowEdit", "False")]
        public int DureeJours
        {
            get => dureeJours;
            set => SetPropertyValue(nameof(DureeJours), ref dureeJours, value);
        }

        int dureeHeures;
        [XafDisplayName("Durée (heures)")]
        public int DureeHeures
        {
            get => dureeHeures;
            set => SetPropertyValue(nameof(DureeHeures), ref dureeHeures, value);
        }

        string lieu;
        [Size(200)]
        [XafDisplayName("Lieu")]
        public string Lieu
        {
            get => lieu;
            set => SetPropertyValue(nameof(Lieu), ref lieu, value?.Trim());
        }

        // ── Formateur / Organisme ─────────────────────────────
        string formateurNom;
        [Size(150)]
        [XafDisplayName("Formateur / Organisme")]
        public string FormateurNom
        {
            get => formateurNom;
            set => SetPropertyValue(nameof(FormateurNom), ref formateurNom, value?.Trim());
        }

        bool formateurInterne;
        [XafDisplayName("Formateur interne")]
        public bool FormateurInterne
        {
            get => formateurInterne;
            set => SetPropertyValue(nameof(FormateurInterne), ref formateurInterne, value);
        }

        // ── Budget & coût ────────────────────────────────────
        decimal coutPrevisionnel;
        [XafDisplayName("Coût prévisionnel (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal CoutPrevisionnel
        {
            get => coutPrevisionnel;
            set => SetPropertyValue(nameof(CoutPrevisionnel), ref coutPrevisionnel, value);
        }

        decimal coutReel;
        [XafDisplayName("Coût réel (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal CoutReel
        {
            get => coutReel;
            set => SetPropertyValue(nameof(CoutReel), ref coutReel, value);
        }

        int capaciteMax;
        [XafDisplayName("Capacité max")]
        public int CapaciteMax
        {
            get => capaciteMax;
            set => SetPropertyValue(nameof(CapaciteMax), ref capaciteMax, value);
        }

        // ── Statut ───────────────────────────────────────────
        SessionFormationStatut statut;
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public SessionFormationStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        string motifAnnulation;
        [Size(500)]
        [XafDisplayName("Motif d'annulation")]
        public string MotifAnnulation
        {
            get => motifAnnulation;
            set => SetPropertyValue(nameof(MotifAnnulation), ref motifAnnulation, value?.Trim());
        }

        // ── Objectifs & contenu ───────────────────────────────
        string objectifs;
        [Size(2000)]
        [XafDisplayName("Objectifs pédagogiques")]
        public string Objectifs
        {
            get => objectifs;
            set => SetPropertyValue(nameof(Objectifs), ref objectifs, value?.Trim());
        }

        string programme;
        [Size(4000)]
        [XafDisplayName("Programme / Contenu")]
        public string Programme
        {
            get => programme;
            set => SetPropertyValue(nameof(Programme), ref programme, value?.Trim());
        }

        // ── KPIs calculés ────────────────────────────────────
        [NonPersistent]
        [XafDisplayName("Nb inscrits")]
        public int NbInscrits => Inscriptions.Count(i =>
            i.Statut != InscriptionStatut.Annulee);

        [NonPersistent]
        [XafDisplayName("Nb présents")]
        public int NbPresents => Inscriptions.Count(i =>
            i.Statut == InscriptionStatut.Confirmee && i.Presence);

        [NonPersistent]
        [XafDisplayName("Taux de présence (%)")]
        [ModelDefault("DisplayFormat", "N1")]
        public decimal TauxPresence =>
            NbInscrits > 0 ? Math.Round((decimal)NbPresents / NbInscrits * 100, 1) : 0;

        [NonPersistent]
        [XafDisplayName("Places disponibles")]
        public int PlacesDisponibles =>
            CapaciteMax > 0 ? Math.Max(0, CapaciteMax - NbInscrits) : 9999;

        // ── Collections ──────────────────────────────────────
        [Association("SessionFormation-Inscriptions"), Aggregated]
        [XafDisplayName("Inscriptions")]
        public XPCollection<InscriptionFormation> Inscriptions
            => GetCollection<InscriptionFormation>(nameof(Inscriptions));

        // ── Transitions ──────────────────────────────────────
        public void Confirmer()
        {
            if (Statut != SessionFormationStatut.Planifiee)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "La session doit être planifiée pour être confirmée.");
            Statut = SessionFormationStatut.Confirmee;
        }

        public void Demarrer()
        {
            if (Statut != SessionFormationStatut.Confirmee)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "La session doit être confirmée pour démarrer.");
            Statut = SessionFormationStatut.EnCours;
        }

        public void Terminer()
        {
            if (Statut != SessionFormationStatut.EnCours)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "La session doit être en cours pour être clôturée.");
            Statut = SessionFormationStatut.Terminee;
        }

        public void Annuler(string motif)
        {
            if (Statut == SessionFormationStatut.Terminee)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "Une session terminée ne peut pas être annulée.");
            Statut = SessionFormationStatut.Annulee;
            MotifAnnulation = motif;
        }

        // ── Helpers ───────────────────────────────────────────
        public void RecalculerDuree()
        {
            if (DateFin >= DateDebut)
                DureeJours = Math.Max(1, (DateFin.Date - DateDebut.Date).Days + 1);
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateDebut = DateTime.Today;
            DateFin = DateTime.Today;
            DureeJours = 1;
            CapaciteMax = 20;
            Statut = SessionFormationStatut.Planifiee;
            Modalite = FormationModalite.Presentiel;
        }
    }
}
