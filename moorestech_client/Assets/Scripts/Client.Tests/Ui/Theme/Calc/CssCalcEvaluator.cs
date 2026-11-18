// moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/Calc/CssCalcEvaluator.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Client.Tests.Ui.Theme.Calc
{
    /// <summary>
    /// USS値でcalcを評価（L2検算専用）
    /// Evaluates calc from USS values (L2 recheck only)
    /// </summary>
    public sealed class CssCalcEvaluator
    {
        private const float RemPx = 16f;
        private static readonly Regex TokenPattern = new(@"\s*(var\(--[\w-]+\)|\d*\.?\d+(?:px|rem)?|[-+*/()])\s*");
        private static readonly Regex NumberLiteral = new(@"^(-?\d*\.?\d+)(px|rem)?$");
        private readonly IReadOnlyDictionary<string, string> _declaredValues;
        private List<string> _tokens;
        private int _position;

        public CssCalcEvaluator(IReadOnlyDictionary<string, string> declaredValues)
        {
            _declaredValues = declaredValues;
        }

        public float Evaluate(string expression)
        {
            _tokens = Tokenize(expression);
            _position = 0;
            var value = ParseSum();
            if (_position != _tokens.Count) throw new FormatException($"unexpected '{_tokens[_position]}' in '{expression}'");
            return value;

            #region Internal

            List<string> Tokenize(string expression)
            {
                var tokens = new List<string>();
                var position = 0;
                while (position < expression.Length)
                {
                    var match = TokenPattern.Match(expression, position);
                    if (!match.Success || match.Index != position) throw new FormatException($"cannot read '{expression.Substring(position)}'");
                    tokens.Add(match.Groups[1].Value);
                    position += match.Length;
                }
                return tokens;
            }

            #endregion
        }

        private float ParseSum()
        {
            var value = ParseProduct();
            while (Peek() == "+" || Peek() == "-")
            {
                var op = _tokens[_position++];
                var right = ParseProduct();
                value = op == "+" ? value + right : value - right;
            }
            return value;
        }

        private float ParseProduct()
        {
            var value = ParseFactor();
            while (Peek() == "*" || Peek() == "/")
            {
                var op = _tokens[_position++];
                var right = ParseFactor();
                value = op == "*" ? value * right : value / right;
            }
            return value;
        }

        private float ParseFactor()
        {
            var token = _tokens[_position++];
            if (token == "-") return -ParseFactor();
            if (token == "(")
            {
                var inner = ParseSum();
                if (Peek() != ")") throw new FormatException("missing ')'");
                _position++;
                return inner;
            }
            if (token.StartsWith("var("))
            {
                // 参照先は宣言値（px か単位なし）だけを受ける
                // A reference accepts only a declared px or unitless value
                var name = token.Substring(4, token.Length - 5);
                if (!_declaredValues.TryGetValue(name, out var declared)) throw new KeyNotFoundException($"{name} is not declared in the token USS");
                return ParseLiteral(declared.Trim());
            }
            return ParseLiteral(token);
        }

        private string Peek()
        {
            return _position < _tokens.Count ? _tokens[_position] : null;
        }

        private static float ParseLiteral(string text)
        {
            var match = NumberLiteral.Match(text);
            if (!match.Success) throw new FormatException($"'{text}' is not a px, rem or unitless number");
            var number = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return match.Groups[2].Value == "rem" ? number * RemPx : number;
        }
    }
}
