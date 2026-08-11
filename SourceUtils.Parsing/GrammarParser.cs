using System.Text.RegularExpressions;

namespace SourceUtils.Parsing
{
    /// <summary>
    /// Hand-authored bootstrap parser for the text grammar DSL consumed by
    /// <see cref="GrammarBuilder.FromString"/>. Defines rule syntax like:
    /// <code>
    /// Name = "token" | /regex/i | OtherRule ("," OtherRule)*;
    /// ignore Whitespace { ... }
    /// </code>
    /// </summary>
    public sealed class GrammarParser : CustomParser
    {
        public NamedParser StatementBlock;
        public NamedParser Statement;
        public NamedParser SpecialBlock;
        public NamedParser SpecialBlockHeader;
        public NamedParser SpecialBlockType;
        public NamedParser Definition;
        public NamedParser Branch;
        public NamedParser Concat;
        public NamedParser Modifier;
        public NamedParser ModifierPrefix;
        public NamedParser ModifierPostfix;
        public NamedParser Term;
        public NamedParser NonTerminal;
        public NamedParser String;
        public NamedParser StringValueSingle;
        public NamedParser StringValueDouble;
        public NamedParser Regex;
        public NamedParser RegexValue;
        public NamedParser RegexOptions;
        public NamedParser RegexOption;

        protected override Parser OnDefine()
        {
            Parser ignore = "ignore";
            Parser noignore = "noignore";
            Parser collapse = "collapse";

            // Enum member access must be fully qualified below: the fields
            // named Regex/RegexOptions on this class shadow the identically
            // named framework type/enum for plain member-access expressions
            // (object-creation "new Regex(...)" is unaffected, since `new`
            // resolves its type name without considering instance members).
            this[NonTerminal] = new Regex( @"[a-z_][a-z0-9_]*(\.[a-z_][a-z0-9_]*)*",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant );
            this[String] = "\"" + StringValueDouble + "\"" | "'" + StringValueSingle + "'";
            this[StringValueSingle] = new Regex( @"(\\[\\rnt']|[^\\'])*" );
            this[StringValueDouble] = new Regex( @"(\\[\\""rnt]|[^\\""])*" );
            this[Regex] = "/" + RegexValue + "/" + RegexOptions;
            this[RegexValue] = new Regex( @"(\\.|\[[^\]]+\]|[^\\[/])+" );
            this[RegexOptions] = RegexOption.Repeated.Optional;
            this[RegexOption] = "i";

            using ( AllowWhitespace( new Regex( @"\s+|//[^\n]*(\n|$)|/\*([^*]|\*[^/])*\*/" ) ) )
            {
                this[StatementBlock] = Statement.Repeated;
                this[Statement] = SpecialBlock | Definition;
                this[SpecialBlock] = SpecialBlockHeader + "{" + StatementBlock + "}";
                this[SpecialBlockHeader] = SpecialBlockType + ("," + SpecialBlockType).Repeated.Optional;
                this[SpecialBlockType] = ignore + Branch | noignore | collapse;
                this[Definition] = !(ignore | noignore | collapse) + NonTerminal + ("=" + Branch).Optional +
                                   (";" | "{" + StatementBlock + "}");
                this[Branch] = Concat + ("|" + Concat).Repeated.Optional;
                this[Concat] = Modifier.Repeated;
                this[Modifier] = ModifierPrefix.Optional + Term + ModifierPostfix.Optional;
                this[ModifierPrefix] = (Parser) "!" | "$";
                this[ModifierPostfix] = (Parser) "?" | "*" | "+";
                this[Term] = String | Regex | NonTerminal | "(" + Branch + ")";
            }

            return StatementBlock + EndOfInput;
        }
    }
}
