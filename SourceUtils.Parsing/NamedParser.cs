using System;

namespace SourceUtils.Parsing
{
    public interface INamedParserResolver
    {
        bool ResolveDefinition( NamedParser named );
    }

    /// <summary>
    /// A forward-declared, lazily-resolved reference to a rule by name (optionally
    /// namespace-qualified, e.g. "Definition.List"). Used both for rules produced by
    /// <see cref="GrammarBuilder"/> and for hand-authored parsers built on <see cref="CustomParser"/>.
    /// </summary>
    public class NamedParser : Parser
    {
        private Parser _resolvedParser;
        private readonly string _nameEnd;
        private bool _collapseSingletons;

        public string Name { get; }
        public string Namespace { get; }
        public INamedParserResolver Resolver { get; }

        public override bool CollapseIfSingleElement
        {
            get
            {
                if ( !IsResolved ) AttemptResolve();
                return _collapseSingletons;
            }
        }

        public string ResolvedName { get; set; }

        public bool IsResolved => _resolvedParser != null;

        public Parser ResolvedParser => _resolvedParser ??= AttemptResolve();

        private Parser AttemptResolve()
        {
            Resolver?.ResolveDefinition( this );
            return _resolvedParser;
        }

        public void Define( Parser value )
        {
            _resolvedParser = value;
            _collapseSingletons = CurrentCollapseState;
        }

        public void Define( Parser value, bool collapseIfSingleElement )
        {
            _resolvedParser = value;
            _collapseSingletons = collapseIfSingleElement;
        }

        protected NamedParser()
        {
            _nameEnd = Name = GetType().Name;
            Resolver = null;
        }

        public NamedParser( string name, string @namespace = null, INamedParserResolver resolver = null )
        {
            Name = name;
            Namespace = @namespace;
            Resolver = resolver;

            _nameEnd = name.Substring( name.LastIndexOf( '.' ) + 1 );
        }

        protected override bool OnParse( ParseResult result, bool errorPass )
        {
            if ( ResolvedParser == null ) throw new Exception( $"Could not resolve parser with name '{Name}'" );

            return result.Read( ResolvedParser, errorPass );
        }

        public override string ElementName => ResolvedName ?? Name;

        public override string ToString() => $"<{ElementName}>";
    }
}
