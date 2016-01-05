using System;

namespace Sjsmp.Client
{
    public class SjsmpException : Exception
    {
        public SjsmpException(string message)
            : base(message)
        {
        }
    }
}
