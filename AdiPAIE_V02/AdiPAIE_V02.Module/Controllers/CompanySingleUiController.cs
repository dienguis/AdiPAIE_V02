using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;

public class CompanySingleUiController : ViewController<ListView>
{
    protected override void OnActivated()
    {
        base.OnActivated();
        if (View.ObjectTypeInfo?.Type != typeof(Company)) return;

        var newCtrl = Frame.GetController<NewObjectViewController>();
        if (newCtrl?.NewObjectAction != null)
        {
            bool hasOne = ObjectSpace.GetObjectsCount(typeof(Company), null) > 0;
            newCtrl.NewObjectAction.Active["MonoCompany"] = !hasOne; // désactive si déjà 1
        }
    }
}
