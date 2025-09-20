using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
//using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
  //  [DefaultClassOptions]l
   // [NavigationItem("Paie")]
    [DefaultProperty(nameof(DisplayName))]
    [Persistent("BulletinModel")]

    public class BulletinModele : BaseObject
    { // Inherit from a different class to provide a custom primary key, concurrency and deletion behavior, etc. (https://docs.devexpress.com/eXpressAppFramework/113146/business-model-design-orm/business-model-design-with-xpo/base-persistent-classes).
        // Use CodeRush to create XPO classes and properties with a few keystrokes.
        // https://docs.devexpress.com/CodeRushForRoslyn/118557
        public BulletinModele(Session session)
            : base(session)
        {
        }
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            // Place your initialization code here (https://docs.devexpress.com/eXpressAppFramework/112834/getting-started/in-depth-tutorial-winforms-webforms/business-model-design/initialize-a-property-after-creating-an-object-xpo?v=22.1).
        }


        Salarie salarie;
        [RuleRequiredField]
        [Association("Salarie-Modele")]
       
        [RuleRequiredField]
        public Salarie Salarie { get => salarie; set => SetPropertyValue(nameof(Salarie), ref salarie, value); }
         

        bool actif = true;
        public bool Actif { get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value); }

        public DateTime DateCreation { get => dateCreation; set => SetPropertyValue(nameof(DateCreation), ref dateCreation, value); }
        DateTime dateCreation = DateTime.Today;
      
        // 🔒 Un seul modèle ACTIF par salarié(contrainte d’unicité)
