using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceUtils.Parsing
{
    public abstract class GrammarException : Exception
    {
        public ParseResult Context { get; }

        protected GrammarException( ParseResult context, string message )
            : base( $"{message} at line {context.LineNumber}, column {context.ColumnNumber}" )
        {
            Context = context;
        }
    }

    public sealed class GrammarParseException : GrammarException
    {
        public GrammarParseException( ParseResult context )
            : base( context.Errors.First(), context.ErrorMessage )
        {
        }
    }

    /// <summary>The set of named rules produced by parsing a grammar, indexable by (optionally dotted) name.</summary>
    public sealed class NamedParserCollection : INamedParserResolver
    {
        private readonly Dictionary<string, NamedParser> _namedParsers = new Dictionary<string, NamedParser>();
        private readonly Stack<string> _namespace = new Stack<string>();

        public void PushNamespace( string @namespace )
        {
            if ( _namespace.Count > 0 ) @namespace = $"{_namespace.Peek()}.{@namespace}";
            _namespace.Push( @namespace );
        }

        public void PopNamespace() => _namespace.Pop();

        public void Add( string name, Parser definition )
        {
            if ( _namespace.Count > 0 ) name = $"{_namespace.Peek()}.{name}";

            if ( _namedParsers.TryGetValue( name, out var parser ) )
            {
                parser.Define( parser.ResolvedParser | definition, parser.CollapseIfSingleElement );
            }
            else
            {
                parser = new NamedParser( name );
                _namedParsers.Add( name, parser );
                parser.Define( definition );
            }
        }

        public NamedParser Get( string name ) => new NamedParser( name, _namespace.Count == 0 ? null : _namespace.Peek(), this );

        public NamedParser this[ string name ] => _namedParsers[name];

        public IEnumerable<string> ParserNames => _namedParsers.Keys;

        public override string ToString() =>
            string.Join( Environment.NewLine, _namedParsers.Select( x => $"{x.Key} = {x.Value.ResolvedParser};" ) );

        private NamedParser GetExisting( string fullName ) => _namedParsers.TryGetValue( fullName, out var parser ) ? parser : null;

        public bool ResolveDefinition( NamedParser named )
        {
            if ( named.Namespace == null )
            {
                var existing = GetExisting( named.Name );
                if ( existing == null ) return false;

                named.ResolvedName = existing.Name;
                named.Define( existing.ResolvedParser, existing.CollapseIfSingleElement );
                return true;
            }

            var splitIndex = named.Namespace.Length;
            while ( true )
            {
                var name = splitIndex <= 0 ? named.Name : $"{named.Namespace.Substring( 0, splitIndex )}.{named.Name}";
                var existing = GetExisting( name );
                if ( existing != null )
                {
                    named.ResolvedName = existing.Name;
                    named.Define( existing.ResolvedParser, existing.CollapseIfSingleElement );
                    return true;
                }
                if ( splitIndex <= 0 ) break;

                splitIndex = named.Namespace.LastIndexOf( ".", splitIndex - 1, StringComparison.Ordinal );
            }

            return false;
        }
    }

    /// <summary>Builds a <see cref="NamedParserCollection"/> of rules from a text grammar, e.g. as loaded from KeyValues.txt.</summary>
    public static class GrammarBuilder
    {
        private static GrammarParser Parser { get; } = new GrammarParser();

        public static NamedParserCollection FromString( string str )
        {
            var result = Parser.Parse( str );
            if ( !result.Success ) throw new GrammarParseException( result );

            var rules = new NamedParserCollection();
            ReadStatementBlock( result[0], rules );

            return rules;
        }

        private static void ReadStatementBlock( ParseResult statementBlock, NamedParserCollection rules )
        {
            foreach ( var statement in statementBlock )
            {
                var value = statement[0];
                if ( value.Parser == Parser.Definition ) ReadDefinition( value, rules );
                else if ( value.Parser == Parser.SpecialBlock ) ReadSpecialBlock( value, rules );
            }
        }

        private static void ReadDefinition( ParseResult definition, NamedParserCollection rules )
        {
            Debug.Assert( definition.Parser == Parser.Definition );

            var name = definition[0].Value;

            rules.PushNamespace( name );

            var branch = definition.FirstOrDefault( x => x.Parser == Parser.Branch );
            var statementBlock = definition.FirstOrDefault( x => x.Parser == Parser.StatementBlock );

            if ( statementBlock != null ) ReadStatementBlock( statementBlock, rules );

            var parser = branch != null ? ReadBranch( branch, rules ) : null;

            rules.PopNamespace();

            if ( parser != null ) rules.Add( name, parser );
        }

        private static Parser ReadBranch( ParseResult branch, NamedParserCollection rules )
        {
            Debug.Assert( branch.Parser == Parser.Branch );

            return branch.InnerCount == 1
                ? ReadConcat( branch[0], rules )
                : new BranchParser( branch.Select( x => ReadConcat( x, rules ) ) );
        }

        private static Parser ReadConcat( ParseResult concat, NamedParserCollection rules )
        {
            Debug.Assert( concat.Parser == Parser.Concat );

            return concat.InnerCount == 1
                ? ReadModifier( concat[0], rules )
                : new ConcatParser( concat.Select( x => ReadModifier( x, rules ) ) );
        }

        private static Parser ReadModifier( ParseResult modifier, NamedParserCollection rules )
        {
            Debug.Assert( modifier.Parser == Parser.Modifier );

            var prefix = modifier.FirstOrDefault( x => x.Parser == Parser.ModifierPrefix );
            var term = ReadTerm( modifier.First( x => x.Parser == Parser.Term ), rules );
            var postfix = modifier.FirstOrDefault( x => x.Parser == Parser.ModifierPostfix );

            if ( prefix != null )
            {
                term = prefix.Value switch
                {
                    "$" => term.Strict,
                    "!" => term.Not,
                    _ => throw new Exception( $"Unrecognised modifier '{prefix.Value}'." )
                };
            }

            if ( postfix != null )
            {
                term = postfix.Value switch
                {
                    "?" => term.Optional,
                    "+" => term.Repeated,
                    "*" => term.Repeated.Optional,
                    _ => throw new Exception( $"Unrecognised modifier '{postfix.Value}'." )
                };
            }

            return term;
        }

        private static Parser ReadTerm( ParseResult term, NamedParserCollection rules )
        {
            Debug.Assert( term.Parser == Parser.Term );

            var value = term[0];
            if ( value.Parser == Parser.String ) return ReadString( value );
            if ( value.Parser == Parser.Regex ) return ReadRegex( value );
            if ( value.Parser == Parser.NonTerminal ) return ReadNonTerminal( value, rules );
            if ( value.Parser == Parser.Branch ) return ReadBranch( value, rules );
            throw new NotImplementedException();
        }

        private static Parser ReadString( ParseResult str )
        {
            var value = str[0];
            if ( value.Length == 0 ) return EmptyParser.Instance;

            var builder = new StringBuilder( value.Length );
            var escaped = false;
            for ( var i = 0; i < value.Length; ++i )
            {
                var c = value.Value[i];
                if ( escaped )
                {
                    escaped = false;
                    builder.Append( c switch { 'r' => '\r', 'n' => '\n', 't' => '\t', _ => c } );
                }
                else if ( c == '\\' ) escaped = true;
                else builder.Append( c );
            }

            return builder.ToString();
        }

        private static Parser ReadRegex( ParseResult regex )
        {
            var pattern = regex[0];
            var options = regex[1];

            var builder = new StringBuilder( pattern.Length );
            var escaped = false;
            for ( var i = 0; i < pattern.Length; ++i )
            {
                var c = pattern.Value[i];
                if ( escaped )
                {
                    escaped = false;
                    builder.Append( c == '/' ? "/" : "\\" + c );
                }
                else if ( c == '\\' ) escaped = true;
                else builder.Append( c );
            }

            var parsedOptions = RegexOptions.None;
            for ( var i = 0; i < options.Length; ++i )
            {
                if ( options.Value[i] == 'i' ) parsedOptions |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            }

            return new Regex( builder.ToString(), parsedOptions );
        }

        private static Parser ReadNonTerminal( ParseResult nonTerminal, NamedParserCollection rules ) => rules.Get( nonTerminal.Value );

        private static void ReadSpecialBlock( ParseResult specialBlock, NamedParserCollection rules )
        {
            var header = specialBlock[0];
            var block = specialBlock[1];
            var stack = new Stack<IDisposable>();

            while ( header != null )
            {
                var item = header[0];

                if ( item.InnerCount == 1 )
                {
                    var ignore = ReadBranch( item[0], rules );
                    stack.Push( Parsing.Parser.AllowWhitespace( ignore ) );
                }
                else
                {
                    switch ( item.Value )
                    {
                        case "noignore":
                            stack.Push( Parsing.Parser.ForbidWhitespace() );
                            break;
                        case "collapse":
                            stack.Push( Parsing.Parser.EnableCollapseIfSingleElement() );
                            break;
                        default:
                            throw new NotImplementedException( item.Value );
                    }
                }

                header = header.InnerCount == 1 ? null : header[1];
            }

            ReadStatementBlock( block, rules );

            while ( stack.Count > 0 ) stack.Pop().Dispose();
        }
    }
}
