using System;

// Class of static functions

public class Wla {
    public static string ToHalfByte(byte data) {
        string s = "$"+data.ToString("x1");
        return s;
    }
    public static string ToByte(byte data) {
        string s = "$"+data.ToString("x2");
        return s;
    }
    public static string ToWord(int data) {
        string s = "$"+data.ToString("x4");
        return s;
    }
}
