namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// netstandard2.1 lacks this marker type; the C# 9 compiler needs it for <c>init</c> accessors and records.
    /// Unity documents the same polyfill.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
