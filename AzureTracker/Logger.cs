using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureTracker
{
    enum LogLevel
    {
        Info,
        Warning, 
        Error
    };
    public class Logger
    {
        public delegate void NewLogDelegate();

        public NewLogDelegate? NewLog = null;
        private Logger()
        { }

        static Logger? m_instance = null;
        public static Logger Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new Logger();
                }
                return m_instance;
            }
        }

        StringBuilder m_sbLog = new StringBuilder();
        public string LogBuffer
        {
            get
            {
                return m_sbLog.ToString();
            }
        }

        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            m_sbLog.Append($"{DateTime.Now.ToLocalTime()} : {level} : {message} \n");
            NewLog?.Invoke();
        }

        public void Info(string message)
        {
            Log(message, LogLevel.Info);
        }

        public void Error(string message)
        {
            Log(message, LogLevel.Error);
        }

        public void Warn(string message)
        {
            Log(message, LogLevel.Warning);
        }

        internal void Clear()
        {
            m_sbLog?.Clear();
            NewLog?.Invoke();
        }

        internal void Save(string path)
        {
            File.WriteAllText(path, m_sbLog.ToString());
        }
    }
}
