using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LynnaLab {

    // Some static functions
    public class Helper {

        // Convert a string range into a list of ints.
        // Example: "13,15-18" => {13,15,16,17,18}
        public static HashSet<int> GetIntListFromRange(string s) {
            if (s == "")
                return new HashSet<int>();

            Func<string,Tuple<int,int>> f = str =>
            {
                int i = str.IndexOf('-');
                if (i == -1) {
                    int n = Convert.ToInt32(str,16);
                    return new Tuple<int,int>(n,n);
                }
                int n1 = Convert.ToInt32(str.Substring(0,i),16);
                int n2 = Convert.ToInt32(str.Substring(i+1),16);
                return new Tuple<int,int>(n1,n2);
            };

            int ind = s.IndexOf(',');
            if (ind == -1)  {
                var ret = new HashSet<int>();
                var tuple = f(s);
                for (int i=tuple.Item1;i<=tuple.Item2;i++) {
                    ret.Add(i);
                }
                return ret;
            }
            HashSet<int> ret2 = GetIntListFromRange(s.Substring(0,ind));
            ret2.UnionWith(GetIntListFromRange(s.Substring(ind+1)));
            return ret2;
        }

        // Like Directory.GetFiles(), but guaranteed to be sorted.
        public static IList<string> GetSortedFiles(string dir) {
            List<string> files = new List<string>(Directory.GetFiles(dir));
            files.Sort();
            return files;
        }

        // Read a resource file
        public static string ReadResourceFile(string name) {
            return new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name)).ReadToEnd();
        }
    }

}
