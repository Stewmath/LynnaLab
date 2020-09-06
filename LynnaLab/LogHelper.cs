using System;
using log4net;
using System.Reflection;

namespace LynnaLab
{
    public class LogHelper
    {
        public static void AddAppenderToRootLogger(log4net.Appender.IAppender a) {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly())).Root.AddAppender(a);
        }

        public static void RemoveAppenderFromRootLogger(log4net.Appender.IAppender a) {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly())).Root.RemoveAppender(a);
        }
    }
}

