using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Collections.Generic;
using System.IO;

namespace BlogWebApi.Helpers
{
    public class Email
    {
        private readonly EmailSettings _emailSettings;

        public Email(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public string SendMessage(string msg, string subject, List<string> emailTo)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("No Reply", _emailSettings.Username));
            foreach (var email in emailTo)
            {
                message.To.Add(new MailboxAddress("Recipient", email));
            }

            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = msg };

            using (var client = new SmtpClient())
            {
                client.Connect(_emailSettings.Host, _emailSettings.Port, SecureSocketOptions.StartTls);
                client.Authenticate(_emailSettings.Username, _emailSettings.Password);
                client.Send(message);
                client.Disconnect(true);
            }

            return "Success";
        }

        public string SendMessageWithAttachment(string msg, string subject, string emailTo, Stream file, string name)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("No Reply", _emailSettings.Username));
            message.To.Add(new MailboxAddress("Recipient", emailTo));
            message.Subject = subject;

            var body = new TextPart("plain") { Text = msg };

            var attachment = new MimePart("application", "octet-stream")
            {
                Content = new MimeContent(file),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = Path.GetFileName(name)
            };

            var multipart = new Multipart("mixed") { body, attachment };
            message.Body = multipart;

            using (var client = new SmtpClient())
            {
                client.Connect(_emailSettings.Host, _emailSettings.Port, SecureSocketOptions.StartTls);
                client.Authenticate(_emailSettings.Username, _emailSettings.Password);
                client.Send(message);
                client.Disconnect(true);
            }

            return "Success";
        }
    }
}
