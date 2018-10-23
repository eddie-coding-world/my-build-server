using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace BuildHelper
{
    public class Logger
    {
        static private string _logFilePath = string.Empty;
        static private StreamWriter sw = null;

        static public void Start(string logFilePath)
        {
            if (File.Exists(logFilePath))
                File.Delete(logFilePath);
            _logFilePath = logFilePath;
        }
        static public void Write(string message)
        {
            string logMessage = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), message, Environment.NewLine);
            File.AppendAllText(_logFilePath, logMessage);
            Console.WriteLine(logMessage);            
        }

    }
}
