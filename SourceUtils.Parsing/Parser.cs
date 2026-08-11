using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceUtils.Parsing
{
    /// <summary>
    /// Base class for a PEG-style parser combinator. Concrete parsers are
    /// combined using +, |, and ! (concat/branch/not), or built from a text
    /// grammar via <see cref="GrammarBuilder"/>.
    /// </summary>
    public abstract class Parser
    {
        [ThreadStatic]
        private static Stack<Parser> _sWhitespaceParserStack;
        private static Stack<Parser> WhitespaceParserStack => _sWhitespaceParserStack ??= new Stack<Parser>();

        [ThreadStatic]
        private static Stack<bool> _sCollapseStateStack;
        private static Stack<bool> CollapseStateStack => _sCollapseStateStack ??= new Stack<bool>();

        private static Parser CurrentWhitespaceParser => WhitespaceParserStack.Count == 0 ? null : WhitespaceParserStack.Peek();
        protected static bool CurrentCollapseState => CollapseStateStack.Count != 0 && CollapseStateStack.Peek();

        public static Parser EndOfInput { get; } = new RegexParser( new Regex( "$" ) );

        private sealed class PopDisposable : IDisposable
        {
            private readonly Action _pop;
            public PopDisposable( Action pop ) { _pop = pop; }
            public void Dispose() => _pop();
        }

        public static IDisposable ForbidWhitespace()
        {
            WhitespaceParserStack.Push( null );
            return new PopDisposable( () => WhitespaceParserStack.Pop() );
        }

        public static IDisposable AllowWhitespace( Parser whitespaceParser )
        {
            WhitespaceParserStack.Push( CurrentWhitespaceParser == null ? whitespaceParser : CurrentWhitespaceParser | whitespaceParser );
            return new PopDisposable( () => WhitespaceParserStack.Pop() );
        }

        public static IDisposable EnableCollapseIfSingleElement()
        {
            CollapseStateStack.Push( true );
            return new PopDisposable( () => CollapseStateStack.Pop() );
        }

        public static implicit operator Parser( string token ) => token.Length == 0 ? EmptyParser.Instance : new TokenParser( token );

        public static implicit operator Parser( Regex regex ) => new RegexParser( regex );

        public static NotParser operator !( Parser parser ) => new NotParser( parser );

        public static ConcatParser operator +( Parser a, Parser b )
        {
            var result = new ConcatParser();
            if ( a is ConcatParser aConcat ) result.AddRange( aConcat.Inner ); else result.Add( a );
            if ( b is ConcatParser bConcat ) result.AddRange( bConcat.Inner ); else result.Add( b );
            return result;
        }

        public static BranchParser operator |( Parser a, Parser b )
        {
            var result = new BranchParser();
            if ( a is BranchParser aBranch ) result.AddRange( aBranch.Inner ); else result.Add( a );
            if ( b is BranchParser bBranch ) result.AddRange( bBranch.Inner ); else result.Add( b );
            return result;
        }

        /// <summary>
        /// Parses the given source. If the first attempt fails, re-parses in
        /// "error pass" mode so a descriptive <see cref="ParseResult.ErrorMessage"/>
        /// can be produced (the fast path skips that bookkeeping).
        /// </summary>
        public ParseResult Parse( string source )
        {
            var result = new ParseResult( source, this );
            if ( !Parse( result, false ) || !result.Success )
            {
                result = new ParseResult( source, this );
                Parse( result, true );
            }

            return CollapseIfSingleElement && result.InnerCount == 1 ? result[0] : result;
        }

        // Captured when the parser is constructed, so parsers created inside an
        // AllowWhitespace/ForbidWhitespace scope automatically skip whitespace.
        internal Parser WhitespaceParser { get; } = CurrentWhitespaceParser;

        public virtual bool CollapseIfSingleElement => false;
        public virtual bool FlattenHierarchy => false;
        public virtual bool OmitFromResult => false;

        protected abstract bool OnParse( ParseResult result, bool errorPass );

        private void SkipWhitespace( ParseResult result )
        {
            if ( WhitespaceParser == null || result.LastReadWhitespace ) return;

            ParseResult whitespace;
            while ( (whitespace = result.Peek( WhitespaceParser, false )).Success && whitespace.Length > 0 )
            {
                result.Skip( whitespace );
            }

            result.LastReadWhitespace = true;
        }

        public bool Parse( ParseResult result, bool errorPass )
        {
            SkipWhitespace( result );
            if ( !OnParse( result, errorPass ) ) return false;
            SkipWhitespace( result );
            return true;
        }

        private string _elementName;
        public virtual string ElementName
        {
            get
            {
                if ( _elementName != null ) return _elementName;

                _elementName = GetType().Name;
                if ( _elementName.EndsWith( "Parser" ) )
                {
                    _elementName = _elementName.Substring( 0, _elementName.Length - "Parser".Length );
                }

                return _elementName;
            }
        }

        public Parser Repeated => new RepeatedParser( this );
        public Parser Optional => this | "";
        public Parser Strict => new StrictParser( this );
        public Parser Not => !this;
    }
}
