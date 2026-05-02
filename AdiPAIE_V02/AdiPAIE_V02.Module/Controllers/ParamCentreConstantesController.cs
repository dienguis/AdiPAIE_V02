using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace AdiPAIE_V02.Module.Controllers
{
    public class ParamCentreConstantesController : ObjectViewController<DetailView, CentreConstantesPaie>
    {
        readonly SimpleAction openBaremeIR;
        readonly SimpleAction openBaremeTRIMF;
        readonly SimpleAction openIRReductionFamille;
        readonly SimpleAction openTypeRubrique;
        readonly SimpleAction openRubrique;
        // readonly SimpleAction openParametresPaie;

        public ParamCentreConstantesController()
        {
            // Catégorie CUSTOM => visible seulement dans l'ActionContainerViewItem (ContainerId = "ParamsHub")
            openBaremeIR = new SimpleAction(this, "OpenBaremeIR", "ParamsHub")
            { Caption = "Barème IR", ImageName = "Shopping_Percent", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            openBaremeIR.Execute += (_, __) => OpenListView(typeof(BaremeIR));

            openBaremeTRIMF = new SimpleAction(this, "OpenBaremeTRIMF", "ParamsHub")
            { Caption = "Barème TRIMF", ImageName = "BO_List", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            openBaremeTRIMF.Execute += (_, __) => OpenListView(typeof(BaremeTRIMF));

            openIRReductionFamille = new SimpleAction(this, "OpenIRReductionFamille", "ParamsHub")
            { Caption = "Réduction familiale IR", ImageName = "BO_Organization", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            openIRReductionFamille.Execute += (_, __) => OpenListView(typeof(IRReductionFamille));

            openTypeRubrique = new SimpleAction(this, "openTypeRubrique", "HubRubrique")
            { Caption = "Types rubriques", ImageName = "BO_Unknown", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            openTypeRubrique.Execute += (_, __) => OpenListView(typeof(RubriqueTypeRef));

            openRubrique = new SimpleAction(this, "openRubrique", "HubRubrique")
            { Caption = "Rubriques paie", ImageName = "BO_Unknown", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            openRubrique.Execute += (_, __) => OpenListView(typeof(Rubrique));
            //  openParametresPaie = new SimpleAction(this, "OpenParametresPaie", "ParamsHub")
            // { Caption = "Paramètres Paie", ImageName = "BO_Properties", PaintStyle = ActionItemPaintStyle.CaptionAndImage };
            // openParametresPaie.Execute += (_, __) => OpenSingletonDetail(typeof(ParametresPaie));
        }

        void OpenListView(Type t)
        {
            var os = Application.CreateObjectSpace(t);
            var lv = Application.CreateListView(os, t, true);
            var svp = new ShowViewParameters(lv) { TargetWindow = TargetWindow.NewWindow };
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(null, null));
        }

        void OpenSingletonDetail(Type t)
        {
            var os = Application.CreateObjectSpace(t);
            var obj = os.FindObject(t, null) ?? os.CreateObject(t); // unique
            var dv = Application.CreateDetailView(os, obj);
            dv.ViewEditMode = ViewEditMode.Edit;
            var svp = new ShowViewParameters(dv) { TargetWindow = TargetWindow.NewWindow };
            Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(null, null));
        }
       
    }
}
