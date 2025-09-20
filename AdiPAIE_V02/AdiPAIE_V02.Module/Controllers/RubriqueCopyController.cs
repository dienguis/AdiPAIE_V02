using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Action "Copier la rubrique" : duplique la rubrique sélectionnée en copiant
    /// tous les champs non-uniques (sans recopier Code, Oid ni OverridesComptes).
    /// Ouvre la nouvelle fiche en édition avec un Code proposé unique.
    /// </summary>
    public sealed class RubriqueCopyController : ObjectViewController<ObjectView, Rubrique>
    {
        private readonly SimpleAction copyAction;

        public RubriqueCopyController()
        {
            TargetObjectType = typeof(Rubrique);
            TargetViewType = ViewType.Any;

            copyAction = new SimpleAction(this, "CopyRubrique", PredefinedCategory.Edit)
            {
                Caption = "Copier la rubrique",
                ImageName = "Copy",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject,
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            copyAction.Execute += OnCopyExecute;
        }

        private void OnCopyExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var src = GetCurrentRubrique();
            if (src == null)
            {
                Application.ShowViewStrategy.ShowMessage("Aucune rubrique sélectionnée.", InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            // Nouvel OS pour la nouvelle fiche
            var newOS = Application.CreateObjectSpace(typeof(Rubrique));
            var copy = newOS.CreateObject<Rubrique>();

            // ====== Champs copiés (NON-uniques) ======
            // Identité (Libellé), type de référence et dérivés
            copy.Libelle = src.Libelle;
            copy.TypeRef = newOS.GetObject(src.TypeRef);           // ref lookup
            copy.TypeCalcul = src.TypeCalcul;                          // valeur persistée
            copy.BrutFiscal = src.BrutFiscal;
            copy.BrutSocial = src.BrutSocial;

            // Présentation
            copy.OrdreAffichage = src.OrdreAffichage;
            copy.Actif = src.Actif;

            // Paramètres de calcul
            copy.Taux1 = src.Taux1;
            copy.Taux2 = src.Taux2;
            copy.Plafond = src.Plafond;

            // Comptes par défaut (références déplacées dans le nouvel OS)
            copy.CompteDebitDefaut = newOS.GetObject(src.CompteDebitDefaut);
            copy.CompteCreditDefaut = newOS.GetObject(src.CompteCreditDefaut);
            copy.CompteTaux1 = newOS.GetObject(src.CompteTaux1);
            copy.CompteTaux2 = newOS.GetObject(src.CompteTaux2);

            // Canonique : on NE recopie PAS par sécurité (les canoniques sont “rôles système”)
            copy.Canonique = null;

            // Collection OverridesComptes : NON copiée (souvent spécifique/dated)
            // -> si tu veux une variante qui copie aussi les overrides, je peux te donner l’extension plus tard.

            // ====== Code unique proposé ======
            copy.Code = GenerateUniqueCode(newOS, src.Code);

            // Ouvrir la fiche en édition
            var dv = Application.CreateDetailView(newOS, copy, true);
            dv.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.Edit;
            e.ShowViewParameters.CreatedView = dv;
            e.ShowViewParameters.TargetWindow = TargetWindow.NewModalWindow; // ou NewWindow selon préférence
        }

        private Rubrique GetCurrentRubrique()
        {
            if (View is DetailView dv) return dv.CurrentObject as Rubrique;
            if (View is ListView lv) return lv.SelectedObjects?.Cast<Rubrique>()?.FirstOrDefault();
            return null;
        }

        /// <summary>
        /// Propose un Code unique respectant la règle ^[A-Z0-9_]{2,20}$.
        /// Ex.: SB  -> SB_COPY   (ou SB_1, SB_2, … si collision)
        ///      IPRES_RG -> IPRES_RG_COPY (tronqué si nécessaire).
        /// </summary>
        private static string GenerateUniqueCode(IObjectSpace os, string sourceCode)
        {
            // Sanitize MAJUSCULES + _
            string baseCode = (sourceCode ?? "RUB").ToUpperInvariant();
            baseCode = Regex.Replace(baseCode, @"[^A-Z0-9_]", "_");

            // Préfixe tronqué pour garder la place du suffixe (_COPY)
            const string copySuffix = "_COPY";
            string prefix = baseCode;
            if (prefix.Length + copySuffix.Length > 20)
                prefix = prefix.Substring(0, Math.Max(2, 20 - copySuffix.Length));

            // 1) Essai avec _COPY
            string candidate = prefix + copySuffix;
            if (!ExistsCode(os, candidate)) return candidate;

            // 2) Boucle _1, _2, …
            // On tronque si besoin pour loger _{n}
            prefix = baseCode;
            for (int i = 1; i < 1000; i++)
            {
                string suffix = "_" + i.ToString();
                string p = prefix;
                if (p.Length + suffix.Length > 20)
                    p = p.Substring(0, Math.Max(2, 20 - suffix.Length));

                candidate = p + suffix;
                if (!ExistsCode(os, candidate)) return candidate;
            }

            // 3) Secours improbable
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static bool ExistsCode(IObjectSpace os, string code)
        {
            return os.GetObjectsQuery<Rubrique>().Any(r => r.Code == code);
        }
    }
}
