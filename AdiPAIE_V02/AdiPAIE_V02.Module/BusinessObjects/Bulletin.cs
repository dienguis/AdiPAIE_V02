using AdiPAIE_V02.Module.Domain;
using DevExpress.Data.Filtering;
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_Person")]
    [OptimisticLocking(true)]
    [RuleCombinationOfPropertiesIsUnique(
        "Bulletin_Salarie_Annee_Mois_Unique", DefaultContexts.Save,
        "Salarie;Annee;Mois",
        CustomMessageTemplate = "Un bulletin existe déjà pour ce salarié sur cette période ({Annee}/{Mois}).")]
    [Appearance(
    "Bulletin_ReadOnly_When_Closed",
    AppearanceItemType = "ViewItem",
    TargetItems = "*",
    Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Cloture#",
    Enabled = false,
    Context = "DetailView"
)]
    // V1.6.2 — Badge statut coloré sur le bulletin (workflow paie)
    [Appearance("Bulletin_Statut_Brouillon",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Brouillon#",
        BackColor = "Gainsboro", FontColor = "DimGray", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Bulletin_Statut_Valide",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Valide#",
        BackColor = "LightSkyBlue", FontColor = "DarkBlue", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Bulletin_Statut_Envoye",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Envoye#",
        BackColor = "PaleGreen", FontColor = "DarkGreen", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Bulletin_Statut_Comptabilise",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Comptabilise#",
        BackColor = "Plum", FontColor = "Indigo", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    [Appearance("Bulletin_Statut_Cloture",
        TargetItems = "Statut",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+BulletinStatut,Cloture#",
        BackColor = "DarkGray", FontColor = "White", FontStyle = DevExpress.Drawing.DXFontStyle.Bold)]
    public class Bulletin : BaseObject
    {
        public Bulletin(Session session) : base(session) { }

        // Bornes du mois
        public DateTime MoisStart => new DateTime(Annee, Mois, 1);
        public DateTime MoisEnd => new DateTime(Annee, Mois, DateTime.DaysInMonth(Annee, Mois));

        // Helpers
        private static decimal N(decimal? v) => v ?? 0m;
        private static decimal N(decimal v) => v;

        // --- Identité / Période ---
        Salarie salarie;
        [RuleRequiredField]
        [Association("Salarie-Bulletins")]
        public Salarie Salarie
        {
            get => salarie;
            set
            {
                if (SetPropertyValue(nameof(Salarie), ref salarie, value) && salarie != null)
                {
                    var parts = salarie.NombrePartsFiscales;
                    if (parts <= 0) parts = 1m;
                    NombrePartsFiscales = parts;
                }
            }
        }

        int annee;
        [RuleRange(2000, 2100)]
        public int Annee { get => annee; set => SetPropertyValue(nameof(Annee), ref annee, value); }

        int mois; // 1..12
        [RuleRange(1, 12)]
        public int Mois { get => mois; set => SetPropertyValue(nameof(Mois), ref mois, value); }

        public DateTime? DateDebut { get => dateDebut; set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value); }
        DateTime? dateDebut;

        public DateTime? DateFin { get => dateFin; set => SetPropertyValue(nameof(DateFin), ref dateFin, value); }
        DateTime? dateFin;

        [PersistentAlias("Concat(Iif(Mois < 10, Concat('0', ToStr(Mois)), ToStr(Mois)),'/',ToStr(Annee))")]
        public string Periode
        {
            get
            {
                try { return (string)EvaluateAlias(nameof(Periode)); }
                catch (ObjectDisposedException) { return $"{Mois:00}/{Annee}"; }
            }
        }

        // (On évite l'attribut [Indexed] ici pour compatibilité large)
        public string PeriodeKey => $"{Salarie?.Oid}|{Annee}|{Mois}";

        BulletinStatut statut = BulletinStatut.Brouillon;
        public BulletinStatut Statut { get => statut; set => SetPropertyValue(nameof(Statut), ref statut, value); }

        // --- Jours travaillés (prorata) ---
        // Valeur par défaut = 30. Modifiable par le RH pour proratiser le salaire
        // (recrue en cours de mois, arrêt pour faute, absence non payée, etc.)
        // sans toucher à la fiche salarié (Salarie.Base30Jour reste intact).
        int joursTravailles = 30;
        [XafDisplayName("Jours travaillés")]
        [ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [RuleRange(0, 30, CustomMessageTemplate = "Les jours travaillés doivent être entre 0 et 30.")]
        public int JoursTravailles
        {
            get => joursTravailles;
            set => SetPropertyValue(nameof(JoursTravailles), ref joursTravailles, value);
        }

        // --- Totaux ---
        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal TotalGains { get => totalGains; set => SetPropertyValue(nameof(TotalGains), ref totalGains, value); }
        decimal totalGains;

        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal TotalRetenuesFiscales { get => totalRetenuesFiscales; set => SetPropertyValue(nameof(TotalRetenuesFiscales), ref totalRetenuesFiscales, value); }
        decimal totalRetenuesFiscales;

        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal TotalCotisationsSociales { get => totalCotisationsSociales; set => SetPropertyValue(nameof(TotalCotisationsSociales), ref totalCotisationsSociales, value); }
        decimal totalCotisationsSociales;

        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal TotalAutresRetenues { get => totalAutresRetenues; set => SetPropertyValue(nameof(TotalAutresRetenues), ref totalAutresRetenues, value); }
        decimal totalAutresRetenues;

        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal BrutFiscal { get => brutFiscal; set => SetPropertyValue(nameof(BrutFiscal), ref brutFiscal, value); }
        decimal brutFiscal;

        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal BrutSocial { get => brutSocial; set => SetPropertyValue(nameof(BrutSocial), ref brutSocial, value); }
        decimal brutSocial;

        [DbType("decimal(18,0)"), EditorAlias(EditorAliases.DecimalPropertyEditor)]
        public decimal NetAPayer { get => netAPayer; set => SetPropertyValue(nameof(NetAPayer), ref netAPayer, value); }
        decimal netAPayer;

        [PersistentAlias("Concat(Periode, ' — ', DisplayName)")]
        [Browsable(true)]
        [ModelDefault("AllowEdit", "False")]
        public string HeaderCompact
        {
            get
            {
                try { return (string)EvaluateAlias(nameof(HeaderCompact)); }
                catch
                {
                    // Fallback au cas où l’évaluation se fait côté client
                    return $"{Periode} — {DisplayName}";
                }
            }
        }

        // Lignes + échéances prélevées
        [Association("Bulletin-Lignes"), Aggregated]
        public XPCollection<BulletinLigne> Lignes => GetCollection<BulletinLigne>(nameof(Lignes));

        [Association("Bulletin-PretsPreleves")]
        public XPCollection<PretEcheance> EcheancesPrelevees => GetCollection<PretEcheance>(nameof(EcheancesPrelevees));

        [Association("Bulletin-HeuresSupplementaires"), Aggregated]
        public XPCollection<HeureSupplementaire> HeuresSupplementaires => GetCollection<HeureSupplementaire>(nameof(HeuresSupplementaires));

        [NonPersistent]
        public string DisplayName => $"{Salarie?.FullName} - {Periode}";

        // --- Synthèse fiscale (mois & cumul annuel) ---
        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("TRIMF (mois)")]
        public decimal TRIMF_Mois { get => trimfMois; set => SetPropertyValue(nameof(TRIMF_Mois), ref trimfMois, value); }
        private decimal trimfMois;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IR (mois)")]
        public decimal IR_Mois { get => irMois; set => SetPropertyValue(nameof(IR_Mois), ref irMois, value); }
        private decimal irMois;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul TRIMF (YTD)")]
        public decimal TRIMF_CumulAnnee { get => trimfCumul; set => SetPropertyValue(nameof(TRIMF_CumulAnnee), ref trimfCumul, value); }
        private decimal trimfCumul;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul IR (YTD)")]
        public decimal IR_CumulAnnee { get => irCumul; set => SetPropertyValue(nameof(IR_CumulAnnee), ref irCumul, value); }
        private decimal irCumul;

        // --- IPRES (parts salariales) ---
        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IPRES RG (mois)")]
        public decimal IPRES_RG_Mois { get => ipresRgMois; set => SetPropertyValue(nameof(IPRES_RG_Mois), ref ipresRgMois, value); }
        private decimal ipresRgMois;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul IPRES RG (YTD)")]
        public decimal IPRES_RG_CumulAnnee { get => ipresRgCumul; set => SetPropertyValue(nameof(IPRES_RG_CumulAnnee), ref ipresRgCumul, value); }
        private decimal ipresRgCumul;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("IPRES RC (mois)")]
        public decimal IPRES_RC_Mois { get => ipresRcMois; set => SetPropertyValue(nameof(IPRES_RC_Mois), ref ipresRcMois, value); }
        private decimal ipresRcMois;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul IPRES RC (YTD)")]
        [ModelDefault("AllowEdit", "False")]
        public decimal IPRES_RC_CumulAnnee { get => ipresRcCumul; set => SetPropertyValue(nameof(IPRES_RC_CumulAnnee), ref ipresRcCumul, value); }
        private decimal ipresRcCumul;

        // --- Cumuls YTD (mois inclus) ---
        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul Brut Fiscal (YTD)")]
        [ModelDefault("AllowEdit", "False")]
        public decimal BrutFiscal_CumulAnnee { get => bfCumul; set => SetPropertyValue(nameof(BrutFiscal_CumulAnnee), ref bfCumul, value); }
        private decimal bfCumul;

        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul Brut Social (YTD)")]
        [ModelDefault("AllowEdit", "False")]
        public decimal BrutSocial_CumulAnnee { get => bsCumul; set => SetPropertyValue(nameof(BrutSocial_CumulAnnee), ref bsCumul, value); }
        private decimal bsCumul;

        // --- Cumul Net à payer (YTD) ---
        [DbType("decimal(18,0)"), ModelDefault("DisplayFormat", "N0"), ModelDefault("EditMask", "N0")]
        [XafDisplayName("Cumul Net à payer (YTD)")]
        [ModelDefault("AllowEdit", "False")]
        public decimal NetAPayer_CumulAnnee { get => netCumul; set => SetPropertyValue(nameof(NetAPayer_CumulAnnee), ref netCumul, value); }
        private decimal netCumul;

        //Archivage PDF
        private FileData _pdfArchive;
        [Aggregated, ExpandObjectMembers(ExpandObjectMembers.Never)]
        [VisibleInListView(false), VisibleInDetailView(false)]
        [Browsable(false)] // QW3 — empêche apparition dans rapports auto-générés
        public FileData PdfArchive
        {
            get => _pdfArchive;
            set => SetPropertyValue(nameof(PdfArchive), ref _pdfArchive, value);
        }

        // ── V1.4.3 — Métadonnées de publication ───────────────────────
        // Renseignés par BulletinPublicationService.Publier() au moment où
        // RH publie le bulletin (statut → Envoye). Audit trail + UI.
        private DateTime? _datePublication;
        [XafDisplayName("Date publication")]
        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DatePublication
        {
            get => _datePublication;
            set => SetPropertyValue(nameof(DatePublication), ref _datePublication, value);
        }

        private string _publieParUser;
        [Size(100)]
        [XafDisplayName("Publié par")]
        [VisibleInListView(false)]
        [ModelDefault("AllowEdit", "False")]
        public string PublieParUser
        {
            get => _publieParUser;
            set => SetPropertyValue(nameof(PublieParUser), ref _publieParUser, value);
        }

        /// <summary>
        /// True si le bulletin est consultable par le salarié dans son Espace
        /// Salarié. Couvre tous les statuts ≥ Envoye (publié, comptabilisé,
        /// clôturé). Utilisé comme critère de filtre dans Bulletin_EspaceSalarie_ListView.
        /// </summary>
        [NonPersistent]
        [VisibleInListView(false), VisibleInDetailView(false)]
        public bool EstPublie =>
            Statut >= AdiPAIE_V02.Module.Domain.DomainEnums.BulletinStatut.Envoye;

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (Annee == 0) Annee = first.Year;
            if (Mois == 0) Mois = first.Month;

            if (!DateDebut.HasValue || !DateFin.HasValue)
            {
                DateDebut = new DateTime(Annee, Mois, 1);
                DateFin = DateDebut.Value.AddMonths(1).AddDays(-1);
            }
            if (NombrePartsFiscales <= 0m) NombrePartsFiscales = 1m;

            // Jours travaillés : 30 par défaut, sera ajusté si prorata détecté
            if (joursTravailles <= 0) JoursTravailles = 30;
        }

        // NOTE : Le recalcul est géré par BulletinRecalcController (bouton "Recalculer" en toolbar)
        // Les méthodes [Action] ci-dessous ont été supprimées car redondantes.

        // ── Initialisation intelligente des jours travaillés ─────────────
        // Appelé dans RecalculerCotisationsEtTotaux(depuisParametrage=true).
        // À ce stade Salarie, Annee et Mois sont tous renseignés.
        //
        // Règle : on ne touche à JoursTravailles QUE si le RH ne l'a pas
        // déjà modifié manuellement. Le marqueur est simple :
        //   - JoursTravailles == 30 (ou 0) → on peut ajuster (prorata)
        //   - JoursTravailles != 30 et != 0 → le RH a touché → on respecte
        //
        // Détection prorata premier bulletin :
        //   - DateEmbauche tombe dans le mois du bulletin
        //   - Aucun bulletin antérieur pour ce salarié
        //   → JoursTravailles = 30 - (jourEmbauche - 1)
        private void InitJoursTravailles()
        {
            if (Salarie == null || Annee <= 0 || Mois <= 0) return;

            // Si le RH a manuellement mis une valeur différente de 30 → on respecte
            if (JoursTravailles != 30 && JoursTravailles != 0) return;

            // Base par défaut depuis la fiche salarié
            var base30 = Salarie.Base30Jour > 0 ? Salarie.Base30Jour : 30;
            JoursTravailles = base30;

            // Détection prorata : embauche en cours de mois
            var embauche = Salarie.DateEmbauche;
            if (embauche != default
                && embauche.Year == Annee
                && embauche.Month == Mois
                && embauche.Day > 1)
            {
                // Premier bulletin ? Vérifier qu'il n'y a pas de bulletin antérieur
                var dejaBulletin = Session.FindObject<Bulletin>(
                    CriteriaOperator.Parse(
                        "Salarie = ? AND (Annee < ? OR (Annee = ? AND Mois < ?))",
                        Salarie, Annee, Annee, Mois));

                if (dejaBulletin == null)
                {
                    // Prorata : jours restants dans le mois (base 30)
                    JoursTravailles = Math.Max(1, 30 - (embauche.Day - 1));
                }
            }
        }

        private void RecalculerTotauxDepuisLignes()
        {
            decimal gains = 0m, retFisc = 0m, cotSoc = 0m, autresRet = 0m;
            decimal bf = 0m, bs = 0m;

            // V1.8.1 — Suivi des avantages en nature pour les exclure du Net.
            // Les avantages en nature (véhicule, téléphone, logement, etc.)
            // sont imposables IR/TRIMF/CFCE mais NE SONT PAS du cash à
            // encaisser par le salarié. Ils ne doivent donc pas entrer dans
            // le Net à payer. La détection se fait via le code TypeRef qui
            // commence par "AV_NAT" (cohérent avec le calcul de la base CFCE).
            decimal gainsAvantagesNature = 0m;

            foreach (var l in Lignes)
            {
                var r = l.Rubrique;
                if (r == null) continue;
                var m = N(l.Montant);

                switch (r.TypeCalcul)
                {
                    case RubriqueTypeCalcul.Gain:
                        gains += m;
                        // V1.8.1 — Compter à part les avantages en nature
                        if (EstAvantageEnNature(r))
                            gainsAvantagesNature += m;
                        break;
                    case RubriqueTypeCalcul.Retenue:
                        var grp = r.SectionImpression ?? "";
                        if (grp.Contains("Retenues Fiscales", StringComparison.OrdinalIgnoreCase))
                            retFisc += Math.Abs(m);
                        else if (grp.Contains("Cotisations Sociales", StringComparison.OrdinalIgnoreCase))
                            cotSoc += Math.Abs(m);
                        else
                            autresRet += Math.Abs(m);
                        break;
                }

                if (r.BrutFiscal && r.TypeCalcul == RubriqueTypeCalcul.Gain) bf += m;
                if (r.BrutSocial && r.TypeCalcul == RubriqueTypeCalcul.Gain) bs += m;
            }

            TotalGains = gains;
            TotalRetenuesFiscales = retFisc;
            TotalCotisationsSociales = cotSoc;
            TotalAutresRetenues = autresRet;
            BrutFiscal = bf;
            BrutSocial = bs;
            // V1.8.1 — Net = gains HORS avantages en nature - retenues
            // (les avantages en nature ne sont pas du cash à verser)
            NetAPayer = (gains - gainsAvantagesNature) - (retFisc + cotSoc + autresRet);
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted)
            {
                if (Salarie == null) throw new UserFriendlyException("Salarié obligatoire.");
                if (Annee <= 0 || Mois <= 0) throw new UserFriendlyException("Période invalide.");

                // Contrôle : pas de bulletin avant la date d'embauche
                ValiderPeriodeEmbauche();

                // RecalculerCotisationsEtTotaux();
                RecalculerSurGrilleExistante();
                //UpdateSyntheseFiscale();
            }
        }

        /// <summary>
        /// Empêche la création/sauvegarde d'un bulletin pour un mois
        /// où le salarié n'était pas encore recruté.
        /// Ex : embauché le 17/04/2026 → pas de bulletin mars 2026 ni avant.
        /// </summary>
        private void ValiderPeriodeEmbauche()
        {
            if (Salarie == null || Annee <= 0 || Mois <= 0) return;

            var embauche = Salarie.DateEmbauche;
            if (embauche == default) return; // DateEmbauche non renseignée → on laisse passer

            // Le bulletin couvre le mois Annee/Mois.
            // Le salarié doit avoir été embauché au plus tard dans ce mois.
            // Si embauché le 17/04 → avril OK (prorata), mars KO.
            var debutMoisBulletin = new DateTime(Annee, Mois, 1);
            var finMoisBulletin = debutMoisBulletin.AddMonths(1).AddDays(-1);

            if (embauche > finMoisBulletin)
            {
                throw new UserFriendlyException(
                    $"Impossible de créer un bulletin pour {Mois:D2}/{Annee}. " +
                    $"{Salarie.FullName} n'a été recruté(e) que le {embauche:dd/MM/yyyy}. " +
                    $"Le premier bulletin possible est {embauche:MM/yyyy}.");
            }
        }



        public void RemplacerLignesIssuesDuModele(bool onlyIncludeDefault = false)
        {
            foreach (var l in Lignes.Where(x => x.Source == RubriqueSource.DepuisModele).ToList())
                l.Delete();
            Session.FlushChanges();

            CopierDepuisModele(null, overwriteExistingLines: false, onlyIncludeDefault: onlyIncludeDefault);
            // RecalculerTotaux();
            RecalculerSurGrilleExistante();
        }

        BulletinModele bulletinModeleSource;
        [Association("Modele-BulletinsUtilisant")]
        public BulletinModele BulletinModeleSource
        {
            get => bulletinModeleSource;
            set => SetPropertyValue(nameof(BulletinModeleSource), ref bulletinModeleSource, value);
        }


        // ===== Helpers ordre (dans la classe Bulletin) =====
        private static int ComputeOrdreFor(BulletinModeleLigne ml, ref int cursor)
        {
            // 1) Si la rubrique a un OrdreAffichage explicite, on le reprend
            if (ml?.Rubrique?.OrdreAffichage.HasValue == true && ml.Rubrique.OrdreAffichage.Value > 0)
                return ml.Rubrique.OrdreAffichage.Value;

            // 2) Sinon si la ligne modèle a un Ordre > 0, on le reprend
            if (ml != null && ml.Ordre > 0)
                return ml.Ordre;

            // 3) Sinon, on progresse avec un curseur local
            cursor += 10;
            return cursor;
        }

        // ===== Overload pratique pour éviter les erreurs d'appel =====
        public void CopierDepuisModele(bool overwriteExistingLines, bool onlyIncludeDefault)
            => CopierDepuisModele(null, overwriteExistingLines, onlyIncludeDefault);

        // ===== Version principale alignée =====
        public void CopierDepuisModele(
            BulletinModele modele = null,
            bool overwriteExistingLines = false,
            bool onlyIncludeDefault = true)
        {
            if (Salarie == null)
                throw new UserFriendlyException("Affectez d'abord le Salarié.");

            // 1) Résolution du modèle
            if (modele == null)
            {
                // Priorité : modèle actif du salarié
                modele = Session.FindObject<BulletinModele>(
                    CriteriaOperator.Parse("Salarie = ? AND Actif = true", Salarie));
            }
            // Filet : si rien trouvé, on tente la dernière source connue
            if (modele == null)
                modele = BulletinModeleSource;

            // Si toujours rien : on sort proprement (pas d’exception ici)
            if (modele == null)
            {
                // On peut garder BulletinModeleSource à null
                return;
            }

            BulletinModeleSource = modele;

            // 2) Purge si demandé
            if (overwriteExistingLines)
            {
                foreach (var l in Lignes.ToList())
                    l.Delete();
                Session.FlushChanges();
            }

            // 3) Indexer les lignes existantes par RubriqueId (pour ne pas dupliquer)
            var existByRub = Lignes
                .Where(x => x.Rubrique != null)
                .GroupBy(x => x.Rubrique.Oid)
                .ToDictionary(g => g.Key, g => g.First());

            // Curseur d’ordre : on part du max déjà présent pour éviter les collisions
            int ordreCursor = Lignes
                .Where(x => x.OrdreCalcul.HasValue && x.OrdreCalcul.Value > 0)
                .Select(x => x.OrdreCalcul.Value)
                .DefaultIfEmpty(100)
                .Max();

            // 4) Source filtrée et triée
            var source = modele.Lignes
                .Where(x => !onlyIncludeDefault || x.InclureParDefaut)
                .ToList();

            // On impose un tri stable : d’abord OrdreAffichage de la rubrique (si dispo), sinon Ordre du modèle
            source = source
                .OrderBy(ml => ml.Rubrique?.OrdreAffichage ?? int.MaxValue)
                .ThenBy(ml => ml.Ordre)
                .ThenBy(ml => ml.Oid) // ultime stabilisation
                .ToList();

            foreach (var ml in source)
            {
                if (ml.Rubrique == null) continue;

                var rubId = ml.Rubrique.Oid;

                // Ne pas créer si une ligne existe déjà et qu’on n’écrase pas
                if (!overwriteExistingLines && existByRub.ContainsKey(rubId))
                    continue;

                // Créer la ligne
                var bl = new BulletinLigne(Session)
                {
                    Bulletin = this,
                    Rubrique = ml.Rubrique,
                    Reference = ml.ReferenceDefaut,
                    Source = RubriqueSource.DepuisModele,
                    // Ordre : priorité à Rubrique.OrdreAffichage, sinon Ordre du modèle, sinon curseur
                    OrdreCalcul = ComputeOrdreFor(ml, ref ordreCursor)
                };

                // N’affecter les valeurs que si elles sont renseignées sur le modèle (nullable)
                if (ml.BaseDefaut.HasValue) bl.Base = ml.BaseDefaut.Value;
                if (ml.TauxDefaut.HasValue) bl.Taux = ml.TauxDefaut.Value;
                if (ml.MontantDefaut.HasValue)
                {
                    bl.Montant = ml.MontantDefaut.Value;
                }
                else if (ml.BaseDefaut.HasValue && ml.TauxDefaut.HasValue)
                {
                    // Calcul auto si Base & Taux fournis
                    bl.Montant = Math.Round(
                        ml.BaseDefaut.Value * ml.TauxDefaut.Value / 100m,
                        0, MidpointRounding.AwayFromZero);
                }
                else
                {
                    bl.Montant = 0m;
                }
            }

            // 5) Recalcule les totaux
            // RecalculerTotaux();
            RecalculerSurGrilleExistante();
        }

        // ========================= MOTEUR =========================
        bool _recalcLock;

        public void RecalculerCotisationsEtTotaux(bool depuisParametrage)
        {
            if (_recalcLock) return;
            _recalcLock = true;
            try
            {
                // Prorata automatique : si JoursTravailles n'a jamais été initialisé
                // (= 30 par défaut) et qu'on détecte un premier bulletin avec embauche
                // en cours de mois, on ajuste automatiquement. Le RH peut toujours
                // corriger JoursTravailles manuellement avant de relancer le recalcul.
                if (depuisParametrage)
                    InitJoursTravailles();

                // Ici : on ne touche aux gains standards QUE si on veut repartir du paramétrage
                if (depuisParametrage)
                {
                    RecalculerGainsStandards();  // recalcule SB, LOGT, ANC, SURSAL, TRANSPORT depuis le profil salarié
                }
                else
                {
                    // Filet de sécurité : si SB ou LOGT ont Base > 0 mais Montant = 0
                    // (ex. chargé depuis modèle sans MontantDefaut), on calcule le Montant
                    // sans toucher aux lignes saisies manuellement qui ont déjà un Montant correct.
                    RepairerGainsNuls();
                }

                // Heures supplémentaires → met à jour la ligne HS dans les gains
                CalculerHeuresSupplementaires();

                var bf = CalculerBrutFiscal();
                var bs = CalculerBrutSocial();

                CalculerIPRES_CSS(bs);
                // V1.8 — Base CFCE configurable dans ParametresPaie :
                //   AvecAvantagesNature  = brut fiscal complet (recommandé DAF actuel)
                //   SansAvantagesNature  = base dédiée hors Av Nature (ancien système ELTON)
                CalculerCFCE(CalculerBaseCFCE_SelonParametres(bf));
                CalculerTRIMF_Baremise(bf);

                CalculerIRPP(bf);

                // Prêts (une seule routine)
                CalculerRetenuePrets();

                //RecalculerTotaux();
                RecalculerTotauxDepuisLignes();
                UpdateSyntheseFiscale();
            }
            finally
            {
                _recalcLock = false;
            }
        }

        // Version par défaut = grille actuelle
        public void RecalculerCotisationsEtTotaux()
            => RecalculerCotisationsEtTotaux(false);

        // Helpers lisibles
        public void RecalculerDepuisParametrage()
            => RecalculerCotisationsEtTotaux(true);

        public void RecalculerSurGrilleExistante()
            => RecalculerCotisationsEtTotaux(false);


        // ─── Filet de sécurité : répare les lignes SB/LOGT dont Base > 0 mais Montant = 0 ──────────
        // Appelé uniquement en mode "grille existante" (depuisParametrage=false).
        // Ne touche PAS aux lignes qui ont déjà un Montant > 0 (valeurs manuelles respectées).
        private void RepairerGainsNuls()
        {
            // Utilise JoursTravailles du bulletin (prorata par bulletin, pas par salarié)
            var jours = Math.Max(0, JoursTravailles > 0 ? JoursTravailles : 30);

            var sbLine = FindLine(RubriqueCanonique.SalaireDeBase);
            if (sbLine != null && N(sbLine.Base) > 0m && N(sbLine.Montant) == 0m)
                sbLine.Montant = Math.Round((N(sbLine.Base) / 30m) * jours, 0, MidpointRounding.AwayFromZero);

            var logtLine = FindLine(RubriqueCanonique.IndemniteLogement);
            if (logtLine != null && N(logtLine.Base) > 0m && N(logtLine.Montant) == 0m)
                logtLine.Montant = Math.Round((N(logtLine.Base) / 30m) * jours, 0, MidpointRounding.AwayFromZero);

            // Ancienneté : Taux renseigné mais Montant = 0
            var ancLine = FindLine(RubriqueCanonique.PrimeAnciennete);
            if (ancLine != null && N(ancLine.Base) > 0m && N(ancLine.Taux) > 0m && N(ancLine.Montant) == 0m)
                ancLine.Montant = Math.Round(N(ancLine.Base) * N(ancLine.Taux) / 100m, 0, MidpointRounding.AwayFromZero);
        }

        private void RecalculerGainsStandards()
        {
            // Utilise JoursTravailles du bulletin (prorata par bulletin, pas par salarié)
            var jours = Math.Max(0, JoursTravailles > 0 ? JoursTravailles : 30);

            // Salaire de base
            var sb = EnsureLine(RubriqueCanonique.SalaireDeBase);
            if (sb != null)
            {
                var baseMensuelle = Salarie?.SalaireBase ?? 0m;
                sb.Base = baseMensuelle;
                sb.Taux = null;
                sb.Montant = Math.Round((baseMensuelle / 30m) * jours, 0, MidpointRounding.AwayFromZero);
                sb.IsSystem = false;
                if (!sb.OrdreCalcul.HasValue) sb.OrdreCalcul = sb.Rubrique?.OrdreAffichage;
            }

            // Indemnité logement
            var logt = EnsureLine(RubriqueCanonique.IndemniteLogement);
            if (logt != null)
            {
                var indem = Salarie?.IndemniteLogement ?? 0m;
                logt.Base = indem;
                logt.Taux = null;
                logt.Montant = Math.Round((indem / 30m) * jours, 0, MidpointRounding.AwayFromZero);
                logt.IsSystem = false;
                if (!logt.OrdreCalcul.HasValue) logt.OrdreCalcul = logt.Rubrique?.OrdreAffichage;
            }

            // Ancienneté 2..25 ans
            var finPeriode = new DateTime(Annee, Mois, 1).AddMonths(1).AddDays(-1);
            var anc = AncienneteHelper.NombreAnnee(Salarie?.DateEmbauche ?? DateTime.Today, finPeriode);
            // var ancLine = EnsureLine(RubriqueCanonique.PrimeAnciennete, createIfMissing: anc >= 2 && anc <= 25);
            // On NE crée rien par défaut : on regarde s'il existe déjà une ligne
            var ancLine = FindLine(RubriqueCanonique.PrimeAnciennete);


            if (anc >= 2 && anc <= 25)
            {
                // créer si manquante
                if (ancLine == null)
                    ancLine = EnsureLine(RubriqueCanonique.PrimeAnciennete, createIfMissing: true);

                if (ancLine != null)
                {
                    ancLine.Base = Salarie?.SalaireBase ?? 0m;
                    ancLine.Taux = Math.Floor((decimal)anc);
                    ancLine.Montant = Math.Round(N(ancLine.Base) * N(ancLine.Taux) / 100m, 0, MidpointRounding.AwayFromZero);
                    ancLine.IsSystem = false;
                    if (!ancLine.OrdreCalcul.HasValue)
                        ancLine.OrdreCalcul = ancLine.Rubrique?.OrdreAffichage;
                }
            }
            else
            {
                // hors plage → supprimer si elle existe (pas d'accès après Delete)
                if (ancLine != null && !ancLine.IsDeleted)
                    ancLine.Delete();
            }

            // Sursalaire (montant)
            var vSurs = Salarie?.Sursalaire ?? 0m;
            if (vSurs > 0m)
            {
                var sur = EnsureLine(RubriqueCanonique.Sursalaire, createIfMissing: true);
                if (sur != null)
                {
                    sur.Base = vSurs; sur.Taux = null; sur.Montant = vSurs;
                    sur.IsSystem = false;
                    if (!sur.OrdreCalcul.HasValue) sur.OrdreCalcul = sur.Rubrique?.OrdreAffichage;
                }
            }
            else
            {
                DeleteLineIfExists(RubriqueCanonique.Sursalaire);
            }




            // ============ Exclusivité Transport / Avantage véhicule ============
            var vTrans = Salarie?.PrimeTransport ?? 0m;
            var vVeh = Salarie?.AvantageVehicule ?? 0m;

            if (vVeh > 0m)
            {
                // Avantage véhicule : créer/MAJ ; supprimer la ligne transport si présente
                var veh = EnsureLine(RubriqueCanonique.AvantageNatureVehicule, createIfMissing: true);
                if (veh != null)
                {
                    veh.Base = vVeh; veh.Taux = null; veh.Montant = vVeh;
                    veh.IsSystem = false;
                    if (!veh.OrdreCalcul.HasValue) veh.OrdreCalcul = veh.Rubrique?.OrdreAffichage;
                }
                DeleteLineIfExists(RubriqueCanonique.PrimeTransport);
            }
            else if (vTrans > 0m)
            {
                // Prime transport : créer/MAJ ; supprimer la ligne avantage véhicule si présente
                var trp = EnsureLine(RubriqueCanonique.PrimeTransport, createIfMissing: true);
                if (trp != null)
                {
                    trp.Base = vTrans; trp.Taux = null; trp.Montant = vTrans;
                    trp.IsSystem = false;
                    if (!trp.OrdreCalcul.HasValue) trp.OrdreCalcul = trp.Rubrique?.OrdreAffichage;
                }
                DeleteLineIfExists(RubriqueCanonique.AvantageNatureVehicule);
            }
            else
            {
                // Les deux à 0 → supprimer les deux lignes si elles existent
                DeleteLineIfExists(RubriqueCanonique.PrimeTransport);
                DeleteLineIfExists(RubriqueCanonique.AvantageNatureVehicule);
            }

        }

        // ─── Heures supplémentaires ─────────────────────────────────────────
        // Totalise les HeuresSupplementaires du bulletin et crée/met à jour
        // une ligne de gain avec la rubrique canonique HeuresSupplementaires.
        // Si le module HS est désactivé ou qu'il n'y a aucune HS, la ligne est supprimée.
        private void CalculerHeuresSupplementaires()
        {
            var prm = ParametresPaie.TryGet(Session);
            var hsActif = prm?.ActiverHeuresSupplementaires ?? false;

            var hsLines = HeuresSupplementaires?.ToList()
                          ?? new System.Collections.Generic.List<HeureSupplementaire>();

            if (!hsActif || hsLines.Count == 0)
            {
                DeleteLineIfExists(RubriqueCanonique.HeuresSupplementaires);
                return;
            }

            // Recalculer chaque ligne HS (taux horaire + montants)
            foreach (var hs in hsLines)
            {
                hs.CalculerTauxHoraireBase();
                hs.RecalculerMontants();
            }

            var totalHS = hsLines.Sum(h => h.MontantTotal);

            if (totalHS <= 0m)
            {
                DeleteLineIfExists(RubriqueCanonique.HeuresSupplementaires);
                return;
            }

            var totalHeures = hsLines.Sum(h => h.NombreHeures);

            var ligne = EnsureLine(RubriqueCanonique.HeuresSupplementaires, createIfMissing: true);
            if (ligne != null)
            {
                ligne.Base = totalHS;          // Base = montant total des HS
                ligne.Taux = null;
                ligne.Montant = totalHS;       // Gain = montant total
                ligne.MontantEmployeur = 0m;
                ligne.IsSystem = true;
                ligne.Source = RubriqueSource.Calcul;
                if (!ligne.OrdreCalcul.HasValue)
                    ligne.OrdreCalcul = ligne.Rubrique?.OrdreAffichage;
            }
        }

        private decimal CalculerBrutFiscal()
            => Lignes.Where(l => l.Rubrique?.BrutFiscal == true && l.Rubrique.TypeCalcul != RubriqueTypeCalcul.Retenue)
                     .Sum(l => N(l.Montant));

        private decimal CalculerBrutSocial()
            => Lignes.Where(l => l.Rubrique?.BrutSocial == true && l.Rubrique.TypeCalcul != RubriqueTypeCalcul.Retenue)
                     .Sum(l => N(l.Montant));

        /// <summary>
        /// V1.8 — Base CFCE selon le paramètre <c>ParametresPaie.ModeBaseCFCE</c> :
        ///   AvecAvantagesNature  → renvoie le brut fiscal complet (par défaut)
        ///   SansAvantagesNature  → renvoie la base dédiée hors avantages nature
        ///
        /// Permet au DAF de basculer entre les 2 interprétations du Code
        /// Général des Impôts Sénégal sans modifier le code.
        /// </summary>
        private decimal CalculerBaseCFCE_SelonParametres(decimal brutFiscalComplet)
        {
            try
            {
                var prm = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
                var mode = prm?.ModeBaseCFCE
                    ?? DomainEnums.ModeBaseCFCE.AvecAvantagesNature;

                return mode == DomainEnums.ModeBaseCFCE.SansAvantagesNature
                    ? CalculerBaseCFCE_HorsAvantagesNature()
                    : brutFiscalComplet;
            }
            catch
            {
                // En cas de problème lecture paramètre : on retombe sur le
                // comportement par défaut (avec avantages nature).
                return brutFiscalComplet;
            }
        }

        /// <summary>
        /// Base CFCE dédiée qui EXCLUT les avantages en nature.
        /// Utilisée uniquement si <c>ParametresPaie.ModeBaseCFCE = SansAvantagesNature</c>.
        /// Conforme à l'ancien système ELTON (bulletin DAF mai 2026 :
        /// CFCE base = 5 879 859 hors Av Nature véhicule 20 000).
        /// </summary>
        private decimal CalculerBaseCFCE_HorsAvantagesNature()
        {
            return Lignes
                .Where(l => l.Rubrique?.BrutFiscal == true
                         && l.Rubrique.TypeCalcul != RubriqueTypeCalcul.Retenue
                         && !EstAvantageEnNature(l.Rubrique))
                .Sum(l => N(l.Montant));
        }

        /// <summary>
        /// Détermine si une rubrique est un avantage en nature (imposable
        /// ou non) — donc à exclure de la base CFCE.
        /// On se base sur le code du TypeRef (commence par "AV_NAT").
        /// </summary>
        private static bool EstAvantageEnNature(Rubrique r)
        {
            var codeTypeRef = r?.TypeRef?.Code;
            if (string.IsNullOrEmpty(codeTypeRef)) return false;
            return codeTypeRef.StartsWith("AV_NAT", StringComparison.OrdinalIgnoreCase);
        }

        // Cotisations sociales
        private void CalcCotisationDouble(BulletinLigne l, decimal assiette)
        {
            if (l?.Rubrique == null) return;
            var r = l.Rubrique;
            var plafond = r.Plafond ?? 0m;
            var baseCalc = plafond > 0m ? Math.Min(assiette, plafond) : assiette;

            var t1 = r.Taux1 ?? 0m; // Salariale
            var t2 = r.Taux2 ?? 0m; // Patronale

            l.Base = baseCalc;
            l.Taux = t1;
            l.Montant = Math.Round(baseCalc * t1 / 100m, 0, MidpointRounding.AwayFromZero);
            l.MontantEmployeur = Math.Round(baseCalc * t2 / 100m, 0, MidpointRounding.AwayFromZero);
            l.IsSystem = true;
            if (!l.OrdreCalcul.HasValue) l.OrdreCalcul = l.Rubrique?.OrdreAffichage;
        }

        // ---------------------------------------------------------------------
        // V1.8.1 — Détection « cadre » via drapeau explicite sur Categories
        //
        // AVANT (V1.8) : on cherchait le mot "cadre" dans le libellé via
        // IndexOf, ce qui matchait à tort "Non cadre" / "Non-cadre".
        // Conséquence : IPRES Régime Cadre appliqué à tort à des non-cadres.
        //
        // MAINTENANT : on lit le booléen Categories.EstCadre, coché manuellement
        // par le RH. Fallback sur l'ancienne heuristique uniquement si le
        // booléen est faux ET que le libellé commence par "Cadre" (compat
        // ascendante pour les catégories pas encore migrées).
        // ---------------------------------------------------------------------
        private bool EstCadre(Salarie s)
        {
            var cat = s?.Categories ?? s?.Echelon?.Categories;
            if (cat == null) return false;
            if (cat.EstCadre) return true;

            // Compat ascendante : tant que le RH n'a pas migré toutes ses
            // catégories, on accepte aussi les libellés qui commencent par
            // "Cadre" et ne contiennent pas "Non". À retirer en V1.9.
            var lib = (cat.Intitule ?? "").Trim();
            if (string.IsNullOrEmpty(lib)) return false;
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    lib, @"\bnon\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return false;
            return lib.StartsWith("cadre", StringComparison.OrdinalIgnoreCase);
        }

        private void CalculerIPRES_CSS(decimal brutSocial)
        {
            var rg = EnsureLine(RubriqueCanonique.IPRES_RG);
            if (rg != null) CalcCotisationDouble(rg, brutSocial);

            var rc = Lignes.FirstOrDefault(x => x.Rubrique?.Canonique == RubriqueCanonique.IPRES_RC);
            if (EstCadre(Salarie))
            {
                rc ??= EnsureLine(RubriqueCanonique.IPRES_RC);
                if (rc != null) CalcCotisationDouble(rc, brutSocial);
            }
            else if (rc != null)
            {
                // V1.8.1 — On supprime carrément la ligne IPRES_RC pour les
                // non-cadres (avant on la laissait à 0, ce qui polluait le
                // bulletin avec une ligne vide). Comportement attendu : aucune
                // trace d'IPRES Régime Cadre sur le bulletin d'un non-cadre.
                Lignes.Remove(rc);
                rc.Delete();
            }

            var at = EnsureLine(RubriqueCanonique.CSS_AccidentTravail);
            if (at != null) CalcCotisationDouble(at, brutSocial);

            var af = EnsureLine(RubriqueCanonique.CSS_AllocationFamiliale);
            if (af != null) CalcCotisationDouble(af, brutSocial);
        }

        // CFCE : part employeur uniquement, base = brut fiscal HORS
        //        avantages en nature (cf. CalculerBaseCFCE V1.8).
        //        Le paramètre s'appelle historiquement brutSocial mais
        //        reçoit en réalité la base CFCE dédiée depuis V1.8.
        private void CalculerCFCE(decimal brutSocial)
        {
            // Trouve la rubrique CFCE soit par canonique, soit par code
            var rub = new XPQuery<Rubrique>(Session)
                .FirstOrDefault(r => r.Actif && (
                        r.Canonique == RubriqueCanonique.CFCE
                        || (r.Code ?? "").ToUpper() == "CFCE"));

            if (rub == null) return;

            var l = Lignes.FirstOrDefault(x => x.Rubrique?.Oid == rub.Oid)
                    ?? new BulletinLigne(Session)
                    {
                        Bulletin = this,
                        Rubrique = rub,
                        OrdreCalcul = rub.OrdreAffichage,
                        IsSystem = true
                    };

            l.Base = brutSocial;
            l.Taux = 0m; // pas de part salariale
            l.Montant = 0m;
            var tPat = rub.Taux2 ?? 0m;
            l.MontantEmployeur = Math.Round(brutSocial * tPat / 100m, 0, MidpointRounding.AwayFromZero);
            l.IsSystem = true;
        }


        // ===== TRIMF =====
        private bool ShouldRegularizeTRIMF()
        {
            if (Mois == 12) return true;
            var ds = Salarie?.DateSortie;
            return ds.HasValue && ds.Value.Year == Annee && ds.Value.Month == Mois;
        }

        private string GetBaremeCodeTRIMF()
        {
            var def = $"TRIMF_{Annee}";
            var p = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            var code = p?.CodeBaremeTRIMF_Mensuel;
            return string.IsNullOrWhiteSpace(code) ? def : code.Trim();
        }
        private string GetBaremeCodeTRIMFAnnuel()
        {
            var def = $"TRIMF_AN_{Annee}";
            var p = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            var code = p?.CodeBaremeTRIMF_Annuel;
            return string.IsNullOrWhiteSpace(code) ? def : code.Trim();
        }

        private BaremeTRIMF GetBaremeTRIMF(PeriodiciteBareme periodicite)
        {
            bool isAnnuel = (periodicite == PeriodiciteBareme.Annuel);
            var codeAttendu = isAnnuel ? GetBaremeCodeTRIMFAnnuel() : GetBaremeCodeTRIMF();
            var q = new XPQuery<BaremeTRIMF>(Session).Where(b => b.Actif);

            if (!string.IsNullOrWhiteSpace(codeAttendu))
            {
                var exact = q.FirstOrDefault(b => b.Code == codeAttendu);
                if (exact != null) return exact;
            }
            if (isAnnuel)
                q = q.Where(b => b.Code != null && b.Code.StartsWith("TRIMF_AN_"));
            else
                q = q.Where(b => b.Code != null && b.Code.StartsWith("TRIMF_") && !b.Code.StartsWith("TRIMF_AN_"));

            var d = new DateTime(Annee, Mois, 1).AddMonths(1).AddDays(-1);
            try
            {
                var withDates = q.Where(b => (b.DateDebut == null || b.DateDebut <= d) && (b.DateFin == null || d <= b.DateFin))
                                 .OrderByDescending(b => b.DateDebut ?? new DateTime(1900, 1, 1))
                                 .FirstOrDefault();
                if (withDates != null) return withDates;
            }
            catch { }
            return q.OrderBy(b => b.Code).FirstOrDefault();
        }

        private BaremeTRIMFTranche FindTranche(BaremeTRIMF bareme, decimal baseAssiette)
        {
            if (bareme == null) return null;
            return bareme.Tranches
                .Where(t => baseAssiette >= t.MontantMin && baseAssiette <= t.MontantMax)
                .OrderBy(t => t.Ordre).ThenBy(t => t.MontantMin).FirstOrDefault();
        }

        private decimal EvalTrimfMensuel(decimal brutFiscalMois, int parts, DateTime _)
        {
            if (parts <= 0) return 0m;
            var bareme = GetBaremeTRIMF(PeriodiciteBareme.Mensuel);
            var tr = FindTranche(bareme, brutFiscalMois);
            return (tr == null) ? 0m : (N(tr.Montant) * parts);
        }

        private decimal EvalTrimfAnnuel(decimal cumulBrutFiscal, int parts, DateTime _)
        {
            if (parts <= 0) return 0m;
            var bareme = GetBaremeTRIMF(PeriodiciteBareme.Annuel);
            var tr = FindTranche(bareme, cumulBrutFiscal);
            return (tr == null) ? 0m : (N(tr.Montant) * parts);
        }

        private void CalculerTRIMF_Baremise(decimal brutFiscal)
        {
            var tr = EnsureLine(RubriqueCanonique.TRIMF);
            if (tr == null) return;

            var dateFinMois = new DateTime(Annee, Mois, 1).AddMonths(1).AddDays(-1);
            var parts = Math.Max(1, Salarie?.TrimfParts ?? 1);

            var mensuel = EvalTrimfMensuel(brutFiscal, parts, dateFinMois);

            var bulletinsYtd = new XPQuery<Bulletin>(Session)
                .Where(b => b.Salarie == Salarie && b.Annee == Annee && b.Mois <= Mois).ToList();

            var cumulBF = bulletinsYtd.SelectMany(b => b.Lignes)
                                      .Where(l => l.Rubrique != null && l.Rubrique.BrutFiscal && l.Rubrique.TypeCalcul != RubriqueTypeCalcul.Retenue)
                                      .Sum(l => N(l.Montant));

            var bulletinsAvant = new XPQuery<Bulletin>(Session)
                .Where(b => b.Salarie == Salarie && b.Annee == Annee && b.Mois < Mois).ToList();

            var cumulTrimfAvant = bulletinsAvant.SelectMany(b => b.Lignes)
                .Where(l => l.Rubrique != null && l.Rubrique.Canonique == RubriqueCanonique.TRIMF)
                .Sum(l => N(l.Montant));

            decimal deltaRegul = 0m;
            if (ShouldRegularizeTRIMF())
            {
                var theoriqueYTD = EvalTrimfAnnuel(cumulBF, parts, dateFinMois);
                var aPayerTotalYTD = cumulTrimfAvant + mensuel;
                deltaRegul = theoriqueYTD - aPayerTotalYTD;
            }

            tr.Base = brutFiscal;
            tr.Taux = parts;                    // affiche le nombre de parts sur la ligne TRIMF
            tr.Montant = mensuel + deltaRegul;
            tr.IsSystem = true;
            if (!tr.OrdreCalcul.HasValue) tr.OrdreCalcul = tr.Rubrique?.OrdreAffichage;
        }

        // ======= PRETS =======
        public void ValiderRemboursementsPrets()
        {
            var debutMois = new DateTime(Annee, Mois, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            if (Statut == BulletinStatut.Brouillon)
                throw new UserFriendlyException("Validez au moins le bulletin avant de marquer les remboursements de prêts.");

            var aValider = new XPQuery<PretEcheance>(Session)
                .Where(e => e.Pret.Salarie == Salarie
                         && e.DateEcheance >= debutMois && e.DateEcheance <= finMois
                         && e.Statut == PretEcheanceStatut.Prevue)
                .ToList();

            foreach (var e in aValider)
            {
                e.Statut = PretEcheanceStatut.Prelevee;
                e.DatePrelevement = DateTime.Now;   // propriété à prévoir sur PretEcheance
                e.BulletinPreleveur = this;
                e.Pret?.RecalculerEtat();
            }
        }

        private Rubrique FindRubriquePret()
        {
            var rub = new XPQuery<Rubrique>(Session)
                .FirstOrDefault(r => r.Actif && r.Canonique == RubriqueCanonique.RemboursementPret);
            if (rub != null) return rub;

            return new XPQuery<Rubrique>(Session)
                .FirstOrDefault(r => r.Actif &&
                    (r.Code == "PRET" || r.Code == "REM_PRET" ||
                     (r.Libelle ?? "").ToLower().Contains("pret") || (r.Libelle ?? "").ToLower().Contains("prêt")));
        }


        private void CalculerRetenuePrets()
        {
            var debutMois = new DateTime(Annee, Mois, 1);
            var finMois = debutMois.AddMonths(1).AddDays(-1);

            // Purge des anciennes lignes système liées aux prêts/avances (canoniques)
            foreach (var old in Lignes.Where(l => l.IsSystem && l.Rubrique != null &&
                   (l.Rubrique.Canonique == RubriqueCanonique.RemboursementPret ||
                    l.Rubrique.Canonique == RubriqueCanonique.RemboursementAvance)).ToList())
                old.Delete();

            // Échéances dues
            var dues = new XPQuery<PretEcheance>(Session)
                .Where(e => e.Pret != null
                         && e.Pret.Salarie == Salarie
                         && e.Statut == PretEcheanceStatut.Prevue
                         && e.Pret.Statut == PretStatut.EnCours
                         && e.DateEcheance >= debutMois && e.DateEcheance <= finMois)
                .ToList();

            // Groupage par Rubrique effective (Prêt/Type/Paramètres)
            var groups = dues
                .Select(d => new { E = d, Rub = d.Pret.GetRubriqueRetenueEffective() })
                .Where(x => x.Rub != null)
                .GroupBy(x => x.Rub);

            foreach (var g in groups)
            {
                var rub = g.Key;
                var total = g.Sum(x => x.E.MontantTotal);
                if (total <= 0m) continue;

                var l = Lignes.FirstOrDefault(lg => lg.Rubrique == rub && lg.IsSystem)
                        ?? new BulletinLigne(Session)
                        {
                            Bulletin = this,
                            Rubrique = rub,
                            IsSystem = true,
                            OrdreCalcul = rub.OrdreAffichage
                        };
                l.Base = total;
                l.Taux = null;
                l.Montant = total;
                l.MontantEmployeur = 0m;
                if (!l.OrdreCalcul.HasValue) l.OrdreCalcul = rub.OrdreAffichage;
                l.Reference = string.Join(" + ", g.Select(x => x.E.Pret.Oid.ToString().Substring(0, 8)).Distinct());
            }
        }




        // ======= IRPP =======
        private BaremeIR GetBaremeIR()
        {
            var p = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            var code = p?.ResolveCodeBaremeIRAnnuel(Annee) ?? $"IR_DPP_{Annee}";
            var today = new DateTime(Annee, Mois, 1).AddMonths(1).AddDays(-1);

            return new XPQuery<BaremeIR>(Session)
                .Where(b => b.Actif
                    && (b.DateDebut == null || b.DateDebut <= today)
                    && (b.DateFin == null || today <= b.DateFin)
                    && b.Code == code)
                .FirstOrDefault();
        }

        private static decimal EvalIRProgressifAnnuelFromBareme(BaremeIR bareme, decimal baseAnnuelle)
        {
            if (bareme == null || baseAnnuelle <= 0) return 0m;
            decimal total = 0m;
            foreach (var t in bareme.Tranches.OrderBy(x => x.MontantMin))
            {
                if (baseAnnuelle <= t.MontantMin) break;
                var sup = Math.Min(baseAnnuelle, t.MontantMax);
                var assiette = Math.Max(0m, sup - t.MontantMin);
                if (assiette <= 0) continue;
                total += Math.Round(assiette * (t.Taux / 100m), 0, MidpointRounding.AwayFromZero);
                if (baseAnnuelle <= t.MontantMax) break;
            }
            return total;
        }

        private static decimal TroncMille(decimal v) => v <= 0 ? 0m : Math.Floor(v / 1000m) * 1000m;

        private void CalculerIRPP(decimal brutFiscal)
        {
            var ir = EnsureLine(RubriqueCanonique.IRPP, createIfMissing: true);
            if (ir == null) return;

            var p = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            var bareme = GetBaremeIR();

            decimal parts = (ir.Taux.HasValue && ir.Taux.Value > 0m)
                ? ir.Taux.Value
                : Math.Max(1m, Salarie?.NombrePartsFiscales ?? 1m);
            ir.Taux = parts; // affichage des parts utilisées

            var baseMensuelle = (p?.R_IR_TronquerBaseAuxMille ?? true) ? TroncMille(brutFiscal) : brutFiscal;

            var pctImab = (p?.R_IR_Abattement_TauxPercent ?? 30m) / 100m;
            var plfA = p?.R_IR_Abattement_PlafondAnnuel ?? 900_000m;
            var plfM = p?.R_IR_Abattement_PlafondMensuel ?? 75_000m;

            var revenuAnnuelTheo = baseMensuelle * 12m;
            var abattAnnuelTheo = revenuAnnuelTheo * pctImab;
            var imabMensuel = (abattAnnuelTheo >= plfA)
                ? plfM
                : Math.Round(abattAnnuelTheo / 12m, 0, MidpointRounding.AwayFromZero);

            var dppAnnuelTheo = Math.Max(0m, (baseMensuelle - imabMensuel) * 12m);
            var progAnnuelTheo = EvalIRProgressifAnnuelFromBareme(bareme, dppAnnuelTheo);

            var (rfPct, rfMinA, rfMaxA) = ResolveIRReduction(parts);

            var reduc = Math.Round(progAnnuelTheo * rfPct, 0, MidpointRounding.AwayFromZero);
            if (rfMinA > 0 && reduc < rfMinA) reduc = rfMinA;
            if (rfMaxA > 0 && reduc > rfMaxA) reduc = rfMaxA;

            var impotAnnuelTheoNet = Math.Max(0m, progAnnuelTheo - reduc);
            var mensuelProvisoire = Math.Round(impotAnnuelTheoNet / 12m, 0, MidpointRounding.AwayFromZero);

            decimal deltaRegul = 0m;
            if (ShouldRegularizeIRPP())
            {
                var bulletinsYtd = new XPQuery<Bulletin>(Session)
                    .Where(b => b.Salarie == Salarie && b.Annee == Annee && b.Mois <= Mois)
                    .ToList();

                var cumulBF = bulletinsYtd.SelectMany(b => b.Lignes)
                    .Where(l => l.Rubrique != null && l.Rubrique.BrutFiscal && l.Rubrique.TypeCalcul != RubriqueTypeCalcul.Retenue)
                    .Sum(l => l.Montant);

                var abattAnnuelReel = cumulBF * pctImab;
                var imabAnnuelReel = Math.Min(abattAnnuelReel, plfA);

                var dppAnnuelReel = Math.Max(0m, cumulBF - imabAnnuelReel);
                var progAnnuelReel = EvalIRProgressifAnnuelFromBareme(bareme, dppAnnuelReel);

                var reducReel = Math.Round(progAnnuelReel * rfPct, 0, MidpointRounding.AwayFromZero);
                if (rfMinA > 0 && reducReel < rfMinA) reducReel = rfMinA;
                if (rfMaxA > 0 && reducReel > rfMaxA) reducReel = rfMaxA;

                var impotAnnuelReelNet = Math.Max(0m, progAnnuelReel - reducReel);

                var cumulIrAvant = new XPQuery<Bulletin>(Session)
                    .Where(b => b.Salarie == Salarie && b.Annee == Annee && b.Mois < Mois)
                    .SelectMany(b => b.Lignes)
                    .Where(l => l.Rubrique != null && l.Rubrique.Canonique == RubriqueCanonique.IRPP)
                    .Sum(l => l.Montant);

                deltaRegul = impotAnnuelReelNet - (cumulIrAvant + mensuelProvisoire);
            }

            ir.Base = baseMensuelle;
            ir.Montant = mensuelProvisoire + deltaRegul;
            ir.IsSystem = true;
            if (!ir.OrdreCalcul.HasValue) ir.OrdreCalcul = ir.Rubrique?.OrdreAffichage;
        }

        private bool ShouldRegularizeIRPP()
        {
            var p = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            var regDec = p?.IR_Regularisation_FinAnnee ?? true;
            var regDepart = p?.IR_Regularisation_MoisDepart ?? true;

            if (regDec && Mois == 12) return true;

            if (regDepart)
            {
                var ds = Salarie?.DateSortie;
                if (ds.HasValue && ds.Value.Year == Annee && ds.Value.Month == Mois)
                    return true;
            }
            return false;
        }

        [ModelDefault("DisplayFormat", "N1"), ModelDefault("EditMask", "N1")]
        [DbType("decimal(4,1)")]
        [XafDisplayName("Parts fiscales (snapshot)")]
        public decimal NombrePartsFiscales
        {
            get => nbParts;
            set => SetPropertyValue(nameof(NombrePartsFiscales), ref nbParts, value);
        }
        private decimal nbParts = 1m;

        private (decimal pct, decimal minAnnuel, decimal maxAnnuel) ResolveIRReduction(decimal parts)
        {
            var rf = new XPQuery<IRReductionFamille>(Session)
                        .FirstOrDefault(r => r.Actif && r.NbrePart == parts);
            if (rf != null) return (rf.Taux / 100m, rf.MinAnnuel, rf.MaxAnnuel);

            var p = new XPQuery<ParametresPaie>(Session).FirstOrDefault();
            var pct = (p?.R_IR_ReductionFamille_Pourcentage ?? 0m) / 100m;
            var minA = (p?.R_IR_ReductionFamille_MinParPart_Annuel ?? 0m) * parts;
            var maxA = (p?.R_IR_ReductionFamille_MaxParPart_Annuel ?? 0m) * parts;
            return (pct, minA, maxA);
        }

        // --- utilitaire lignes ---
        public BulletinLigne EnsureLine(RubriqueCanonique role, bool createIfMissing = true)
        {
            var line = Lignes.FirstOrDefault(l => l.Rubrique != null && l.Rubrique.Canonique == role);
            if (line != null) return line;
            if (!createIfMissing) return null;

            var rub = new XPQuery<Rubrique>(Session).FirstOrDefault(r => r.Canonique == role && r.Actif);
            if (rub == null) return null;

            line = new BulletinLigne(Session)
            {
                Bulletin = this,
                Rubrique = rub,
                OrdreCalcul = rub.OrdreAffichage
            };
            return line;
        }


        // Helpers (à mettre dans la classe Bulletin si pas déjà présents)
        private BulletinLigne FindLine(RubriqueCanonique rc)
            => Lignes?.FirstOrDefault(l => l.Rubrique != null && l.Rubrique.Canonique == rc);

        private void DeleteLineIfExists(RubriqueCanonique rc)
        {
            var ln = FindLine(rc);
            if (ln != null && !ln.IsDeleted)
                ln.Delete(); // XPO: supprime l’objet (agrégé -> OK)
        }
        private void UpdateSyntheseFiscale()
        {
            // --- MOIS COURANT (ce bulletin) ---
            TRIMF_Mois = SumMontantByCriteria(
                CriteriaOperator.Parse("Bulletin = ? AND Rubrique.Canonique = ?", this, RubriqueCanonique.TRIMF));

            IR_Mois = SumMontantByCriteria(
                CriteriaOperator.Parse("Bulletin = ? AND Rubrique.Canonique = ?", this, RubriqueCanonique.IRPP));

            IPRES_RG_Mois = SumMontantByCriteria(
                CriteriaOperator.Parse("Bulletin = ? AND Rubrique.Canonique = ?", this, RubriqueCanonique.IPRES_RG));

            IPRES_RC_Mois = SumMontantByCriteria(
                CriteriaOperator.Parse("Bulletin = ? AND Rubrique.Canonique = ?", this, RubriqueCanonique.IPRES_RC));

            // --- CUMUL ANNUEL (même salarié, même année, jusqu’au mois courant inclus) ---
            TRIMF_CumulAnnee = SumMontantByCriteria(
                CriteriaOperator.Parse(
                    "Bulletin.Salarie = ? AND Bulletin.Annee = ? AND Bulletin.Mois <= ? AND Rubrique.Canonique = ?",
                    Salarie, Annee, Mois, RubriqueCanonique.TRIMF));

            IR_CumulAnnee = SumMontantByCriteria(
                CriteriaOperator.Parse(
                    "Bulletin.Salarie = ? AND Bulletin.Annee = ? AND Bulletin.Mois <= ? AND Rubrique.Canonique = ?",
                    Salarie, Annee, Mois, RubriqueCanonique.IRPP));

            IPRES_RG_CumulAnnee = SumMontantByCriteria(
                CriteriaOperator.Parse(
                    "Bulletin.Salarie = ? AND Bulletin.Annee = ? AND Bulletin.Mois <= ? AND Rubrique.Canonique = ?",
                    Salarie, Annee, Mois, RubriqueCanonique.IPRES_RG));

            IPRES_RC_CumulAnnee = SumMontantByCriteria(
                CriteriaOperator.Parse(
                    "Bulletin.Salarie = ? AND Bulletin.Annee = ? AND Bulletin.Mois <= ? AND Rubrique.Canonique = ?",
                    Salarie, Annee, Mois, RubriqueCanonique.IPRES_RC));

            // V1.8.1 — Bug fix : SumMontantByCriteria utilise Session.Evaluate
            // qui ne voit pas les BulletinLigne ajoutées en mémoire (ex : ligne
            // d'avantage en nature ajoutée manuellement). Résultat : Cumul YTD
            // Brut Fiscal/Social en retard d'une modification.
            //
            // Solution : calculer via XPQuery sur les Bulletin (qui ont déjà
            // leurs propriétés BrutFiscal/BrutSocial calculées en mémoire pour
            // les bulletins dirty).
            var sommeBrutFiscalAutres = new XPQuery<Bulletin>(Session)
                .Where(b => b.Salarie != null
                         && b.Salarie.Oid == Salarie.Oid
                         && b.Annee == Annee
                         && b.Mois <= Mois
                         && b.Oid != this.Oid)
                .Sum(b => b.BrutFiscal);
            BrutFiscal_CumulAnnee = sommeBrutFiscalAutres + BrutFiscal;

            var sommeBrutSocialAutres = new XPQuery<Bulletin>(Session)
                .Where(b => b.Salarie != null
                         && b.Salarie.Oid == Salarie.Oid
                         && b.Annee == Annee
                         && b.Mois <= Mois
                         && b.Oid != this.Oid)
                .Sum(b => b.BrutSocial);
            BrutSocial_CumulAnnee = sommeBrutSocialAutres + BrutSocial;
            // V1.8.1 — Bug fix : Session.Evaluate lisait les valeurs déjà
            // commitées en BDD, sans tenir compte des modifications en
            // mémoire du bulletin courant (ex : ajout d'une ligne d'avantage
            // en nature qui change le NetAPayer). Résultat : Cumul YTD
            // affichait l'ancien total au lieu du nouveau.
            //
            // Solution : on calcule la somme des AUTRES bulletins de l'année
            // via XPQuery (qui voit les objets dirty), puis on ajoute le
            // NetAPayer courant en mémoire.
            var sommeAutresBulletins = new XPQuery<Bulletin>(Session)
                .Where(b => b.Salarie != null
                         && b.Salarie.Oid == Salarie.Oid
                         && b.Annee == Annee
                         && b.Mois <= Mois
                         && b.Oid != this.Oid)
                .Sum(b => b.NetAPayer);

            NetAPayer_CumulAnnee = sommeAutresBulletins + NetAPayer;
        }



        private decimal SumMontantByCriteria(CriteriaOperator criteria)
        {
            // Agrégat SQL côté serveur, null-safe sur Montant
            var expr = CriteriaOperator.Parse("Sum(Iif(IsNull(Montant), 0, Montant))");
            var result = Session.Evaluate(typeof(BulletinLigne), expr, criteria);
            return result is decimal d ? d : Convert.ToDecimal(result ?? 0m);
        }

    }


}
