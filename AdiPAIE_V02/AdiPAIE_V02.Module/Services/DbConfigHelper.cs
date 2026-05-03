// AdiPAIE_V02.Module/Services/DbConfigHelper.cs
// Lecture/écriture du fichier dbconfig.json pour rendre la connexion BDD
// configurable depuis l'UI (Paramètres de paie → Connexion BDD).
// Après modification, un redémarrage de l'application est nécessaire.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text.Json;

namespace AdiPAIE_V02.Module.Services
{
    public static class DbConfigHelper
    {
        private static readonly object _lock = new();

        /// <summary>
        /// Chemin vers dbconfig.json (à côté de l'exécutable).
        /// </summary>
        private static string ConfigPath
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.json");

        // ── Mapping clé interne → clé SqlConnectionStringBuilder ──
        private static readonly Dictionary<string, string> KeyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DataSource"] = "Data Source",
            ["InitialCatalog"] = "Initial Catalog",
            ["IntegratedSecurity"] = "Integrated Security",
            ["UserID"] = "User ID",
            ["Password"] = "Password",
            ["TrustServerCertificate"] = "TrustServerCertificate",
        };

        /// <summary>Lit une valeur depuis la connection string stockée dans dbconfig.json.</summary>
        public static string GetValue(string key)
        {
            try
            {
                var connStr = ReadConnectionString();
                if (string.IsNullOrWhiteSpace(connStr)) return null;

                var builder = new DbConnectionStringBuilder { ConnectionString = connStr };
                var sqlKey = KeyMap.TryGetValue(key, out var mapped) ? mapped : key;

                return builder.TryGetValue(sqlKey, out var val) ? val?.ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Modifie une valeur et sauvegarde dans dbconfig.json.</summary>
        public static void SetValue(string key, string value)
        {
            lock (_lock)
            {
                try
                {
                    var connStr = ReadConnectionString() ?? "";
                    var builder = new DbConnectionStringBuilder();
                    if (!string.IsNullOrWhiteSpace(connStr))
                        builder.ConnectionString = connStr;

                    var sqlKey = KeyMap.TryGetValue(key, out var mapped) ? mapped : key;
                    builder[sqlKey] = value ?? "";

                    // Nettoyer login/password si SSPI
                    if (builder.TryGetValue("Integrated Security", out var intSec))
                    {
                        var v = intSec?.ToString()?.ToUpperInvariant();
                        if (v is "SSPI" or "TRUE" or "YES")
                        {
                            builder.Remove("User ID");
                            builder.Remove("Password");
                        }
                    }

                    WriteConnectionString(builder.ConnectionString);
                }
                catch
                {
                    // Silencieux — ne pas bloquer l'UI
                }
            }
        }

        /// <summary>Construit la connection string complète (lecture seule pour aperçu).
        /// Le mot de passe est masqué pour la sécurité.</summary>
        public static string BuildConnectionString()
        {
            try
            {
                var connStr = ReadConnectionString();
                if (string.IsNullOrWhiteSpace(connStr)) return "(non configurée)";

                var builder = new DbConnectionStringBuilder { ConnectionString = connStr };
                if (builder.TryGetValue("Password", out var pwd)
                    && pwd != null
                    && !string.IsNullOrEmpty(pwd.ToString()))
                {
                    builder["Password"] = "********";
                }
                return builder.ConnectionString;
            }
            catch
            {
                return "(erreur de lecture)";
            }
        }

        // ── Lecture/écriture du fichier JSON ──

        private static string ReadConnectionString()
        {
            var path = ConfigPath;
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                && cs.TryGetProperty("ConnectionString", out var val))
            {
                return val.GetString();
            }
            return null;
        }

        private static void WriteConnectionString(string connStr)
        {
            var obj = new
            {
                ConnectionStrings = new
                {
                    ConnectionString = connStr
                }
            };
            var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
