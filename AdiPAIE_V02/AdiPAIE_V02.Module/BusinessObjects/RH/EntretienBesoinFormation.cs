using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Partie II — Besoins en formation identifiés lors de l'entretien.
    /// Jusqu'à 3 formations par entretien avec points d'amélioration et mesures.
    /// </summary>
    [XafDisplayName("Besoin en formation")]
    [DefaultProperty(nameof(IntituleFormation))]
    public class EntretienBesoinFormation : BaseObject
    {
        public EntretienBesoinFormation(Session session) : base(session) { }

        // ── Entretien parent ──────────────────────────────────
        [Association("EntretienAnnuel-BesoinsFormation")]
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
        public int Numero
        {
            get => numero;
            set => SetPropertyValue(nameof(Numero), ref numero, value);
        }

        string intituleFormation;
        [RuleRequiredField]
        [Size(200)]
        [XafDisplayName("Intitulé de la formation")]
        public string IntituleFormation
        {
            get => intituleFormation;
            set => SetPropertyValue(nameof(IntituleFormation), ref intituleFormation, value?.Trim());
        }

        string pointsAmelioration;
        [Size(1000)]
        [XafDisplayName("Points d'amélioration visés")]
        public string PointsAmelioration
        {
            get => pointsAmelioration;
            set => SetPropertyValue(nameof(PointsAmelioration), ref pointsAmelioration, value?.Trim());
        }

        string mesuresMoyens;
        [Size(1000)]
        [XafDisplayName("Mesures et moyens à mettre en œuvre")]
        public string MesuresMoyens
        {
            get => mesuresMoyens;
            set => SetPropertyValue(nameof(MesuresMoyens), ref mesuresMoyens, value?.Trim());
        }

        int anneePrevisionnelle;
        [XafDisplayName("Année prévisionnelle")]
        public int AnneePrevisionnelle
        {
            get => anneePrevisionnelle;
            set => SetPropertyValue(nameof(AnneePrevisionnelle), ref anneePrevisionnelle, value);
        }

        bool prioritaire;
        [XafDisplayName("Prioritaire")]
        public bool Prioritaire
        {
            get => prioritaire;
            set => SetPropertyValue(nameof(Prioritaire), ref prioritaire, value);
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            AnneePrevisionnelle = System.DateTime.Today.Year + 1;
        }
    }
}
