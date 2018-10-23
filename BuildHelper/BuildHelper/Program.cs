//#define BUILD_TAGS
#define EMAIL_NOTIFY

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;

namespace BuildHelper
{
    public struct GitLogElems
    {
        public string projectName;
        public string commitNumber;
        public string author;
        public string date;
        public string comment;
        public string hasPackageFile;
        public string hasBuildFailLog;
        public string authorEmail;
//        public string packageFileName;
//        public string buildLogFileName;
        public string buildSessionId;
        public string buildResult;
    }

    public struct ProjectConfigInfo
    {
        public string projectName;
        public string vdiName;
        public string vdiFilePath;
        public string gitProtocol;
        public string gitUrl;
        public string gitUser;
        public string gitPassword;
        public string buildTrunk;
        public string buildUser;
        public string buildGroup;
        public string buildSH;
        public string buildOS;
        public List<string> notifyPeople;
    }

    public class GitRepositoryInfo
    {
        public string project_name { get; set; }         
        public List<GitLogElems> master { get; set; }
        public List<GitLogElems> tags { get; set; }
    }

    public struct ProgramSettings
    {
        public string virtualBoxExePath;
        public string releasePackageDirPath;
        public string vdiRootDirPath;
        public string localSharedDir;
        public string buildTrunk;
        public string buildResultJsonFilesDirRelativeToBuildServerDir;
        //BUILR_RESULT_OF_ALL_PROJECTS_JSON_FILE_NAME
        public string buildResultOfAllProjectsJsonFileName;
        public int vmCpuCount;
        public int vmRamSizeInMB;
        public int keepMasterBranchReleasePackagesCount;
        public string releasePackageDir;
        public string notifyEmailUserName;
        public string notifyEmailUserPassword;
        public List<string> notifyCcEmailsList;

    }

    class Program
    {
        private enum EnumBranch
        {
            Master,
            Tag
        }

               
        private  const int SCAN_SLEEP_TIME_SECONDS = 1;
        //System related paths
        private static string _buildServerRootDir = string.Empty;
        private static string _projectWorkRootDir = string.Empty;        
        private static string _virtualBoxExePath = string.Empty;
        private static string _buildScriptFilePath = string.Empty;
        private static string _hostShareDir = string.Empty;
        private static string _buildScriptTemplateRootDir = string.Empty;
        private static string _programSettingConfigFilePath = string.Empty;
        private static string _gitLogFilePath = string.Empty;
        private static string _buildResultFilePath = string.Empty;
        private static string _buildServerLogDir = string.Empty;
        private static string _buildResultJsonFilesDirRealPath = string.Empty;

        private static string _buildLogFileName = "build.log";
        private static string _buildLogFilePath = string.Empty;
        private static string _buildHelperLogFileName = "bulld_helper.log";
        private static string _buildResultFileName = "build_result.log";
        private static string _buildScriptFileName = "build.script";
        private static string _gitLogFileName = "git.log";
        private static string _buildScriptLogFileName = "build_script.log";
        private static string _vmExecStatusFileName = "vm_status.txt";
        private static string _vmExecStatusOngoing = "vm_processing";
        private static string _vmExecStatusBeginGitUpdateCode = "vm_begin_git_update_code";
        private static string _vmExecStatusFinishGitUpdateCode = "vm_finish_git_update_code";
        private static string _vmExecStatusBeginBuildCode = "vm_begin_build_code";
        private static string _vmExecStatusFinishBuildCode = "vm_finish_build_code";
        private static string _vmExecStatusBeginPackageFiles = "vm_begin_package_files";
        private static string _vmExecStatusFinishPackageFiles = "vm_finish_package_files";
        private static string _vmExecStatusFinished = "vm_finished";
        private static string _vmGetBuildLogBackupFileName = "vm_get_build_log_backup_file_name";
        
        private static string _buildSuccessMessage = "Build successfully";
        private static string _buildErrorMessage = "Build failed";
        private static string _buildNoBuildFileMessage = "No build file";
        private static string _buildResultMessage = "No build";
        private static string _updateGitCodeErrorMessage = "Update source code failed";

        private static int _tasksIntervalSeconds = 5;
        private static string _elemSeparatorString = "--@my_elem_sep@--";
        private static string _lineSeparatorString = "--@my_line_sep@--";
        private static string _configsDir = string.Empty;
        private static string _totalProjectBuildResultJsonFilePath = string.Empty;

        private static GitOperations _gitOp;
        private static VirtualboxOperations _vboxOp;
        private static ProgramSettings _ps = new ProgramSettings();
        private static List<ProjectConfigInfo> _projectConfigInfoList = new List<ProjectConfigInfo>();

        static void prepareSystemRelatedPaths()
        {
            _buildServerRootDir = Path.Combine(Directory.GetDirectoryRoot(AppDomain.CurrentDomain.BaseDirectory), "BuildServer");
            _buildServerLogDir = Path.Combine(_buildServerRootDir, @"Logs");

            if (!Directory.Exists(_buildServerLogDir))
                Directory.CreateDirectory(_buildServerLogDir);

            _configsDir = Path.Combine(_buildServerRootDir, @"Configs");
            _programSettingConfigFilePath = Path.Combine(_configsDir, @"Settings.conf");
            _buildScriptTemplateRootDir = Path.Combine(_configsDir, "BuildScriptTemplate");

            _buildScriptFilePath = Path.Combine(_ps.localSharedDir, _buildScriptFileName);
            _projectWorkRootDir = _ps.localSharedDir;
            _buildResultJsonFilesDirRealPath = Path.Combine(_buildServerRootDir, _ps.buildResultJsonFilesDirRelativeToBuildServerDir);

            if (!Directory.Exists(_buildResultJsonFilesDirRealPath))
                Directory.CreateDirectory(_buildResultJsonFilesDirRealPath);

            _totalProjectBuildResultJsonFilePath = Path.Combine(_buildServerRootDir,
                                                        Path.Combine(_ps.buildResultJsonFilesDirRelativeToBuildServerDir, _ps.buildResultOfAllProjectsJsonFileName));

        }

