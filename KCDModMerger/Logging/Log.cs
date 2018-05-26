#region usings

using System;

#endregion

namespace KCDModMerger.Logging
{
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class Log : Attribute
    {
        // Leave empty
    }
}