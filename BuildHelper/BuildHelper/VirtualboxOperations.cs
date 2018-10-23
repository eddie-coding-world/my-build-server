using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace BuildHelper
{
    public class VirtualboxOperations
    {
        private enum ENUM_OP_ACTION
        {
            CreateVM,
            GetRunningVMList,
            GetVMNameByVDIFilePath,
        }

        private List<string> _runningVMList;
        private string _virtualBoxExePath = string.Empty;
        private ENUM_OP_ACTION eOpAction;
        private string vdiMappingVMName = string.Empty;

        public VirtualboxOperations(string virtualBoxExePath)
        {
            _virtualBoxExePath = virtualBoxExePath;
        }

        public void CloneVdi(string srcVdiFilePath, string destVdiFilePath)
        {
            
            eOpAction = ENUM_OP_ACTION.CreateVM;
            RunCommand(_virtualBoxExePath, string.Format("clonevdi {0} {1}", srcVdiFilePath, destVdiFilePath));
        }

        public string GetVMNameByVDIFilePath(string vdiFilePath)
        {
            eOpAction = ENUM_OP_ACTION.GetVMNameByVDIFilePath;
            vdiMappingVMName = string.Empty;
            RunCommand(_virtualBoxExePath, string.Format("showvdiinfo {0}", vdiFilePath));
            return vdiMappingVMName;
        }

        public void SetVMCpuCount(string vmName, int cpuCount)
        {
            RunCommand(_virtualBoxExePath, string.Format("modifyvm {0} --cpus {1}", vmName, cpuCount.ToString()));
        }

        public void SetVMRamSizeInMB(string vmName, int ramSizeInMB)
        {
            RunCommand(_virtualBoxExePath, string.Format("modifyvm {0} --memory {1}", vmName, ramSizeInMB.ToString()));
        }

        public void AddShareFolder(string vmName, string shareFolderName, string shareFolderPath)
        {
            RunCommand(_virtualBoxExePath, string.Format("sharedfolder add {0} --name {1} --hostpath {2} --automount", vmName, shareFolderName, shareFolderPath));
        }

        public List<string> GetRunningVMList()
        {
            eOpAction = ENUM_OP_ACTION.GetRunningVMList;
            _runningVMList = new List<string>();
            RunCommand(_virtualBoxExePath, "list -s runningvms");
            return _runningVMList;
        }

        public void StartVMByName(string vmName)
        {
            RunCommand(_virtualBoxExePath, string.Format("startvm {0} --type {1}", vmName, "gui"));
        }

        public void StopCurrentlyRunningVMs()
        {
            foreach (string runningVM in GetRunningVMList())
            {
                RunCommand(_virtualBoxExePath, string.Format("controlvm {0} poweroff", runningVM.Trim()));
            }
        }

        private int RunCommand(string fileName, string argument)
        {
            if (!File.Exists(fileName))
                return -1;
            
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
            if (e.Data == null)
                return;

            if (e.Data.Trim().Length <= 0)
                return;

            string data = e.Data.Trim();

            if (eOpAction == ENUM_OP_ACTION.GetRunningVMList)
            {
                string vmName = string.Empty;
                string vmNameIdfr = "name:";

                vmName = data.Split(new string[] { " " }, StringSplitOptions.None).ToList()[0];
                if (!string.IsNullOrEmpty(vmName))
                {
                    vmName = vmName.Trim('"');
                    _runningVMList.Add(vmName);
                }
            }
            else if (eOpAction == ENUM_OP_ACTION.GetVMNameByVDIFilePath)
            {
                string idfr = "in use by vms:";                

                if (data.ToLower().StartsWith(idfr))
                {
                    data = data.Substring(idfr.Length);
                    vdiMappingVMName = data.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList()[0];                    
                }
            }
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

        }

        private void process_Exited(object sender, EventArgs e)
        {
        }


    }
}
