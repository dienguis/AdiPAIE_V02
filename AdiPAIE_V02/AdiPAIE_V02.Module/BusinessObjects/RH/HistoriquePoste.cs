using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    /// <summary>
    /// Historique des situations professionnelles d'un salarié.
    /// Créé automatiquement à chaque application d'un avancement.
    /// Permet de reconstituer le parcours complet du salarié.
    /// </summary>
    [DefaultClassOptions]
    [XafDisplayName("Historique de poste")]
    [DefaultProperty(nameof(DisplayHistorique))]
    [ImageName("BO_Audit")]
   // [NavigationItem("Ressources humaines")]
    public class HistoriquePoste : BaseObject
    {
        public HistoriquePoste(Session session) : base(session) { }

        // ── Salarié ───────────────────────────────────────────
        [Association("Salarie-HistoriquePostes")]
        [XafDisplayName("Salarié")]
        [ModelDefault("AllowEdit", "False")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        // ── Demande source ────────────────────────────────────
        [Association("Avancement-Historique")]
        [XafDisplayName("Avancement source")]
        [ModelDefault("AllowEdit", "False")]
        public DemandeAvancement AvancementSource
        {
            get => avancementSource;
            set => SetPropertyValue(nameof(AvancementSource), ref avancementSource, value);
        }
        DemandeAvancement avancementSource;

        // ── Période ───────────────────────────────────────────
        DateTime dateDebut;
        [XafDisplayName("Date de début")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime DateDebut
        {
            get => dateDebut;
            set => SetPropertyValue(nameof(DateDebut), ref dateDebut, value);
        }

        DateTime? dateFin;
        [XafDisplayName("Date de fin")]
        [ModelDefault("AllowEdit", "False")]
        public DateTime? DateFin
        {
            get => dateFin;
            set => SetPropertyValue(nameof(DateFin), ref dateFin, value);
        }

        [NonPersistent]
        [XafDisplayName("Durée (mois)")]
        public int DureeMois =>
            DateFin.HasValue
                ? AncienneteHelper.TotalMois(DateDebut, DateFin.Value)
                : AncienneteHelper.TotalMois(DateDebut, DateTime.Today);

        // ── Situation ─────────────────────────────────────────
        string fonctionLibelle;
        [Size(120)]
        [XafDisplayName("Fonction")]
        [ModelDefault("AllowEdit", "False")]
        public string FonctionLibelle
        {
            get => fonctionLibelle;
            set => SetPropertyValue(nameof(FonctionLibelle), ref fonctionLibelle, value);
        }

        string departementLibelle;
        [Size(120)]
        [XafDisplayName("Département")]
        [ModelDefault("AllowEdit", "False")]
        public string DepartementLibelle
        {
            get => departementLibelle;
            set => SetPropertyValue(nameof(DepartementLibelle), ref departementLibelle, value);
        }

        string echelonLibelle;
        [Size(100)]
        [XafDisplayName("Échelon")]
        [ModelDefault("AllowEdit", "False")]
        public string EchelonLibelle
        {
            get => echelonLibelle;
            set => SetPropertyValue(nameof(EchelonLibelle), ref echelonLibelle, value);
        }

        decimal salaireBase;
        [XafDisplayName("Salaire de base (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal SalaireBase
        {
            get => salaireBase;
            set => SetPropertyValue(nameof(SalaireBase), ref salaireBase, value);
        }

        decimal indemniteLogement;
        [XafDisplayName("Indemnité logement (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        [ModelDefault("AllowEdit", "False")]
        public decimal IndemniteLogement
        {
            get => indemniteLogement;
            set => SetPropertyValue(nameof(IndemniteLogement), ref indemniteLogement, value);
        }

        [NonPersistent]
        [XafDisplayName("Salaire brut (FCFA)")]
        [ModelDefault("DisplayFormat", "N0")]
        public decimal SalaireBrut => SalaireBase + IndemniteLogement;

        string typeAvancementLibelle;
        [Size(100)]
        [XafDisplayName("Type d'avancement")]
        [ModelDefault("AllowEdit", "False")]
        public string TypeAvancementLibelle
        {
            get => typeAvancementLibelle;
            set => SetPropertyValue(nameof(TypeAvancementLibelle), ref typeAvancementLibelle, value);
        }

        // ── Affichage ─────────────────────────────────────────
        [NonPersistent]
        public string DisplayHistorique =>
            $"{Salarie?.LastName} — {FonctionLibelle} depuis {DateDebut:MM/yyyy}";

        // ── Factory ───────────────────────────────────────────
        /// <summary>
        /// Crée une entrée d'historique depuis un avancement appliqué.
        /// Ferme également la ligne précédente (DateFin = dateEffet - 1 jour).
        /// </summary>
        public static HistoriquePoste CreerDepuisAvancement(
            DevExpress.ExpressApp.IObjectSpace os,
            DemandeAvancement avancement)
        {
            if (avancement?.Salarie == null) return null;

            // Fermer la ligne précédente ouverte
            var precedent = os.GetObjectsQuery<HistoriquePoste>()
                .ToList()
                .FirstOrDefault(h =>
                    h.Salarie?.Oid == avancement.Salarie.Oid
                    && h.DateFin == null);
            if (precedent != null)
                precedent.DateFin = avancement.DateEffet.AddDays(-1);

            // Créer la nouvelle ligne
            var h = os.CreateObject<HistoriquePoste>();
            h.Salarie = avancement.Salarie;
            h.AvancementSource = avancement;
            h.DateDebut = avancement.DateEffet;
            h.FonctionLibelle = avancement.NouvelleFonction?.Intitule
                                     ?? avancement.Salarie.Fonction?.Intitule ?? "—";
            h.DepartementLibelle = avancement.NouveauDepartement?.Nom
                                     ?? avancement.Salarie.Departement?.Nom ?? "—";
            h.EchelonLibelle = avancement.NouvelEchelon?.Libelle
                                     ?? avancement.Salarie.Echelon?.Libelle ?? "—";
            h.SalaireBase = avancement.NouveauSalaireBase;
            h.IndemniteLogement = avancement.NouvelleIndemnite;
            h.TypeAvancementLibelle = avancement.TypeAvancement.ToString();

            return h;
        }
    }
}
