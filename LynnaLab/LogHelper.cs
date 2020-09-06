using System;
using System.IO;
using log4net;
using System.Reflection;

namespace LynnaLab
{
    public class LogHelper
    {
        /// When using .NET framework this was not necessary. But after switching to .NET Core it
        /// became necessary to add this static constructor which loads the log4net config.
        static LogHelper() {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            log4net.Config.XmlConfigurator.Configure(logRepository, new System.IO.FileInfo("log4net.config"));
        }


        public static void AddAppenderToRootLogger(log4net.Appender.IAppender a) {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly())).Root.AddAppender(a);
        }

        public static void RemoveAppenderFromRootLogger(log4net.Appender.IAppender a) {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly())).Root.RemoveAppender(a);
        }
    }
}

