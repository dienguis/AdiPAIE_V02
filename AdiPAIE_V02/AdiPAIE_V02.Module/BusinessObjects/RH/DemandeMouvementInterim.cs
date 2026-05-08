// =============================================================================
//  DemandeMouvementInterim.cs — V1.5
//
//  Demande de mouvement d'un intérimaire initiée par un Assistant Commercial (AC).
//
//  Workflow :
//    Brouillon → SoumiseAssistantRH → ValideeAssistantRH → ValideeRH
//             → (option ValideeDAF) → Appliquee
//             → MouvementInterimaire généré + ContratInterim mis à jour
//
//  Court-circuit possible : RH peut valider directement (bypass Assistant RH)
//  si l'Assistant RH est absent.
//
//  Différence avec DemandeRecrutementInterim :
//    - Recrutement = nouvelle embauche d'un intérimaire (création de fiche)
//    - Mouvement   = changement sur un intérimaire DÉJÀ EN MISSION
// =============================================================================
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
    [DefaultClassOptions]
    [XafDisplayName("Demande de mouvement intérimaire")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("Action_Forward")]
    [Appearance("DMI_Brouillon", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,Brouillon#",
        FontColor = "Gray")]
    [Appearance("DMI_EnAttente", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,SoumiseAssistantRH# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,ValideeAssistantRH# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,ValideeRH# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,ValideeDAF#",
        FontColor = "DarkOrange")]
    [Appearance("DMI_Appliquee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,Appliquee#",
        FontColor = "Green")]
    [Appearance("DMI_Rejetee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,RejeteeAssistantRH# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,RejeteeRH# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,RejeteeDAF# "
                 + "OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+DemandeMouvementStatut,Annulee#",
        FontColor = "Red", FontStyle = DevExpress.Drawing.DXFontStyle.Strikeout)]
    public class DemandeMouvementInterim : BaseObject
    {
        public DemandeMouvementInterim(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Statut = DemandeMouvementStatut.Brouillon;
            DateCreation = DateTime.Today;
            DateSouhaitee = DateTime.Today.AddDays(7);
            try { SaisiPar = DevExpress.ExpressApp.SecuritySystem.CurrentUserName; } catch { }
            Reference = $"DMI-{DateTime.Today:yyyy}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        }

        // ── Référence ─────────────────────────────────────────────────
        [Size(40)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Référence")]
        public string Reference
        {
            get => reference;
            set => SetPropertyValue(nameof(Reference), ref reference, value);
        }
        string reference;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Statut")]
        public DemandeMouvementStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }
        DemandeMouvementStatut statut;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Saisi par")]
        [Size(80)]
        public string SaisiPar
        {
            get => saisiPar;
            set => SetPropertyValue(nameof(SaisiPar), ref saisiPar, value);
        }
        string saisiPar;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date création")]
        public DateTime DateCreation
        {
            get => dateCreation;
            set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value);
        }
        DateTime dateCreation;

        // ── Initiateur (AC) ───────────────────────────────────────────
        [XafDisplayName("Initiateur (AC)")]
        public Salarie Initiateur
        {
            get => initiateur;
            set => SetPropertyValue(nameof(Initiateur), ref initiateur, value);
        }
        Salarie initiateur;

        // ── Demande ───────────────────────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Intérimaire concerné")]
        public Interimaire Interimaire
        {
            get => interimaire;
            set
            {
                SetPropertyValue(nameof(Interimaire), ref interimaire, value);
                // Auto-rempli StationOrigine et PosteActuel depuis le contrat actif
                if (value != null)
                {
                    var contratActif = value.ContratActif;
                    if (contratActif != null)
                    {
                        if (StationOrigine == null) StationOrigine = contratActif.Station;
                        if (PosteActuel == null) PosteActuel = contratActif.PosteOccupe;
                    }
                }
            }
        }
        Interimaire interimaire;

        [RuleRequiredField]
        [XafDisplayName("Type de mouvement")]
        [ImmediatePostData]
        public TypeMouvementInterim? TypeMouvement
        {
            get => typeMouvement;
            set => SetPropertyValue(nameof(TypeMouvement), ref typeMouvement, value);
        }
        TypeMouvementInterim? typeMouvement;

        [RuleRequiredField]
        [XafDisplayName("Date souhaitée")]
        public DateTime DateSouhaitee
        {
            get => dateSouhaitee;
            set => SetPropertyValue(nameof(DateSouhaitee), ref dateSouhaitee, value);
        }
        DateTime dateSouhaitee;

        // ── Origine (auto-remplie depuis ContratInterim actif) ────────
        [XafDisplayName("Station origine")]
        [ModelDefault("AllowEdit", "False")]
        public StationService StationOrigine
        {
            get => stationOrigine;
            set => SetPropertyValue(nameof(StationOrigine), ref stationOrigine, value);
        }
        StationService stationOrigine;

        [XafDisplayName("Poste actuel")]
        [ModelDefault("AllowEdit", "False")]
        public PosteInterimaire PosteActuel
        {
            get => posteActuel;
            set => SetPropertyValue(nameof(PosteActuel), ref posteActuel, value);
        }
        PosteInterimaire posteActuel;

        // ── Destination (selon TypeMouvement) ─────────────────────────
        [XafDisplayName("Station destination")]
        [Appearance("DMI_StationDest_Visible", Visibility = ViewItemVisibility.Hide,
            Criteria = "TypeMouvement <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeMouvementInterim,ChangementStation#")]
        public StationService StationDestination
        {
            get => stationDestination;
            set => SetPropertyValue(nameof(StationDestination), ref stationDestination, value);
        }
        StationService stationDestination;

        [XafDisplayName("Poste souhaité")]
        [Appearance("DMI_PosteSouhaite_Visible", Visibility = ViewItemVisibility.Hide,
            Criteria = "TypeMouvement <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeMouvementInterim,ChangementPoste#")]
        public PosteInterimaire PosteSouhaite
        {
            get => posteSouhaite;
            set => SetPropertyValue(nameof(PosteSouhaite), ref posteSouhaite, value);
        }
        PosteInterimaire posteSouhaite;

        // ── Remplacement temporaire ───────────────────────────────────
        [XafDisplayName("Salarié remplacé (titulaire absent)")]
        [Appearance("DMI_TitulaireRemp_Visible", Visibility = ViewItemVisibility.Hide,
            Criteria = "TypeMouvement <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeMouvementInterim,RemplacementTemporaire#")]
        public Salarie TitulaireRemplace
        {
            get => titulaireRemplace;
            set => SetPropertyValue(nameof(TitulaireRemplace), ref titulaireRemplace, value);
        }
        Salarie titulaireRemplace;

        [XafDisplayName("Durée remplacement (jours)")]
        [Appearance("DMI_DureeRempl_Visible", Visibility = ViewItemVisibility.Hide,
            Criteria = "TypeMouvement <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeMouvementInterim,RemplacementTemporaire#")]
        public int DureeRemplacementJours
        {
            get => dureeRemplacementJours;
            set => SetPropertyValue(nameof(DureeRemplacementJours), ref dureeRemplacementJours, value);
        }
        int dureeRemplacementJours;

        // ── Motif & justification ─────────────────────────────────────
        [RuleRequiredField]
        [Size(1000)]
        [XafDisplayName("Motif")]
        public string Motif
        {
            get => motif;
            set => SetPropertyValue(nameof(Motif), ref motif, value);
        }
        string motif;

        [Size(500)]
        [XafDisplayName("Précision (si « Autre »)")]
        [Appearance("DMI_MotifAutre_Visible", Visibility = ViewItemVisibility.Hide,
            Criteria = "TypeMouvement <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+TypeMouvementInterim,Autre#")]
        public string MotifAutre
        {
            get => motifAutre;
            set => SetPropertyValue(nameof(MotifAutre), ref motifAutre, value);
        }
        string motifAutre;

        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Pièce jointe (optionnelle)")]
        public FileData PieceJointe
        {
            get => pieceJointe;
            set => SetPropertyValue(nameof(PieceJointe), ref pieceJointe, value);
        }
        FileData pieceJointe;

        // ── Options workflow ──────────────────────────────────────────
        [XafDisplayName("Notifier le RFE après application")]
        public bool OptionNotifierRFE
        {
            get => optionNotifierRFE;
            set => SetPropertyValue(nameof(OptionNotifierRFE), ref optionNotifierRFE, value);
        }
        bool optionNotifierRFE;

        [XafDisplayName("Approbation DAF requise")]
        public bool OptionApprobationDAF
        {
            get => optionApprobationDAF;
            set => SetPropertyValue(nameof(OptionApprobationDAF), ref optionApprobationDAF, value);
        }
        bool optionApprobationDAF;

        // ── Audit workflow ────────────────────────────────────────────
        [Size(80)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé par Assistant RH")]
        public string AssistantRHValidationUser
        {
            get => assistantRHValidationUser;
            set => SetPropertyValue(nameof(AssistantRHValidationUser), ref assistantRHValidationUser, value);
        }
        string assistantRHValidationUser;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation Assistant RH")]
        public DateTime? AssistantRHDate
        {
            get => assistantRHDate;
            set => SetPropertyValue(nameof(AssistantRHDate), ref assistantRHDate, value);
        }
        DateTime? assistantRHDate;

        [Size(500)]
        [XafDisplayName("Commentaire Assistant RH")]
        public string AssistantRHCommentaire
        {
            get => assistantRHCommentaire;
            set => SetPropertyValue(nameof(AssistantRHCommentaire), ref assistantRHCommentaire, value);
        }
        string assistantRHCommentaire;

        [Size(80)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé par RH")]
        public string RHValidationUser
        {
            get => rhValidationUser;
            set => SetPropertyValue(nameof(RHValidationUser), ref rhValidationUser, value);
        }
        string rhValidationUser;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation RH")]
        public DateTime? RHDate
        {
            get => rhDate;
            set => SetPropertyValue(nameof(RHDate), ref rhDate, value);
        }
        DateTime? rhDate;

        [Size(500)]
        [XafDisplayName("Commentaire RH")]
        public string RHCommentaire
        {
            get => rhCommentaire;
            set => SetPropertyValue(nameof(RHCommentaire), ref rhCommentaire, value);
        }
        string rhCommentaire;

        [Size(80)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Validé par DAF")]
        public string DAFValidationUser
        {
            get => dafValidationUser;
            set => SetPropertyValue(nameof(DAFValidationUser), ref dafValidationUser, value);
        }
        string dafValidationUser;

        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date validation DAF")]
        public DateTime? DAFDate
        {
            get => dafDate;
            set => SetPropertyValue(nameof(DAFDate), ref dafDate, value);
        }
        DateTime? dafDate;

        [Size(500)]
        [XafDisplayName("Commentaire DAF")]
        public string DAFCommentaire
        {
            get => dafCommentaire;
            set => SetPropertyValue(nameof(DAFCommentaire), ref dafCommentaire, value);
        }
        string dafCommentaire;

        // ── Lien vers MouvementInterimaire généré ────────────────────
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Mouvement généré")]
        public MouvementInterimaire MouvementGenere
        {
            get => mouvementGenere;
            set => SetPropertyValue(nameof(MouvementGenere), ref mouvementGenere, value);
        }
        MouvementInterimaire mouvementGenere;

        // ── DisplayName ───────────────────────────────────────────────
        [VisibleInListView(false), VisibleInDetailView(false)]
        public string DisplayName =>
            $"{Reference} — {Interimaire?.FullName ?? "?"} — {TypeMouvement}";
    }
}
