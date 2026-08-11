using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceUtils.Parsing
{
    public enum ParseError
    {
        None,
        SubParser,
        ExpectedToken,
        InvalidGrammar
    }

    /// <summary>
    /// A node in the tree produced by parsing. Holds the matched range of the
    /// source, any child results kept by the grammar, and (on failure) error
    /// details used to build <see cref="ErrorMessage"/>.
    /// </summary>
    public sealed class ParseResult : IEnumerable<ParseResult>
    {
        private readonly string _source;
        private readonly List<ParseResult> _inner = new List<ParseResult>();

        private int _lineNumber;
        private int _columnNumber;
        private int _lineIndex;
        private int _lineLength;

        private string _errorMessage;
        private string _innerMessage;
        private List<ParseResult> _errorResults;

        public Parser Parser { get; }
        public ParseResult Parent { get; private set; }

        public int Index { get; }
        public int TrimmedIndex { get; private set; }

        public int Length { get; private set; }
        public int TrimmedLength { get; private set; }

        internal bool LastReadWhitespace { get; set; }

        public int LineNumber { get { if ( _lineNumber == 0 ) GetLineCol( Index ); return _lineNumber; } }
        public int ColumnNumber { get { if ( _columnNumber == 0 ) GetLineCol( Index ); return _columnNumber; } }

        private int ReadPos => Index + Length;

        public bool Success { get; private set; } = true;

        public string Value => _source.Substring( TrimmedIndex, TrimmedLength );
        public string SourceLine => _source.Substring( _lineIndex, _lineLength );
        public string Source => _source;

        public string ErrorMessage => GetErrorMessage();
        public ParseError ErrorType { get; private set; }

        public string Expected
        {
            get
            {
                if ( ErrorType != ParseError.ExpectedToken ) throw new InvalidOperationException();
                return Parser is NamedParser named && InnerCount == 0 ? named.Name : _errorMessage;
            }
        }

        public IEnumerable<ParseResult> Errors => _errorResults ??= GetErrors();

        public int InnerCount => _inner.Count;

        public ParseResult this[ int index ] => _inner[index];

        public ParseResult this[ string elementName ]
            => _inner.FirstOrDefault( x => x.Parser.ElementName == elementName );

        public int MaxErrorIndex => ErrorType == ParseError.SubParser && _inner.Count > 0
            ? _inner.Max( x => x.MaxErrorIndex )
            : ErrorType == ParseError.None ? -1 : Index;

        private void GetLineCol( int index )
        {
            _lineNumber = 1;
            _columnNumber = 1;

            for ( var i = 0; i < index; ++i )
            {
                switch ( _source[i] )
                {
                    case '\r':
                        _columnNumber = 1;
                        break;
                    case '\n':
                        ++_lineNumber;
                        _columnNumber = 1;
                        _lineIndex = i + 1;
                        break;
                    default:
                        ++_columnNumber;
                        break;
                }
            }

            _lineLength = _source.Length - _lineIndex;

            for ( var i = index; i < _source.Length; ++i )
            {
                if ( _source[i] == '\r' || _source[i] == '\n' )
                {
                    _lineLength = i - _lineIndex;
                    return;
                }
            }
        }

        internal ParseResult( string source, Parser parser )
        {
            _source = source;
            Parser = parser;
        }

        internal ParseResult( ParseResult parent, Parser parser )
        {
            _source = parent.Source;
            Parser = parser;
            TrimmedIndex = Index = parent.Index + parent.Length;
            LastReadWhitespace = parent.Parser.WhitespaceParser == parser.WhitespaceParser && parent.LastReadWhitespace;
        }

        private bool IsIdentical( ParseResult other ) => other.Parser == Parser && other.Index == Index && other.Length == Length;

        public bool GetIsInfiniteRecursion()
        {
            var parent = this;
            while ( (parent = parent.Parent) != null )
            {
                if ( IsIdentical( parent ) ) return true;
            }

            return false;
        }

        private List<ParseResult> GetErrors( List<ParseResult> dst = null )
        {
            dst ??= new List<ParseResult>();

            if ( _errorResults != null )
            {
                if ( _errorResults.Count > 0 )
                {
                    dst.RemoveAll( x => x.Index < _errorResults[0].Index );
                    dst.AddRange( _errorResults );
                }
                return dst;
            }

            if ( ErrorType == ParseError.None ) return dst;

            if ( ErrorType != ParseError.SubParser )
            {
                if ( dst.Count == 0 ) dst.Add( this );
                else if ( dst[0].Index <= Index )
                {
                    dst.Add( this );
                    dst.RemoveAll( x => x.Index < Index );
                }
                return dst;
            }

            foreach ( var inner in _inner )
            {
                dst = inner.GetErrors( dst );
            }

            return dst;
        }

        private string GetErrorMessage()
        {
            if ( _innerMessage != null ) return _innerMessage;

            var errors = GetErrors();
            if ( errors.Count == 0 ) return _innerMessage = string.Empty;

            var nonExpect = errors.FirstOrDefault( x => x.ErrorType != ParseError.ExpectedToken );
            if ( nonExpect != null ) return _innerMessage = nonExpect.ErrorMessage;

            var distinct = errors.Select( x => x.Expected ).Distinct().ToArray();
            var builder = new StringBuilder();

            for ( var i = 0; i < distinct.Length; ++i )
            {
                if ( i > 0 )
                {
                    builder.Append( ", " );
                    if ( i == distinct.Length - 1 ) builder.Append( "or " );
                }

                builder.Append( distinct[i] );
            }

            return _innerMessage = $"Expected {builder}";
        }

        private bool ShouldFlattenInner( ParseResult result )
        {
            if ( result.Parser.FlattenHierarchy ) return true;
            if ( result.Parser.CollapseIfSingleElement )
            {
                if ( result.InnerCount == 0 && result.Length == 0 ) return true;
                if ( result.InnerCount == 1 && result[0].Parser is NamedParser ) return true;
            }
            return false;
        }

        private void AddInner( ParseResult result, bool errorPass, bool updateParsedLength )
        {
            var len = result.ReadPos - Index;
            if ( Length < len && updateParsedLength )
            {
                Length = len;
                TrimmedLength = result.Index + result.TrimmedLength - TrimmedIndex;
                LastReadWhitespace = LastReadWhitespace && result.Length == 0 || result.LastReadWhitespace && result.Parser.WhitespaceParser == Parser.WhitespaceParser;
            }

            Success &= result.Success;

            if ( !errorPass && !result.Success ) return;

            if ( !ShouldFlattenInner( result ) )
            {
                if ( !result.Parser.OmitFromResult || !result.Success )
                {
                    result.Parent = this;
                    _inner.Add( result );
                }
                return;
            }

            foreach ( var inner in result )
            {
                AddInner( inner, errorPass, updateParsedLength );
            }
        }

        public bool Error( ParseError type, string message )
        {
            Success = false;
            ErrorType = type;
            _errorMessage = message;
            return false;
        }

        public bool Error( ParseResult inner, bool errorPass, bool updateParsedLength = true )
        {
            Success = false;
            if ( ErrorType == ParseError.None ) ErrorType = ParseError.SubParser;
            AddInner( inner, errorPass, updateParsedLength );
            return false;
        }

        public bool Read( string token )
        {
            var index = ReadPos;
            for ( var i = 0; i < token.Length; ++i, ++index )
            {
                if ( index >= _source.Length || _source[index] != token[i] ) return false;
            }

            Length += token.Length;
            TrimmedLength += token.Length;
            LastReadWhitespace = LastReadWhitespace && token.Length == 0;
            return true;
        }

        public bool Read( Regex regex ) => Read( regex, out _ );

        public bool Read( Regex regex, out Match match )
        {
            match = regex.Match( _source, ReadPos );
            if ( !match.Success || match.Index != ReadPos ) return false;

            Length += match.Length;
            TrimmedLength += match.Length;
            LastReadWhitespace = LastReadWhitespace && match.Length == 0;
            return true;
        }

        public bool Read( Parser parser, bool errorPass )
        {
            var result = Peek( parser, errorPass );
            if ( result.Success )
            {
                Apply( result, errorPass );
                return true;
            }

            Error( result, errorPass );
            return false;
        }

        public void Skip( ParseResult inner )
        {
            if ( inner.Index != ReadPos ) throw new ArgumentException();

            Length = inner.ReadPos - Index;
            if ( TrimmedLength == 0 ) TrimmedIndex = inner.ReadPos;

            LastReadWhitespace = LastReadWhitespace && inner.Length == 0 || inner.LastReadWhitespace && inner.Parser.WhitespaceParser == Parser.WhitespaceParser;
        }

        public void Apply( ParseResult inner, bool errorPass )
        {
            if ( inner.Index != ReadPos ) throw new ArgumentException();
            AddInner( inner, errorPass, true );
        }

        public ParseResult Peek( Parser parser, bool errorPass )
        {
            var inner = new ParseResult( this, parser );
            parser.Parse( inner, errorPass );
            return inner;
        }

        public bool IsBetterThan( ParseResult other )
        {
            var thisEnd = TrimmedIndex + TrimmedLength;
            var otherEnd = other == null ? -1 : other.TrimmedIndex + other.TrimmedLength;

            return other == null || thisEnd > otherEnd || thisEnd == otherEnd && Success && !other.Success;
        }

        public IEnumerator<ParseResult> GetEnumerator() => _inner.GetEnumerator();

        public override string ToString() => Success ? _source.Substring( TrimmedIndex, TrimmedLength ) : ErrorMessage;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
