// AdiPAIE_V02.Module/Services/EmailSenderAsyncExtensions.cs
using System.Threading.Tasks;
using System.Net.Mail; // Attachment

namespace AdiPAIE_V02.Module.Services
{
    public static class EmailSenderAsyncExtensions
    {
        public static Task SendAsync(
            this IEmailSender sender,
            string to,
            string subject,
            string htmlBody,
            Attachment attachment = null)
        {
            // On délègue au threadpool le Send synchrone pour ne pas bloquer l’UI
            return Task.Run(() => sender.Send(to, subject, htmlBody, attachment));
        }
    }
}
