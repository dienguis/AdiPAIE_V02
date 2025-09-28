using System;
using System.Net;
using System.Net.Mail;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Actions;

namespace AdiPAIE_V02.Module.Services
{
    public interface IEmailSender
    {
        void Send(string to, string subject, string body, Attachment attachment = null);
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly string host;
        private readonly int port;
        private readonly bool enableSsl;
        private readonly string user;
        private readonly string pass;
        private readonly string from;

        public SmtpEmailSender(string host, int port, bool enableSsl, string user, string pass, string from)
        {
            this.host = host; this.port = port; this.enableSsl = enableSsl;
            this.user = user; this.pass = pass; this.from = from;
        }

        public void Send(string to, string subject, string body, Attachment attachment = null)
        {
            using var client = new SmtpClient(host, port) { EnableSsl = enableSsl, Credentials = new NetworkCredential(user, pass) };
            using var msg = new MailMessage(from, to, subject, body) { IsBodyHtml = true };
            if (attachment != null) msg.Attachments.Add(attachment);
            client.Send(msg);
        }
    }
}
