using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace BuildHelper
{
    
    class GitOperations
    {
        private string authUser = string.Empty;
        private string authPassword = string.Empty;
        private string targetGitUrl = string.Empty;
        private string gitExe = "git.exe";
        private List<string> runCommandOutputList;

        public string MasterHeadCommitNumber = string.Empty;
        public List<string> TagList = new List<string>();

        public void SetGitUrlAndAuth(string gitUrl, string user, string password)
        {
            targetGitUrl = gitUrl;
            authUser = user;
            authPassword = password;
        }
        public bool CheckTheLatestCommit()
        {
            if (string.IsNullOrEmpty(targetGitUrl)
                  || string.IsNullOrEmpty(authUser)
                  || string.IsNullOrEmpty(authPassword))
                return false;

            string gitArgument = string.Format("ls-remote --refs https://{0}:{1}@{2}", authUser, authPassword, targetGitUrl);

            runCommandOutputList = new List<string>();

            if (RunCommand(gitExe, gitArgument) != 0)
                return false;

            string masterHeadCommit = string.Empty;
            List<string> tagList = new List<string>();
            ParseTheGitUrlHeadsTags(runCommandOutputList, ref masterHeadCommit, ref tagList);

            MasterHeadCommitNumber = masterHeadCommit;
            TagList = tagList;

            return true;
        }

        private void ParseTheGitUrlHeadsTags(List<string> outputList, ref string masterHeadCommit, ref List<string> tagList)
        {
            List<string> elemList;
            string branchName;
            string commitNumber;
            string refTagIdfr = "refs/tags/";
            string refHeadsIdfr = "refs/heads/master";

            foreach (string o in outputList)
            {
                elemList = o.Split(new string[] { " ", "\t" }, StringSplitOptions.None).ToList();
                if (elemList.Count == 2)
                {
                    commitNumber = elemList[0];
                    branchName = elemList[1];

                    if (branchName.Trim().ToLower() == refHeadsIdfr)
                        masterHeadCommit = commitNumber.Trim();

                    if (branchName.Trim().ToLower().StartsWith(refTagIdfr))
                        tagList.Add(branchName.Substring(refTagIdfr.Length));
                }
            }
        }
        
        private int RunCommand(string fileName, string argument)
        {
            int result = 0;

            using (Process proc = new Process())
            {
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.FileName = fileName;
                proc.StartInfo.Arguments = argument;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;

                proc.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(process_OutputDataReceived);
                proc.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(process_ErrorDataReceived);
                proc.Exited += new System.EventHandler(process_Exited);
                proc.EnableRaisingEvents = true;                

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
                proc.WaitForExit();
                result = proc.ExitCode;
            }

            return result;
        }

        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null || e.Data.Trim().Length <= 0)
                return;

            runCommandOutputList.Add(e.Data.Trim());
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null || e.Data.Trim().Length <= 0)
                return;

        }

        private void process_Exited(object sender, EventArgs e)
        {
            
        }

    }
}