        static void copyFilesByExtensions(string srcDir, string destDir, List<string> extensionsList)
        {            
            string sourcePath = srcDir;
            string targetPath =  destDir;

            var extensions = extensionsList.ToArray();

            var files = (from file in Directory.EnumerateFiles(sourcePath)
                         where extensions.Contains(Path.GetExtension(file), StringComparer.InvariantCultureIgnoreCase)
                         select new 
                                        { 
                                          Source = file, 
                                          Destination = Path.Combine(targetPath, Path.GetFileName(file))
                                        });

            foreach(var file in files)
            {
                File.Copy(file.Source, file.Destination, true);
            }
        }

        static void Main(string[] args)
        {            
            loadSettingsConf();
            prepareSystemRelatedPaths();

            _gitOp = new GitOperations();
            _vboxOp = new VirtualboxOperations(_ps.virtualBoxExePath);

            GitRepositoryInfo gitRepoInfo = new GitRepositoryInfo();

            MailMessager.GmailUserName = _ps.notifyEmailUserName;
            MailMessager.GmailPassword = _ps.notifyEmailUserPassword;

            Logger.Start(Path.Combine(_buildServerLogDir, _buildHelperLogFileName));            
            Logger.Write("BuildHelper get started");

            //1. Check if there is any Virtual Box file running at this moment.
            //   If yes, stop them from running. 

            _vboxOp.StopCurrentlyRunningVMs();
            Logger.Write("Stopped all running VMs");

            //2. Check the config file and run VMs.
            //  The config file format : 
            //  {The project name} - {The corresponding VM for building source code} - {The build script path}           
            string projectWorkDir = string.Empty;
            string jsonString = string.Empty;
            string projectBuildResultJsonFilePath = string.Empty;
            string projectBuildResultJsonFileText = string.Empty;
            string totalProjectBuildResultJsonFileText = string.Empty;
            string vmName = string.Empty;
            string projectBuildResultJsonOutputDir = string.Empty;

            foreach (ProjectConfigInfo project in _projectConfigInfoList)
            {
                Logger.Write(string.Format("================== Start working Project {0} ==================", project.projectName));
                projectWorkDir = Path.Combine(_projectWorkRootDir, project.projectName);
                _gitLogFilePath = Path.Combine(projectWorkDir, _gitLogFileName);
                _buildResultFilePath = Path.Combine(projectWorkDir, _buildResultFileName);
                _buildLogFilePath = Path.Combine(projectWorkDir, _buildLogFileName);

                Logger.Write(string.Format("Checking the project working folder({0}) existence", projectWorkDir));
                if (!Directory.Exists(projectWorkDir))
                    Directory.CreateDirectory(projectWorkDir);
                Logger.Write(string.Format("Checked the project working folder({0}) - OK!", projectWorkDir));

                projectBuildResultJsonFilePath = Path.Combine(_buildResultJsonFilesDirRealPath, string.Format("BuildResult_{0}.json", project.projectName));
                projectBuildResultJsonFileText = string.Empty;

                if (File.Exists(projectBuildResultJsonFilePath))
                    projectBuildResultJsonFileText = File.ReadAllText(projectBuildResultJsonFilePath).Trim();

                gitRepoInfo = parseJSONStringToGitRepoInfo(projectBuildResultJsonFileText);
                gitRepoInfo.project_name = project.projectName;

                _gitOp.SetGitUrlAndAuth(project.gitUrl, project.gitUser, project.gitPassword);
                _gitOp.CheckTheLatestCommit();
                string masterHeadCommitNumber = _gitOp.MasterHeadCommitNumber;
                List<string> tagList = _gitOp.TagList;

                vmName = _vboxOp.GetVMNameByVDIFilePath(project.vdiFilePath);
                if (string.IsNullOrEmpty(vmName))
                {
                    Logger.Write(string.Format("{0} does not have a corresponding VM", project.vdiFilePath));                 
                    continue;
                }

                _vboxOp.SetVMCpuCount(vmName, _ps.vmCpuCount);
                Logger.Write(string.Format("VM's CPU count = {0}", _ps.vmCpuCount));

                _vboxOp.SetVMRamSizeInMB(vmName, _ps.vmRamSizeInMB);
                Logger.Write(string.Format("VM's RAM size in MB = {0}", _ps.vmRamSizeInMB));

                _vboxOp.AddShareFolder(vmName, Path.GetFileName(_ps.localSharedDir), _ps.localSharedDir);
                Logger.Write(string.Format("Mounted {0} as the shared folder", _ps.localSharedDir));

                                                                                   
                //Make a comment here...
                //3. Wait for the VM to finish building
                if ( (gitRepoInfo.master != null && gitRepoInfo.master.Count == 0) 
                        || (gitRepoInfo.master[0].commitNumber != null && !masterHeadCommitNumber.StartsWith(gitRepoInfo.master[0].commitNumber)))
                {
                    Logger.Write(string.Format("Project {0} : Found one new commit. Start building process...", project.projectName));
                    createUbuntuOSBuildScript(project, project.buildTrunk);
                    buildCodeAndMergeResult(gitRepoInfo, vmName, EnumBranch.Master, _gitLogFilePath, _buildLogFilePath, _buildResultFilePath, _ps.buildTrunk, project.notifyPeople);
                }
                else
                    Logger.Write(string.Format("Project {0} : No new commit to build", project.projectName));
                
#if BUILD_TAGS
                foreach (string tag in tagList)
                {
                    if (gitRepoInfo.tags.FindIndex(t => t.commitNumber == tag) < 0)
                    {
                        Logger.Write(string.Format("Project {0} : Found one new tag {1}. Start building process...", project.projectName, tag));                        
                        createUbuntuOSBuildScript(project, tag);
                        buildCodeAndMergeResult(gitRepoInfo, vmName, EnumBranch.Tag, _gitLogFilePath, _buildLogFilePath, _buildResultFilePath, tag, project.notifyPeople);
                    }
                }
#endif

                jsonString = JsonConvert.SerializeObject(gitRepoInfo);
                Logger.Write(string.Format("Creating json-formatted build string for {0}...", project.projectName));
                File.WriteAllText(projectBuildResultJsonFilePath, jsonString);
                Logger.Write(string.Format("Finished json-formatted build string for {0}...", project.projectName));

                totalProjectBuildResultJsonFileText += jsonString + ",";
                Logger.Write(string.Format("================== Finish working Project {0} ==================", project.projectName));
            }

            totalProjectBuildResultJsonFileText = totalProjectBuildResultJsonFileText.TrimEnd(new char[] { ',' });

            File.WriteAllText(_totalProjectBuildResultJsonFilePath, totalProjectBuildResultJsonFileText);
            Logger.Write("BuildHelper says, \"Let's call it day!\"");
        }

