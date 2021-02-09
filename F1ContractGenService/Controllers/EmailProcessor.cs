using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace F1ContractGenService.Controllers
{
    public class EmailProcessor
    {
        public bool SendAlertOnBookingFailed(string filePath, string ToEmail)
        {

            try
            {

                var fromAddress = new MailAddress("gimhanishiran@gmail.com", "Ushan Blinds Australia");
                var toAddress = new MailAddress(ToEmail, "To Customer");
                const string fromPassword = "Gimhani20201111";
                const string subject = "Quotation for Customer";
                const string body = "Sample Quation with the attachment!";
                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword),

                };


                System.Net.Mail.Attachment attachment;
                attachment = new System.Net.Mail.Attachment(@"C:\USBProperties\Documents\Quatations\QUOTATION.pdf");


                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,


                })
                {
                    message.Attachments.Add(attachment);
                    smtp.Send(message);
                }


                return true;
            }
            catch (Exception ex)
            {

                return false;
            }


        }
    }
}