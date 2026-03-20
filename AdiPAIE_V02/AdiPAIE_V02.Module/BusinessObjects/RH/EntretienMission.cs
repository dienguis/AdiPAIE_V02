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
    /// Partie I-A de la fiche d'évaluation : missions et responsabilités principales.
    /// Jusqu'à 5 missions par entretien, chacune avec une note et un commentaire.
    /// </summary>
    [XafDisplayName("Mission / Responsabilité")]
    [DefaultProperty(nameof(IntituleMission))]

    [Appearance("Mission_Superieur",
        Criteria = "Note = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NoteMission,Superieur#",
        TargetItems = "Note", FontColor = "Green")]
    [Appearance("Mission_Insuffisant",
        Criteria = "Note = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NoteMission,Insuffisant#",
        TargetItems = "Note", FontColor = "Red")]
    public class EntretienMission : BaseObject
    {
        public EntretienMission(Session session) : base(session) { }

        // ── Entretien parent ──────────────────────────────────
        [Association("EntretienAnnuel-Missions")]
        [Browsable(false)]
        public EntretienAnnuel Entretien
        {
            get => entretien;
            set => SetPropertyValue(nameof(Entretien), ref entretien, value);
        }
        EntretienAnnuel entretien;

        // ── Contenu ───────────────────────────────────────────
        int numero;
        [XafDisplayName("N°")]
        [ModelDefault("DisplayFormat", "N0")]
        public int Numero
        {
            get => numero;
            set => SetPropertyValue(nameof(Numero), ref numero, value);
        }

        string intituleMission;
        [RuleRequiredField]
        [Size(300)]
        [XafDisplayName("Intitulé de la mission")]
        public string IntituleMission
        {
            get => intituleMission;
            set => SetPropertyValue(nameof(IntituleMission), ref intituleMission, value?.Trim());
        }

        // ── Notes ─────────────────────────────────────────────
        NoteMission? noteManager;
        [XafDisplayName("Note manager")]
        public NoteMission? NoteManager
        {
            get => noteManager;
            set => SetPropertyValue(nameof(NoteManager), ref noteManager, value);
        }

        NoteMission? noteAutoEval;
        [XafDisplayName("Note auto-évaluation")]
        public NoteMission? NoteAutoEval
        {
            get => noteAutoEval;
            set => SetPropertyValue(nameof(NoteAutoEval), ref noteAutoEval, value);
        }

        // ── Commentaires ──────────────────────────────────────
        string commentaireManager;
        [Size(2048)]
        [XafDisplayName("Commentaires manager")]
        public string CommentaireManager
        {
            get => commentaireManager;
            set => SetPropertyValue(nameof(CommentaireManager), ref commentaireManager, value?.Trim());
        }

        string commentaireSalarie;
        [Size(2048)]
        [XafDisplayName("Commentaires collaborateur")]
        public string CommentaireSalarie
        {
            get => commentaireSalarie;
            set => SetPropertyValue(nameof(CommentaireSalarie), ref commentaireSalarie, value?.Trim());
        }

        // ── Propriété raccourci pour affichage ────────────────
        /// <summary>Alias pour compatibilité avec NoteMission dans les Appearance.</summary>
        [NonPersistent]
        public NoteMission? Note => NoteManager;

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            NoteManager = NoteMission.SO;
            NoteAutoEval = NoteMission.SO;
        }
    }
}
