using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using FluentAssertions.Primitives;
using SharpGraphQl;
using Xunit;

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
                TokenType.CloseParen,
                TokenType.Whitespace,
                TokenType.OpenBrace,
                TokenType.Whitespace,
                TokenType.CloseBrace
            );
        }

        public IToken[] Tokenize(string src)
        {
            return null;
        }
    }

    //public static class TokenAssertionHelpers
    //{
    //    public static TokenAssertions Should(this IToken token)
    //    {
    //        return new TokenAssertions(token);
    //    }
    //}

    //public class TokenAssertions : ReferenceTypeAssertions<IToken, TokenAssertions>
    //{
    //    public TokenAssertions(IToken token)
    //    {
    //        Subject = token;
    //    }

    //    protected override string Identifier => "Token";

    //    public AndConstraint<TokenAssertions> HaveType(TokenType tokenType)
    //    {

    //    }
    //}
}
