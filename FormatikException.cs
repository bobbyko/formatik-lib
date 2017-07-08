using System;

namespace Octagon.Formatik
{
    public class FormatikException: Exception
    {
        public FormatikException() {}

        public FormatikException(string message) : base(message) {}
        
        public FormatikException(string message, Exception inner) : base(message, inner) {}
    }
}