using System;
using System.Linq;
using System.Collections.Generic;

/// Like Gtk.Statusbar, but the "contextID" parameter to the "Push" method also functions as
/// a "priority" number. Higher numbers are always displayed (lower numbers can't displace them).
namespace LynnaLab {
    public class PriorityStatusbar : Gtk.HBox {

        Gtk.Statusbar child = new Gtk.Statusbar();
        Dictionary<uint, List<string>> messages = new Dictionary<uint, List<string>>();


        public PriorityStatusbar() {
            Add(child);
        }


        public void Push(uint contextID, string text) {
            GetMessageList(contextID).Add(text);
            DetermineMessageToDisplay();
        }

        /// Set is like Push, but only one message per contextID is allowed
        public void Set(uint contextID, string text) {
            var l = GetMessageList(contextID);
            l.Clear();
            l.Add(text);
            DetermineMessageToDisplay();
        }

        public void Pop(uint contextID) {
            if (!messages.ContainsKey(contextID))
                return;
            List<string> l = GetMessageList(contextID);
            l.RemoveAt(l.Count-1);
            if (l.Count == 0)
                messages.Remove(contextID);
            DetermineMessageToDisplay();
        }

        public void RemoveAll(uint contextID) {
            if (messages.ContainsKey(contextID)) {
                messages.Remove(contextID);
                DetermineMessageToDisplay();
            }
        }

        List<string> GetMessageList(uint index) {
            List<string> ret;
            if (!messages.TryGetValue(index, out ret)) {
                ret = new List<string>();
                messages[index] = ret;
            }
            return ret;
        }

        void DetermineMessageToDisplay() {
            child.RemoveAll(0);

            if (messages.Count == 0)
                return;

            List<string> l = messages[messages.Keys.Max()];
            string msg = l[l.Count-1];

            child.Push(0, msg);
        }
    }
}
