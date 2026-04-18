using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Design;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.ExpressApp.ReportsV2.Win;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Win;
using DevExpress.ExpressApp.Win.ApplicationBuilder;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;

namespace AdiPAIE_V02.Win
{
    public class ApplicationBuilder : IDesignTimeApplicationFactory
    {
        public static WinApplication BuildApplication(string connectionString)
        {
            var builder = WinApplication.CreateBuilder();

            builder.UseApplication<AdiPAIE_V02WindowsFormsApplication>();
            builder.Modules
                .AddAuditTrailXpo()
                .AddCharts()
                .AddCloning()
                .AddConditionalAppearance()
                .AddDashboards(options =>
                {
                    options.DashboardDataType = typeof(DevExpress.Persistent.BaseImpl.DashboardData);
                    options.DesignerFormStyle = DevExpress.XtraBars.Ribbon.RibbonFormStyle.Ribbon;
                })
                .AddFileAttachments()
                //.AddKpi() // Désactivé temporairement — conflit version 25.1.9 vs 25.1.10
                .AddNotifications()
                .AddOffice()
                .AddPivotChart()
                .AddPivotGrid()
                .AddReports(options =>
                {
                    options.EnableInplaceReports = true;
                    options.ReportDataType = typeof(DevExpress.Persistent.BaseImpl.ReportDataV2);
                    options.ReportStoreMode = DevExpress.ExpressApp.ReportsV2.ReportStoreModes.XML;
                })
                .AddScheduler()
#if DEBUG
                .AddScriptRecorder()
#endif
                .AddStateMachine(options =>
                {
                    options.StateMachineStorageType = typeof(DevExpress.ExpressApp.StateMachine.Xpo.XpoStateMachine);
                })
                .AddTreeListEditors()
                .AddValidation(options =>
                {
                    options.AllowValidationDetailsAccess = false;
                })
                .AddViewVariants()
                .Add<AdiPAIE_V02.Module.AdiPAIE_V02Module>()
                .Add<AdiPAIE_V02WinModule>();

            builder.ObjectSpaceProviders
                .AddSecuredXpo((application, options) =>
                {
                    // Lit la ConnectionString depuis App.config
                    string connStr = ConfigurationManager
                        .ConnectionStrings["ConnectionString"]?.ConnectionString
                        ?? connectionString;
                    options.ConnectionString = connStr;
                })
                .AddNonPersistent();

            builder.Security
                .UseIntegratedMode(options =>
                {
                    options.Lockout.Enabled = true;
                    options.RoleType = typeof(PermissionPolicyRole);
                    options.UserType = typeof(AdiPAIE_V02.Module.BusinessObjects.ApplicationUser);
                    options.UserLoginInfoType = typeof(AdiPAIE_V02.Module.BusinessObjects.ApplicationUserLoginInfo);
                    options.UseXpoPermissionsCaching();
                    options.Events.OnSecurityStrategyCreated += securityStrategy =>
                    {
                        ((SecurityStrategy)securityStrategy).PermissionsReloadMode =
                            PermissionsReloadMode.NoCache;
                        SecurityStrategy.EnableSecurityForActions = true;
                    };
                })
                .AddPasswordAuthentication();

            builder.AddBuildStep(application =>
            {
                application.ConnectionString = connectionString;
#if DEBUG
                if (System.Diagnostics.Debugger.IsAttached &&
                    application.CheckCompatibilityType == CheckCompatibilityType.DatabaseSchema)
                {
                    application.DatabaseUpdateMode = DatabaseUpdateMode.UpdateDatabaseAlways;
                }
#endif
            });

            var winApplication = builder.Build();
            return winApplication;
        }

        XafApplication IDesignTimeApplicationFactory.Create()
            => BuildApplication(XafApplication.DesignTimeConnectionString);
    }
}