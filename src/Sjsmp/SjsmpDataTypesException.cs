using System;

namespace Sjsmp
{
    public sealed class SjsmpDataTypesException : Exception
    {
        public SjsmpDataTypesException(string message)
            : base(message)
        {
        }

        public SjsmpDataTypesException(string message, Exception e)
            : base(message, e)
        {
        }
    }
}
