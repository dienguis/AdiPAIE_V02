// AdiPAIE_V02.Module/Services/PowerBIConfigService.cs
// Auto-détecte les paramètres SQL depuis la connection string de l'application
// et pré-remplit les champs Power BI dans ParametresPaie.
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System.Data.Common;

namespace AdiPAIE_V02.Module.Services
{
    public static class PowerBIConfigService
    {
        /// <summary>
        /// Pré-remplit les champs PowerBI_SqlServer et PowerBI_SqlDatabase
        /// à partir de la connection string active de l'application XAF,
        /// uniquement si ces champs sont encore vides.
        /// </summary>
        public static void AutoFillFromAppConnectionString(
            IObjectSpace os, string appConnectionString)
        {
            if (string.IsNullOrWhiteSpace(appConnectionString)) return;

            var param = ParametresPaie.TryGet(os);
            if (param == null) return;

            // Ne pas écraser si déjà renseigné
            if (!string.IsNullOrWhiteSpace(param.PowerBI_SqlServer)
                && !string.IsNullOrWhiteSpace(param.PowerBI_SqlDatabase))
                return;

            try
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = appConnectionString };

                if (string.IsNullOrWhiteSpace(param.PowerBI_SqlServer))
                {
                    if (builder.TryGetValue("Data Source", out var ds))
                        param.PowerBI_SqlServer = ds?.ToString();
                    else if (builder.TryGetValue("Server", out var srv))
                        param.PowerBI_SqlServer = srv?.ToString();
                }

                if (string.IsNullOrWhiteSpace(param.PowerBI_SqlDatabase))
                {
                    if (builder.TryGetValue("Initial Catalog", out var db))
                        param.PowerBI_SqlDatabase = db?.ToString();
                    else if (builder.TryGetValue("Database", out var dbAlt))
                        param.PowerBI_SqlDatabase = dbAlt?.ToString();
                }

                if (builder.TryGetValue("Integrated Security", out var intSec))
                {
                    var val = intSec?.ToString()?.ToUpperInvariant();
                    param.PowerBI_IntegratedSecurity =
                        val == "SSPI" || val == "TRUE" || val == "YES";
                }

                if (!param.PowerBI_IntegratedSecurity)
                {
                    if (string.IsNullOrWhiteSpace(param.PowerBI_SqlLogin)
                        && builder.TryGetValue("User ID", out var uid))
                        param.PowerBI_SqlLogin = uid?.ToString();
                }
            }
            catch
            {
                // Parsing échoué — l'admin remplira manuellement
            }
        }
    }
}
