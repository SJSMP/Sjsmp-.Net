using System;

namespace Sjsmp.Server
{
    public sealed class SjsmpServerException : Exception
    {
        public SjsmpServerException(string message)
            : base(message)
        {
        }

        public SjsmpServerException(string message, Exception e)
            : base(message, e)
        {
        }
    }
}
