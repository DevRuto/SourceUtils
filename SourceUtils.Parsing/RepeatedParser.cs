namespace SourceUtils.Parsing
{
    /// <summary>Matches the inner parser one or more times (the <c>+</c> postfix modifier).</summary>
    public sealed class RepeatedParser : Parser
    {
        private readonly Parser _inner;

        public override bool FlattenHierarchy => true;
        public Parser Inner => _inner;

        public RepeatedParser( Parser inner )
        {
            _inner = inner;
        }

        protected override bool OnParse( ParseResult result, bool errorPass )
        {
            if ( !result.Read( _inner, errorPass ) ) return false;

            ParseResult peek;
            while ( (peek = result.Peek( _inner, errorPass )).Success && peek.Length > 0 )
            {
                result.Apply( peek, errorPass );
            }

            return true;
        }

        public override string ToString() => $"{_inner}+";
    }
}
