using System.Collections.Generic;
using System.Net;
using BlogWebApi.Models;
using MailKit.Security;
using MailKit.Net.Smtp;
using MimeKit;
using System.IO;
namespace BlogWebApi.Helpers
{
    public class Email
    {
        public static string SendMessage(string msg, string subject, List<string> emailTo)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("No Reply", "arhabumer5@gmail.com"));
            foreach (var email in emailTo)
            {
                try
                {
                    message.To.Add(new MailboxAddress("Recipient", email));
                }
                catch { }
            }
            message.Subject = subject;

            message.Body = new TextPart("plain")
            {
                Text = msg
            };
            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);

                client.Authenticate("arhabumer5@gmail.com", "arhab2004");

                var options = FormatOptions.Default.Clone();

                if (client.Capabilities.HasFlag(SmtpCapabilities.UTF8))
                    options.International = true;

                client.Send(options, message);

                client.Disconnect(true);
            }

            return "Success";
        }

        public static string SendMessageWithAttachment(string msg, string subject, string emailTo, Stream file, string name)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("No Reply", "arhabumer5@gmail.com"));
            message.To.Add(new MailboxAddress("Recipient", emailTo));
            message.Subject = subject;

            var body = new TextPart("plain")
            {
                Text = msg
            };

            var attachment = new MimePart("image", "gif")
            {
                Content = new MimeContent(file),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = Path.GetFileName(name)
            };
            var multipart = new Multipart("mixed");
            multipart.Add(body);
            multipart.Add(attachment);
            message.Body = multipart;

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);

                client.Authenticate("arhabumer5@gmail.com", "arhab2004");

                var options = FormatOptions.Default.Clone();

                if (client.Capabilities.HasFlag(SmtpCapabilities.UTF8))
                    options.International = true;

                client.Send(options, message);

                client.Disconnect(true);
            }

            return "Success";
        }
    }
}
