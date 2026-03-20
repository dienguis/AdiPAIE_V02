using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
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

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions, XafDisplayName("Paramètres de paie")]
    [DefaultProperty(nameof(DisplayName))]
    public partial class ParametresPaie : BaseObject
    {
        public ParametresPaie(Session session) : base(session) { }

        // ============== Méta / Affichage ==============
        [PersistentAlias("'Paramètres globaux'")]
        public string DisplayName => (string)EvaluateAlias(nameof(DisplayName));

        // ── ONGLET 1 : Général ──────────────────────────────────────
        // ============== SEED / Démo ====================
        [Category("Général")]
        [XafDisplayName("Activer le jeu de données démo")]
        public bool ActiverSeedDemo
        {
            get => actSeed;
            set => SetPropertyValue(nameof(ActiverSeedDemo), ref actSeed, value);
        }
        bool actSeed;

        // ============== Signataire (états / PDF) =======
        [Size(120)]
        [XafDisplayName("Signataire - Nom")]
        [Category("Général")]
        public string SignatureName
        {
            get => signName;
            set => SetPropertyValue(nameof(SignatureName), ref signName, value?.Trim());
        }
        string signName;

        [Size(120)]
        [XafDisplayName("Signataire - Fonction")]
        [Category("Général")]
        public string SignatureTitle
        {
            get => signTitle;
            set => SetPropertyValue(nameof(SignatureTitle), ref signTitle, value?.Trim());
        }
        string signTitle;


        [ImageEditor(ListViewImageEditorCustomHeight = 60, DetailViewImageEditorFixedHeight = 160)]
        [Category("Général")]
        public byte[] LogoImage
        {
            get => logoImage;
            set => SetPropertyValue(nameof(LogoImage), ref logoImage, value);
        }
        private byte[] logoImage;

        [ImageEditor(ListViewImageEditorCustomHeight = 60, DetailViewImageEditorFixedHeight = 160)]
        [Category("Général")]
        public byte[] SignatureImage
        {
            get => signatureImage;
            set => SetPropertyValue(nameof(SignatureImage), ref signatureImage, value);
        }
        private byte[] signatureImage;

        [ImageEditor(ListViewImageEditorCustomHeight = 60, DetailViewImageEditorFixedHeight = 160)]
        [Category("Général")]
        public byte[] CachetImage
        {
            get => cachetImage;
            set => SetPropertyValue(nameof(CachetImage), ref cachetImage, value);
        }
        private byte[] cachetImage;

        [Size(120)]
        [Category("Général")]

        public string SignatoryName
        {
            get => signatoryName;
            set => SetPropertyValue(nameof(SignatoryName), ref signatoryName, value?.Trim());
        }

        private string signatoryName;

        [Size(120)]
        [Category("Général")]

        public string SignatoryTitle
        {
            get => signatoryTitle;
            set => SetPropertyValue(nameof(SignatoryTitle), ref signatoryTitle, value?.Trim());
        }
        private string signatoryTitle;

        // ── ONGLET 2 : Fiscalité ────────────────────────────────────
        // ============== TRIMF : codes barèmes ==========
        // Tu peux mettre un format avec {YYYY} / {YY}, ex: "TRIMF_{YYYY}"
        [Category("Fiscalité")]
        [Size(80)]
        [XafDisplayName("Code barème TRIMF (mensuel)")]
        public string CodeBaremeTRIMF_Mensuel
        {
            get => codeTrimfM;
            set => SetPropertyValue(nameof(CodeBaremeTRIMF_Mensuel), ref codeTrimfM, value?.Trim());
        }
        string codeTrimfM;

        [Category("Fiscalité")]
        [Size(80)]
        [XafDisplayName("Code barème TRIMF (annuel)")]
        public string CodeBaremeTRIMF_Annuel
        {
            get => codeTrimfA;
            set => SetPropertyValue(nameof(CodeBaremeTRIMF_Annuel), ref codeTrimfA, value?.Trim());
        }
        string codeTrimfA;

        // ============== IR : code barème DPP annuel ====
        [Category("Fiscalité")]
        [Size(80)]
        [XafDisplayName("Code barème IR (annuel)")]
        public string CodeBaremeIR_Annuel
        {
            get => codeIrAnnuel;
            set => SetPropertyValue(nameof(CodeBaremeIR_Annuel), ref codeIrAnnuel, value?.Trim());
        }
        string codeIrAnnuel;


        // ============== IR : options de calcul =========
        [Category("Fiscalité")]
        [XafDisplayName("IR – Tronquer la base aux milliers")]
        public bool R_IR_TronquerBaseAuxMille
        {
            get => irTroncMille;
            set => SetPropertyValue(nameof(R_IR_TronquerBaseAuxMille), ref irTroncMille, value);
        }
        bool irTroncMille;

        [Category("Fiscalité")]
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "p0"), ModelDefault("EditMask", "p0")]
        [XafDisplayName("IR – IMAB (% abattement annuel)")]
        public decimal R_IR_Abattement_TauxPercent
        {
            get => irImabPct;
            set => SetPropertyValue(nameof(R_IR_Abattement_TauxPercent), ref irImabPct, value);
        }
        decimal irImabPct;

        [Category("Fiscalité")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IR – IMAB Plafond Annuel")]
        public decimal R_IR_Abattement_PlafondAnnuel
        {
            get => irImabPlafA;
            set => SetPropertyValue(nameof(R_IR_Abattement_PlafondAnnuel), ref irImabPlafA, value);
        }
        decimal irImabPlafA;
        [Category("Fiscalité")]

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IR – IMAB Plafond Mensuel")]
        public decimal R_IR_Abattement_PlafondMensuel
        {
            get => irImabPlafM;
            set => SetPropertyValue(nameof(R_IR_Abattement_PlafondMensuel), ref irImabPlafM, value);
        }
        decimal irImabPlafM;

        [Category("Fiscalité")]
        [XafDisplayName("IR – Régularisation en fin d'année")]
        public bool IR_Regularisation_FinAnnee
        {
            get => irRegFin;
            set => SetPropertyValue(nameof(IR_Regularisation_FinAnnee), ref irRegFin, value);
        }

        bool irRegFin;

        [Category("Fiscalité")]

        [XafDisplayName("IR – Régularisation mois de départ")]
        public bool IR_Regularisation_MoisDepart
        {
            get => irRegDepart;
            set => SetPropertyValue(nameof(IR_Regularisation_MoisDepart), ref irRegDepart, value);
        }
        bool irRegDepart;

        // ============== IR : Réduction familiale =========
        [Category("Fiscalité")]
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "p0"), ModelDefault("EditMask", "p0")]
        [XafDisplayName("Réduction familiale – % par défaut")]
        public decimal R_IR_ReductionFamille_Pourcentage
        {
            get => rfPct;
            set => SetPropertyValue(nameof(R_IR_ReductionFamille_Pourcentage), ref rfPct, value);
        }
        decimal rfPct;

        [Category("Fiscalité")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Réduction familiale – Min annuel / part")]
        public decimal R_IR_ReductionFamille_MinParPart_Annuel
        {
            get => rfMinPartA;
            set => SetPropertyValue(nameof(R_IR_ReductionFamille_MinParPart_Annuel), ref rfMinPartA, value);
        }
        decimal rfMinPartA;

        [Category("Fiscalité")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Réduction familiale – Max annuel / part")]
        public decimal R_IR_ReductionFamille_MaxParPart_Annuel
        {
            get => rfMaxPartA;
            set => SetPropertyValue(nameof(R_IR_ReductionFamille_MaxParPart_Annuel), ref rfMaxPartA, value);
        }
        decimal rfMaxPartA;

        // ── ONGLET 4 : Modèle bulletin ───────────────────────────────
        // ============== Modèle auto à la création salarié =========
        [Category("Modèle bulletin")]
        [XafDisplayName("Créer modèle au Save&Close du salarié")]
        public bool ModeleAuto_CreerAuSave
        {
            get => mdlCreate;
            set => SetPropertyValue(nameof(ModeleAuto_CreerAuSave), ref mdlCreate, value);
        }
        bool mdlCreate;

        [Category("Modèle bulletin")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Défaut – Sursalaire")]
        public decimal? ModeleAuto_Defaut_Sursalaire
        {
            get => mdlDefSur;
            set => SetPropertyValue(nameof(ModeleAuto_Defaut_Sursalaire), ref mdlDefSur, value);
        }
        decimal? mdlDefSur;

        [Category("Modèle bulletin")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Défaut – Prime de transport")]
        public decimal? ModeleAuto_Defaut_PrimeTransport
        {
            get => mdlDefTrans;
            set => SetPropertyValue(nameof(ModeleAuto_Defaut_PrimeTransport), ref mdlDefTrans, value);
        }
        decimal? mdlDefTrans;

        [Category("Modèle bulletin")]
        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Défaut – Avantage véhicule")]
        public decimal? ModeleAuto_Defaut_AvantageVehicule
        {
            get => mdlDefVeh;
            set => SetPropertyValue(nameof(ModeleAuto_Defaut_AvantageVehicule), ref mdlDefVeh, value);
        }
        decimal? mdlDefVeh;

        // Gestion Remboursement
        [Category("Modèle bulletin")]
        [XafDisplayName("Rubrique retenue prêt")]
        public Rubrique RubriqueRetenuePretDefaut
        {
            get => _rPret; set => SetPropertyValue(nameof(RubriqueRetenuePretDefaut), ref _rPret, value);
        }
        Rubrique _rPret;

        [Category("Modèle bulletin")]
        [XafDisplayName("Rubrique retenue avance")]
        public Rubrique RubriqueRetenueAvanceDefaut
        {
            get => _rAv; set => SetPropertyValue(nameof(RubriqueRetenueAvanceDefaut), ref _rAv, value);
        }
        Rubrique _rAv;

        public Rubrique ResolveRubriqueRetenue(PretNature nature)
        {
            if (nature == PretNature.AvanceSalaire && RubriqueRetenueAvanceDefaut != null)
                return RubriqueRetenueAvanceDefaut;
            if (nature == PretNature.Pret && RubriqueRetenuePretDefaut != null)
                return RubriqueRetenuePretDefaut;

            var q = new XPQuery<Rubrique>(Session).Where(r => r.Actif);
            if (nature == PretNature.AvanceSalaire)
            {
                return q.FirstOrDefault(r => r.Canonique == RubriqueCanonique.RemboursementAvance)
                    ?? q.FirstOrDefault(r => (r.Code ?? "").ToUpper() == "AVANCE_SAL");
            }
            return q.FirstOrDefault(r => r.Canonique == RubriqueCanonique.RemboursementPret)
                ?? q.FirstOrDefault(r => (r.Code ?? "").ToUpper() == "PRET");
        }



        // =================== Helpers ====================
        public override void AfterConstruction()
        {
            base.AfterConstruction();

            // Defaults raisonnables
            if (string.IsNullOrWhiteSpace(CodeBaremeTRIMF_Mensuel)) CodeBaremeTRIMF_Mensuel = "TRIMF_{YYYY}";
            if (string.IsNullOrWhiteSpace(CodeBaremeTRIMF_Annuel)) CodeBaremeTRIMF_Annuel = "TRIMF_AN_{YYYY}";
            if (string.IsNullOrWhiteSpace(CodeBaremeIR_Annuel)) CodeBaremeIR_Annuel = "IR_DPP_{YYYY}";

            R_IR_TronquerBaseAuxMille = true;
            R_IR_Abattement_TauxPercent = 30m;        // 30%
            R_IR_Abattement_PlafondAnnuel = 900_000m; // ex Sénégal
            R_IR_Abattement_PlafondMensuel = 75_000m;

            IR_Regularisation_FinAnnee = true;
            IR_Regularisation_MoisDepart = true;

            // Réduction familiale "fallback"
            R_IR_ReductionFamille_Pourcentage = 0m;
            R_IR_ReductionFamille_MinParPart_Annuel = 0m;
            R_IR_ReductionFamille_MaxParPart_Annuel = 0m;

            // Modèle auto
            ModeleAuto_CreerAuSave = true;
            ModeleAuto_Defaut_AvantageVehicule = 20_000m; // ex valeur locale
            ModeleAuto_Defaut_PrimeTransport = 0m;
            ModeleAuto_Defaut_Sursalaire = 0m;

            if (SmtpPort == 0) SmtpPort = 587;
            if (string.IsNullOrWhiteSpace(SmtpHost)) SmtpHost = "smtp.office365.com";
            if (!SmtpUseSsl) SmtpUseSsl = true;

            if (string.IsNullOrWhiteSpace(MailFromDisplayName))
                MailFromDisplayName = "Service Paie";
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            // "Singleton soft" : empêche plusieurs enregistrements
            var existsOther = new XPQuery<ParametresPaie>(Session)
                .Any(p => p.Oid != Oid);
            if (existsOther)
                throw new UserFriendlyException("Un seul enregistrement Paramètres de paie est autorisé.");
        }

        // =================== Méthodes utilitaires publiques ===================

        /// <summary>Renvoie l’année à utiliser. Si year est null → année courante.</summary>
        public int ResolveAnnee(int? year = null) => year ?? DateTime.Today.Year;

        /// <summary>Résout le code TRIMF mensuel pour l’année donnée.</summary>
        public string ResolveCodeTRIMFMensuel(int? year = null)
            => ResolveCodeWithYear(CodeBaremeTRIMF_Mensuel, year, fallbackPrefix: "TRIMF_");

        /// <summary>Résout le code TRIMF annuel pour l’année donnée.</summary>
        public string ResolveCodeTRIMFAnnuel(int? year = null)
            => ResolveCodeWithYear(CodeBaremeTRIMF_Annuel, year, fallbackPrefix: "TRIMF_AN_");

        /// <summary>Résout le code du barème IR annuel (DPP) pour l’année donnée.</summary>
        public string ResolveCodeBaremeIRAnnuel(int? year = null)
            => ResolveCodeWithYear(CodeBaremeIR_Annuel, year, fallbackPrefix: "IR_DPP_");

        private string ResolveCodeWithYear(string patternOrCode, int? year, string fallbackPrefix)
        {
            int y = ResolveAnnee(year);
            string yy = (y % 100).ToString("00");

            if (!string.IsNullOrWhiteSpace(patternOrCode))
            {
                // Support des tokens {YYYY} / {YY}
                return patternOrCode
                    .Replace("{YYYY}", y.ToString())
                    .Replace("{YY}", yy)
                    .Trim();
            }
            return $"{fallbackPrefix}{y}";
        }

        // =================== Helpers statiques ===================

        /// <summary>Récupère (ou crée) le singleton via XPO Session.</summary>
        public static ParametresPaie GetOrCreate(Session session, bool commitIfCreated = true)
        {
            if (session == null) return null;
            var p = new XPQuery<ParametresPaie>(session).FirstOrDefault();
            if (p != null) return p;

            p = new ParametresPaie(session);
            if (commitIfCreated && session.InTransaction)
                session.CommitTransaction();
            return p;
        }

        /// <summary>Récupère (ou crée) le singleton via IObjectSpace.</summary>
        public static ParametresPaie GetOrCreate(IObjectSpace os, bool commitIfCreated = true)
        {
            if (os == null) return null;
            var p = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();
            if (p != null) return p;

            p = os.CreateObject<ParametresPaie>();
            if (commitIfCreated) os.CommitChanges();
            return p;
        }

        /// <summary>Essaye de récupérer (ne crée pas) via Session.</summary>
        public static ParametresPaie TryGet(Session session)
            => session == null ? null : new XPQuery<ParametresPaie>(session).FirstOrDefault();

        /// <summary>Essaye de récupérer (ne crée pas) via IObjectSpace.</summary>
        public static ParametresPaie TryGet(IObjectSpace os)
            => os?.GetObjectsQuery<ParametresPaie>().FirstOrDefault();


        // ── ONGLET 3 : Messagerie ────────────────────────────────────

        // ===================== MESSAGERIE / SMTP =====================
        [Category("Messagerie")]
        [XafDisplayName("Messagerie active")]
        public bool EmailActif
        {
            get => emailActif;
            set => SetPropertyValue(nameof(EmailActif), ref emailActif, value);
        }
        bool emailActif;

        [Category("Messagerie")]
        [Size(200)]
        [XafDisplayName("SMTP - Hôte")]
        public string SmtpHost
        {
            get => smtpHost;
            set => SetPropertyValue(nameof(SmtpHost), ref smtpHost, value?.Trim());
        }
        string smtpHost;

        [Category("Messagerie")]
        [XafDisplayName("SMTP - Port")]
        [RuleRange(1, 65535)]
        public int SmtpPort
        {
            get => smtpPort;
            set => SetPropertyValue(nameof(SmtpPort), ref smtpPort, value);
        }
        int smtpPort;

        [Category("Messagerie")]
        [XafDisplayName("SMTP - SSL/TLS")]
        public bool SmtpUseSsl
        {
            get => smtpSsl;
            set => SetPropertyValue(nameof(SmtpUseSsl), ref smtpSsl, value);
        }
        bool smtpSsl;

        [Size(200)]
        [XafDisplayName("SMTP - Utilisateur")]
        public string SmtpUserName
        {
            get => smtpUser;
            set => SetPropertyValue(nameof(SmtpUserName), ref smtpUser, value?.Trim());
        }
        string smtpUser;

        // NOTE : pour faire simple on stocke en clair.
        // Si tu préfères chiffrer, on peut remplacer par ProtectedContent.
        [Category("Messagerie")]
        [Size(200)]
        [XafDisplayName("SMTP - Mot de passe")]
        [ModelDefault("IsPassword", "True")]
        public string SmtpPassword
        {
            get => smtpPwd;
            set => SetPropertyValue(nameof(SmtpPassword), ref smtpPwd, value);
        }
        string smtpPwd;

        [Category("Messagerie")]
        [Size(200)]
        [XafDisplayName("De (adresse e-mail)")]
        public string MailFromAddress
        {
            get => mailFrom;
            set => SetPropertyValue(nameof(MailFromAddress), ref mailFrom, value?.Trim());
        }
        string mailFrom;

        [Category("Messagerie")]
        [Size(200)]
        [XafDisplayName("De (affiché)")]
        public string MailFromDisplayName
        {
            get => mailFromName;
            set => SetPropertyValue(nameof(MailFromDisplayName), ref mailFromName, value?.Trim());
        }
        string mailFromName;

        [Category("Messagerie")]
        [Size(200)]
        [XafDisplayName("Répondre à (reply-to)")]
        public string MailReplyTo
        {
            get => replyTo;
            set => SetPropertyValue(nameof(MailReplyTo), ref replyTo, value?.Trim());
        }
        string replyTo;

        [Category("Messagerie")]
        [Size(500)]
        [XafDisplayName("BCC par défaut (séparés par ; ou ,)")]
        public string MailBccDefault
        {
            get => bccDefault;
            set => SetPropertyValue(nameof(MailBccDefault), ref bccDefault, value?.Trim());
        }
        string bccDefault;

        // ---- Fabrique l'expéditeur email à partir des paramètres ----
        public IEmailSender CreateEmailSender()
        {
            if (!EmailActif)
                throw new UserFriendlyException("La messagerie n'est pas activée dans Paramètres de paie.");

            if (EmailProviderKind == EmailProvider.Graph)
            {
                if (string.IsNullOrWhiteSpace(GraphTenantId) ||
                    string.IsNullOrWhiteSpace(GraphClientId) ||
                    string.IsNullOrWhiteSpace(GraphClientSecret))
                    throw new UserFriendlyException("Renseignez TenantId / ClientId / ClientSecret (Graph).");

                var fromUpn = string.IsNullOrWhiteSpace(GraphFromUserUpn) ? MailFromAddress : GraphFromUserUpn;
                if (string.IsNullOrWhiteSpace(fromUpn))
                    throw new UserFriendlyException("Renseignez l’expéditeur (MailFromAddress ou GraphFromUserUpn).");

                return new GraphEmailSender(GraphTenantId, GraphClientId, GraphClientSecret, fromUpn);
            }

            // --- Fallback SMTP (existant) ---
            if (string.IsNullOrWhiteSpace(SmtpHost))
                throw new UserFriendlyException("Saisissez l'hôte SMTP dans Paramètres de paie.");
            if (string.IsNullOrWhiteSpace(MailFromAddress))
                throw new UserFriendlyException("Saisissez l'adresse 'De' (MailFromAddress) dans Paramètres de paie.");

            return new SmtpEmailSender(
                host: SmtpHost,
                port: SmtpPort > 0 ? SmtpPort : 587,
                enableSsl: SmtpUseSsl,
                user: SmtpUserName,
                pass: SmtpPassword,
                from: MailFromAddress
            );
        }




        public enum EmailProvider { Smtp = 0, Graph = 1 }

        [XafDisplayName("Fournisseur e-mail")]
        public EmailProvider EmailProviderKind
        {
            get => emailProviderKind;
            set => SetPropertyValue(nameof(EmailProviderKind), ref emailProviderKind, value);
        }
        private EmailProvider emailProviderKind;

        [Size(200)]
        [XafDisplayName("Graph – Tenant ID")]
        public string GraphTenantId { get => graphTenantId; set => SetPropertyValue(nameof(GraphTenantId), ref graphTenantId, value?.Trim()); }
        private string graphTenantId;

        [Size(200)]
        [XafDisplayName("Graph – Client ID (App)")]
        public string GraphClientId { get => graphClientId; set => SetPropertyValue(nameof(GraphClientId), ref graphClientId, value?.Trim()); }
        private string graphClientId;

        [Size(400)]
        [ModelDefault("IsPassword", "True")]
        [XafDisplayName("Graph – Client Secret")]
        public string GraphClientSecret { get => graphClientSecret; set => SetPropertyValue(nameof(GraphClientSecret), ref graphClientSecret, value); }
        private string graphClientSecret;

        [Size(200)]
        [XafDisplayName("Graph – Envoyer en tant que (UPN)")]
        public string GraphFromUserUpn { get => graphFromUpn; set => SetPropertyValue(nameof(GraphFromUserUpn), ref graphFromUpn, value?.Trim()); }
        private string graphFromUpn;



        // ── Alertes Attestation ───────────────────────────────────────

        /// <summary>
        /// Délai en jours avant qu'une demande "Soumise" génère une alerte RH.
        /// 0 = alertes désactivées.
        /// </summary>
        [Category("GRH - Alertes")]
        [XafDisplayName("Délai alerte attestation (jours)")]
        [ModelDefault("DisplayFormat", "N0")]
        [RuleRange("ParametresPaie_DelaiAlerte_Range", DefaultContexts.Save, 0, 365,
            CustomMessageTemplate = "Le délai doit être entre 0 et 365 jours.")]
        public int DelaiAlertAttestationJours
        {
            get => delaiAlertAttestation;
            set => SetPropertyValue(nameof(DelaiAlertAttestationJours), ref delaiAlertAttestation, value);
        }
        int delaiAlertAttestation = 2; // valeur par défaut : 2 jours

        /// <summary>
        /// Adresse email du ou des responsables RH qui reçoivent les alertes.
        /// Plusieurs adresses séparées par un point-virgule.
        /// Ex : rh@company.com;drh@company.com
        /// </summary>
        [Category("GRH - Alertes")]
        [XafDisplayName("Email(s) RH pour alertes (séparés par ;)")]
        [Size(500)]
        public string EmailsRHAlertes
        {
            get => emailsRHAlertes;
            set => SetPropertyValue(nameof(EmailsRHAlertes), ref emailsRHAlertes, value?.Trim());
        }
        string emailsRHAlertes;

        /// <summary>
        /// Heure de déclenchement du service d'alertes (0-23).
        /// Par défaut : 8h du matin.
        /// </summary>
        [Category("GRH - Alertes")]
        [XafDisplayName("Heure d'envoi des alertes (0-23)")]
        [RuleRange("ParametresPaie_HeureAlerte_Range", DefaultContexts.Save, 0, 23)]
        public int HeureEnvoiAlertes
        {
            get => heureEnvoiAlertes;
            set => SetPropertyValue(nameof(HeureEnvoiAlertes), ref heureEnvoiAlertes, value);
        }
        int heureEnvoiAlertes = 8;


        // ── Templates d'attestation ───────────────────────────────────

        /// <summary>
        /// Template Word (.docx) pour l'attestation de travail.
        /// Le RH uploade ici le fichier Word avec les marqueurs {{...}}.
        /// </summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template attestation de travail (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateAttestation
        {
            get => templateAttestation;
            set => SetPropertyValue(nameof(TemplateAttestation), ref templateAttestation, value);
        }
        FileData templateAttestation;

        /// <summary>
        /// Template Word pour l'attestation de salaire.
        /// </summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template attestation de congés (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateAttestationDeConges
        {
            get => templateAttestationDeConges;
            set => SetPropertyValue(nameof(TemplateAttestationDeConges), ref templateAttestationDeConges, value);
        }
        FileData templateAttestationDeConges;

        /// <summary>
        /// Template Word pour le certificat d'emploi (fin de contrat).
        /// </summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template certificat d'emploi (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateCertificatEmploi
        {
            get => templateCertificatEmploi;
            set => SetPropertyValue(nameof(TemplateCertificatEmploi), ref templateCertificatEmploi, value);
        }
        FileData templateCertificatEmploi;

        /// <summary>
        /// Template Word pour le cessation de paiement
        /// </summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template cessation de paiement (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateCessationPaiement
        {
            get => templateCessationPaiement;
            set => SetPropertyValue(nameof(TemplateCessationPaiement), ref templateCessationPaiement, value);
        }
        FileData templateCessationPaiement;

        // ── Template entretien annuel ─────────────────────────────

        /// <summary>
        /// Template Word (.docx) pour la fiche d'entretien annuel.
        /// Le RH uploade ici le fichier Word avec les marqueurs {{...}}.
        /// </summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template entretien annuel (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateEntretienAnnuel
        {
            get => templateEntretienAnnuel;
            set => SetPropertyValue(nameof(TemplateEntretienAnnuel), ref templateEntretienAnnuel, value);
        }
        FileData templateEntretienAnnuel;


        /// <summary>Email du DAF pour les notifications d'ordre de mission approuvé.</summary>
        [Category("GRH - Alertes")]
        [XafDisplayName("Email DAF (ordre de mission)")]
        [Size(300)]
        public string EmailDAF
        {
            get => emailDAF;
            set => SetPropertyValue(nameof(EmailDAF), ref emailDAF, value?.Trim());
        }
        string emailDAF;

        /// <summary>Email(s) du/des comptable(s) pour les notifications de décaissement.</summary>
        [Category("GRH - Alertes")]
        [XafDisplayName("Email(s) Comptable (séparés par ;)")]
        [Size(300)]
        public string EmailsComptable
        {
            get => emailsComptable;
            set => SetPropertyValue(nameof(EmailsComptable), ref emailsComptable, value?.Trim());
        }
        string emailsComptable;

        // ============================================================
        // Ajouter dans la section GRH - Templates :
        // ============================================================

        /// <summary>Template Word pour l'ordre de mission.</summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template ordre de mission (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateOrdreMission
        {
            get => templateOrdreMission;
            set => SetPropertyValue(nameof(TemplateOrdreMission), ref templateOrdreMission, value);
        }
        FileData templateOrdreMission;
        /// <summary>
        /// Template Word pour l'ÉTAT DE FRAIS (note comptable après retour).
        /// Contient la ligne modèle {{#FRAIS_ROW}} pour les frais dynamiques.
        /// Uploader le fichier Template_Etat_Frais_Mission.docx
        /// </summary>
        [Category("GRH - Templates")]
        [XafDisplayName("Template état de frais de mission (.docx)")]
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [FileTypeFilter("Documents Word", "*.docx")]
        public FileData TemplateEtatFrais
        {
            get => templateEtatFrais;
            set => SetPropertyValue(nameof(TemplateEtatFrais),
                ref templateEtatFrais, value);
        }
        FileData templateEtatFrais;


    }

}
