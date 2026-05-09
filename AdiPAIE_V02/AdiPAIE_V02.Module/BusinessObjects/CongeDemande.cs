using AdiPAIE_V02.Module.BusinessObjects.RH;
using AdiPAIE_V02.Module.Domain;
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
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [XafDisplayName("Demande de congé")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("Action_GrantPermission")]
   // [NavigationItem("GRH - Espace salarié")]

    // ── Validation ────────────────────────────────────────────
    [RuleCriteria("Conge_DateFin_GTE_DateDebut", DefaultContexts.Save,
        "DateFin >= DateDebut",
        CustomMessageTemplate = "La date de fin doit être >= à la date de début.")]
    [RuleCriteria("Conge_DateReprise_PostDateFin", DefaultContexts.Save,
        "DateReprise IS NULL OR DateReprise > DateFin",
        CustomMessageTemplate = "La date de reprise doit être postérieure à la date de fin du congé.")]

    // ── Apparences selon statut ───────────────────────────────
    [Appearance("Conge_Accordee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Accordee#",
        FontColor = "Green", FontStyle = DXFontStyle.Bold)]

    [Appearance("Conge_Refusee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Refusee#",
        FontColor = "Red")]

    [Appearance("Conge_Annulee", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Annulee#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic)]

    [Appearance("Conge_EnAttente", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,EnAttenteN1#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,EnAttenteN2#",
        FontColor = "DarkOrange")]

    // ── Champs éditables uniquement en Brouillon ──────────────
    [Appearance("Conge_LockWhenSubmitted", TargetItems = "Type;DateDebut;DateFin;Motif",
        Criteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Brouillon#",
        Enabled = false)]

    // ── DateReprise : visible quand Soumise ou Accordée, caché sinon ──
    // L'editabilite est geree par CongeAccordController.UpdateDateRepriseEditable()
    [Appearance("Conge_DateReprise_Visible",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#"
            + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Accordee#",
        Visibility = ViewItemVisibility.Show, TargetItems = nameof(DateReprise))]
    [Appearance("Conge_DateReprise_Hidden",
        Criteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#"
            + " AND Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Accordee#",
        Visibility = ViewItemVisibility.Hide, TargetItems = nameof(DateReprise))]
    // V1.6.2 — Badges colorés sur la cellule Statut (workflow congés)
    [Appearance("Conge_Badge_Brouillon",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Brouillon#",
        BackColor = "Gainsboro", FontColor = "DimGray", FontStyle = DXFontStyle.Bold)]
    [Appearance("Conge_Badge_EnAttente",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,EnAttenteN1#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,EnAttenteN2#"
                 + " OR Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Soumise#",
        BackColor = "Moccasin", FontColor = "DarkOrange", FontStyle = DXFontStyle.Bold)]
    [Appearance("Conge_Badge_Accordee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Accordee#",
        BackColor = "PaleGreen", FontColor = "DarkGreen", FontStyle = DXFontStyle.Bold)]
    [Appearance("Conge_Badge_Refusee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Refusee#",
        BackColor = "LightCoral", FontColor = "DarkRed", FontStyle = DXFontStyle.Bold)]
    [Appearance("Conge_Badge_Annulee",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+CongeStatut,Annulee#",
        BackColor = "DarkGray", FontColor = "White", FontStyle = DXFontStyle.Bold)]
    public class CongeDemande : BaseObject
    {
        public CongeDemande(Session session) : base(session) { }

        // ── Salarié ───────────────────────────────────────────
        [RuleRequiredField]
        [Association("Salarie-Conges")]
        [XafDisplayName("Salarié")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Type et période ───────────────────────────────────
        [RuleRequiredField]
        [XafDisplayName("Type de congé")]
        public CongeType Type
        {
            get => type;
            set => SetPropertyValue(nameof(Type), ref type, value);
        }
        CongeType type;

        [RuleRequiredField]
        [XafDisplayName("Date de début")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }
        DateTime dateDebut = DateTime.Today;

        [RuleRequiredField]
        [XafDisplayName("Date de fin")]
        public DateTime DateFin
        {
            get => dateFin;
            set => SetPropertyValue(nameof(DateFin), ref dateFin, value.Date);
        }
        DateTime dateFin = DateTime.Today;

        [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
        [DbType("decimal(18,2)")]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Durée (jours)")]
        public decimal DureeJours
        {
            get => dureeJours;
            set => SetPropertyValue(nameof(DureeJours), ref dureeJours, value);
        }
        decimal dureeJours;


        // ── Durées détaillées ─────────────────────────────────
        [ModelDefault("DisplayFormat", "N2")]
        [DbType("decimal(18,2)")]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Jours calendaires")]
        public decimal DureeJoursCalendaires
        {
            get => dureeJoursCalendaires;
            set => SetPropertyValue(nameof(DureeJoursCalendaires),
                ref dureeJoursCalendaires, value);
        }
        decimal dureeJoursCalendaires;

        // ── Justificatif ──────────────────────────────────────
        [XafDisplayName("Justificatif fourni")]
        public bool JustificatifFourni
        {
            get => justificatifFourni;
            set => SetPropertyValue(nameof(JustificatifFourni),
                ref justificatifFourni, value);
        }
        bool justificatifFourni;

        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [XafDisplayName("Pièce justificative")]
        public DevExpress.Persistent.BaseImpl.FileData PieceJustificative
        {
            get => pieceJustificative;
            set => SetPropertyValue(nameof(PieceJustificative),
                ref pieceJustificative, value);
        }
        DevExpress.Persistent.BaseImpl.FileData pieceJustificative;

        // ── Impact paie ───────────────────────────────────────
        [XafDisplayName("Impact paie")]
        [ModelDefault("AllowEdit", "False")]
        public bool ImpactPaieGenere
        {
            get => impactPaieGenere;
            set => SetPropertyValue(nameof(ImpactPaieGenere),
                ref impactPaieGenere, value);
        }
        bool impactPaieGenere;

        // ── Solde vérifié ─────────────────────────────────────
        [XafDisplayName("Solde vérifié")]
        [ModelDefault("AllowEdit", "False")]
        public bool SoldeVerifie
        {
            get => soldeVerifie;
            set => SetPropertyValue(nameof(SoldeVerifie),
                ref soldeVerifie, value);
        }
        bool soldeVerifie;

        [NonPersistent]
        [XafDisplayName("Solde disponible")]
        [ModelDefault("DisplayFormat", "N1")]
        public decimal SoldeDisponible
        {
            get
            {
                // Calculé à la volée depuis la session
                try
                {
                    if (Salarie == null || Type == null) return 0;
                    var solde = new XPQuery<SoldeConge>(Session)
                        .FirstOrDefault(s =>
                            s.Salarie.Oid == Salarie.Oid &&
                            s.TypeConge.Oid == Type.Oid &&
                            s.Annee == DateDebut.Year &&
                            s.Statut == SoldeCongeStatut.Actif);
                    return solde?.SoldeDisponible ?? 0;
                }
                catch { return 0; }
            }
        }

        [Size(240)]
        [XafDisplayName("Motif")]
        public string Motif
        {
            get => motif;
            set => SetPropertyValue(nameof(Motif), ref motif, value?.Trim());
        }
        string motif;

        // ── Statut ────────────────────────────────────────────
        CongeStatut statut = CongeStatut.Brouillon;
        [XafDisplayName("Statut")]
        public CongeStatut Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        // ── Hiérarchie (snapshot à la soumission) ─────────────
        [XafDisplayName("Valideur N+1")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie ValideurN1
        {
            get => valideurN1;
            set => SetPropertyValue(nameof(ValideurN1), ref valideurN1, value);
        }
        Salarie valideurN1;

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

        // ── Traitement RH ─────────────────────────────────────
        [XafDisplayName("Traité par (RH)")]
        [ModelDefault("AllowEdit", "False")]
        [Size(100)]
        public string TraiteParRH
        {
            get => traiteParRH;
            set => SetPropertyValue(nameof(TraiteParRH), ref traiteParRH, value);
        }
        string traiteParRH;

        [XafDisplayName("Date de traitement RH")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateTraitementRH
        {
            get => dateTraitementRH;
            set => SetPropertyValue(nameof(DateTraitementRH), ref dateTraitementRH, value);
        }
        DateTime? dateTraitementRH;

        [Size(500)]
        [XafDisplayName("Commentaire RH")]
        public string CommentaireRH
        {
            get => commentaireRH;
            set => SetPropertyValue(nameof(CommentaireRH), ref commentaireRH, value?.Trim());
        }
        string commentaireRH;

        /// <summary>
        /// Date de reprise effective — saisie par le RH au moment de l'accord.
        /// Peut différer de DateFin + 1 jour (récupération, pont, weekend, etc.)
        /// </summary>
        [XafDisplayName("Date de reprise")]
        public DateTime? DateReprise
        {
            get => dateReprise;
            set => SetPropertyValue(nameof(DateReprise), ref dateReprise, value);
        }
        DateTime? dateReprise;

        // ── Motif de rejet ────────────────────────────────────
        [Size(500)]
        [XafDisplayName("Motif de rejet")]
        [ModelDefault("AllowEdit", "False")]
        public string MotifRejet
        {
            get => motifRejet;
            set => SetPropertyValue(nameof(MotifRejet), ref motifRejet, value?.Trim());
        }
        string motifRejet;

        [Size(100)]
        [XafDisplayName("Rejeté par")]
        [ModelDefault("AllowEdit", "False")]
        public string RejeteParNom
        {
            get => rejeteParNom;
            set => SetPropertyValue(nameof(RejeteParNom), ref rejeteParNom, value);
        }
        string rejeteParNom;

        // ── Attestations liées ────────────────────────────────
        [Association("CongeDemande-Attestations")]
        [XafDisplayName("Attestations générées")]
        public XPCollection<DemandeAttestation> Attestations
            => GetCollection<DemandeAttestation>(nameof(Attestations));

        [Association("CongeDemande-EvenementConge"), Aggregated]
        [Browsable(false)]
        public XPCollection<EvenementConge> EvenementsCalendrier
        => GetCollection<EvenementConge>(nameof(EvenementsCalendrier));

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayName =>
            $"{Salarie?.FullName} : {DateDebut:dd/MM} → {DateFin:dd/MM/yyyy} ({DureeJours:n1} j) — {Statut}";

        // ── Init ──────────────────────────────────────────────
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            RecalculerDuree();
            try
            {
                // Auto-rattachement salarié connecté
                var currentUser = Session.FindObject<ApplicationUser>(
                    DevExpress.Data.Filtering.CriteriaOperator.Parse(
                        "UserName = ?", SecuritySystem.CurrentUserName));
                if (currentUser?.Salarie != null)
                    Salarie = currentUser.Salarie;
            }
            catch { }
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted) RecalculerDuree();
        }

        // ── Méthodes métier ───────────────────────────────────


        public void RecalculerDuree()
        {
            // Jours ouvrables (selon le type de congé)
            DureeJours = CalculerJoursDemande(
                DateDebut, DateFin,
                Type?.CompteEnJoursOuvrables ?? true);

            // Jours calendaires (toujours calculés)
            DureeJoursCalendaires = CalculerJoursDemande(
                DateDebut, DateFin, false);
        }



        /// <summary>
        /// Soumet la demande selon la chaîne hiérarchique du salarié.
        /// Si N+1 → EnAttenteN1, sinon Soumise directement.
        /// </summary>
        public void Soumettre()
        {
            if (Statut != CongeStatut.Brouillon)
                throw new UserFriendlyException(
                    "Seule une demande en Brouillon peut être soumise.");

            if (Salarie == null)
                throw new UserFriendlyException("Le salarié n'est pas renseigné.");

            var chain = Salarie.GetManagerChain(2);
            if (chain.Count == 0)
            {
                Statut = CongeStatut.Soumise;
                return;
            }

            ValideurN1 = chain.Count >= 1 ? chain[0] : null;
            ValideurN2 = chain.Count >= 2 ? chain[1] : null;
            Statut = CongeStatut.EnAttenteN1;
        }

        /// <summary>Validation par le N+1.</summary>
        public void ValiderN1()
        {
            if (Statut != CongeStatut.EnAttenteN1)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de validation N+1.");

            DateValidationN1 = DateTime.Now;
            Statut = ValideurN2 != null
                ? CongeStatut.EnAttenteN2
                : CongeStatut.Soumise;
        }

        /// <summary>Validation par le N+2.</summary>
        public void ValiderN2()
        {
            if (Statut != CongeStatut.EnAttenteN2)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de validation N+2.");

            DateValidationN2 = DateTime.Now;
            Statut = CongeStatut.Soumise;
        }

        /// <summary>
        /// Rejet hiérarchique — repasse en Brouillon pour permettre modification.
        /// </summary>
        public void RejeterHierarchie(string motif = null)
        {
            if (Statut != CongeStatut.EnAttenteN1 && Statut != CongeStatut.EnAttenteN2)
                throw new UserFriendlyException(
                    "La demande n'est pas en attente de validation hiérarchique.");

            try { RejeteParNom = SecuritySystem.CurrentUserName; } catch { }
            if (!string.IsNullOrWhiteSpace(motif)) MotifRejet = motif;

            // Repasse en Brouillon — le salarié peut modifier et resoumettre
            Statut = CongeStatut.Brouillon;

            // Réinitialise les valideurs pour une nouvelle soumission
            ValideurN1 = null;
            ValideurN2 = null;
            DateValidationN1 = null;
            DateValidationN2 = null;
        }

        /// <summary>Accord par le RH avec date de reprise obligatoire.</summary>
        public void Accorder(DateTime dateReprise)
        {
            if (Statut != CongeStatut.Soumise)
                throw new UserFriendlyException(
                    "Seule une demande soumise peut être accordée.");

            DateReprise = dateReprise;
            DateTraitementRH = DateTime.Now;
            try { TraiteParRH = SecuritySystem.CurrentUserName; } catch { }
            Statut = CongeStatut.Accordee;
        }

        /// <summary>Refus par le RH.</summary>
        public void Refuser(string motif = null)
        {
            if (Statut != CongeStatut.Soumise)
                throw new UserFriendlyException(
                    "Seule une demande soumise peut être refusée.");

            if (!string.IsNullOrWhiteSpace(motif)) CommentaireRH = motif;
            DateTraitementRH = DateTime.Now;
            try { TraiteParRH = SecuritySystem.CurrentUserName; } catch { }
            Statut = CongeStatut.Refusee;
        }

        /// <summary>Annulation par le RH après accord.</summary>
        public void Annuler(string motif = null)
        {
            if (Statut != CongeStatut.Accordee)
                throw new UserFriendlyException(
                    "Seule une demande accordée peut être annulée.");

            if (!string.IsNullOrWhiteSpace(motif)) CommentaireRH = motif;
            Statut = CongeStatut.Annulee;
        }

        // ── Calcul durée ──────────────────────────────────────
        private decimal CalculerJoursDemande(
            DateTime dStart, DateTime dEnd, bool ouvrables)
        {
            if (dEnd < dStart) return 0m;
            var dates = Enumerable.Range(0, (dEnd - dStart).Days + 1)
                                  .Select(i => dStart.AddDays(i));

            if (ouvrables)
                dates = dates.Where(dt =>
                    dt.DayOfWeek != DayOfWeek.Saturday &&
                    dt.DayOfWeek != DayOfWeek.Sunday);

            var feries = new XPQuery<JourFerie>(Session)
                .Where(f => f.Date >= dStart && f.Date <= dEnd)
                .Select(f => f.Date)
                .ToHashSet();

            return dates.Count(d => !feries.Contains(d));
        }
    }
}
