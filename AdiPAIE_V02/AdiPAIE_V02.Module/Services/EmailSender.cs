using System;
using System.Net;
using System.Net.Mail;
using System.Threading;

namespace AdiPAIE_V02.Module.Services
{
    public interface IEmailSender
    {
        void Send(string to, string subject, string body, Attachment attachment = null);
    }

    /// <summary>
    /// V1.5.2 (QW4) — SmtpEmailSender amélioré :
    ///   - Timeout configurable (par défaut 30 secondes)
    ///   - Retry exponentiel (3 tentatives avec backoff 1s / 3s / 9s) sur
    ///     erreurs transitoires (timeout, déconnexion réseau, status 4xx/5xx
    ///     temporaires)
    ///   - using strict sur SmtpClient pour éviter fuite de socket
    ///   - .NET SmtpClient gère déjà le pooling de connexion en interne au
    ///     niveau du process (clés [host:port:user:ssl]) → créer une instance
    ///     fraîche par Send() reste performant tant que les paramètres sont
    ///     constants (cas chez ELTON)
    ///
    /// Le SendAsync (extension dans EmailSenderAsyncExtensions) délègue
    /// au threadpool — cumul de Send synchrones, déjà non bloquant pour l'UI.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly string host;
        private readonly int port;
        private readonly bool enableSsl;
        private readonly string user;
        private readonly string pass;
        private readonly string from;

        // V1.5.2 — config robustesse
        private readonly int timeoutMs;
        private readonly int maxRetries;
        private readonly int initialBackoffMs;

        public SmtpEmailSender(string host, int port, bool enableSsl,
            string user, string pass, string from,
            int timeoutSec = 30,
            int maxRetries = 3,
            int initialBackoffMs = 1000)
        {
            this.host = host;
            this.port = port;
            this.enableSsl = enableSsl;
            this.user = user;
            this.pass = pass;
            this.from = from;
            this.timeoutMs = Math.Max(5_000, timeoutSec * 1_000);
            this.maxRetries = Math.Max(1, maxRetries);
            this.initialBackoffMs = Math.Max(200, initialBackoffMs);
        }

        public void Send(string to, string subject, string body, Attachment attachment = null)
        {
            Exception lastEx = null;
            int backoff = initialBackoffMs;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    SendOnce(to, subject, body, attachment);
                    return; // succès
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    lastEx = ex;
                    System.Diagnostics.Debug.WriteLine(
                        $"[SMTP retry {attempt}/{maxRetries}] {ex.GetType().Name} : {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(backoff);
                        backoff *= 3; // exponentiel : 1s → 3s → 9s
                    }
                }
                catch (Exception ex)
                {
                    // Erreur permanente (config invalide, recipient bounce…) → on relève
                    System.Diagnostics.Debug.WriteLine(
                        $"[SMTP fatal] {ex.GetType().Name} : {ex.Message}");
                    throw;
                }
            }

            // Tous les retries ont échoué
            throw new InvalidOperationException(
                $"Échec envoi email après {maxRetries} tentatives. "
                + $"Dernière erreur : {lastEx?.Message}", lastEx);
        }

        private void SendOnce(string to, string subject, string body, Attachment attachment)
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(user, pass),
                Timeout = timeoutMs,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };
            using var msg = new MailMessage(from, to, subject, body)
            {
                IsBodyHtml = true,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };
            if (attachment != null) msg.Attachments.Add(attachment);
            client.Send(msg);
        }

        /// <summary>
        /// Détecte les erreurs SMTP transitoires (réessayables).
        /// Filtre sur SmtpException + catégorie statut, et inclut TimeoutException
        /// + SocketException (réseau temporairement indisponible).
        /// </summary>
        private static bool IsTransient(Exception ex)
        {
            if (ex is TimeoutException) return true;
            if (ex is System.Net.Sockets.SocketException) return true;

            if (ex is SmtpException smtp)
            {
                // 4xx = transient, 5xx = permanent (en général)
                switch (smtp.StatusCode)
                {
                    case SmtpStatusCode.ServiceNotAvailable:        // 421
                    case SmtpStatusCode.MailboxBusy:                // 450
                    case SmtpStatusCode.LocalErrorInProcessing:     // 451
                    case SmtpStatusCode.InsufficientStorage:        // 452
                    case SmtpStatusCode.GeneralFailure:             // -1, parfois transient
                    case SmtpStatusCode.TransactionFailed:          // 554, parfois transient
                        return true;
                    default:
                        return false;
                }
            }

            // Inner exception réseau ?
            if (ex.InnerException != null && IsTransient(ex.InnerException)) return true;

            return false;
        }
    }
}
