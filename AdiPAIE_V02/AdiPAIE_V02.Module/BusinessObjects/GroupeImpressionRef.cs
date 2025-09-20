using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultProperty(nameof(Libelle))]
    public class GroupeImpressionRef : BaseObject
    {
        public GroupeImpressionRef(Session session) : base(session) { }

        // ✅ Nouveau : CODE unique (recommandé : requis)
        string code;
        [RuleRequiredField, Size(30)]
        [Indexed(Unique = true)]
        public string Code
        {
            get => code;
            set => SetPropertyValue(nameof(Code), ref code, value?.Trim().ToUpperInvariant());
        }

        string libelle;
        [RuleRequiredField, Size(120)]
        public string Libelle { get => libelle; set => SetPropertyValue(nameof(Libelle), ref libelle, value); }

        // Unicité libellé “normalisé”
        string norm;
        [Indexed(Unique = true)]
        [Size(140)]
        public string NormalizedKey { get => norm; protected set => SetPropertyValue(nameof(NormalizedKey), ref norm, value); }

        public int? OrdreGroupe { get => ordre; set => SetPropertyValue(nameof(OrdreGroupe), ref ordre, value); }
        int? ordre;

        public bool Actif { get => actif; set => SetPropertyValue(nameof(Actif), ref actif, value); }
        bool actif = true;

        [Association("GroupeImpressionRef-Types")]
        public XPCollection<RubriqueTypeRef> Types => GetCollection<RubriqueTypeRef>(nameof(Types));

        protected override void OnSaving()
        {
            base.OnSaving();
            Libelle = (Libelle ?? "").Trim();
            var oneSpace = Regex.Replace(Libelle, @"\s+", " ");
            NormalizedKey = oneSpace.ToUpperInvariant();

            // Sécurise Code : si vide, essaie d’en générer un minimal
            if (string.IsNullOrWhiteSpace(Code))
            {
                // slug simple du libellé (ACMESANSACCENTS -> mots clés)
                var c = Regex.Replace(NormalizedKey, @"[^A-Z0-9]+", "_");
                c = Regex.Replace(c, @"_+", "_").Trim('_');
                Code = c.Length > 20 ? c.Substring(0, 20) : c;
            }
        }
    }
}
