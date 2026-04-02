using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    public enum ModeVirement
    {
        [XafDisplayName("Montant fixe (FCFA)")]
        MontantFixe = 0,
        [XafDisplayName("Pourcentage (%)")]
        Pourcentage = 1,
        [XafDisplayName("Reliquat")]
        Reliquat = 2,
    }

    /// <summary>
    /// Compte bancaire de domiciliation d'un salarié.
    /// Mode : MontantFixe, Pourcentage ou Reliquat (un seul reliquat par salarié).
    /// Le champ Valeur contient le montant FCFA ou le % selon le mode choisi.
    /// Si Mode = Reliquat, Valeur est ignoré — le compte reçoit Net - déjà alloué.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Compte bancaire")]
    [DefaultProperty(nameof(DisplayCompte))]
    [Appearance("Compte_Reliquat_DisableValeur",
        Criteria = "Mode = 2",
        TargetItems = "Valeur",
        Enabled = false)]
    [RuleCriteria("Compte_ValeurRequise", DefaultContexts.Save,
        "Mode = 2 OR Valeur > 0",
        CustomMessageTemplate = "Saisissez un montant ou un pourcentage selon le mode choisi.")]
    [RuleCriteria("Compte_PourcentageMax", DefaultContexts.Save,
        "Mode <> 1 OR Valeur <= 100",
        CustomMessageTemplate = "Le pourcentage ne peut pas dépasser 100%.")]
    public class CompteBancaireSalarie : BaseObject
    {
        public CompteBancaireSalarie(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Actif = true;
            Mode = ModeVirement.MontantFixe;
        }

        // ── Salarié (côté enfant de l'association) ────────────────
        [Association("Salarie-ComptesBancaires")]
        [Browsable(false)]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Banque ────────────────────────────────────────────────
        [RuleRequiredField]
        [Size(100)]
        [XafDisplayName("Banque")]
        public string Banque
        {
            get => banque;
            set => SetPropertyValue(nameof(Banque), ref banque, value?.Trim());
        }
        string banque;

        [Size(10)]
        [XafDisplayName("Code banque")]
        public string CodeBanque
        {
            get => codeBanque;
            set => SetPropertyValue(nameof(CodeBanque), ref codeBanque, value?.Trim());
        }
        string codeBanque;

        [Size(10)]
        [XafDisplayName("Code guichet")]
        public string CodeGuichet
        {
            get => codeGuichet;
            set => SetPropertyValue(nameof(CodeGuichet), ref codeGuichet, value?.Trim());
        }
        string codeGuichet;

        [RuleRequiredField]
        [Size(30)]
        [XafDisplayName("Numéro de compte")]
        public string NumeroCompte
        {
            get => numeroCompte;
            set => SetPropertyValue(nameof(NumeroCompte), ref numeroCompte, value?.Trim());
        }
        string numeroCompte;

        [Size(5)]
        [XafDisplayName("Clé RIB")]
        public string CleRib
        {
            get => cleRib;
            set => SetPropertyValue(nameof(CleRib), ref cleRib, value?.Trim());
        }
        string cleRib;

        [Size(11)]
        [XafDisplayName("BIC / SWIFT")]
        public string BIC
        {
            get => bic;
            set => SetPropertyValue(nameof(BIC), ref bic, value?.Trim()?.ToUpperInvariant());
        }
        string bic;

        [Size(100)]
        [XafDisplayName("Nom du titulaire")]
        public string NomTitulaire
        {
            get => nomTitulaire;
            set => SetPropertyValue(nameof(NomTitulaire), ref nomTitulaire, value?.Trim());
        }
        string nomTitulaire;

        // ── Mode de répartition ───────────────────────────────────
        /// <summary>
        /// Nullable requis par XPO pour les enums dans les RuleCriteria.
        /// MontantFixe : Valeur = montant FCFA
        /// Pourcentage : Valeur = % (0-100)
        /// Reliquat    : Valeur ignoré, reçoit Net - déjà alloué
        /// </summary>
        [RuleRequiredField]
        [XafDisplayName("Mode")]
        [ImmediatePostData]
        public ModeVirement? Mode
        {
            get => mode;
            set
            {
                SetPropertyValue(nameof(Mode), ref mode, value);
                if (!IsLoading && value == ModeVirement.Reliquat)
                    Valeur = 0;
            }
        }
        ModeVirement? mode;

        /// <summary>
        /// Montant FCFA si Mode=MontantFixe, pourcentage 1-100 si Mode=Pourcentage.
        /// Laissé à 0 si Mode=Reliquat (désactivé par Appearance).
        /// </summary>
        [XafDisplayName("Valeur")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("EditMask", "N0")]
        public decimal Valeur
        {
            get => valeur;
            set => SetPropertyValue(nameof(Valeur), ref valeur, value);
        }
        decimal valeur;

        [XafDisplayName("Actif")]
        public bool Actif
        {
            get => actif;
            set => SetPropertyValue(nameof(Actif), ref actif, value);
        }
        bool actif;

        [Size(200)]
        [XafDisplayName("Observations")]
        public string Observations
        {
            get => observations;
            set => SetPropertyValue(nameof(Observations), ref observations, value?.Trim());
        }
        string observations;

        // ── Affichage ─────────────────────────────────────────────
        [NonPersistent]
        [XafDisplayName("Répartition")]
        public string DisplayRepartition
        {
            get
            {
                return Mode switch
                {
                    ModeVirement.MontantFixe => $"{Valeur:N0} FCFA",
                    ModeVirement.Pourcentage => $"{Valeur:N0} %",
                    ModeVirement.Reliquat => "Reliquat",
                    _ => "—"
                };
            }
        }

        [NonPersistent]
        public string DisplayCompte =>
            $"{Banque ?? "—"} — {NumeroCompte ?? "—"} ({DisplayRepartition})";

        public override string ToString() => DisplayCompte;

        // ── Calcul montant effectif ───────────────────────────────
        /// <summary>
        /// Calcule le montant à virer sur ce compte.
        /// netAPayer : net total du bulletin
        /// dejaAlloue : somme déjà allouée aux comptes précédents
        /// </summary>
        public decimal CalculerMontant(decimal netAPayer, decimal dejaAlloue)
        {
            if (!Actif) return 0m;
            return Mode switch
            {
                ModeVirement.MontantFixe =>
                    Math.Min(Valeur, Math.Max(0m, netAPayer - dejaAlloue)),
                ModeVirement.Pourcentage =>
                    Math.Round(netAPayer * Valeur / 100m, 0, MidpointRounding.AwayFromZero),
                ModeVirement.Reliquat =>
                    Math.Max(0m, netAPayer - dejaAlloue),
                _ => 0m
            };
        }
    }
}
