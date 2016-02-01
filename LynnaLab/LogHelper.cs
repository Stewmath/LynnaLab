using System;
using log4net;

namespace LynnaLab
{
    public class LogHelper
    {
        public static void AddAppenderToRootLogger(log4net.Appender.IAppender a) {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.AddAppender(a);
        }

        public static void RemoveAppenderFromRootLogger(log4net.Appender.IAppender a) {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.RemoveAppender(a);
        }
    }
}

