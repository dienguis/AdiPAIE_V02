using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
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
    /// Objectif annuel : peut être un bilan de l'objectif N (avec statut d'atteinte)
    /// ou un objectif fixé pour l'année N+1.
    /// </summary>
    [XafDisplayName("Objectif annuel")]
    [DefaultProperty(nameof(Libelle))]

    [Appearance("Objectif_NonAtteint",
        Criteria = "StatutAtteinte = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ObjectifStatut,NonAtteint#",
        TargetItems = "StatutAtteinte", FontColor = "Red")]
    [Appearance("Objectif_Depasse",
        Criteria = "StatutAtteinte = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+ObjectifStatut,Depasse#",
        TargetItems = "StatutAtteinte", FontColor = "Green")]
    public class ObjectifAnnuel : BaseObject
    {
        public ObjectifAnnuel(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            EstObjectifNplus1 = false;
            StatutAtteinte = ObjectifStatut.NonAtteint;
        }

        // ── Entretien parent ──────────────────────────────────────
        [Association("EntretienAnnuel-Objectifs")]
        [Browsable(false)]
        public EntretienAnnuel Entretien
        {
            get => entretien;
            set => SetPropertyValue(nameof(Entretien), ref entretien, value);
        }
        EntretienAnnuel entretien;

        // ── Contenu ───────────────────────────────────────────────
        string libelle;
        [RuleRequiredField]
        [Size(300)]
        [XafDisplayName("Description de l'objectif")]
        public string Libelle
        {
            get => libelle;
            set => SetPropertyValue(nameof(Libelle), ref libelle, value?.Trim());
        }

        /// <summary>
        /// true = objectif à fixer pour N+1 | false = bilan objectif N
        /// </summary>
        bool estObjectifNplus1;
        [XafDisplayName("Objectif N+1 (à fixer)")]
        [ImmediatePostData]
        public bool EstObjectifNplus1
        {
            get => estObjectifNplus1;
            set => SetPropertyValue(nameof(EstObjectifNplus1), ref estObjectifNplus1, value);
        }

        int ordre;
        [XafDisplayName("Ordre")]
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }

        // ── Mesure / indicateur ───────────────────────────────────
        string indicateur;
        [Size(150)]
        [XafDisplayName("Indicateur de mesure")]
        public string Indicateur
        {
            get => indicateur;
            set => SetPropertyValue(nameof(Indicateur), ref indicateur, value?.Trim());
        }

        string cible;
        [Size(80)]
        [XafDisplayName("Cible / valeur attendue")]
        public string Cible
        {
            get => cible;
            set => SetPropertyValue(nameof(Cible), ref cible, value?.Trim());
        }

        string realise;
        [Size(80)]
        [XafDisplayName("Réalisé / valeur atteinte")]
        [Appearance("Realise_Hidden_If_N1",
            Criteria = "EstObjectifNplus1 = true",
            TargetItems = "Realise;StatutAtteinte;CommentaireBilan",
            Visibility = ViewItemVisibility.Hide)]
        public string Realise
        {
            get => realise;
            set => SetPropertyValue(nameof(Realise), ref realise, value?.Trim());
        }

        // ── Bilan (masqué pour les objectifs N+1) ─────────────────
        ObjectifStatut statutAtteinte;
        [XafDisplayName("Statut d'atteinte")]
        public ObjectifStatut StatutAtteinte
        {
            get => statutAtteinte;
            set => SetPropertyValue(nameof(StatutAtteinte), ref statutAtteinte, value);
        }

        string commentaireBilan;
        [Size(512)]
        [XafDisplayName("Commentaire bilan")]
        public string CommentaireBilan
        {
            get => commentaireBilan;
            set => SetPropertyValue(nameof(CommentaireBilan), ref commentaireBilan, value?.Trim());
        }

        string commentaireObjectif;
        [Size(512)]
        [XafDisplayName("Contexte / moyens associés")]
        public string CommentaireObjectif
        {
            get => commentaireObjectif;
            set => SetPropertyValue(nameof(CommentaireObjectif), ref commentaireObjectif, value?.Trim());
        }
    }
}
