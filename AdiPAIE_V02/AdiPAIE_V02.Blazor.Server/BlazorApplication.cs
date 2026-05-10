using AdiPAIE_V02.Blazor.Server.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Blazor;
using DevExpress.ExpressApp.MultiTenancy;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Security.ClientServer;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Updating;
using DevExpress.ExpressApp.Xpo;
using Microsoft.Extensions.DependencyInjection;

namespace AdiPAIE_V02.Blazor.Server
{
    public class AdiPAIE_V02BlazorApplication : BlazorApplication
    {
        public AdiPAIE_V02BlazorApplication()
        {
            ApplicationName = "SunuPaie";
            CheckCompatibilityType = DevExpress.ExpressApp.CheckCompatibilityType.DatabaseSchema;
            DatabaseVersionMismatch += AdiPAIE_V02BlazorApplication_DatabaseVersionMismatch;
        }

        protected override void OnSetupStarted()
        {
            base.OnSetupStarted();
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached && CheckCompatibilityType == CheckCompatibilityType.DatabaseSchema)
            {
                DatabaseUpdateMode = DatabaseUpdateMode.UpdateDatabaseAlways;
            }
#endif
        }

        void AdiPAIE_V02BlazorApplication_DatabaseVersionMismatch(object sender, DatabaseVersionMismatchEventArgs e)
        {
#if EASYTEST
            e.Updater.Update();
            e.Handled = true;
#else
            // V1.7 — Permettre l'update auto en production (déploiement initial + migrations futures).
            // XPO applique uniquement des ALTER TABLE non-destructifs (jamais de DROP COLUMN).
            // Pour bloquer l'auto-update en prod (par sécurité), commenter ces 2 lignes
            // et décommenter le bloc en dessous.
            e.Updater.Update();
            e.Handled = true;

            /* Ancien comportement strict — décommenter si on veut bloquer l'update prod
            if (System.Diagnostics.Debugger.IsAttached || TenantId != null)
            {
                e.Updater.Update();
                e.Handled = true;
            }
            else
            {
                string message = "The application cannot connect to the specified database, " +
                    "because the database doesn't exist, its version is older " +
                    "than that of the application or its schema does not match " +
                    "the ORM data model structure. To avoid this error, use one " +
                    "of the solutions from the https://www.devexpress.com/kb=T367835 KB Article.";
                if (e.CompatibilityError != null && e.CompatibilityError.Exception != null)
                {
                    message += "\r\n\r\nInner exception: " + e.CompatibilityError.Exception.Message;
                }
                throw new InvalidOperationException(message);
            }
            */
#endif
        }

        Guid? TenantId
        {
            get
            {
                return ServiceProvider?.GetService<ITenantProvider>()?.TenantId;
            }
        }
    }
}