// AdiPAIE_V02.Module/Services/DbConfigHelper.cs
// Lecture/écriture du fichier dbconfig.json pour rendre la connexion BDD
// configurable depuis l'UI (Paramètres de paie → Connexion BDD).
// Après modification, un redémarrage de l'application est nécessaire.
//
// V1.1 (2026-05-03) — Storage déplacé vers %PROGRAMDATA%\AdiPAIE_V02\dbconfig.json
// pour SURVIVRE au clean+rebuild. La copie dans le bin de l'app n'est utilisée
// que comme TEMPLATE initial au premier démarrage (bootstrap automatique).
//
// V1.1 (2026-05-03) — Mot de passe SQL CHIFFRÉ via DPAPI (Windows ProtectedData)
// avec scope LocalMachine. Format stocké : password=DPAPI:<base64>;
// L'UI Paramètres de paie (et les SqlClient) reçoivent le mot de passe en clair :
// la couche helper chiffre/déchiffre de manière transparente.
// Migration auto : un mot de passe legacy en clair est chiffré au 1er ReadConnectionString.
//
// Avant V1.1, le fichier était dans bin/Debug/net8.0/ — donc supprimé au moindre
// "Clean Solution" et écrasé par la version source du repo au build suivant. ET
// le mot de passe était stocké en clair, lisible par tout admin local.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdiPAIE_V02.Module.Services
{
    public static class DbConfigHelper
    {
        private static readonly object _lock = new();
        private static bool _bootstrapAttempted;

        /// <summary>
        /// Chemin de PRODUCTION du dbconfig.json — survit au clean+rebuild.
        /// Emplacement : %PROGRAMDATA%\AdiPAIE_V02\dbconfig.json
        ///               (ex: C:\ProgramData\AdiPAIE_V02\dbconfig.json)
        /// Le dossier est créé automatiquement à la première écriture.
        /// </summary>
        public static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "AdiPAIE_V02");
                try { Directory.CreateDirectory(dir); } catch { /* permissions */ }
                return Path.Combine(dir, "dbconfig.json");
            }
        }

        /// <summary>
        /// Chemin LEGACY (bin/dbconfig.json) — utilisé uniquement comme TEMPLATE
        /// initial au premier démarrage, pour bootstrap depuis le repo.
        /// </summary>
        private static string LegacyBinPath
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.json");

        /// <summary>
        /// Migration one-shot : si %PROGRAMDATA%\AdiPAIE_V02\dbconfig.json
        /// n'existe pas mais qu'on a un template dans le bin, copier.
        /// Idempotent (ne fait rien après la première copie).
        /// </summary>
        public static void EnsureBootstrapped()
        {
            if (_bootstrapAttempted) return;
            _bootstrapAttempted = true;

            try
            {
                var prod = ConfigPath;
                if (File.Exists(prod)) return; // déjà bootstrappé

                var legacy = LegacyBinPath;
                if (File.Exists(legacy))
                {
                    File.Copy(legacy, prod, overwrite: false);
                }
            }
            catch
            {
                // Permissions, disque plein, etc. : silencieux —
                // l'app peut quand même démarrer si appsettings.json a la conn.
            }
        }

        // ── Chiffrement DPAPI du mot de passe SQL ──
        private const string DPAPI_PREFIX = "DPAPI:";

        /// <summary>Chiffre une valeur via DPAPI (machine scope). Retourne la valeur
        /// préfixée DPAPI: + base64. Idempotent : si déjà chiffré, ne re-chiffre pas.
        /// En cas d'échec (ex : non-Windows), retourne la valeur en clair.</summary>
        private static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            if (plain.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal)) return plain;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(plain);
                var encrypted = ProtectedData.Protect(bytes, optionalEntropy: null,
                    scope: DataProtectionScope.LocalMachine);
                return DPAPI_PREFIX + Convert.ToBase64String(encrypted);
            }
            catch
            {
                // DPAPI indisponible (non-Windows, droits insuffisants, etc.)
                // → fallback en clair : mieux que perdre la donnée
                return plain;
            }
        }

        /// <summary>Déchiffre une valeur DPAPI: si présente, sinon retourne tel quel.
        /// Retourne null si le déchiffrement échoue (ex : changement de machine).</summary>
        private static string Unprotect(string maybeEncrypted)
        {
            if (string.IsNullOrEmpty(maybeEncrypted)) return maybeEncrypted;
            if (!maybeEncrypted.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal))
                return maybeEncrypted;
            try
            {
                var encrypted = Convert.FromBase64String(maybeEncrypted.Substring(DPAPI_PREFIX.Length));
                var decrypted = ProtectedData.Unprotect(encrypted, optionalEntropy: null,
                    scope: DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // Possible si le fichier a été copié depuis une autre machine,
                // ou si le profil machine a changé (réinstall Windows, etc.).
                // Le user devra re-saisir le mot de passe via l'UI.
                return null;
            }
        }

        /// <summary>Déchiffre le mot de passe DPAPI dans une chaîne de connexion SQL,
        /// le remplaçant par le clair pour SqlClient. Utilisé par Program.cs au démarrage.</summary>
        public static string DecryptConnectionString(string connStr)
        {
            if (string.IsNullOrWhiteSpace(connStr)) return connStr;
            try
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = connStr };
                if (builder.TryGetValue("Password", out var pwdObj))
                {
                    var pwd = pwdObj?.ToString();
                    if (!string.IsNullOrEmpty(pwd) && pwd.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal))
                    {
                        var clear = Unprotect(pwd);
                        if (clear != null)
                        {
                            builder["Password"] = clear;
                            return builder.ConnectionString;
                        }
                    }
                }
                return connStr;
            }
            catch
            {
                return connStr;
            }
        }

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

        /// <summary>Lit une valeur depuis la connection string stockée dans dbconfig.json.
        /// Pour le Password, déchiffre automatiquement le DPAPI: pour exposer le clair
        /// à l'UI (les autres clés sont retournées telles quelles).</summary>
        public static string GetValue(string key)
        {
            try
            {
                var connStr = ReadConnectionString();
                if (string.IsNullOrWhiteSpace(connStr)) return null;

                var builder = new DbConnectionStringBuilder { ConnectionString = connStr };
                var sqlKey = KeyMap.TryGetValue(key, out var mapped) ? mapped : key;

                var raw = builder.TryGetValue(sqlKey, out var val) ? val?.ToString() : null;

                // Pour le Password : déchiffrer DPAPI: si présent (transparent pour l'UI)
                if (string.Equals(sqlKey, "Password", StringComparison.OrdinalIgnoreCase))
                    return Unprotect(raw);

                return raw;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Modifie une valeur et sauvegarde dans dbconfig.json.
        /// Pour le Password, chiffre automatiquement via DPAPI avant écriture
        /// (le caller passe le mot de passe en clair, le helper s'occupe du reste).</summary>
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

                    // Pour le Password : chiffrer DPAPI avant stockage
                    var valueToStore = string.Equals(sqlKey, "Password", StringComparison.OrdinalIgnoreCase)
                        ? Protect(value)
                        : (value ?? "");

                    builder[sqlKey] = valueToStore ?? "";

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
            // Au premier accès, copier le template bin → ProgramData si nécessaire
            EnsureBootstrapped();

            var path = ConfigPath;
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                || !cs.TryGetProperty("ConnectionString", out var val))
                return null;

            var connStr = val.GetString();

            // Migration auto : si legacy (password en clair), chiffrer en arrière-plan
            EnsurePasswordEncryptedOnDisk(connStr);

            return connStr;
        }

        /// <summary>Migration one-shot : si le password est en clair sur disque (legacy),
        /// le chiffre via DPAPI et ré-écrit le fichier. Ne touche jamais à un password
        /// déjà au format DPAPI: ni à un password vide.</summary>
        private static void EnsurePasswordEncryptedOnDisk(string connStr)
        {
            if (string.IsNullOrWhiteSpace(connStr)) return;
            try
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = connStr };
                if (!builder.TryGetValue("Password", out var pwdObj)) return;

                var pwd = pwdObj?.ToString();
                if (string.IsNullOrEmpty(pwd)) return;
                if (pwd.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal)) return; // déjà chiffré

                // Password legacy en clair détecté → chiffrer + ré-écrire
                lock (_lock)
                {
                    builder["Password"] = Protect(pwd);
                    WriteConnectionString(builder.ConnectionString);
                }
            }
            catch
            {
                // Silencieux : la migration sera retentée au prochain Read
            }
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
