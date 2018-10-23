using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;

namespace BuildHelper
{
    public class GitLabOperation
    {
        public struct UserInfo
        {
            public string name;
            public string id;
            public string email;
        }

        private static string privateToken = "ikrisSAcRRfTPKvmsNvG";
        private static string domainString = "https://os-gits";


        public static List<UserInfo> GetProjectUsers(string projectName)
        {            
            string responseString = getJsonByGivenHttpRequestString(domainString + "/api/v3/projects?private_token=" + privateToken);
            string projectId = getProjectIdByProjectName(responseString, projectName);
            responseString = getJsonByGivenHttpRequestString(string.Format(domainString + "/api/v3/projects/{0}/members?private_token=" + privateToken, projectId));
            List<UserInfo> projectMembers = getProjectMembers(responseString);

            responseString = getJsonByGivenHttpRequestString(domainString + "/api/v3/users?private_token=" + privateToken);
            List<UserInfo> allUsers = getAllUsers(responseString);
            return allUsers.Where(t => projectMembers.Any(d => d.name == t.name)).ToList();
        }
        private static List<UserInfo> getAllUsers(string gitlabReturnJsonString)
        {
            List<UserInfo> lst = new List<UserInfo>();
            UserInfo m;

            var objects = JArray.Parse(gitlabReturnJsonString);

            foreach (JObject root in objects)
            {
                m = new UserInfo();
                m.name = (string)root["name"].Value<string>();
                m.id = (string)root["id"].Value<string>();
                m.email = (string)root["email"].Value<string>();
                lst.Add(m);
            }
            return lst;
        }


        private static List<UserInfo> getProjectMembers(string gitlabReturnJsonString)
        {
            List<UserInfo> lst = new List<UserInfo>();
            UserInfo m;

            var objects = JArray.Parse(gitlabReturnJsonString);

            foreach (JObject root in objects)
            {
                m = new UserInfo();
                m.name = (string)root["name"].Value<string>();
                m.id = (string)root["id"].Value<string>();
                lst.Add(m);
            }
            return lst;
        }
        private static string getProjectIdByProjectName(string gitlabReturnJsonString, string projectName)
        {
            var objects = JArray.Parse(gitlabReturnJsonString);

            foreach (JObject root in objects)
            {
                if ((string)root["name"].Value<string>() == projectName)
                {
                    return (string)root["id"].Value<string>();
                }
            }
            return string.Empty;
        }

        private static bool TrustCertificate(object sender, X509Certificate x509Certificate, X509Chain x509Chain, SslPolicyErrors sslPolicyErrors)
        {
            // all Certificates are accepted
            return true;
        }

        static string getJsonByGivenHttpRequestString(string httpUrlString)
        {
            ServicePointManager.ServerCertificateValidationCallback = TrustCertificate;
            var request = (HttpWebRequest)WebRequest.Create(httpUrlString);

            var response = (HttpWebResponse)request.GetResponse();

            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            return reader.ReadToEnd();
        }

    }


}
