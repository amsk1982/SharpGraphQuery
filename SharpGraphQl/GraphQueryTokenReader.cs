using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpGraphQl
{
    public interface IToken
    {
        LexerPosition StartPosition { get; }
        LexerPosition EndPosition { get; }
        TokenType TokenType { get; }
        string StringValue { get; }
        int? IntValue { get; }
        double? DoubleValue { get; }
        object Value { get; }
    }

    public class Token : IToken
    {
        public LexerPosition StartPosition { get; }
        public LexerPosition EndPosition { get; }
        public TokenType TokenType { get; }
        public string StringValue { get; }
        public int? IntValue { get; }
        public double? DoubleValue { get; }
        public object Value { get; }

        public Token() {}

        public Token(LexerPosition start, LexerPosition end, TokenType type, object value)
        {
            StartPosition = start;
            EndPosition = end;
            TokenType = type;
            Value = value;
            if (value is string str)
                StringValue = str;
            if (value is int i)
                IntValue = i;
            if (value is double d)
                DoubleValue = d;
        }

        public Token(IToken other)
        {
            StartPosition = other.StartPosition;
            EndPosition = other.EndPosition;
            TokenType = other.TokenType;
            StringValue = other.StringValue;
            IntValue = other.IntValue;
            DoubleValue = other.DoubleValue;
            Value = other.Value;
        }
    }

    public class GraphQueryTokenReader : IToken
    {
        private readonly string _text;

        private int _currentPosition;

        private string _currentStringValue;
        private int? _currentIntValue;
        private double? _currentDoubleValue;
        private TokenType _currentTokenType = TokenType.None;

        private LexerPosition CurrentTokenStart { get; set; }
        private LexerPosition CurrentTokenEnd { get; set; }

        private int _line = 1;
        private int _column = 1;

        private LexerPosition Position => new LexerPosition(_line, _column);

        public GraphQueryTokenReader(string text)
        {
            _text = text;
        }

        public static IEnumerable<IToken> LexAll(string src)
        {
            GraphQueryTokenReader rdr = new GraphQueryTokenReader(src);
            while (rdr.Next())
            {
                yield return rdr.ToToken();
            }
        }

        public IToken ToToken()
        {
            return new Token(this);
        }

        public bool Next()
        {
            _currentStringValue = null;
            _currentIntValue = null;
            _currentDoubleValue = null;

            if (_currentPosition >= _text.Length)
                return false;

            char c = _text[_currentPosition];
            switch (c)
            {
                case ' ':
                case '\t':
                    ReadWhitespace();
                    return true;

                case '\r':
                    ReadLineFeed();
                    return true;

                case '\n':
                    ReadNewline();
                    return true;

                case '#':
                    ReadComment();
                    return true;

                case '.':
                    ReadEllipsis();
                    return true;

                case '_':
                case 'A': case 'B': case 'C': case 'D': case 'E':
                case 'F': case 'G': case 'H': case 'I': case 'J':
                case 'K': case 'L': case 'M': case 'N': case 'O':
                case 'P': case 'Q': case 'R': case 'S': case 'T':
                case 'U': case 'V': case 'W': case 'X': case 'Y':
                case 'Z':
                case 'a': case 'b': case 'c': case 'd': case 'e':
                case 'f': case 'g': case 'h': case 'i': case 'j':
                case 'k': case 'l': case 'm': case 'n': case 'o':
                case 'p': case 'q': case 'r': case 's': case 't':
                case 'u': case 'v': case 'w': case 'x': case 'y':
                case 'z':
                    ReadName();
                    return true;

                case '0': case '1': case '2': case '3': case '4':
                case '5': case '6': case '7': case '8': case '9':
                case '-':
                    ReadNumber();
                    return true;

                case ',':
                    ReadSingleChar(',', TokenType.Comma);
                    return true;

                case '!':
                    ReadSingleChar('!', TokenType.Bang);
                    return true;

                case '$':
                    ReadSingleChar('$', TokenType.Dollar);
                    return true;

                case '(':
                    ReadSingleChar('(', TokenType.OpenParen);
                    return true;

                case ')':
                    ReadSingleChar(')', TokenType.CloseParen);
                    return true;

                case ':':
                    ReadSingleChar(':', TokenType.Colon);
                    return true;

                case '=':
                    ReadSingleChar('=', TokenType.Eq);
                    return true;

                case '@':
                    ReadSingleChar('@', TokenType.AtSign);
                    return true;

                case '[':
                    ReadSingleChar('[', TokenType.OpenBracket);
                    return true;

                case ']':
                    ReadSingleChar(']', TokenType.CloseBracket);
                    return true;

                case '{':
                    ReadSingleChar('{', TokenType.OpenBrace);
                    return true;

                case '}':
                    ReadSingleChar('}', TokenType.CloseBrace);
                    return true;

                case '|':
                    ReadSingleChar('|', TokenType.Pipe);
                    return true;

                case '"':
                    ReadString();
                    return true;

                default:
                    throw new GraphQlLexerException("Unknown character in query: " + c, Position);
            }
        }

        private void ReadName()
        {
            int length = _text.Length;
            int position = _currentPosition;
            CurrentTokenStart = Position;

            while((++position) < length)
            {
                char c = _text[position];
                if (!(
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_'))
                    break;
            }

            int diff = (position - _currentPosition);
            _currentStringValue = _text.Substring(_currentPosition, diff);
            _currentPosition = position;
            _column += diff;
            CurrentTokenEnd = new LexerPosition(_line, _column - 1);

            _currentTokenType = TokenType.Name;
        }

        private void ReadString()
        {
            int position = _currentPosition;
            CurrentTokenStart = Position;

            while(++position < _text.Length)
            {

            }

            throw new GraphQlLexerException("Missing end of string", new LexerPosition(_line, _column + (position - _currentPosition)));
        }

        private void ReadNumber()
        {
            CurrentTokenStart = Position;

            int position = _currentPosition;
            int start = position;
            ReadInt(ref position);

            if (position >= _text.Length)
            {
                SetupIntValue(start, position);
                return;
            }

            char c = _text[position];

            bool isDouble = false;
            if (c == '.')
            {
                isDouble = true;
                ReadFraction(ref position);
            }

            if (c == 'E' || c == 'e')
            {
                isDouble = true;
                ReadExponent(ref position);
            }

            if (isDouble)
                SetupDoubleValue(start, position);
            else
                SetupIntValue(start, position);

            _column += (position - _currentPosition);
            _currentPosition = position;
            CurrentTokenEnd = new LexerPosition(_line, _column - 1);
        }

        private void SetupIntValue(int start, int position)
        {
            int value = int.Parse(_text.Substring(start, position - start));
            _currentTokenType = TokenType.IntValue;
            _currentIntValue = value;
        }

        private void SetupDoubleValue(int start, int position)
        {
            double value = double.Parse(_text.Substring(start, position - start));
            _currentTokenType = TokenType.FloatValue;
            _currentDoubleValue = value;
        }

        private void ReadInt(ref int position)
        {
            bool negative = false;
            if (_text[position] == '-')
            {
                negative = true;
                ++position;
                if (position >= _text.Length || !IsDigit(_text[position]))
                {
                    throw new GraphQlLexerException("Invalid token. Expecting a digit", new LexerPosition(_line, _column + 1));
                }
            }
            if (_text[position] == '0')
            {
                ++position;
                if (position < _text.Length)
                {
                    char c = _text[position];
                    if (!IsDigit(c))
                    {
                        return;
                    }
                    if (c == '0')
                    {
                        throw new GraphQlLexerException("Invalid token. Expecting a non-zero digit but got '0'", 
                            new LexerPosition(_line, _column + (negative ? 2 : 1)));
                    }
                }
            }

            while(position++ < _text.Length)
            {
                char c = _text[position];
                if (!IsDigit(c))
                    break;
            }
        }

        private void ReadFraction(ref int position)
        {
            ++position;
            if (position >= _text.Length || !IsDigit(_text[position]))
                throw new GraphQlLexerException("Missing fractional digits", Position);

            while(++position < _text.Length)
            {
                char c = _text[position];
                if (!IsDigit(c))
                    break;
            }
        }

        private void ReadExponent(ref int position)
        {
            ++position;
            if (position >= _text.Length)
            {
                throw new GraphQlLexerException("Invalid exponent", Position);
            }

            char c = _text[position];
            if (c == '-' || c == '+')
            {
                ++position;
                if (position >= _text.Length)
                {
                    throw new GraphQlLexerException("Invalid exponent", Position);
                }
            }

            c = _text[position];
            if (!IsDigit(c))
            {
                throw new GraphQlLexerException("Invalid exponent", Position);
            }

            while(++position < _text.Length)
            {
                if (!IsDigit(_text[position]))
                    break;
            }
        }

        private bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private void ReadWhitespace()
        {
            int length = _text.Length;
            int position = _currentPosition;
            CurrentTokenStart = Position;

            while((++position) < length)
            {
                char c = _text[position];
                if (c != ' ' && c != '\t')
                    break;
            }


            int diff = (position - _currentPosition);
            _currentPosition = position;
            _column += diff;
            CurrentTokenEnd = new LexerPosition(_line, _column - 1);

            _currentStringValue = null;
            _currentTokenType = TokenType.Whitespace;
        }

        private void ReadNewline()
        {
            _currentStringValue = null;
            _currentTokenType = TokenType.LineTerminator;
            CurrentTokenStart = Position;
            CurrentTokenEnd = Position;
            _currentPosition += 1;
            _column = 1;
            _line += 1;
        }

        private void ReadLineFeed()
        {
            _currentStringValue = null;
            _currentTokenType = TokenType.LineTerminator;
            CurrentTokenStart = Position;
            _currentPosition += 1;

            if (_currentPosition < _text.Length && _text[_currentPosition] == '\n')
                _currentPosition += 1;

            CurrentTokenEnd = new LexerPosition(_line, _column);
            _column = 1;
            _line += 1;
        }

        private void ReadComment()
        {
            int position = _currentPosition;
            while((++position) < _text.Length)
            {
                if (_text[position] == '\n' || _text[position] == '\r')
                    break;
            }

            int diff = (position - _currentPosition);
            _currentPosition = position;
            _column += diff;
            CurrentTokenEnd = new LexerPosition(_line, _column - 1);

            _currentStringValue = null;
            _currentTokenType = TokenType.Whitespace;
        }

        private void ReadEllipsis()
        {
            int lastEllipsisPos = _currentPosition + 2;
            if (lastEllipsisPos >= _text.Length)
            {
                throw new GraphQlLexerException("Invalid token, expecting '.'", Position);
            }

            if (_text[_currentPosition + 1] != '.')
            {
                _currentPosition += 1;
                throw new GraphQlLexerException("Invalid token, expecting '.'", Position);
            }

            if (_text[_currentPosition + 2] != '.')
            {
                _currentPosition += 2;
                throw new GraphQlLexerException("Invalid token, expecting '.'", Position);
            }

            CurrentTokenStart = new LexerPosition(_line, _column - 1);
            _currentPosition += 3;
            _column += 4;
            CurrentTokenEnd = Position;
            _currentStringValue = null;
            _currentTokenType = TokenType.Ellipsis;
        }

        private void ReadSingleChar(char c, TokenType tokenType)
        {
            CurrentTokenStart = Position;
            CurrentTokenEnd = Position;
            _column += 1;
            _currentTokenType = tokenType;
            _currentPosition += 1;
            _currentStringValue = null;
        }

        public LexerPosition StartPosition => CurrentTokenStart;
        public LexerPosition EndPosition => CurrentTokenEnd;
        public TokenType TokenType => _currentTokenType;
        public string StringValue => _currentStringValue;
        public int? IntValue => _currentIntValue;
        public double? DoubleValue => _currentDoubleValue;
        public object Value => ((object)StringValue) ?? ((object)IntValue) ?? ((object)DoubleValue);
    }

    public class GraphQlLexerException : Exception
    {
        public GraphQlLexerException(string message, LexerPosition lexerPosition)
        {

        }
    }

    [DebuggerDisplay("{Line}, {Column}")]
    public struct LexerPosition
    {
        public int Line { get; }
        public int Column { get; }

        public LexerPosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"({Line}, {Column})";
        }
    }

    public enum TokenType
    {
        None,
        Whitespace,
        LineTerminator,
        Comma,
        Bang,
        Dollar,
        OpenParen,
        CloseParen,
        Colon,
        Eq,
        AtSign,
        OpenBracket,
        CloseBracket,
        OpenBrace,
        CloseBrace,
        Pipe,
        Ellipsis,
        Name,
        IntValue,
        FloatValue,
        StringValue
    }
}
