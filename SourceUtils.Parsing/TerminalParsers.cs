using System.Text.RegularExpressions;

namespace SourceUtils.Parsing
{
    public sealed class EmptyParser : Parser
    {
        public static EmptyParser Instance { get; } = new EmptyParser();

        public override bool OmitFromResult => true;

        protected override bool OnParse( ParseResult result, bool errorPass ) => true;
    }

    public sealed class TokenParser : Parser
    {
        public string Token { get; }
        public override bool OmitFromResult => true;

        public TokenParser( string token )
        {
            Token = token;
        }

        protected override bool OnParse( ParseResult result, bool errorPass ) =>
            result.Read( Token ) || result.Error( ParseError.ExpectedToken, errorPass ? $"'{Token}'" : "" );

        public override string ToString() => $"\"{Token}\"";
    }

    public sealed class RegexParser : Parser
    {
        public Regex Regex { get; }
        public override bool OmitFromResult => true;

        public RegexParser( Regex regex )
        {
            Regex = regex;
        }

        protected override bool OnParse( ParseResult result, bool errorPass ) =>
            result.Read( Regex ) || result.Error( ParseError.ExpectedToken, errorPass ? $"/{Regex}/" : "" );

        public override string ToString() => $"/{Regex}/";
    }
}
