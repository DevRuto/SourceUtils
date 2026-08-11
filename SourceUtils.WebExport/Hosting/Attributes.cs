using System;

namespace SourceUtils.WebExport.Hosting
{
    /// <summary>
    /// Marks a <see cref="Controller"/> subclass with a URL prefix that its action
    /// methods are matched relative to. Multiple instances can be applied to
    /// register the same controller under several prefixes.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = true )]
    public sealed class PrefixAttribute : Attribute
    {
        public string Value { get; }

        /// <summary>
        /// Default file extension required for action methods that don't specify
        /// their own <see cref="GetAttribute.Extension"/>.
        /// </summary>
        public string Extension { get; set; }

        public PrefixAttribute( string value )
        {
            Value = value;
        }
    }

    /// <summary>
    /// Marks a method as the handler for GET requests matching the containing
    /// controller's prefix, optionally extended by <see cref="Pattern"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method )]
    public sealed class GetAttribute : Attribute
    {
        public string Pattern { get; }

        /// <summary>
        /// If true (the default), the full request path must match exactly.
        /// If false, only the prefix (plus <see cref="Extension"/>, if given) is matched,
        /// allowing arbitrary path segments in between.
        /// </summary>
        public bool MatchAllUrl { get; set; } = true;

        public string Extension { get; set; }

        public GetAttribute( string pattern = null )
        {
            Pattern = pattern;
        }
    }

    /// <summary>
    /// Binds an action method parameter to a named capture in the matched URL,
    /// e.g. <c>{map}</c> in <c>/maps/{map}/index.html</c>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Parameter )]
    public sealed class UrlAttribute : Attribute
    {
    }
}
