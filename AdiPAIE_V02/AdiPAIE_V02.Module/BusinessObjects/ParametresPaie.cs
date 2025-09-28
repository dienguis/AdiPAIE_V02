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

        // ============== SEED / Démo ====================
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
        public string SignatureName
        {
            get => signName;
            set => SetPropertyValue(nameof(SignatureName), ref signName, value?.Trim());
        }
        string signName;

        [Size(120)]
        [XafDisplayName("Signataire - Fonction")]
        public string SignatureTitle
        {
            get => signTitle;
            set => SetPropertyValue(nameof(SignatureTitle), ref signTitle, value?.Trim());
        }
        string signTitle;

        // ============== TRIMF : codes barèmes ==========
        // Tu peux mettre un format avec {YYYY} / {YY}, ex: "TRIMF_{YYYY}"
        [Size(80)]
        [XafDisplayName("Code barème TRIMF (mensuel)")]
        public string CodeBaremeTRIMF_Mensuel
        {
            get => codeTrimfM;
            set => SetPropertyValue(nameof(CodeBaremeTRIMF_Mensuel), ref codeTrimfM, value?.Trim());
        }
        string codeTrimfM;

        [Size(80)]
        [XafDisplayName("Code barème TRIMF (annuel)")]
        public string CodeBaremeTRIMF_Annuel
        {
            get => codeTrimfA;
            set => SetPropertyValue(nameof(CodeBaremeTRIMF_Annuel), ref codeTrimfA, value?.Trim());
        }
        string codeTrimfA;

        // ============== IR : code barème DPP annuel ====
        [Size(80)]
        [XafDisplayName("Code barème IR (annuel)")]
        public string CodeBaremeIR_Annuel
        {
            get => codeIrAnnuel;
            set => SetPropertyValue(nameof(CodeBaremeIR_Annuel), ref codeIrAnnuel, value?.Trim());
        }
        string codeIrAnnuel;

        // ============== IR : options de calcul =========
        [XafDisplayName("IR – Tronquer la base aux milliers")]
        public bool R_IR_TronquerBaseAuxMille
        {
            get => irTroncMille;
            set => SetPropertyValue(nameof(R_IR_TronquerBaseAuxMille), ref irTroncMille, value);
        }
        bool irTroncMille;

        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "p0"), ModelDefault("EditMask", "p0")]
        [XafDisplayName("IR – IMAB (% abattement annuel)")]
        public decimal R_IR_Abattement_TauxPercent
        {
            get => irImabPct;
            set => SetPropertyValue(nameof(R_IR_Abattement_TauxPercent), ref irImabPct, value);
        }
        decimal irImabPct;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IR – IMAB Plafond Annuel")]
        public decimal R_IR_Abattement_PlafondAnnuel
        {
            get => irImabPlafA;
            set => SetPropertyValue(nameof(R_IR_Abattement_PlafondAnnuel), ref irImabPlafA, value);
        }
        decimal irImabPlafA;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IR – IMAB Plafond Mensuel")]
        public decimal R_IR_Abattement_PlafondMensuel
        {
            get => irImabPlafM;
            set => SetPropertyValue(nameof(R_IR_Abattement_PlafondMensuel), ref irImabPlafM, value);
        }
        decimal irImabPlafM;

        [XafDisplayName("IR – Régularisation en fin d'année")]
        public bool IR_Regularisation_FinAnnee
        {
            get => irRegFin;
            set => SetPropertyValue(nameof(IR_Regularisation_FinAnnee), ref irRegFin, value);
        }
        bool irRegFin;

        [XafDisplayName("IR – Régularisation mois de départ")]
        public bool IR_Regularisation_MoisDepart
        {
            get => irRegDepart;
            set => SetPropertyValue(nameof(IR_Regularisation_MoisDepart), ref irRegDepart, value);
        }
        bool irRegDepart;

        // ============== IR : Réduction familiale =========
        [DbType("decimal(18,2)")]
        [ModelDefault("DisplayFormat", "p0"), ModelDefault("EditMask", "p0")]
        [XafDisplayName("Réduction familiale – % par défaut")]
        public decimal R_IR_ReductionFamille_Pourcentage
        {
            get => rfPct;
            set => SetPropertyValue(nameof(R_IR_ReductionFamille_Pourcentage), ref rfPct, value);
        }
        decimal rfPct;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Réduction familiale – Min annuel / part")]
        public decimal R_IR_ReductionFamille_MinParPart_Annuel
        {
            get => rfMinPartA;
            set => SetPropertyValue(nameof(R_IR_ReductionFamille_MinParPart_Annuel), ref rfMinPartA, value);
        }
        decimal rfMinPartA;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Réduction familiale – Max annuel / part")]
        public decimal R_IR_ReductionFamille_MaxParPart_Annuel
        {
            get => rfMaxPartA;
            set => SetPropertyValue(nameof(R_IR_ReductionFamille_MaxParPart_Annuel), ref rfMaxPartA, value);
        }
        decimal rfMaxPartA;

        // ============== Modèle auto à la création salarié =========
        [XafDisplayName("Créer modèle au Save&Close du salarié")]
        public bool ModeleAuto_CreerAuSave
        {
            get => mdlCreate;
            set => SetPropertyValue(nameof(ModeleAuto_CreerAuSave), ref mdlCreate, value);
        }
        bool mdlCreate;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Défaut – Sursalaire")]
        public decimal? ModeleAuto_Defaut_Sursalaire
        {
            get => mdlDefSur;
            set => SetPropertyValue(nameof(ModeleAuto_Defaut_Sursalaire), ref mdlDefSur, value);
        }
        decimal? mdlDefSur;

        [DbType("decimal(18,0)")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Défaut – Prime de transport")]
        public decimal? ModeleAuto_Defaut_PrimeTransport
        {
            get => mdlDefTrans;
            set => SetPropertyValue(nameof(ModeleAuto_Defaut_PrimeTransport), ref mdlDefTrans, value);
        }
        decimal? mdlDefTrans;

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
        public Rubrique RubriqueRetenuePretDefaut
        {
            get => _rPret; set => SetPropertyValue(nameof(RubriqueRetenuePretDefaut), ref _rPret, value);
        }
        Rubrique _rPret;

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



        // ===================== MESSAGERIE / SMTP =====================
        [XafDisplayName("Messagerie active")]
        public bool EmailActif
        {
            get => emailActif;
            set => SetPropertyValue(nameof(EmailActif), ref emailActif, value);
        }
        bool emailActif;

        [Size(200)]
        [XafDisplayName("SMTP - Hôte")]
        public string SmtpHost
        {
            get => smtpHost;
            set => SetPropertyValue(nameof(SmtpHost), ref smtpHost, value?.Trim());
        }
        string smtpHost;

        [XafDisplayName("SMTP - Port")]
        [RuleRange(1, 65535)]
        public int SmtpPort
        {
            get => smtpPort;
            set => SetPropertyValue(nameof(SmtpPort), ref smtpPort, value);
        }
        int smtpPort;

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
        [Size(200)]
        [XafDisplayName("SMTP - Mot de passe")]
        [ModelDefault("IsPassword", "True")]
        public string SmtpPassword
        {
            get => smtpPwd;
            set => SetPropertyValue(nameof(SmtpPassword), ref smtpPwd, value);
        }
        string smtpPwd;

        [Size(200)]
        [XafDisplayName("De (adresse e-mail)")]
        public string MailFromAddress
        {
            get => mailFrom;
            set => SetPropertyValue(nameof(MailFromAddress), ref mailFrom, value?.Trim());
        }
        string mailFrom;

        [Size(200)]
        [XafDisplayName("De (affiché)")]
        public string MailFromDisplayName
        {
            get => mailFromName;
            set => SetPropertyValue(nameof(MailFromDisplayName), ref mailFromName, value?.Trim());
        }
        string mailFromName;

        [Size(200)]
        [XafDisplayName("Répondre à (reply-to)")]
        public string MailReplyTo
        {
            get => replyTo;
            set => SetPropertyValue(nameof(MailReplyTo), ref replyTo, value?.Trim());
        }
        string replyTo;

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
        [ImageEditor(ListViewImageEditorCustomHeight = 60, DetailViewImageEditorFixedHeight = 160)]
        public byte[] LogoImage
        {
            get => logoImage;
            set => SetPropertyValue(nameof(LogoImage), ref logoImage, value);
        }
        private byte[] logoImage;

        [ImageEditor(ListViewImageEditorCustomHeight = 60, DetailViewImageEditorFixedHeight = 160)]
        public byte[] SignatureImage
        {
            get => signatureImage;
            set => SetPropertyValue(nameof(SignatureImage), ref signatureImage, value);
        }
        private byte[] signatureImage;

        [ImageEditor(ListViewImageEditorCustomHeight = 60, DetailViewImageEditorFixedHeight = 160)]
        public byte[] CachetImage
        {
            get => cachetImage;
            set => SetPropertyValue(nameof(CachetImage), ref cachetImage, value);
        }
        private byte[] cachetImage;

        [Size(120)]
        public string SignatoryName
        {
            get => signatoryName;
            set => SetPropertyValue(nameof(SignatoryName), ref signatoryName, value?.Trim());
        }
        private string signatoryName;

        [Size(120)]
        public string SignatoryTitle
        {
            get => signatoryTitle;
            set => SetPropertyValue(nameof(SignatoryTitle), ref signatoryTitle, value?.Trim());
        }
        private string signatoryTitle;

   

    
 

    }
}
