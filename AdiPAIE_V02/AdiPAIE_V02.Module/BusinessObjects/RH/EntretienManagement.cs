using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Partie III — Aptitudes au management.
    /// Uniquement pour les collaborateurs en situation d'encadrement.
    /// 7 aptitudes standard + appréciation libre.
    /// </summary>
    [XafDisplayName("Aptitude au management")]
    [DefaultProperty(nameof(Aptitude))]

    [Appearance("Management_AAccquerir",
        Criteria = "Niveau = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NiveauMaitrise,AAccquerir#",
        TargetItems = "Niveau", FontColor = "Red")]
    [Appearance("Management_Excellente",
        Criteria = "Niveau = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NiveauMaitrise,ExcellenteMaitrise#",
        TargetItems = "Niveau", FontColor = "Green")]
    public class EntretienManagement : BaseObject
    {
        public EntretienManagement(Session session) : base(session) { }

        // ── Entretien parent ──────────────────────────────────
        [Association("EntretienAnnuel-Aptitudes")]
        [Browsable(false)]
        public EntretienAnnuel Entretien
        {
            get => entretien;
            set => SetPropertyValue(nameof(Entretien), ref entretien, value);
        }
        EntretienAnnuel entretien;

        // ── Aptitude ──────────────────────────────────────────
        string aptitude;
        [Size(200)]
        [XafDisplayName("Aptitude")]
        [ModelDefault("AllowEdit", "False")]
        public string Aptitude
        {
            get => aptitude;
            set => SetPropertyValue(nameof(Aptitude), ref aptitude, value?.Trim());
        }

        int ordre;
        public int Ordre
        {
            get => ordre;
            set => SetPropertyValue(nameof(Ordre), ref ordre, value);
        }

        // ── Évaluation ────────────────────────────────────────
        NiveauMaitrise? niveau;
        [XafDisplayName("Niveau")]
        public NiveauMaitrise? Niveau
        {
            get => niveau;
            set => SetPropertyValue(nameof(Niveau), ref niveau, value);
        }

        string appreciation;
        [Size(500)]
        [XafDisplayName("Appréciation")]
        public string Appreciation
        {
            get => appreciation;
            set => SetPropertyValue(nameof(Appreciation), ref appreciation, value?.Trim());
        }
    }

    /// <summary>
    /// Initialise les 7 aptitudes standard de la Partie III.
    /// Appelé par EntretienWorkflowController quand EstEnSituationEncadrement = true.
    /// </summary>
    public static class EntretienManagementInitializer
    {
        public static readonly string[] AptitudesStandard = new[]
        {
            "Capacité à déléguer",
            "Capacité à mobiliser et valoriser les compétences",
            "Capacité d'organisation et de pilotage",
            "Attention portée au développement professionnel des collaborateurs",
            "Aptitude à prévenir, arbitrer et gérer les conflits",
            "Aptitude à la prise de décision",
            "Capacité à fixer des objectifs cohérents",
        };

        public static void Initialiser(EntretienAnnuel entretien, DevExpress.Xpo.Session session)
        {
            if (entretien.Aptitudes.Count > 0) return; // idempotent

            for (int i = 0; i < AptitudesStandard.Length; i++)
            {
                var apt = new EntretienManagement(session)
                {
                    Entretien = entretien,
                    Aptitude = AptitudesStandard[i],
                    Ordre = i + 1,
                    Niveau = NiveauMaitrise.ADevelopper
                };
            }
        }
    }
}
