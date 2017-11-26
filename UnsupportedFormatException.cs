using System;

namespace Octagon.Formatik
{
    public class UnsupportedFormatException: FormatikException
    {
        public UnsupportedFormatException() {}

        public UnsupportedFormatException(string message) : base(message) {}
        
        public UnsupportedFormatException(string message, Exception inner) : base(message, inner) {}
        
    }
}