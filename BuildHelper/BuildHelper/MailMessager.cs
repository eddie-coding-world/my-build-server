using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;

namespace BuildHelper
{
    public class MailMessager
    {
        public static string GmailUserName = string.Empty;
        public static string GmailPassword = string.Empty;

        public static List<string> SendToList = new List<string>();
        public static List<string> SendCcList = new List<string>();

        public static void SendMessage(string subjectString, string message, List<string> attachedFilePathList)
        {
            try
            {
                System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();

                if (SendToList != null && SendToList.Count > 0)
                {
                    foreach (string to in SendToList)
                        msg.To.Add(to);
                }

                if (SendCcList != null && SendCcList.Count > 0)
                {
                    foreach (string cc in SendCcList)
                        msg.CC.Add(cc);
                }


                if (msg.To.Count == 0 && msg.CC.Count == 0)
                {
                    Logger.Write("No receipients to send.");
                    return;
                }

                //這裡可以隨便填，不是很重要
                msg.From = new MailAddress(GmailUserName, "Protech OS Build Server", System.Text.Encoding.UTF8);
                /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                msg.Subject = subjectString;//郵件標題
                msg.SubjectEncoding = System.Text.Encoding.UTF8;//郵件標題編碼
                msg.Body = message;
                msg.BodyEncoding = System.Text.Encoding.UTF8;//郵件內容編碼 

                //附件
                if (attachedFilePathList != null)
                {
                    foreach (string attachedFilePath in attachedFilePathList)
                        msg.Attachments.Add(new Attachment(attachedFilePath));
                }

                msg.IsBodyHtml = true;//是否是HTML郵件 
                //msg.Priority = MailPriority.High;//郵件優先級 

                SmtpClient client = new SmtpClient();
                client.Credentials = new System.Net.NetworkCredential(GmailUserName, GmailPassword); //這裡要填正確的帳號跟密碼
                client.Host = "smtp.gmail.com"; //設定smtp Server
                client.Port = 587; //設定Port
                client.EnableSsl = true; //gmail預設開啟驗證
                client.Send(msg); //寄出信件
                client.Dispose();
                msg.Dispose();
                Logger.Write("Successfully sent the email");
            }
            catch (Exception ex)
            {
                Logger.Write(string.Format("Failed to send the email(Reason: {0})", ex.Message));
            }
        }
    }
}
