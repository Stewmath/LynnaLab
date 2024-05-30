using System;

// Class of static functions

namespace Util {
    public class Wla {
        public static string ToHex(int data, int digits) {
            return "$"+data.ToString("x" + digits);
        }
        public static string ToHalfByte(byte data) {
            // TODO: assert size?
            return ToHex(data, 1);
        }
        public static string ToByte(byte data) {
            return ToHex(data, 2);
        }
        public static string ToWord(int data) {
            // TODO: assert size?
            return ToHex(data, 4);
        }
        public static string ToBinary(int data) {
            string s = Convert.ToString(data, 2);
            while (s.Length < 8)
                s = "0"+s;
            return "%"+s;
        }
    }
}
