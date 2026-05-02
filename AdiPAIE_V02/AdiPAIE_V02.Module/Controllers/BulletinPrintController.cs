using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.Data.Filtering;
using DevExpress.Drawing;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Base.ReportsV2;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraPrinting.Drawing;
using System;
using System.Drawing;

namespace AdiPAIE_V02.Module.Controllers
{
    // Si ton DetailView affiche des Bulletins, utilise Bulletin ici.
    // Si c'est un autre type (ex: FichePaie), remplace-le.
    public class BulletinPrintController : ObjectViewController<DetailView, Bulletin>
    {
        // ⚠️ Mets EXACTEMENT le nom du rapport tel qu’il apparaît dans la liste (Reports)
        private const string ReportDisplayName = "BulletinPaie";

        public BulletinPrintController()
        {
            var act = new SimpleAction(this, "ImprimerBulletin", PredefinedCategory.Reports)
            {
                Caption = "Imprimer",
                ImageName = "Print",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Imprime / télécharge le bulletin sélectionné en PDF."
            };

            act.Execute += (s, e) =>
            {
                var current = View.CurrentObject;
                if (current == null)
                    return;

                // 1) Récupérer la clé primaire (Oid ou autre) et construire le critère dynamiquement
                var keyMember = View.ObjectTypeInfo.KeyMember; // ex: "Oid"
                var keyValue = ObjectSpace.GetKeyValue(current); // valeur réelle
                var criteria = CriteriaOperator.Parse($"[{keyMember.Name}] = ?", keyValue);

                using var os = Application.CreateObjectSpace(typeof(ReportDataV2));
                var rd = os.FirstOrDefault<ReportDataV2>(d => d.DisplayName == ReportDisplayName);
                if (rd == null)
                    throw new UserFriendlyException($"Rapport '{ReportDisplayName}' introuvable dans Reports.");

                // (Optionnel mais recommandé) Vérifier que le DataType du rapport correspond
                // au type de l'objet en cours (sinon le critère ne filtrera rien).
                var reportTypeName = rd.DataTypeName; // ex: "AdiPAIE_V02.Module.BusinessObjects.Bulletin"
                var viewTypeName = View.ObjectTypeInfo.Type.FullName;
                if (!string.Equals(reportTypeName, viewTypeName, StringComparison.Ordinal))
                {
                    throw new UserFriendlyException(
                        $"Le rapport '{ReportDisplayName}' est basé sur '{reportTypeName}', " +
                        $"mais la vue courante affiche '{viewTypeName}'. " +
                        $"Ouvre le designer du rapport et règle Data Type = {viewTypeName}."
                    );
                }

                // 2) Obtenir le handle et ouvrir l’aperçu avec le critère
                var storage = ReportDataProvider.ReportsStorage; // IReportStorage (25.1)
                string handle = storage.GetReportContainerHandle(rd);

                Frame.GetController<ReportServiceController>()?.ShowPreview(handle, criteria);
            };
        }
    }
}