[PersistentAlias("Concat(ToStr(Salarie.Oid),'|',Iif(Actif,'1','0'))")]
        public string Unique_Salarie_ActifKey => (string)EvaluateAlias(nameof(Unique_Salarie_ActifKey));


        [Association("Modele-Lignes"), Aggregated]
        public XPCollection<BulletinModeleLigne> Lignes => GetCollection<BulletinModeleLigne>(nameof(Lignes));

        [NonPersistent]
        public string DisplayName => $"{Salarie?.FullName} - Modèle";


        [Association("Modele-BulletinsUtilisant")]
        public XPCollection<Bulletin> BulletinsUtilisateurs
        {
            get
            {
                return GetCollection<Bulletin>(nameof(BulletinsUtilisateurs));
            }
        }


        protected override void OnSaving()
        {
            base.OnSaving();
            if (!IsDeleted && Salarie != null)
            {
                var criteria = CriteriaOperator.Parse("Oid <> ? AND Salarie = ?", Oid, Salarie);
                bool exists = Session.FindObject<BulletinModele>(criteria) != null;
                if (exists)
                    throw new UserFriendlyException("Un modèle existe déjà pour ce salarié.");
            }
            if (!IsDeleted && Salarie == null)
                throw new UserFriendlyException("Sélectionnez d'abord le salarié avant d'ajouter des rubriques.");

            if (IsDeleted) return;

            if (Actif)
            {
                // Un seul modèle ACTIF par salarié
                var autreActif = new XPQuery<BulletinModele>(Session)
                    .Any(m => m.Oid != Oid && m.Salarie == Salarie && m.Actif );
                if (autreActif)
                    throw new UserFriendlyException("Un modèle ACTIF existe déjà pour ce salarié.");
            }


        }

        protected override void OnDeleting()
        {
            base.OnDeleting();

            var modelOid = this.Oid;

            // Détacher tous les bulletins qui utilisent ce modèle
            var bulletins = new XPQuery<Bulletin>(Session)
                .Where(b => b.BulletinModeleSource != null
                         && b.BulletinModeleSource.Oid == modelOid)
                .ToList();

            foreach (var b in bulletins)
            {
                b.BulletinModeleSource = null;
            }

          
        }

    }


    [DefaultProperty(nameof(DisplayName))]
    public class BulletinModeleLigne : BaseObject
    {
        public BulletinModeleLigne(Session s) : base(s) { }

        // --- Gabarit parent ---
        [Association("Modele-Lignes"), RuleRequiredField]
        public BulletinModele Modele
        {
            get => modele; set
            {
                if (SetPropertyValue(nameof(Modele), ref modele, value))
                {
                    // Si une rubrique dépend d’un salarié, on (ré)applique les auto-défauts
                    if (!IsLoading && Rubrique != null)
                        TryApplyAutoDefaultsFromSalarie();
                }
            }
        }
        BulletinModele modele;

        // --- Rubrique choisie ---
        [RuleRequiredField]
        [ImmediatePostData] // déclenche OnChanged côté UI
        public Rubrique Rubrique
        {
            get => rubrique; set
            {
                if (SetPropertyValue(nameof(Rubrique), ref rubrique, value))
                {
                    if (!IsLoading && Rubrique != null)
                        TryApplyAutoDefaultsFromSalarie();
                }
            }
        }
        Rubrique rubrique;

        // --- Métadonnées / saisie par défaut dans le modèle ---
        public int Ordre { get => ordre; set => SetPropertyValue(nameof(Ordre), ref ordre, value); }
        int ordre;

        public bool InclureParDefaut { get => incl; set => SetPropertyValue(nameof(InclureParDefaut), ref incl, value); }
        bool incl = true;

        [Size(120)]
        public string ReferenceDefaut { get => reference; set => SetPropertyValue(nameof(ReferenceDefaut), ref reference, value?.Trim()); }
        string reference;

        public decimal? BaseDefaut { get => bdef; set => SetPropertyValue(nameof(BaseDefaut), ref bdef, value); }
        decimal? bdef;

        public decimal? TauxDefaut { get => taux; set => SetPropertyValue(nameof(TauxDefaut), ref taux, value); }
        decimal? taux;

        public decimal? MontantDefaut { get => mdef; set => SetPropertyValue(nameof(MontantDefaut), ref mdef, value); }
        decimal? mdef;

        // Affichage pratique
        [NonPersistent]
        public string DisplayName => Rubrique == null ? "(Rubrique ?)" : $"{Ordre:000} - {Rubrique.Code} - {Rubrique.Libelle}";


        // Dans class BulletinModeleLigne

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);

            if (propertyName == nameof(Rubrique) && Rubrique != null)
            {
                // 👉 Ordre hérité de la Rubrique, sinon prochain créneau
                if (Ordre <= 0)
                    Ordre = Rubrique.OrdreAffichage ?? CalcNextOrdre();

                // 👉 Tes valeurs auto (SB / LOGT, etc.)
                TryApplyAutoDefaultsFromSalarie();
            }
        }

        // Prochain créneau d’ordre (10, 20, 30…)
        private int CalcNextOrdre()
        {
            var last = Modele?.Lignes?
                .Where(x => !Equals(x, this))
                .Select(x => (int?)x.Ordre)
                .DefaultIfEmpty(0)
                .Max() ?? 0;
            return last + 10;
        }

        // ==============================
        // Remplissage auto depuis Salarié
        // ==============================
        private void TryApplyAutoDefaultsFromSalarie()
        {
            // On ne touche pas si rien n’est prêt
            if (Rubrique == null || Modele?.Salarie == null) return;

            // On remplit UNIQUEMENT si pas déjà saisi
            if ((MontantDefaut ?? 0m) != 0m) return;

            var sal = Modele.Salarie;
            var base30 = sal.Base30Jour <= 0 ? 30 : sal.Base30Jour;
            var rub = Rubrique;

            switch (Rubrique.Canonique)
            {
                case RubriqueCanonique.SalaireDeBase:
                    {
                        var montant = Math.Round((sal.SalaireBase / 30m) * base30, 0, MidpointRounding.AwayFromZero);
                        // Pour le modèle, on pose le montant par défaut
                        MontantDefaut = montant;
                        BaseDefaut = montant;
                        TauxDefaut = null;
                        if (string.IsNullOrEmpty(ReferenceDefaut)) ReferenceDefaut = "Auto: SB salarié";
                        break;
                    }
                case RubriqueCanonique.IndemniteLogement:
                    {
                        var montant = Math.Round((sal.IndemniteLogement / 30m) * base30, 0, MidpointRounding.AwayFromZero);
                        MontantDefaut = montant;
                        BaseDefaut = null;
                        TauxDefaut = null;
                        if (string.IsNullOrEmpty(ReferenceDefaut)) ReferenceDefaut = "Auto: Logement salarié";
                        break;
                    }

                // (Optionnel) si tu veux pré-remplir aussi l’ancienneté:
                // case RubriqueCanonique.PrimeAnciennete:
                // {
                //     var sbProrata = Math.Round((sal.SalaireBase / 30m) * base30, 0, MidpointRounding.AwayFromZero);
                //     var tauxAnc = AncienneteHelper.TauxAnciennete(sal.DateEmbauche, DateTime.Today); // ex: 0..25
                //     BaseDefaut = sbProrata;
                //     TauxDefaut = tauxAnc;
                //     MontantDefaut = Math.Round(sbProrata * (tauxAnc ?? 0m) / 100m, 0, MidpointRounding.AwayFromZero);
                //     if (string.IsNullOrEmpty(ReferenceDefaut)) ReferenceDefaut = "Auto: Ancienneté";
                //     break;
                // }

                default:
                    // Autres rubriques : on ne fait rien
                    break;
            }
        }
    }
}