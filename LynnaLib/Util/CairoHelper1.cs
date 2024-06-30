using System;
using System.Collections.Generic;

namespace LynnaLib
{
    class InvalidBitmapFormatException : Exception
    {
        public InvalidBitmapFormatException(String s) : base(s) { }
    }
}
