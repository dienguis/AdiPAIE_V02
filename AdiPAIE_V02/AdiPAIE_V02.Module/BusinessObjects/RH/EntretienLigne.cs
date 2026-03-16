using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Ligne de la grille d'évaluation : un critère évalué dans un entretien.
    /// La note est saisie par le manager (et en option par le salarié en auto-évaluation).
    /// </summary>
    [XafDisplayName("Ligne d'évaluation")]
    [DefaultProperty(nameof(LibelleCritere))]

    // Mise en évidence si note insuffisante
    [Appearance("Ligne_Insuffisant",
        Criteria = "Note = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NoteEvaluation,Insuffisant#",
        TargetItems = "Note", FontColor = "Red")]
    [Appearance("Ligne_TresBien",
        Criteria = "Note = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NoteEvaluation,TresBien#",
        TargetItems = "Note", FontColor = "Green")]
    public class EntretienLigne : BaseObject
    {
        public EntretienLigne(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Note = NoteEvaluation.NonEvalue;
            NoteAutoEval = NoteEvaluation.NonEvalue;
        }

        // ── Entretien parent ──────────────────────────────────────
        [Association("EntretienAnnuel-Lignes")]
        [Browsable(false)]
        public EntretienAnnuel Entretien
        {
            get => entretien;
            set => SetPropertyValue(nameof(Entretien), ref entretien, value);
        }
        EntretienAnnuel entretien;

        // ── Critère de référence ──────────────────────────────────
        CritereEvaluationRef critere;
        [RuleRequiredField]
        [DataSourceCriteria("Actif = true")]
        [XafDisplayName("Critère")]
        [ImmediatePostData]
        public CritereEvaluationRef Critere
        {
            get => critere;
            set
            {
                if (SetPropertyValue(nameof(Critere), ref critere, value) && critere != null)
                {
                    // Copie la pondération par défaut depuis le référentiel
                    Poids = critere.Poids;
                    LibelleCritere = critere.Libelle;
                    TypeCritere = critere.TypeCritere;
                }
            }
        }

        // ── Copie dénormalisée (résiste à la modification du référentiel) ──
        string libelleCritere;
        [Size(150)]
        [XafDisplayName("Libellé du critère")]
        public string LibelleCritere
        {
            get => libelleCritere;
            set => SetPropertyValue(nameof(LibelleCritere), ref libelleCritere, value);
        }

        TypeCritere typeCritere;
        [XafDisplayName("Type")]
        [ModelDefault("AllowEdit", "False")]
        public TypeCritere TypeCritere
        {
            get => typeCritere;
            set => SetPropertyValue(nameof(TypeCritere), ref typeCritere, value);
        }

        decimal poids;
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "N1")]
        [XafDisplayName("Pondération")]
        public decimal Poids
        {
            get => poids;
            set => SetPropertyValue(nameof(Poids), ref poids, value);
        }

        int ordre;
        [XafDisplayName("Ordre")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }

        // ── Notes ─────────────────────────────────────────────────

        /// <summary>Note auto-évaluation saisie par le salarié (optionnel)</summary>
        NoteEvaluation noteAutoEval;
        [XafDisplayName("Auto-évaluation (salarié)")]
        public NoteEvaluation NoteAutoEval
        {
            get => noteAutoEval;
            set => SetPropertyValue(nameof(NoteAutoEval), ref noteAutoEval, value);
        }

        /// <summary>Note définitive saisie par le manager</summary>
        NoteEvaluation note;
        [XafDisplayName("Note (manager)")]
        public NoteEvaluation Note
        {
            get => note;
            set => SetPropertyValue(nameof(Note), ref note, value);
        }

        // ── Commentaires ──────────────────────────────────────────
        string commentaireSalarie;
        [Size(1024)]
        [XafDisplayName("Commentaire salarié")]
        public string CommentaireSalarie
        {
            get => commentaireSalarie;
            set => SetPropertyValue(nameof(CommentaireSalarie), ref commentaireSalarie, value?.Trim());
        }

        string commentaireManager;
        [Size(1024)]
        [XafDisplayName("Commentaire manager")]
        public string CommentaireManager
        {
            get => commentaireManager;
            set => SetPropertyValue(nameof(CommentaireManager), ref commentaireManager, value?.Trim());
        }

        // ── Score pondéré de cette ligne ─────────────────────────
        [NonPersistent]
        [XafDisplayName("Score pondéré")]
        [ModelDefault("DisplayFormat", "N2")]
        public decimal ScorePondere => (decimal)Note * Poids;
    }
}
