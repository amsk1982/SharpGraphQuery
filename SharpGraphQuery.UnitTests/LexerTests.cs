using System;
using System.IO;
using SharpGraphQl;
using System.Linq;
using Xunit;
using FluentAssertions;
using FluentAssertions.Formatting;

namespace UnitTests
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
