namespace SourceUtils.Parsing
{
    /// <summary>Succeeds (matching nothing) only if the inner parser fails (the <c>!</c> prefix modifier).</summary>
    public sealed class NotParser : Parser
    {
        private readonly Parser _inner;

        public Parser Inner => _inner;
        public override bool OmitFromResult => true;

        public NotParser( Parser inner )
        {
            _inner = inner;
        }

        protected override bool OnParse( ParseResult result, bool errorPass ) => !result.Peek( _inner, errorPass ).Success;

        public override string ToString() => $"!{_inner}";
    }
}
