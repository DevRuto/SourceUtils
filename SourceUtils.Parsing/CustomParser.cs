using System.Reflection;

namespace SourceUtils.Parsing
{
    /// <summary>
    /// Base class for hand-authored parsers defined directly in C# (rather than
    /// from a text grammar). Any <see cref="NamedParser"/> fields are auto-instantiated
    /// by name before <see cref="OnDefine"/> runs, so rules can reference each other
    /// before they're all assigned. See <see cref="GrammarParser"/> for the only
    /// consumer of this in practice: the bootstrap parser for the grammar DSL itself.
    /// </summary>
    public abstract class CustomParser : Parser
    {
        private Parser _definedParser;
        private bool IsDefined => _definedParser != null;

        protected abstract Parser OnDefine();

        private void Define()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach ( var field in GetType().GetFields( flags ) )
            {
                if ( field.FieldType != typeof( NamedParser ) ) continue;
                if ( field.GetValue( this ) != null ) continue;

                field.SetValue( this, new NamedParser( field.Name.Replace( '_', '.' ) ) );
            }

            _definedParser = OnDefine();
        }

        public Parser this[ NamedParser parser ]
        {
            get => parser;
            set => parser.Define( value );
        }

        protected override bool OnParse( ParseResult result, bool errorPass )
        {
            if ( !IsDefined ) Define();
            return result.Read( _definedParser, errorPass );
        }
    }
}
