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
using AggregatedAttribute = DevExpress.Xpo.AggregatedAttribute;

namespace AdiPAIE_V02.Module.NonPersistent
{
    /// <summary>
    /// V1.9 - Batch d'import Excel d'une liste d'intérimaires actifs.
    ///
    /// PERSISTANT (BaseObject) car contient un FileData (FileAttachments requiert
    /// un ObjectSpace persistant). Chaque import est tracé en base - le RH peut
    /// consulter l'historique et supprimer les vieux batches.
    ///
    /// Note : le nom du fichier / namespace « NonPersistent » est un vestige V1.9 -
    /// à renommer en « BusinessObjects.Interim » à la V2.0 pour propreté.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Import fichier intérimaires")]
    [ImageName("Action_Import")]
    [DefaultProperty(nameof(DisplayName))]
    public class ImportInterimaireRequest : BaseObject
    {
        public ImportInterimaireRequest(Session s) : base(s) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateImport = DateTime.Now;
            CreerContratActif = true;
            DryRun = true;
            DateFinContratDefaut = new DateTime(DateTime.Today.Year, 12, 31);
            try { ImportePar = SecuritySystem.CurrentUserName; } catch { }
        }

        // -- Métadonnées -----------------------------------------------
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Date d'import")]
        public DateTime DateImport
        {
            get => dateImport;
            set => SetPropertyValue(nameof(DateImport), ref dateImport, value);
        }
        DateTime dateImport;

        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        [XafDisplayName("Importé par")]
        public string ImportePar
        {
            get => importePar;
            set => SetPropertyValue(nameof(ImportePar), ref importePar, value);
        }
        string importePar;

        // -- Fichier ----------------------------------------------------
        [XafDisplayName("Fichier Excel (liste effectif)")]
        [ToolTip("Fichier .xlsx contenant les colonnes : Matricule, Prénoms, Noms, Sexe, " +
                 "Catégorie, Date de naissance, Fonction, Business unit, Agence d'intérim, " +
                 "Site d'affectation, Date d'entrée, Nationalité.")]
        [FileTypeFilter("Excel", ".xlsx")]
        [RuleRequiredField(DefaultContexts.Save, CustomMessageTemplate = "Chargez un fichier Excel.")]
        [Aggregated]
        // ⚠ IMPORTANT : ExpandObjectMembers.Never force XAF à utiliser le
        // FilePropertyEditor (composant unique avec bouton "Charger fichier"),
        // au lieu d'aplatir les sous-propriétés FileName / Size en champs séparés.
        [ExpandObjectMembers(ExpandObjectMembers.Never)]
        public FileData Fichier
        {
            get => fichier;
            set => SetPropertyValue(nameof(Fichier), ref fichier, value);
        }
        FileData fichier;

        // -- Options d'import -------------------------------------------
        [XafDisplayName("Créer aussi un contrat actif par intérimaire")]
        [ToolTip("Recommandé : crée 1 ContratInterim EnCours par intérimaire avec Site + " +
                 "BU + DateDébut = date d'entrée agence + DateFin = fin d'année.")]
        [ModelDefault("ImmediatePostData", "True")]
        public bool CreerContratActif
        {
            get => creerContratActif;
            set => SetPropertyValue(nameof(CreerContratActif), ref creerContratActif, value);
        }
        bool creerContratActif;

        [XafDisplayName("Date fin par défaut des contrats")]
        [ToolTip("Date de fin utilisée si aucune date de fin de mission n'est disponible.")]
        [Appearance("DateFinDefaut_Enabled",
            Criteria = "CreerContratActif = True", Enabled = true)]
        [Appearance("DateFinDefaut_Disabled",
            Criteria = "CreerContratActif = False", Enabled = false)]
        public DateTime DateFinContratDefaut
        {
            get => dateFinContratDefaut;
            set => SetPropertyValue(nameof(DateFinContratDefaut), ref dateFinContratDefaut, value);
        }
        DateTime dateFinContratDefaut;

        [XafDisplayName("Simulation (dry-run) : ne rien créer, juste analyser")]
        [ToolTip("Coché : parse le fichier et affiche le rapport SANS rien créer. " +
                 "Décocher pour importer réellement.")]
        [ModelDefault("ImmediatePostData", "True")]
        public bool DryRun
        {
            get => dryRun;
            set => SetPropertyValue(nameof(DryRun), ref dryRun, value);
        }
        bool dryRun;

        // -- Statistiques (remplies après exécution) --------------------
        [XafDisplayName("Lignes lues")]
        [ModelDefault("AllowEdit", "False")]
        public int LignesLues
        {
            get => lignesLues;
            set => SetPropertyValue(nameof(LignesLues), ref lignesLues, value);
        }
        int lignesLues;

        [XafDisplayName("Intérimaires créés")]
        [ModelDefault("AllowEdit", "False")]
        public int NbCrees
        {
            get => nbCrees;
            set => SetPropertyValue(nameof(NbCrees), ref nbCrees, value);
        }
        int nbCrees;

        [XafDisplayName("Contrats créés")]
        [ModelDefault("AllowEdit", "False")]
        public int NbContratsCrees
        {
            get => nbContratsCrees;
            set => SetPropertyValue(nameof(NbContratsCrees), ref nbContratsCrees, value);
        }
        int nbContratsCrees;

        [XafDisplayName("Doublons (fichier)")]
        [ModelDefault("AllowEdit", "False")]
        public int NbDoublonsFichier
        {
            get => nbDoublonsFichier;
            set => SetPropertyValue(nameof(NbDoublonsFichier), ref nbDoublonsFichier, value);
        }
        int nbDoublonsFichier;

        [XafDisplayName("Déjà en base (skip)")]
        [ModelDefault("AllowEdit", "False")]
        public int NbDejaEnBase
        {
            get => nbDejaEnBase;
            set => SetPropertyValue(nameof(NbDejaEnBase), ref nbDejaEnBase, value);
        }
        int nbDejaEnBase;

        [XafDisplayName("Erreurs")]
        [ModelDefault("AllowEdit", "False")]
        public int NbErreurs
        {
            get => nbErreurs;
            set => SetPropertyValue(nameof(NbErreurs), ref nbErreurs, value);
        }
        int nbErreurs;

        // -- Rapport texte détaillé -------------------------------------
        [XafDisplayName("Rapport détaillé")]
        [FieldSize(FieldSizeAttribute.Unlimited)]
        [ModelDefault("RowCount", "18")]
        [ModelDefault("AllowEdit", "False")]
        public string Rapport
        {
            get => rapport;
            set => SetPropertyValue(nameof(Rapport), ref rapport, value);
        }
        string rapport;

        // V1.9.1 - Nom du fichier importé (trace, pas le binaire)
        [Size(255)]
        [XafDisplayName("Nom fichier source")]
        [ModelDefault("AllowEdit", "False")]
        public string FichierNom
        {
            get => fichierNom;
            set => SetPropertyValue(nameof(FichierNom), ref fichierNom, value);
        }
        string fichierNom;

        [NonPersistent]
        public string DisplayName =>
            $"Import du {DateImport:dd/MM/yyyy HH:mm} - {NbCrees} créé(s) / {NbErreurs} erreur(s)";

        public override string ToString() => DisplayName;
    }
}
