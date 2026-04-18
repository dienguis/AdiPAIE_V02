using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Model.DomainLogics;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.ExpressApp.Updating;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.Xpo;

namespace AdiPAIE_V02.Module
{
    public sealed class AdiPAIE_V02Module : ModuleBase
    {
        public AdiPAIE_V02Module()
        {
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.ModelDifference));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.ModelDifferenceAspect));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.BaseObject));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.AuditDataItemPersistent));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.AuditedObjectWeakReference));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.FileData));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.FileAttachmentBase));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.Analysis));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.Event));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.Resource));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.HCategory));

            // ========== AJOUTER ICI ==========
            //AdditionalExportedTypes.Add(typeof(SimulationSursalaire));
            AdditionalExportedTypes.Add(typeof(AdiPAIE_V02.Module.BusinessObjects.SimulationSursalaire));

            // =================================

            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Security.SecurityModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.AuditTrail.AuditTrailModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Objects.BusinessClassLibraryCustomizationModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Chart.ChartModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.CloneObject.CloneObjectModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ConditionalAppearance.ConditionalAppearanceModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Dashboards.DashboardsModule));
            // RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Kpi.KpiModule)); // Désactivé temporairement — conflit version 25.1.9 vs 25.1.10
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Notifications.NotificationsModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Office.OfficeModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.PivotChart.PivotChartModuleBase));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.PivotGrid.PivotGridModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ReportsV2.ReportsModuleV2));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Scheduler.SchedulerModuleBase));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ScriptRecorder.ScriptRecorderModuleBase));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.StateMachine.StateMachineModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.TreeListEditors.TreeListEditorsModuleBase));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Validation.ValidationModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ViewVariantsModule.ViewVariantsModule));
        }

        public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
        {
            ModuleUpdater updater = new DatabaseUpdate.Updater(objectSpace, versionFromDB);
            return new ModuleUpdater[] { updater };
        }

        public override void Setup(XafApplication application)
        {
            base.Setup(application);

            // ========== AJOUTER ==========
            application.SetupComplete += Application_SetupComplete;
            // =============================

        }

        // ========== AJOUTER CETTE MÉTHODE ==========
        private void Application_SetupComplete(object sender, EventArgs e)
        {
            var app = (XafApplication)sender;

            // Enregistrer le NonPersistentObjectSpaceProvider si pas déjà présent
            if (!app.ObjectSpaceProviders.OfType<NonPersistentObjectSpaceProvider>().Any())
            {
                var npos = new NonPersistentObjectSpaceProvider(app.TypesInfo, null);
                app.ObjectSpaceProviders.Add(npos);
            }
        }
        // ===========================================

        public override void CustomizeTypesInfo(ITypesInfo typesInfo)
        {
            base.CustomizeTypesInfo(typesInfo);
            CalculatedPersistentAliasHelper.CustomizeTypesInfo(typesInfo);
        }
    }
}