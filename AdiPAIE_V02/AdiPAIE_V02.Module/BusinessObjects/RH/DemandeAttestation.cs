using DevExpress.Data.Filtering;
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
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Demande d'attestation émise par un salarié via le portail self-service.
    /// Le RH reçoit la demande, la traite et peut joindre le document généré.
    ///
    /// Workflow : Soumise → EnTraitement → Traitée  (ou Rejetée)
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Demande d'attestation")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_FileAttachment")]
  //  [NavigationItem("GRH - Espace salarié")]

    // Styles par statut
    [Appearance("Demande_Traitee_Style", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Traitee#",
        FontColor = "Green")]
    [Appearance("Demande_Rejetee_Style", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Rejetee#",
        FontColor = "Red", FontStyle = DXFontStyle.Strikeout)]

    // Verrouiller saisie si traitée ou rejetée
    [Appearance("Demande_Lock_Final",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Traitee# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Rejetee#",
        TargetItems = "Nature;Motif;DateSouhaitee", Enabled = false)]
    // V1.6.2 — Badges colorés sur Statut (workflow attestation)
    [Appearance("Demande_Badge_EnAttente",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,EnAttenteN1#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,EnAttenteN2#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Soumise#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,EnTraitement#",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DXFontStyle.Bold)]
    [Appearance("Demande_Badge_Traitee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Traitee#",
        BackColor = "PaleGreen", FontColor = "DarkGreen", FontStyle = DXFontStyle.Bold)]
    [Appearance("Demande_Badge_Rejetee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeStatut,Rejetee#",
        BackColor = "LightCoral", FontColor = "DarkRed", FontStyle = DXFontStyle.Bold)]
    public class DemandeAttestation : BaseObject
    {
        public DemandeAttestation(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateDemande = DateTime.Now;
            //Statut = DemandeStatut.Soumise;
            InitialiserStatut();


            try { CreePar = SecuritySystem.CurrentUserName; } catch { }

            // Auto-rattachement au salarié connecté
            try
            {
                var currentUser = Session.FindObject<ApplicationUser>(
                    CriteriaOperator.Parse("UserName = ?", SecuritySystem.CurrentUserName));
                if (currentUser?.Salarie != null)
                    Salarie = currentUser.Salarie;
            }
            catch { }


        }

        // ── Salarié demandeur ─────────────────────────────────────
        [Association("Salarie-DemandesAttestation")]
        [RuleRequiredField]
        [XafDisplayName("Salarié")]
        [VisibleInDetailView(false)]  // invisible sur le portail
        [VisibleInListView(false)]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

                   // ── Nature & contexte ─────────────────────────────────────
            AttestationNature? nature;
        [RuleRequiredField]
        [XafDisplayName("Nature de l'attestation")]
        public AttestationNature? Nature
        {
            get => nature;
            set => SetPropertyValue(nameof(Nature), ref nature, value);
        }

        string motif;
        [Size(512)]
        [XafDisplayName("Motif / précisions")]
        public string Motif
        {
            get => motif;
            set => SetPropertyValue(nameof(Motif), ref motif, value?.Trim());
        }

        DateTime? dateSouhaitee;
        [XafDisplayName("Date souhaitée de remise")]
        public DateTime? DateSouhaitee
        {
            get => dateSouhaitee;
            set => SetPropertyValue(nameof(DateSouhaitee), ref dateSouhaitee, value);
        }

        // ── Suivi ─────────────────────────────────────────────────
        DateTime dateDemande;
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date de la demande")]
        public DateTime DateDemande
        {
            get => dateDemande;
            set => SetPropertyValue(nameof(DateDemande), ref dateDemande, value);
        }

        DemandeStatut statut;
        [XafDisplayName("Statut")]
        public DemandeStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        // ── Traitement RH ─────────────────────────────────────────
        DateTime? dateTraitement;
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date de traitement")]
        public DateTime? DateTraitement
        {
            get => dateTraitement;
            set => SetPropertyValue(nameof(DateTraitement), ref dateTraitement, value);
        }

        string traitePar;
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Traité par")]
        public string TraitePar
        {
            get => traitePar;
            set => SetPropertyValue(nameof(TraitePar), ref traitePar, value);
        }

        string commentaireRH;
        [Size(1024)]
        [XafDisplayName("Commentaire RH")]
        public string CommentaireRH
        {
            get => commentaireRH;
            set => SetPropertyValue(nameof(CommentaireRH), ref commentaireRH, value?.Trim());
        }

        // ── Document généré ───────────────────────────────────────
        /// <summary>Document attestation joint par le RH (PDF généré)</summary>
        FileData document;
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Document (attestation générée)")]
        public FileData Document
        {
            get => document;
            set => SetPropertyValue(nameof(Document), ref document, value);
        }

        // ── Affichage ─────────────────────────────────────────────
   
        [NonPersistent]
        public string DisplayName =>
       $"{Salarie?.LastName} – {Nature} – {DateDemande:dd/MM/yyyy}";

        // ── Traçabilité ───────────────────────────────────────────
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [Browsable(false)]
        public string CreePar
        {
            get => creePar;
            set => SetPropertyValue(nameof(CreePar), ref creePar, value);
        }
        string creePar;

        // ── Workflow (appelées par controller) ────────────────────
        public void PrendreEnCharge()
        {
            if (Statut != DemandeStatut.Soumise)
                throw new UserFriendlyException("La demande n'est pas en état Soumise.");
            Statut = DemandeStatut.EnTraitement;
            try { TraitePar = SecuritySystem.CurrentUserName; } catch { }
        }

        public void Traiter()
        {
            if (Statut != DemandeStatut.EnTraitement)
                throw new UserFriendlyException("La demande doit être En traitement.");
            Statut = DemandeStatut.Traitee;
            DateTraitement = DateTime.Now;
            try { TraitePar = SecuritySystem.CurrentUserName; } catch { }
        }

        public void Rejeter(string motifRejet)
        {
            Statut = DemandeStatut.Rejetee;
            DateTraitement = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(motifRejet))
                CommentaireRH = motifRejet;
            try { TraitePar = SecuritySystem.CurrentUserName; } catch { }
        }



        // ── Traçabilité hiérarchique ──────────────────────────────────

        /// <summary>Manager N+1 au moment de la soumission (snapshot).</summary>
        [XafDisplayName("Valideur N+1")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ValideurN1
        {
            get => valideurN1;
            set => SetPropertyValue(nameof(ValideurN1), ref valideurN1, value);
        }
        Salarie valideurN1;

        /// <summary>Manager N+2 au moment de la soumission (snapshot).</summary>
        [XafDisplayName("Valideur N+2")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ValideurN2
        {
            get => valideurN2;
            set => SetPropertyValue(nameof(ValideurN2), ref valideurN2, value);
        }
        Salarie valideurN2;

        [XafDisplayName("Date validation N+1")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateValidationN1
        {
            get => dateValidationN1;
            set => SetPropertyValue(nameof(DateValidationN1), ref dateValidationN1, value);
        }
        DateTime? dateValidationN1;

        [XafDisplayName("Date validation N+2")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateValidationN2
        {
            get => dateValidationN2;
            set => SetPropertyValue(nameof(DateValidationN2), ref dateValidationN2, value);
        }
        DateTime? dateValidationN2;

        [XafDisplayName("Rejeté par")]
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        public string RejeteParNom
        {
            get => rejeteParNom;
            set => SetPropertyValue(nameof(RejeteParNom), ref rejeteParNom, value);
        }
        string rejeteParNom;

        // ── PATCH AfterConstruction() ────────────────────────────────
        // Remplacer :  Statut = DemandeStatut.Soumise;
        // Par :        InitialiserStatut();

        /// <summary>
        /// Initialise le statut selon la chaîne hiérarchique du salarié.
        /// Si N+1 existe → EnAttenteN1 et snapshot des valideurs.
        /// Sinon → Soumise (visible directement par le RH).
        /// </summary>
        public void InitialiserStatut()
        {
            if (Salarie == null)
            {
                Statut = DemandeStatut.Soumise;
                return;
            }

            var chain = Salarie.GetManagerChain(2);

            if (chain.Count == 0)
            {
                // Pas de hiérarchie → directement au RH
                Statut = DemandeStatut.Soumise;
                return;
            }

            // Snapshot des valideurs au moment de la soumission
            ValideurN1 = chain.Count >= 1 ? chain[0] : null;
            ValideurN2 = chain.Count >= 2 ? chain[1] : null;

            Statut = DemandeStatut.EnAttenteN1;
        }

        // ── Méthodes workflow hiérarchique ───────────────────────────

        /// <summary>
        /// Validation par le N+1.
        /// Si N+2 existe → passe en EnAttenteN2.
        /// Sinon → passe en Soumise (visible RH).
        /// </summary>
        public void ValiderN1()
        {
            if (Statut != DemandeStatut.EnAttenteN1)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "La demande n'est pas en attente de validation N+1.");

            DateValidationN1 = DateTime.Now;

            if (ValideurN2 != null)
                Statut = DemandeStatut.EnAttenteN2;
            else
                Statut = DemandeStatut.Soumise;
        }

        /// <summary>
        /// Validation par le N+2.
        /// Passe en Soumise (visible RH).
        /// </summary>
        public void ValiderN2()
        {
            if (Statut != DemandeStatut.EnAttenteN2)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "La demande n'est pas en attente de validation N+2.");

            DateValidationN2 = DateTime.Now;
            Statut = DemandeStatut.Soumise;
        }

        /// <summary>
        /// Rejet hiérarchique (N+1 ou N+2).
        /// Renseigne le nom du rejeteur et passe en Rejetée.
        /// </summary>
        public void RejeterHierarchie(string motifRejet = null)
        {
            if (Statut != DemandeStatut.EnAttenteN1 && Statut != DemandeStatut.EnAttenteN2)
                throw new DevExpress.ExpressApp.UserFriendlyException(
                    "La demande n'est pas en attente de validation hiérarchique.");

            try { RejeteParNom = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            if (!string.IsNullOrWhiteSpace(motifRejet))
                CommentaireRH = motifRejet;
            Statut = DemandeStatut.Rejetee;
        }

        // ── Lien avec la demande de congé (si nature = Congé) ────────
        CongeDemande congeSource;

        [XafDisplayName("Congé concerné")]
        [Appearance("CongeSource_Visible",
          Criteria = "Nature = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AttestationNature,Conge#",
          Visibility = ViewItemVisibility.Show, TargetItems = nameof(CongeSource))]
        [Appearance("CongeSource_Hidden",
          Criteria = "Nature <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+AttestationNature,Conge#",
          Visibility = ViewItemVisibility.Hide, TargetItems = nameof(CongeSource))]
        [Association("CongeDemande-Attestations")]
        public CongeDemande CongeSource
        {
            get => congeSource;
            set => SetPropertyValue(nameof(CongeSource), ref congeSource, value);
        }

    


    }
}
