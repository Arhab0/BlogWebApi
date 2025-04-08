using System.Net.Mail;
using System.Net;

namespace BlogWebApi.Helpers
{
    public class Email
    {
        public static void sendMail(string Subject, string body, List<string> toEmail, List<(byte[] FileBytes, string FileName)> attachments = null)
        {

            var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();

            string smtpHost = configuration["EmailSettings:Host"];
            int smtpPort = configuration.GetValue<int>("EmailSettings:Port");
            bool enableSsl = configuration.GetValue<bool>("EmailSettings:EnableSsl");
            string smtpUsername = configuration["EmailSettings:Username"];
            string smtpPassword = configuration["EmailSettings:Password"];
            SmtpClient smtp = new SmtpClient(smtpHost)
            {
                Port = smtpPort,
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            };
            MailMessage mail = new MailMessage
            {
                From = new MailAddress(smtpUsername),
                Subject = Subject,
                Body = body,
                IsBodyHtml = true
            };
            // Add recipients to the mail message
            foreach (var email in toEmail)
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    mail.To.Add(new MailAddress(email));
                }
            }

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    if (attachment.FileBytes != null && !string.IsNullOrWhiteSpace(attachment.FileName))
                    {
                        var memoryStream = new MemoryStream(attachment.FileBytes);
                        mail.Attachments.Add(new Attachment(memoryStream, attachment.FileName));
                    }
                }
            }

            try
            {
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }
            finally
            {
                if (mail.Attachments != null)
                {
                    foreach (var attachment in mail.Attachments)
                    {
                        attachment.Dispose();
                    }
                }
                mail.Dispose();
            }
        }
    }
}
