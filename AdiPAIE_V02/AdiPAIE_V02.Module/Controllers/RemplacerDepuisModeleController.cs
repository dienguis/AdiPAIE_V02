
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{

    public class RemplacerDepuisModeleController : ObjectViewController<DetailView, Bulletin>
    {
        public RemplacerDepuisModeleController()
        {
            // Pas d'InitializeComponent() — fichier Designer supprimé
            var a = new SimpleAction(this, "RemplacerDepuisModele", PredefinedCategory.Edit)
            {
                Caption = "Remplacer modèle",
                ImageName = "Action_Reset",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage,
                ToolTip = "Supprime les lignes existantes et réapplique le modèle.",
                ConfirmationMessage = "Cette action supprime les lignes existantes et réapplique le modèle. Continuer ?"
            };
            a.Execute += OnExecute;
        }

        private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
        {
            var b = View.CurrentObject as Bulletin;
            if (b == null) return;

            if (b.Statut != BulletinStatut.Brouillon)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Action autorisée uniquement en Brouillon.",
                    InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            b.CopierDepuisModele(overwriteExistingLines: true, onlyIncludeDefault: false);
            ObjectSpace.CommitChanges();
            ObjectSpace.Refresh();
            Application.ShowViewStrategy.ShowMessage(
                "Bulletin remplacé depuis le modèle.",
                InformationType.Success, 3000, InformationPosition.Top);
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Bouton retiré de l'interface (remplacé par Recalculer)
            foreach (ActionBase a in Actions) a.Active["Retired"] = false;
        }
        protected override void OnDeactivated() => base.OnDeactivated();
    }
}
