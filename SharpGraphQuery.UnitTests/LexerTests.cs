using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Formatting;
using SharpGraphQl;
using Xunit;

namespace SharpGraphQuery.UnitTests
{
    public class LexerTests
    {
        [Fact]
        public void CanLexPunctuation()
        {
            Tokenize(" (   , ) { }").Select(x => x.TokenType).Should().Equal(
                TokenType.Whitespace,
                TokenType.OpenParen,
                TokenType.Whitespace,
                TokenType.Comma,
                TokenType.Whitespace,
                TokenType.CloseParen,
                TokenType.Whitespace,
                TokenType.OpenBrace,
                TokenType.Whitespace,
                TokenType.CloseBrace
            );
        }

        [Fact]
        public void CanLexNumbers()
        {
            Tokenize("7474 9292.2 \r\n9383E22")
                .Where(x => x.TokenType == TokenType.IntValue || x.TokenType == TokenType.FloatValue)
                .Should().BeEquivalentTo(
                    new Token(
                        new LexerPosition(1, 1), 
                        new LexerPosition(1, 4), 
                        TokenType.IntValue, 
                        7474
                    ),
                    new Token(
                        new LexerPosition(1, 6), 
                        new LexerPosition(1, 11), 
                        TokenType.FloatValue, 
                        9292.2
                    ),
                    new Token(
                        new LexerPosition(2, 1), 
                        new LexerPosition(2, 7), 
                        TokenType.FloatValue, 
                        9383E22
                    )
                );
        }

        [Fact]
        public void CanLexNames()
        {
            Tokenize(" _testMe, aKd, \nksk")
                .Where(x => x.TokenType == TokenType.Name)
                .Should().BeEquivalentTo(
                    new Token(
                        new LexerPosition(1, 2), 
                        new LexerPosition(1, 8), 
                        TokenType.Name, 
                        "_testMe"
                    ),
                    new Token(
                        new LexerPosition(1, 11), 
                        new LexerPosition(1, 13), 
                        TokenType.Name, 
                        "aKd"
                    ),
                    new Token(
                        new LexerPosition(2, 1), 
                        new LexerPosition(2, 3), 
                        TokenType.Name, 
                        "ksk"
                    )
                );
        }

        [Fact]
        public void CanLexSimpleString()
        {
            Tokenize("\"abcdefg\",").Should().BeEquivalentTo(
                new Token(
                    new LexerPosition(1, 1),
                    new LexerPosition(1, 9),
                    TokenType.StringValue,
                    "abcdefg"
                ),
                new Token(
                    new LexerPosition(1, 10),
                    new LexerPosition(1, 10),
                    TokenType.Comma,
                    null
                )
            );
        }

        [Fact]
        public void CanLexStringWithEscapeSequences()
        {
            Tokenize("\"ab\\nde\\\"fg\",").Should().BeEquivalentTo(
                new Token(
                    new LexerPosition(1, 1),
                    new LexerPosition(1, 12),
                    TokenType.StringValue,
                    "ab\nde\"fg"
                ),
                new Token(
                    new LexerPosition(1, 13),
                    new LexerPosition(1, 13),
                    TokenType.Comma,
                    null
                )
            );
        }

        [Fact]
        public void CanLexBlockString()
        {
            string blockString = "  \"\"\"\n    Hello,\n      World!\n\n    Yours,\n      GraphQL.\n  \"\"\",";
            this.Tokenize(blockString).Should().BeEquivalentTo(
                new Token(
                    new LexerPosition(1, 1),
                    new LexerPosition(1, 2),
                    TokenType.Whitespace,
                    null
                ),
                new Token(
                    new LexerPosition(1, 3),
                    new LexerPosition(1, 62),
                    TokenType.StringValue,
                    "Hello,\n  World!\n\nYours,\n  GraphQL."
                ),
                new Token(
                    new LexerPosition(1, 63),
                    new LexerPosition(1, 63),
                    TokenType.Comma,
                    null
                ));
        }

        [Fact]
        public void CanLexEmptyBlockString()
        {
            string blockString = "\"\"\"\"\"\"";
            this.Tokenize(blockString).Should().BeEquivalentTo(
                new Token(
                    new LexerPosition(1, 1),
                    new LexerPosition(1, 6),
                    TokenType.StringValue,
                    ""
                ));
        }

        [Fact]
        public void CanLexBlockStringWithEscape()
        {
            string blockString = "\"\"\"   a\r\n   b\r\n   c\r\n   \\\"\"\"\r\n\"\"\"";
            this.Tokenize(blockString).Should().BeEquivalentTo(
                new Token(
                    new LexerPosition(1, 1),
                    new LexerPosition(1, 33),
                    TokenType.StringValue,
                    "   a\nb\nc\n\"\"\""
                ));
        }

        public IToken[] Tokenize(string src)
        {
            var tokens = GraphQueryTokenReader.LexAll(src).ToArray();
            return tokens;
        }
    }

    public class TokenValueFormatter : IValueFormatter
    {
        public bool CanHandle(object value)
        {
            return value is Token;
        }

        public string Format(object value, FormattingContext context, FormatChild formatChild)
        {
            string newline = context.UseLineBreaks ? Environment.NewLine : "";
            string padding = new string('\t', context.Depth);

            var token = (IToken)value;
            return $"{newline}{padding} {token.TokenType} {token.Value} ({token.StartPosition.Line} {token.StartPosition.Column}) - ({token.EndPosition.Line} {token.EndPosition.Column})";
        }
    }
}
