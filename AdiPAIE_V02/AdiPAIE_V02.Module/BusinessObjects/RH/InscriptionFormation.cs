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
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Inscription d'un salarié à une session de formation.
    /// Gère la présence, l'évaluation à chaud et l'attestation.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Inscription formation")]
    [DefaultProperty(nameof(DisplayInscription))]
    [ImageName("Action_SendMessage")]
   // [NavigationItem("GRH - Formation")]
    [Appearance("InscriptionAnnulee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InscriptionStatut,Annulee#",
        FontColor = "Gray", FontStyle = DevExpress.Drawing.DXFontStyle.Italic)]
    [Appearance("InscriptionAbsente", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+InscriptionStatut,Absente#",
        FontColor = "OrangeRed")]
    public class InscriptionFormation : BaseObject
    {
        public InscriptionFormation(Session session) : base(session) { }

        // ── Session ───────────────────────────────────────────
        [Association("SessionFormation-Inscriptions")]
        [RuleRequiredField]
        [XafDisplayName("Session")]
        public SessionFormation SessionFormation
        {
            get => sessionFormation;
            set => SetPropertyValue(nameof(SessionFormation), ref sessionFormation, value);
        }
        SessionFormation sessionFormation;

        // ── Salarié ───────────────────────────────────────────
        [Association("Salarie-InscriptionsFormation")]
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Inscription ───────────────────────────────────────
        DateTime dateInscription;
        [XafDisplayName("Date d'inscription")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateInscription
        {
            get => dateInscription;
            set => SetPropertyValue(nameof(DateInscription), ref dateInscription, value);
        }

        string inscritPar;
        [Size(100)]
        [XafDisplayName("Inscrit par")]
        [ModelDefault("AllowEdit", "False")]
        public string InscritPar
        {
            get => inscritPar;
            set => SetPropertyValue(nameof(InscritPar), ref inscritPar, value);
        }

        InscriptionStatut statut;
        [XafDisplayName("Statut")]
        [ModelDefault("AllowEdit", "False")]
        public InscriptionStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        string motifAnnulation;
        [Size(300)]
        [XafDisplayName("Motif d'annulation")]
        public string MotifAnnulation
        {
            get => motifAnnulation;
            set => SetPropertyValue(nameof(MotifAnnulation), ref motifAnnulation, value?.Trim());
        }

        // ── Présence (renseignée après la session) ────────────
        bool presence;
        [XafDisplayName("Présent")]
        public bool Presence
        {
            get => presence;
            set => SetPropertyValue(nameof(Presence), ref presence, value);
        }

        // ── Évaluation à chaud ────────────────────────────────
        EvaluationFormationNote? noteContenu;
        [XafDisplayName("Note contenu")]
        public EvaluationFormationNote? NoteContenu
        {
            get => noteContenu;
            set => SetPropertyValue(nameof(NoteContenu), ref noteContenu, value);
        }

        EvaluationFormationNote? noteFormateur;
        [XafDisplayName("Note formateur")]
        public EvaluationFormationNote? NoteFormateur
        {
            get => noteFormateur;
            set => SetPropertyValue(nameof(NoteFormateur), ref noteFormateur, value);
        }

        EvaluationFormationNote? noteOrganisation;
        [XafDisplayName("Note organisation")]
        public EvaluationFormationNote? NoteOrganisation
        {
            get => noteOrganisation;
            set => SetPropertyValue(nameof(NoteOrganisation), ref noteOrganisation, value);
        }

        string commentaireEvaluation;
        [Size(1000)]
        [XafDisplayName("Commentaire évaluation")]
        public string CommentaireEvaluation
        {
            get => commentaireEvaluation;
            set => SetPropertyValue(nameof(CommentaireEvaluation),
                ref commentaireEvaluation, value?.Trim());
        }

        // ── Évaluation à chaud — champs détaillés du formulaire ──
        // Ce que la formation va apporter (question ouverte)
        string apportAttendu;
        [Size(1000)]
        [XafDisplayName("Ce que la formation m'apportera")]
        [Category("Évaluation à chaud")]
        public string ApportAttendu
        {
            get => apportAttendu;
            set => SetPropertyValue(nameof(ApportAttendu), ref apportAttendu, value?.Trim());
        }

        // Critère 2 — Adéquation avec les objectifs
        EvaluationFormationNote? noteAdequationObjectifs;
        [XafDisplayName("Adéquation avec les objectifs")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteAdequationObjectifs
        {
            get => noteAdequationObjectifs;
            set => SetPropertyValue(nameof(NoteAdequationObjectifs), ref noteAdequationObjectifs, value);
        }

        // Critère 3 — Apport immédiat pour le poste
        EvaluationFormationNote? noteApportPoste;
        [XafDisplayName("Apport immédiat pour le poste")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteApportPoste
        {
            get => noteApportPoste;
            set => SetPropertyValue(nameof(NoteApportPoste), ref noteApportPoste, value);
        }

        // Critère 4 — Progression / rythme / alternance théorie-pratique
        EvaluationFormationNote? noteProgression;
        [XafDisplayName("Progression (rythme, alternance théorie/pratique)")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteProgression
        {
            get => noteProgression;
            set => SetPropertyValue(nameof(NoteProgression), ref noteProgression, value);
        }

        // Critère 5 — Clarté du contenu
        EvaluationFormationNote? noteClarteContenu;
        [XafDisplayName("Clarté du contenu")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteClarteContenu
        {
            get => noteClarteContenu;
            set => SetPropertyValue(nameof(NoteClarteContenu), ref noteClarteContenu, value);
        }

        // Critère 7 — Disponibilité de l'animateur
        EvaluationFormationNote? noteDisponibiliteAnimateur;
        [XafDisplayName("Disponibilité de l'animateur")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteDisponibiliteAnimateur
        {
            get => noteDisponibiliteAnimateur;
            set => SetPropertyValue(nameof(NoteDisponibiliteAnimateur), ref noteDisponibiliteAnimateur, value);
        }

        // Critère 8 — Niveau vs groupe
        EvaluationFormationNote? noteNiveauGroupe;
        [XafDisplayName("Votre niveau par rapport au groupe")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteNiveauGroupe
        {
            get => noteNiveauGroupe;
            set => SetPropertyValue(nameof(NoteNiveauGroupe), ref noteNiveauGroupe, value);
        }

        // Critère 9 — Supports de formation
        EvaluationFormationNote? noteSupports;
        [XafDisplayName("Supports de formation transmis")]
        [Category("Évaluation à chaud")]
        public EvaluationFormationNote? NoteSupports
        {
            get => noteSupports;
            set => SetPropertyValue(nameof(NoteSupports), ref noteSupports, value);
        }

        [NonPersistent]
        [XafDisplayName("Note moyenne globale")]
        [ModelDefault("DisplayFormat", "N1")]
        public decimal NoteMoyenneGlobale
        {
            get
            {
                var notes = new EvaluationFormationNote?[]
                {
                    NoteContenu, NoteAdequationObjectifs, NoteApportPoste,
                    NoteProgression, NoteClarteContenu, NoteFormateur,
                    NoteDisponibiliteAnimateur, NoteNiveauGroupe,
                    NoteSupports, NoteOrganisation
                };
                var valides = notes.Where(n => n.HasValue).Select(n => (decimal)(int)n.Value).ToList();
                return valides.Count > 0
                    ? Math.Round(valides.Average(), 1)
                    : 0m;
            }
        }

        // ── Attestation ───────────────────────────────────────
        bool attestationGeneree;
        [XafDisplayName("Attestation générée")]
        [ModelDefault("AllowEdit", "False")]
        public bool AttestationGeneree
        {
            get => attestationGeneree;
            set => SetPropertyValue(nameof(AttestationGeneree), ref attestationGeneree, value);
        }

        DateTime? dateAttestation;
        [XafDisplayName("Date attestation")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateAttestation
        {
            get => dateAttestation;
            set => SetPropertyValue(nameof(DateAttestation), ref dateAttestation, value);
        }

        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Document attestation")]
        public DevExpress.Persistent.BaseImpl.FileData DocumentAttestation
        {
            get => documentAttestation;
            set => SetPropertyValue(nameof(DocumentAttestation), ref documentAttestation, value);
        }
        DevExpress.Persistent.BaseImpl.FileData documentAttestation;

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayInscription =>
            $"{Salarie?.FullName} — {SessionFormation?.Intitule}";

        // ── Suivi post-formation (collection inverse) ────────
        [Association("InscriptionFormation-Suivi"), Aggregated]
        [XafDisplayName("Suivi formation")]
        public XPCollection<SuiviFormation> SuivisFormation
            => GetCollection<SuiviFormation>(nameof(SuivisFormation));

        // ── Transitions ──────────────────────────────────────
        public void Confirmer()
        {
            Statut = InscriptionStatut.Confirmee;
        }

        public void Annuler(string motif)
        {
            Statut = InscriptionStatut.Annulee;
            MotifAnnulation = motif;
        }

        public void MarquerAbsent()
        {
            Statut = InscriptionStatut.Absente;
            Presence = false;
        }

        // ── Cycle de vie ──────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateInscription = DateTime.Now;
            Statut = InscriptionStatut.EnAttente;
            try { InscritPar = SecuritySystem.CurrentUserName; }
            catch { }
        }



    }
}