        static private void loadSettingsConf()
        {
            _buildServerRootDir = Path.Combine(Directory.GetDirectoryRoot(AppDomain.CurrentDomain.BaseDirectory), "BuildServer");
            _configsDir = Path.Combine(_buildServerRootDir, @"Configs");
            _programSettingConfigFilePath = Path.Combine(_configsDir, @"Settings.conf");

            List<string> lines = System.IO.File.ReadLines(_programSettingConfigFilePath).ToList();
            List<string> elemList = new List<string>();            
            ProjectConfigInfo projectConfigInfo = new ProjectConfigInfo();
            string section = string.Empty;
            string buildResultJsonOutputFilePathRelativeToBuildServerDir = string.Empty;
            string sectionProgramSettings = "[Program Settings]";
            string sectionProjectSettings = "[Project]";
            string notifyEmailsString = string.Empty;

            foreach (string line in lines)
            {
                if (line.StartsWith("#"))
                    continue;

                if (line.Trim().ToUpper() == sectionProgramSettings.Trim().ToUpper())
                {
                    section = sectionProgramSettings;
                    continue;
                }
                else if (line.Trim().ToUpper() == sectionProjectSettings.Trim().ToUpper())
                {
                    section = sectionProjectSettings;
                    projectConfigInfo = new ProjectConfigInfo();
                    continue;
                }


                if (section == sectionProgramSettings)
                {
                    elemList = line.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    if (elemList != null && elemList.Count == 2)
                    {
                        if (elemList[0].Trim() == "VIRTUAL_BOX_EXE_PATH")
                            _ps.virtualBoxExePath = elemList[1].Trim();
                        else if (elemList[0].Trim() == "RELEASE_PACKAGE_DIR_PATH")
                            _ps.releasePackageDirPath = elemList[1].Trim();
                        else if (elemList[0].Trim() == "VDI_ROOT_DIR")
                            _ps.vdiRootDirPath = elemList[1].Trim();
                        else if (elemList[0].Trim() == "LOCAL_SHARED_DIR")
                        {
                            _ps.localSharedDir = elemList[1].Trim();

                            if (_ps.localSharedDir.IndexOf(@":\") < 0)                            
                                _ps.localSharedDir = Path.Combine(_buildServerRootDir, _ps.localSharedDir);

                            if (!Directory.Exists(_ps.localSharedDir))
                                Directory.CreateDirectory(_ps.localSharedDir);
                        }
                        else if (elemList[0].Trim() == "BUILD_TRUNK")
                            _ps.buildTrunk = elemList[1].Trim();
                        else if (elemList[0].Trim() == "BUILD_RESULT_JSON_FILES_DIR_RELATIVE_TO_BUILD_SERVER_ROOT_DIR")
                            _ps.buildResultJsonFilesDirRelativeToBuildServerDir = elemList[1].Trim();
                        else if (elemList[0].Trim() == "BUILR_RESULT_OF_ALL_PROJECTS_JSON_FILE_NAME")
                            _ps.buildResultOfAllProjectsJsonFileName = elemList[1].Trim();
//                        else if (elemList[0].Trim() == "RELEASE_PACKAGES_DIR")
                            //_ps.releasePackageDir = elemList[1].Trim();
                        else if (elemList[0].Trim() == "NOTIFY_EMAIL_USER_NAME")
                            _ps.notifyEmailUserName = elemList[1].Trim();
                        else if (elemList[0].Trim() == "NOTIFY_EMAIL_USER_PASSWORD")
                            _ps.notifyEmailUserPassword = elemList[1].Trim();
                        else if (elemList[0].Trim() == "CC_EMAILS_LIST")
                        {
                            try
                            {
                                _ps.notifyCcEmailsList = elemList[1].Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
                            }
                            catch (Exception ex)
                            {
                                _ps.notifyCcEmailsList = new List<string>();
                            }
                        }
                        else if (elemList[0].Trim() == "KEEP_MASTER_BRANCH_RELEASE_PACKAGES_COUNT")
                        {
                            try
                            {
                                _ps.keepMasterBranchReleasePackagesCount = Convert.ToInt32(elemList[1].Trim());
                            }
                            catch (Exception ex)
                            {
                                _ps.keepMasterBranchReleasePackagesCount = 1;
                            }

                        }
                        else if (elemList[0].Trim() == "VM_CPU_NUMBER_COUNT")
                        {
                            try
                            {
                                _ps.vmCpuCount = Convert.ToInt32(elemList[1].Trim());
                            }
                            catch (Exception ex)
                            {
                                _ps.vmCpuCount = 1;
                            }
                        }
                        else if (elemList[0].Trim() == "VM_RAM_SIZE_IN_MB")
                        {
                            try
                            {
                                _ps.vmRamSizeInMB = Convert.ToInt32(elemList[1].Trim());
                            }
                            catch (Exception ex)
                            {
                                _ps.vmRamSizeInMB = 1024 * 4;
                            }
                        }
                    }
                }
                else if (section == sectionProjectSettings)
                {                    
                    elemList = line.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    if (elemList != null && elemList.Count == 2)
                    {   
                        if (elemList[0].Trim() == "PROJECT_NAME")
                            projectConfigInfo.projectName = elemList[1].Trim();
                        else if (elemList[0].Trim() == "VDI_NAME")
                        {
                            projectConfigInfo.vdiName = elemList[1].Trim();
                            projectConfigInfo.vdiFilePath = Path.Combine(_ps.vdiRootDirPath, projectConfigInfo.vdiName);                            
                        }
                        else if (elemList[0].Trim() == "GIT_PROTOCOL")
                            projectConfigInfo.gitProtocol = elemList[1].Trim();
                        else if (elemList[0].Trim() == "GIT_REPO_URL")
                            projectConfigInfo.gitUrl = elemList[1].Trim();
                        else if (elemList[0].Trim() == "GIT_USER")
                            projectConfigInfo.gitUser = elemList[1].Trim();
                        else if (elemList[0].Trim() == "GIT_PASSWORD")
                            projectConfigInfo.gitPassword = elemList[1].Trim();
                        else if (elemList[0].Trim() == "BUILD_TRUNK")
                            projectConfigInfo.buildTrunk = elemList[1].Trim();
                        else if (elemList[0].Trim() == "BUILD_USER")
                            projectConfigInfo.buildUser = elemList[1].Trim();
                        else if (elemList[0].Trim() == "BUILD_GROUP")
                            projectConfigInfo.buildGroup = elemList[1].Trim();
                        else if (elemList[0].Trim() == "BUILD_SH")
                            projectConfigInfo.buildSH = elemList[1].Trim();
                            /*
                        else if (elemList[0].Trim() == "NOTIFY_EMAILS")
                        {
                            notifyEmailsString = elemList[1].Trim();
                            projectConfigInfo.notifyPeople = new List<string>();

                            if (!string.IsNullOrEmpty(notifyEmailsString))
                                projectConfigInfo.notifyPeople = notifyEmailsString.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
                           
                        }
                             */
                        else if (elemList[0].Trim() == "BUILD_SCRIPT_TEMPLATE")
                        {
                            projectConfigInfo.buildOS = elemList[1].Trim();
                            _projectConfigInfoList.Add(projectConfigInfo);
                        }

                    }
                }
            }
        }

        static string getFileText(string filePath)
        {
            var content = (string)null;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    content = reader.ReadLine();
                    reader.Close();
                    //content = reader.ReadToEnd();
                }
                fileStream.Close();
            }

            if (string.IsNullOrEmpty(content))
                content = string.Empty;

            return content;
        }

