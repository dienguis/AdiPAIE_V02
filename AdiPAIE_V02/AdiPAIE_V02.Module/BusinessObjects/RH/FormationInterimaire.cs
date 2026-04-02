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
    // ════════════════════════════════════════════════════════════════════
    // FORMATION INTERIMAIRE
    // ════════════════════════════════════════════════════════════════════

    [DefaultClassOptions]
    [XafDisplayName("Formation intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Organization")]
    //[NavigationItem("GRH - Intérimaires")]
    public class FormationInterimaire : BaseObject
    {
        public FormationInterimaire(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateFormation = DateTime.Today;
            EstObligatoire = false;
        }

        [Association("Interimaire-Formations")]
        [RuleRequiredField]
        [VisibleInDetailView(true)]
        [VisibleInListView(true)]
        [XafDisplayName("Intérimaire")]
        public Interimaire Interimaire
        {
            get => inter;
            set => SetPropertyValue(nameof(Interimaire), ref inter, value);
        }
        Interimaire inter;

        [RuleRequiredField]
        [Size(200)]
        [XafDisplayName("Intitulé de la formation")]
        public string Intitule
        {
            get => intitule;
            set => SetPropertyValue(nameof(Intitule), ref intitule, value?.Trim());
        }
        string intitule;

        [XafDisplayName("Date")]
        [RuleRequiredField]
        public DateTime DateFormation
        {
            get => date;
            set => SetPropertyValue(nameof(DateFormation), ref date, value);
        }
        DateTime date;

        [ModelDefault("DisplayFormat", "N1")]
        [XafDisplayName("Durée (heures)")]
        public decimal DureeHeures
        {
            get => duree;
            set => SetPropertyValue(nameof(DureeHeures), ref duree, value);
        }
        decimal duree;

        [Size(200)]
        [XafDisplayName("Organisme / Formateur")]
        public string Organisme
        {
            get => organisme;
            set => SetPropertyValue(nameof(Organisme), ref organisme, value?.Trim());
        }
        string organisme;

        [XafDisplayName("Formation obligatoire")]
        public bool EstObligatoire
        {
            get => obligatoire;
            set => SetPropertyValue(nameof(EstObligatoire), ref obligatoire, value);
        }
        bool obligatoire;

        [XafDisplayName("Type")]
        public FormationCategorie TypeFormation
        {
            get => typeForm;
            set => SetPropertyValue(nameof(TypeFormation), ref typeForm, value);
        }
        FormationCategorie typeForm;

        [XafDisplayName("Réussite")]
        public bool Reussie
        {
            get => reussie;
            set => SetPropertyValue(nameof(Reussie), ref reussie, value);
        }
        bool reussie;

        [Size(500)]
        [XafDisplayName("Observations")]
        public string Observations
        {
            get => obs;
            set => SetPropertyValue(nameof(Observations), ref obs, value);
        }
        string obs;

        [NonPersistent]
        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        public string DisplayName =>
            $"{Interimaire?.FullName ?? "—"} — {Intitule} ({DateFormation:dd/MM/yyyy})";

        public override string ToString() => DisplayName;
    }

    // ════════════════════════════════════════════════════════════════════
    // ÉVALUATION INTÉRIMAIRE
    // ════════════════════════════════════════════════════════════════════

    [DefaultClassOptions]
    [XafDisplayName("Évaluation intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    //[NavigationItem("GRH - Interimaires")]
    public class EvaluationInterimaire : BaseObject
    {
        public EvaluationInterimaire(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateEvaluation = DateTime.Today;
            try { EvaluePar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
        }

        [Association("Interimaire-Evaluations")]
        [RuleRequiredField]
        [VisibleInDetailView(true)]
        [VisibleInListView(true)]
        [XafDisplayName("Intérimaire")]
        public Interimaire Interimaire
        {
            get => inter;
            set => SetPropertyValue(nameof(Interimaire), ref inter, value);
        }
        Interimaire inter;

        [XafDisplayName("Date")]
        [RuleRequiredField]
        public DateTime DateEvaluation
        {
            get => date;
            set => SetPropertyValue(nameof(DateEvaluation), ref date, value);
        }
        DateTime date;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Évalué par")]
        public string EvaluePar
        {
            get => evaluePar;
            set => SetPropertyValue(nameof(EvaluePar), ref evaluePar, value);
        }
        string evaluePar;

        // ── Critères d'évaluation ─────────────────────────────────────
        [XafDisplayName("Ponctualité")]
        public NoteEvaluation Ponctualite
        {
            get => ponct;
            set => SetPropertyValue(nameof(Ponctualite), ref ponct, value);
        }
        NoteEvaluation ponct;

        [XafDisplayName("Savoir-faire technique")]
        public NoteEvaluation SavoirFaire
        {
            get => savoirFaire;
            set => SetPropertyValue(nameof(SavoirFaire), ref savoirFaire, value);
        }
        NoteEvaluation savoirFaire;

        [XafDisplayName("Comportement")]
        public NoteEvaluation Comportement
        {
            get => comportement;
            set => SetPropertyValue(nameof(Comportement), ref comportement, value);
        }
        NoteEvaluation comportement;

        [XafDisplayName("Respect des consignes")]
        public NoteEvaluation RespectConsignes
        {
            get => respectConsignes;
            set => SetPropertyValue(nameof(RespectConsignes), ref respectConsignes, value);
        }
        NoteEvaluation respectConsignes;

        [XafDisplayName("Productivité")]
        public NoteEvaluation Productivite
        {
            get => productivite;
            set => SetPropertyValue(nameof(Productivite), ref productivite, value);
        }
        NoteEvaluation productivite;

        [NonPersistent]
        [ModelDefault("DisplayFormat", "N1")]
        [XafDisplayName("Note globale / 5")]
        public decimal NoteGlobale =>
            ((int)Ponctualite + (int)SavoirFaire + (int)Comportement
             + (int)RespectConsignes + (int)Productivite) / 5m;

        [XafDisplayName("Recommandé pour renouvellement")]
        public bool Recommande
        {
            get => recommande;
            set => SetPropertyValue(nameof(Recommande), ref recommande, value);
        }
        bool recommande;

        [Size(1000)]
        [XafDisplayName("Commentaires")]
        public string Commentaires
        {
            get => commentaires;
            set => SetPropertyValue(nameof(Commentaires), ref commentaires, value);
        }
        string commentaires;

        [XafDisplayName("Contrat évalué")]
        public ContratInterim Contrat
        {
            get => contrat;
            set => SetPropertyValue(nameof(Contrat), ref contrat, value);
        }
        ContratInterim contrat;

        [NonPersistent]
        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        public string DisplayName =>
            $"{Interimaire?.FullName ?? "—"} — Éval. {DateEvaluation:dd/MM/yyyy} ({NoteGlobale:N1}/5)";

        public override string ToString() => DisplayName;
    }
}
