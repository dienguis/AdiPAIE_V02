// GraphEmailSender.cs
using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using Azure.Identity;
using Microsoft.Graph;
using MsGraph = Microsoft.Graph;
using MsModels = Microsoft.Graph.Models;
using MailAttachment = System.Net.Mail.Attachment;

namespace AdiPAIE_V02.Module.Services
{
    public sealed class GraphEmailSender : IEmailSender
    {
        private readonly GraphServiceClient _graph;
        private readonly string _fromUpn;

        // Seuil "petit" attachement : envoie direct via sendMail
        private const int SmallAttachmentThreshold = 3 * 1024 * 1024; // 3 MiB
        // Chunk 4 MiB (valeur sûre)
        private const int ChunkSize = 4 * 1024 * 1024;

        public GraphEmailSender(string tenantId, string clientId, string clientSecret, string fromUpn)
        {
            var cred = new ClientSecretCredential(tenantId, clientId, clientSecret);
            _graph = new GraphServiceClient(cred, new[] { "https://graph.microsoft.com/.default" });
            _fromUpn = fromUpn ?? throw new ArgumentException(nameof(fromUpn));
        }

        public void Send(string to, string subject, string body, MailAttachment att = null)
        {
            var isHtml = body?.IndexOf('<') >= 0;
            var msg = new MsModels.Message
            {
                Subject = subject ?? string.Empty,
                Body = new MsModels.ItemBody
                {
                    ContentType = isHtml ? MsModels.BodyType.Html : MsModels.BodyType.Text,
                    Content = body ?? string.Empty
                },
                ToRecipients = new List<MsModels.Recipient> {
                    new MsModels.Recipient { EmailAddress = new MsModels.EmailAddress { Address = to } }
                }
            };

            // Taille de la PJ (si stream inconnu, on copie en mémoire)
            long attSize = 0;
            MemoryStream fallbackMs = null;
            if (att != null)
            {
                if (att.ContentStream is MemoryStream mm && mm.TryGetBuffer(out _))
                {
                    attSize = mm.Length;
                    if (mm.CanSeek) mm.Position = 0;
                }
                else if (att.ContentStream.CanSeek)
                {
                    attSize = att.ContentStream.Length;
                    att.ContentStream.Position = 0;
                }
                else
                {
                    fallbackMs = new MemoryStream();
                    att.ContentStream.CopyTo(fallbackMs);
                    attSize = fallbackMs.Length;
                    fallbackMs.Position = 0;
                }
            }

            if (att == null || attSize <= SmallAttachmentThreshold)
            {
                // --- Envoi simple (petite PJ ou pas de PJ)
                if (att != null)
                {
                    var buf = fallbackMs ?? (att.ContentStream as MemoryStream);
                    if (buf == null)
                    {
                        buf = new MemoryStream();
                        att.ContentStream.CopyTo(buf);
                        buf.Position = 0;
                    }

                    var fileAtt = new MsModels.FileAttachment
                    {
                        Name = att.Name,
                        ContentType = att.ContentType?.MediaType,
                        ContentBytes = buf.ToArray()
                    };
                    msg.Attachments = new List<MsModels.Attachment> { fileAtt };
                }

                var req = new MsGraph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = msg,
                    SaveToSentItems = true
                };

                _graph.Users[_fromUpn].SendMail.PostAsync(req)
                      .ConfigureAwait(false).GetAwaiter().GetResult();
                return;
            }

            // --- Gros fichier : draft + upload session (chunk) + send
            // 1) Créer le brouillon
            var draft = _graph.Users[_fromUpn].Messages.PostAsync(msg)
                             .ConfigureAwait(false).GetAwaiter().GetResult();

            // 2) Créer la session d’upload
            var uploadSession = _graph.Users[_fromUpn]
                .Messages[draft.Id]
                .Attachments
                .CreateUploadSession
                .PostAsync(new MsGraph.Users.Item.Messages.Item.Attachments.CreateUploadSession.CreateUploadSessionPostRequestBody
                {
                    AttachmentItem = new MsModels.AttachmentItem
                    {
                        AttachmentType = MsModels.AttachmentType.File,
                        Name = att.Name,
                        Size = attSize,
                        ContentType = att.ContentType?.MediaType
                    }
                })
                .ConfigureAwait(false).GetAwaiter().GetResult();

            // 3) Uploader par tranches sur uploadSession.UploadUrl (HTTP PUT)
            using (var http = new HttpClient())
            {
                Stream src = fallbackMs ?? att.ContentStream;
                if (src.CanSeek) src.Position = 0;

                long total = attSize;
                long offset = 0;
                byte[] buffer = new byte[ChunkSize];

                while (offset < total)
                {
                    int toRead = (int)Math.Min(ChunkSize, total - offset);
                    int read = ReadExactly(src, buffer, 0, toRead);
                    if (read <= 0) break;

                    using var content = new ByteArrayContent(buffer, 0, read);
                    content.Headers.Add("Content-Length", read.ToString());
                    content.Headers.Add("Content-Range", $"bytes {offset}-{offset + read - 1}/{total}");

                    var put = new HttpRequestMessage(HttpMethod.Put, uploadSession.UploadUrl) { Content = content };
                    var resp = http.Send(put);
                    if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.Created && (int)resp.StatusCode != 202)
                    {
                        var err = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        throw new InvalidOperationException($"Upload chunk failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {err}");
                    }

                    offset += read;
                }
            }

            // 4) Envoyer le brouillon
            _graph.Users[_fromUpn].Messages[draft.Id].Send.PostAsync()
                  .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static int ReadExactly(Stream s, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int r = s.Read(buffer, offset + total, count - total);
                if (r == 0) break;
                total += r;
            }
            return total;
        }
    }
}
