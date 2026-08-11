namespace SourceUtils.Parsing
{
    /// <summary>
    /// Wraps a parser so that once it starts matching, failure is reported at
    /// its own error location instead of bubbling up as a plain branch miss
    /// (the <c>$</c> prefix modifier).
    /// </summary>
    public sealed class StrictParser : Parser
    {
        private readonly Parser _inner;

        public override bool FlattenHierarchy => true;
        public Parser Inner => _inner;

        public StrictParser( Parser inner )
        {
            _inner = inner;
        }

        protected override bool OnParse( ParseResult result, bool errorPass )
        {
            var next = result.Peek( _inner, errorPass );
            var success = next.Success;
            if ( success ) result.Apply( next, errorPass );
            else result.Error( next, errorPass, false );
            return success;
        }

        public override string ToString() => $"${_inner}";
    }
}