        static void buildCodeAndMergeResult(GitRepositoryInfo gitRepoInfo, 
                                            string vmName, 
                                            EnumBranch eBranch, 
                                            string gitLogFilePath, 
                                            string buildLogFilePath, 
                                            string buildResultFilePath,
                                            string branchName,
                                            List<string> notifyEmails)
        {
            
            string vmExecStatus = string.Empty;
            string vmExecStatuFilePath = Path.Combine(_projectWorkRootDir, _vmExecStatusFileName);
            string vmExecStatusFinishedFilePath = Path.Combine(_projectWorkRootDir, _vmExecStatusFinished);
            string vmGetBuildLogBackupFilePath = Path.Combine(_projectWorkRootDir, _vmGetBuildLogBackupFileName);

            string buildScriptLogFilePath = Path.Combine(_projectWorkRootDir, _buildScriptLogFileName);
            string buildResult = string.Empty;
            string projectSharedDir = string.Empty;
            string backupProjectReleasePackageDirPath = string.Empty;
            List<string> copyFileExtensionList = new List<string>();
            copyFileExtensionList.Add(".zip");

            //Project shared folder is the folder which the host computer shares VM for each project.
            projectSharedDir = Path.Combine(_ps.localSharedDir, gitRepoInfo.project_name);
            if (!Directory.Exists(projectSharedDir))
                Directory.CreateDirectory(projectSharedDir);

            //The path for backing up generated packages (Can be an independant folder path).
            backupProjectReleasePackageDirPath = Path.Combine(_ps.releasePackageDirPath, gitRepoInfo.project_name);
            if (!Directory.Exists(backupProjectReleasePackageDirPath))
                Directory.CreateDirectory(backupProjectReleasePackageDirPath);

            if (File.Exists(vmExecStatuFilePath))
                File.Delete(vmExecStatuFilePath);

            if (File.Exists(buildScriptLogFilePath))
                File.Delete(buildScriptLogFilePath);

            if (File.Exists(vmExecStatusFinishedFilePath))
                File.Delete(vmExecStatusFinishedFilePath);
           
            //After running the VM, the git log file and the build result file will be generated            
            _vboxOp.StartVMByName(vmName);
            Logger.Write(string.Format("VM : {0} gets started", vmName));

            string prevVmExecStatus = string.Empty;
            //Keep reading the VM's execution status to see if it has finished the source code building. 
            int indexOfDebug = 0;
            while (true)
            {
                if (indexOfDebug > 10000)
                   indexOfDebug = 0;

                System.Threading.Thread.Sleep(SCAN_SLEEP_TIME_SECONDS * 1000);

                Console.Clear();
                Console.WriteLine(string.Format("debug a {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                
                if (File.Exists(vmExecStatusFinishedFilePath))
                    break;

                Console.WriteLine(string.Format("debug b {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                if (File.Exists(vmExecStatuFilePath))
                {
                    Console.WriteLine(string.Format("debug c {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                    vmExecStatus = getFileText(vmExecStatuFilePath);
                    Console.WriteLine(string.Format("debug d {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                    vmExecStatus = vmExecStatus.Trim(new char[] { ' ', '\r', '\n' });
                    Console.WriteLine(string.Format("debug f {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                    if (prevVmExecStatus != vmExecStatus)
                    {
                        Logger.Write(vmExecStatus);
                        prevVmExecStatus = vmExecStatus;
                    }
                    Console.WriteLine(string.Format("debug g {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                }
                Console.WriteLine(string.Format("debug h {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
                //System.Threading.Thread.Sleep(SCAN_SLEEP_TIME_SECONDS * 1000);
                //Console.WriteLine(string.Format("debug i {0} ({1})", (++indexOfDebug).ToString(), vmExecStatus));
            }

            _vboxOp.StopCurrentlyRunningVMs();
            Logger.Write("VM : Shut down all running VMs");

            if (File.Exists(buildResultFilePath))
            {
                buildResult = File.ReadAllText(buildResultFilePath);
                buildResult = buildResult.Trim(new char[] { ' ', '\r', '\n' });
            }

            List<GitLogElems> newLogList = new List<GitLogElems>();
            if (File.Exists(gitLogFilePath))
                newLogList = parseGitLogFileIntoGitLogElemsListNew(gitLogFilePath);

            //We will get a build session id after the build
            //The build session id for master branch -> master-{commit_number}
            //The build session id for a tag branch -> tag_number
            string buildSessionId = string.Empty;
            if (File.Exists(buildLogFilePath))
            {
                buildSessionId = File.ReadAllLines(buildLogFilePath)[0];
                buildSessionId = buildSessionId.Trim(new char[] { ' ', '\r', '\n' });
            }

            //Prepare the emails list that we're going to send. *start*
            if (newLogList.Count > 0)
                MailMessager.SendToList.Add(newLogList[0].authorEmail);

            List<GitLabOperation.UserInfo> projectUsers = GitLabOperation.GetProjectUsers(gitRepoInfo.project_name);

            if (projectUsers != null && projectUsers.Count > 0)
            {
                foreach (GitLabOperation.UserInfo u in projectUsers)
                    MailMessager.SendToList.Add(u.email);
            }

            if (notifyEmails != null && notifyEmails.Count > 0)
                MailMessager.SendToList.AddRange(notifyEmails);

            if (_ps.notifyCcEmailsList != null && _ps.notifyCcEmailsList.Count > 0)
                MailMessager.SendCcList.AddRange(_ps.notifyCcEmailsList);

            List<string> attachFileList = new List<string>();
            //Prepare the emails list that we're going to send. *end*

            string gitContentInEmail = "<br>The latest commit: <br>" +
                                       "====== " + newLogList[0].commitNumber + " =====<br>" +
                                       newLogList[0].comment;

            //Copy release package zip files to the backup project release package folder.
            if (buildResult == _buildSuccessMessage)
            {
                Logger.Write(string.Format("Project {0} {1} branch build code -> Success!", gitRepoInfo.project_name, branchName));
                copyFilesByExtensions(projectSharedDir, backupProjectReleasePackageDirPath, copyFileExtensionList);

                attachFileList.Add(buildLogFilePath);
#if EMAIL_NOTIFY

                MailMessager.SendMessage("Protech OS Build Server's Message(There is a SUCCESSFUL build)", "Good job! Your latest source code committed to the project of " + gitRepoInfo.project_name + " can be built up successfully.<br>" + gitContentInEmail, attachFileList);
#endif
            }
            else if (buildResult == _buildErrorMessage)
            {
                Logger.Write(string.Format("Project {0} {1} branch build code -> Fail!", gitRepoInfo.project_name, branchName));
                File.Copy(buildLogFilePath, Path.Combine(projectSharedDir, buildSessionId),true);

                attachFileList.Add(buildLogFilePath);
#if EMAIL_NOTIFY
                MailMessager.SendMessage("Protech OS Build Server's Message(There is a FAILED build)", "We are regret to inform you that your latest source committed to the project of " + gitRepoInfo.project_name + " cannot be successfully built.<br>" + gitContentInEmail, attachFileList);
#endif
            }

            //House keeping for release packages
            if (eBranch == EnumBranch.Master)
                houseKeepingForReleasePackages(projectSharedDir, backupProjectReleasePackageDirPath);

            bool hasPackageFile = false;
            bool hasBuildFailFile = false;

            if (File.Exists(Path.Combine(projectSharedDir, string.Format("{0}.zip'", buildSessionId))))
                hasPackageFile = true;

            if (File.Exists(Path.Combine(projectSharedDir, string.Format("{0}'", buildSessionId))))
                hasBuildFailFile = true;

            mergeNewGitLogFileToGitRepositoryInfo(gitRepoInfo, eBranch, gitLogFilePath, buildSessionId, buildResult, branchName, hasPackageFile, hasBuildFailFile);
        }

        static void houseKeepingForReleasePackages(string projectSharedFolder, string backupProjectFilesFolderPath)
        {
            List<FileInfo> packageFileInfoList = new DirectoryInfo(projectSharedFolder).GetFiles(_ps.buildTrunk + "*")
                                                               .OrderBy(f => f.LastWriteTime)
                                                               .ToList();

            for (int index = _ps.keepMasterBranchReleasePackagesCount; index < packageFileInfoList.Count; ++index)            
                File.Delete(packageFileInfoList[index].FullName);

            packageFileInfoList = new DirectoryInfo(backupProjectFilesFolderPath).GetFiles(_ps.buildTrunk + "*")
                                                               .OrderBy(f => f.LastWriteTime)
                                                               .ToList();

            for (int index = _ps.keepMasterBranchReleasePackagesCount; index < packageFileInfoList.Count; ++index)
                File.Delete(packageFileInfoList[index].FullName);

        }

        static void updateFirstCommitBuildResult(List<GitLogElems> lst, string buildSessionId, string buildResult, bool hasPackageFile, bool hasBuildFailFile)
        {
            if (lst == null || lst.Count == 0)
                return;

            GitLogElems elem = lst[0];
            elem.buildSessionId = buildSessionId;
            elem.buildResult = buildResult;
            elem.hasPackageFile = (hasPackageFile) ? "yes" : "no";
            elem.hasBuildFailLog = (hasBuildFailFile) ? "yes" : "no";

            lst[0] = elem;
        }


        static void mergeNewGitLogFileToGitRepositoryInfo(GitRepositoryInfo gitRepoInfo, EnumBranch eBranch, string gitLogFile, string buildSessionId, string buildResult, string branchName, bool hasPackageFile, bool hasBuildFailFile)
        {
            if (!File.Exists(gitLogFile))
                return;

            //List<GitLogElems> newLogList = parseGitLogFileIntoGitLogElemsList(gitLogFile);
            List<GitLogElems> newLogList = parseGitLogFileIntoGitLogElemsListNew(gitLogFile);
            updateFirstCommitBuildResult(newLogList, buildSessionId, buildResult, hasPackageFile, hasBuildFailFile);

            if (eBranch == EnumBranch.Master)
                gitRepoInfo.master.InsertRange(0, newLogList);
            else if (eBranch == EnumBranch.Tag)
            {
                if (newLogList != null && newLogList.Count > 0)
                {
                    GitLogElems gle = newLogList[0];
                    gle.commitNumber = branchName;
                    gitRepoInfo.tags.Insert(0, gle);
                }                
            }
        }

        private static GitRepositoryInfo parseJSONStringToGitRepoInfo(string jsonString)
        {
            GitRepositoryInfo gri = new GitRepositoryInfo();

            try
            {
                gri = JsonConvert.DeserializeObject<GitRepositoryInfo>(jsonString);
            }
            catch (Exception ee)
            {
            }

            if (gri == null)
                gri = new GitRepositoryInfo();

            if (gri.master == null)
                gri.master = new List<GitLogElems>();

            if (gri.tags == null)
                gri.tags = new List<GitLogElems>();

            return gri;
        }

        private enum ENUM_LOG_FIELD
        {
            commitNumber,
            author,
            authorEmail,
            date,
            comment,
        }
        static void UpdateGitLogElem(List<GitLogElems> gitLogElemsList, ENUM_LOG_FIELD eLogField, int elemIndex, string value)
        {
            GitLogElems gitLog = gitLogElemsList[elemIndex];

            switch (eLogField)
            {
                case ENUM_LOG_FIELD.commitNumber:
                    gitLog.commitNumber = value;
                    break;
                case ENUM_LOG_FIELD.author:
                    gitLog.author = value;
                    break;
                case ENUM_LOG_FIELD.authorEmail:
                    gitLog.authorEmail = value;
                    break;
                case ENUM_LOG_FIELD.date:
                    gitLog.date = value;
                    break;
                case ENUM_LOG_FIELD.comment:
                    gitLog.comment += value.Replace(" ", "&nbsp") + "<br>";
                    break;
                default:
                    break;
            }
            gitLogElemsList[elemIndex] = gitLog;
        }

        static List<GitLogElems> parseGitLogFileIntoGitLogElemsListNew(string gitLogFilePath)
        {
            List<string> logElemList = new List<string>();
            List<string> lines = System.IO.File.ReadLines(gitLogFilePath).ToList();
            List<GitLogElems> gitLogElemsList = new List<GitLogElems>();
            int elemIndex = 0;
            string tempValue = string.Empty;

            foreach (string line in lines)
            {
                if (line.ToLower().Trim().StartsWith("commit"))
                {
                    ++elemIndex;
                    gitLogElemsList.Add(new GitLogElems());

                    logElemList = line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    UpdateGitLogElem(gitLogElemsList, ENUM_LOG_FIELD.commitNumber, elemIndex - 1, logElemList[1].Substring(0, 6));
                }
                else if (line.ToLower().Trim().StartsWith("author:"))
                {
                    logElemList = line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    UpdateGitLogElem(gitLogElemsList, ENUM_LOG_FIELD.author, elemIndex - 1, logElemList[1]);
                    UpdateGitLogElem(gitLogElemsList, ENUM_LOG_FIELD.authorEmail, elemIndex - 1, logElemList[2].Trim(new char[] { ' ', '<', '>' }));
                }
                else if (line.ToLower().Trim().StartsWith("date:"))
                {
                    tempValue = line.Trim().Substring("date:".Length);
                    UpdateGitLogElem(gitLogElemsList, ENUM_LOG_FIELD.date, elemIndex - 1, tempValue);
                }
                else
                {
                    UpdateGitLogElem(gitLogElemsList, ENUM_LOG_FIELD.comment, elemIndex - 1, line);                    
                }

            }

            return gitLogElemsList;
        }
        static List<GitLogElems> parseGitLogFileIntoGitLogElemsList(string gitLogFilePath)
        {
            GitRepositoryInfo gitRepoInfo = new GitRepositoryInfo();

            string gitLog = File.ReadAllText(gitLogFilePath);
            List<string> lines = gitLog.Split(new string[] { _lineSeparatorString }, StringSplitOptions.RemoveEmptyEntries).ToList();

            //List<string> lines = System.IO.File.ReadLines(gitLogFilePath).ToList();
            List<string> logElemList = new List<string>();

            GitLogElems gitLogElems;
            List<GitLogElems> gitLogElemsList = new List<GitLogElems>();

            foreach (string line in lines)
            {
                logElemList = line.Split(new string[] { _elemSeparatorString }, StringSplitOptions.None).ToList();

                gitLogElems = new GitLogElems();
                gitLogElems.commitNumber = logElemList[0];
                gitLogElems.author = logElemList[1];
                gitLogElems.date = logElemList[2];
                gitLogElems.comment = logElemList[3];
                gitLogElems.authorEmail = logElemList[4];

                gitLogElemsList.Add(gitLogElems);
            }

            return gitLogElemsList;
        }

        private static void readTail(string filename)
        {
            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // Seek 1024 bytes from the end of the file
                fs.Seek(-1024, SeekOrigin.End);
                // read 1024 bytes
                byte[] bytes = new byte[1024];
                fs.Read(bytes, 0, 1024);
                // Convert bytes to string
                string s = Encoding.Default.GetString(bytes);
                // or string s = Encoding.UTF8.GetString(bytes);
                // and output to console
                Console.WriteLine(s);
            }
        }

        private static void createUbuntuOSBuildScript(ProjectConfigInfo pcfi, string buildBranch)
        {
            string templateFilePath = Path.Combine(_buildScriptTemplateRootDir, pcfi.buildOS);
            string s = File.ReadAllText(templateFilePath);

            s = s.Replace("PROJECT_DIR={PROJECT_DIR}",                           "PROJECT_DIR=" + pcfi.projectName);
            s = s.Replace("HOST_SHARE_DIR_NAME={HOST_SHARE_DIR_NAME}",           "HOST_SHARE_DIR_NAME=" + "/media/sf_" + Path.GetFileName(_ps.localSharedDir));
            s = s.Replace("GIT_AUTH_USER={GIT_AUTH_USER}",                       "GIT_AUTH_USER=" + pcfi.gitUser);
            s = s.Replace("GIT_AUTH_PASSWORD={GIT_AUTH_PASSWORD}",               "GIT_AUTH_PASSWORD=" + pcfi.gitPassword);
            s = s.Replace("GIT_PROTOCOL={GIT_PROTOCOL}",                         "GIT_PROTOCOL=" + pcfi.gitProtocol);
            s = s.Replace("GIT_REPO_URL={GIT_REPO_URL}",                         "GIT_REPO_URL=" + pcfi.gitUrl);
            s = s.Replace("BUILD_USER={BUILD_USER}",                             "BUILD_USER=" + pcfi.buildUser);
            s = s.Replace("BUILD_GROUP={BUILD_GROUP}",                           "BUILD_GROUP=" + pcfi.buildGroup);
            s = s.Replace("LOG_LINE_SEPARATOR={LOG_LINE_SEPARATOR}",             "LOG_LINE_SEPARATOR=" + _lineSeparatorString);
            s = s.Replace("LOG_ELEM_SEPARATOR={LOG_ELEM_SEPARATOR}",             "LOG_ELEM_SEPARATOR=" + _elemSeparatorString);
            s = s.Replace("BUILD_TRUNK={BUILD_TRUNK}",                           "BUILD_TRUNK=" + pcfi.buildTrunk);
            s = s.Replace("BUILD_BRANCH={BUILD_BRANCH}",                         "BUILD_BRANCH=" + buildBranch);
            s = s.Replace("BUILD_SH={BUILD_SH}",                                 "BUILD_SH=" + pcfi.buildSH);
            s = s.Replace("BUILD_LOG_FILE={BUILD_LOG_FILE}",                     "BUILD_LOG_FILE=" + _buildLogFileName);
            s = s.Replace("BUILD_RESULT_FILE={BUILD_RESULT_FILE}",               "BUILD_RESULT_FILE=" + _buildResultFileName);
            s = s.Replace("GIT_LOG_FILE={GIT_LOG_FILE}",                         "GIT_LOG_FILE=" + _gitLogFileName);
            s = s.Replace("BUILD_SCRIPT_LOG_FILE={BUILD_SCRIPT_LOG_FILE}",       "BUILD_SCRIPT_LOG_FILE=" + _buildScriptLogFileName);
            s = s.Replace("VM_EXEC_STATUS_FILE={VM_EXEC_STATUS_FILE}",           "VM_EXEC_STATUS_FILE=" + _vmExecStatusFileName);
            s = s.Replace("VM_GET_BUILD_LOG_BACKUP_FILE_NAME={VM_GET_BUILD_LOG_BACKUP_FILE_NAME}", "VM_GET_BUILD_LOG_BACKUP_FILE_NAME=" + _vmGetBuildLogBackupFileName);            
            s = s.Replace("VM_EXEC_STATUS_ONGOING={VM_EXEC_STATUS_ONGOING}",     "VM_EXEC_STATUS_ONGOING=" + _vmExecStatusOngoing);
            s = s.Replace("VM_EXEC_STATUS_FINISHED={VM_EXEC_STATUS_FINISHED}",   "VM_EXEC_STATUS_FINISHED=" + _vmExecStatusFinished);
            s = s.Replace("VM_EXEC_STATUS_BEGIN_GIT_UPDATE_CODE={VM_EXEC_STATUS_BEGIN_GIT_UPDATE_CODE}",   "VM_EXEC_STATUS_BEGIN_GIT_UPDATE_CODE=" + _vmExecStatusBeginGitUpdateCode);
            s = s.Replace("VM_EXEC_STATUS_FINISH_GIT_UPDATE_CODE={VM_EXEC_STATUS_FINISH_GIT_UPDATE_CODE}",   "VM_EXEC_STATUS_FINISH_GIT_UPDATE_CODE=" + _vmExecStatusFinishGitUpdateCode);
            s = s.Replace("VM_EXEC_STATUS_BEGIN_BUILD_CODE={VM_EXEC_STATUS_BEGIN_BUILD_CODE}",   "VM_EXEC_STATUS_BEGIN_BUILD_CODE=" + _vmExecStatusBeginBuildCode);
            s = s.Replace("VM_EXEC_STATUS_FINISH_BUILD_CODE={VM_EXEC_STATUS_FINISH_BUILD_CODE}",   "VM_EXEC_STATUS_FINISH_BUILD_CODE=" + _vmExecStatusFinishBuildCode);
            s = s.Replace("VM_EXEC_STATUS_BEGIN_PACKAGE_FILES={VM_EXEC_STATUS_BEGIN_PACKAGE_FILES}", "VM_EXEC_STATUS_BEGIN_PACKAGE_FILES=" + _vmExecStatusBeginPackageFiles);
            s = s.Replace("VM_EXEC_STATUS_FINISH_PACKAGE_FILES={VM_EXEC_STATUS_FINISH_PACKAGE_FILES}", "VM_EXEC_STATUS_FINISH_PACKAGE_FILES=" + _vmExecStatusFinishPackageFiles);
            s = s.Replace("TASKS_INTERVAL_SECONDS={TASKS_INTERVAL_SECONDS}",               "TASKS_INTERVAL_SECONDS=" + _tasksIntervalSeconds);
            s = s.Replace("BUILD_SUCCESS_MESSAGE={BUILD_SUCCESS_MESSAGE}", "BUILD_SUCCESS_MESSAGE=\"" + _buildSuccessMessage + "\"");
            s = s.Replace("BUILD_ERROR_MESSAGE={BUILD_ERROR_MESSAGE}", "BUILD_ERROR_MESSAGE=\"" + _buildErrorMessage + "\"");
            s = s.Replace("BUILD_NO_BUILD_FILE_MESSAGE={BUILD_NO_BUILD_FILE_MESSAGE}", "BUILD_NO_BUILD_FILE_MESSAGE=\"" + _buildNoBuildFileMessage + "\"");
            s = s.Replace("BUILD_RESULT_MESSAGE={BUILD_RESULT_MESSAGE}", "BUILD_RESULT_MESSAGE=\"" + _buildResultMessage + "\"");
            s = s.Replace("UPDATE_GIT_CODE_ERROR_MESSAGE={UPDATE_GIT_CODE_ERROR_MESSAGE}", "UPDATE_GIT_CODE_ERROR_MESSAGE=\"" + _updateGitCodeErrorMessage + "\"");

            File.WriteAllText(_buildScriptFilePath, s);
        }
    }


}
