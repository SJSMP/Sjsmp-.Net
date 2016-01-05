using System;

namespace Sjsmp.Server
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SjsmpObjectAttribute : Attribute
    {
        public readonly string name;
        public readonly string description;
        public readonly string group;

        public SjsmpObjectAttribute(string name, string description = null, string group = "")
        {
            this.name = name;
            this.description = description ?? name;
            this.group = group;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class SjsmpPropertyAttribute : Attribute
    {
        public readonly string description;
        public readonly bool isReadonly;
        public readonly bool showGraph;

        public SjsmpPropertyAttribute(string description, bool isReadonly = false, bool showGraph = false)
        {
            this.description = description;
            this.isReadonly = isReadonly;
            this.showGraph = showGraph;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class SjsmpPropertyLimitsAttribute : Attribute
    {
        public readonly bool useFloat;
        /*
        public readonly double minFloat, maxFloat;
        public readonly long minInt, maxInt;
         */
        public readonly object min, max;

        public SjsmpPropertyLimitsAttribute(long min, long max)
        {
            this.useFloat = false;
            /*
            this.minInt = min;
            this.maxInt = max;
            this.minFloat = double.NaN;
            this.maxFloat = double.NaN;
             * */
            this.min = min;
            this.max = max;

            if (min > max)
            {
                throw new SjsmpServerException("Min should be LT or equal to max: " + min + " <= " + max);
            }
        }

        public SjsmpPropertyLimitsAttribute(double min, double max)
        {
            this.useFloat = true;
            /*
            this.minInt = min;
            this.maxInt = max;
            this.minFloat = double.NaN;
            this.maxFloat = double.NaN;
             * */
            this.min = min;
            this.max = max;

            if (min > max)
            {
                throw new SjsmpServerException("Min should be LT or equal to max: " + min + " <= " + max);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SjsmpActionAttribute : Attribute
    {
        public readonly string description;
        public readonly bool requireConfirm;

        public SjsmpActionAttribute(string description, bool requireConfirm = false)
        {
            this.description = description;
            this.requireConfirm = requireConfirm;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SjsmpActionParameterAttribute : Attribute
    {
        public readonly string description;

        public SjsmpActionParameterAttribute(string description)
        {
            this.description = description;
        }
    }
}
