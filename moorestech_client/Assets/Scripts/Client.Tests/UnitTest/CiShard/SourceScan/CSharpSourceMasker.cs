namespace Client.Tests.UnitTest.CiShard.SourceScan
{
    // コメント・文字列・文字リテラルを空白に塗り、改行と文字位置を保ったまま「コードだけ」を残す
    // トークン探索や波括弧の対応付けが、コメント中の語や文字列中の括弧に騙されないようにするための前処理
    // Paints comments, string literals and char literals with spaces, keeping newlines and offsets so only code remains
    // This lets token search and brace matching ignore words in comments and brackets inside strings
    public class CSharpSourceMasker
    {
        private readonly string _source;
        private readonly char[] _masked;

        private CSharpSourceMasker(string source)
        {
            _source = source;
            _masked = source.ToCharArray();
        }

        public static string Mask(string source)
        {
            var masker = new CSharpSourceMasker(source);
            masker.MaskAll();
            return new string(masker._masked);
        }

        private void MaskAll()
        {
            var index = 0;
            while (index < _source.Length)
            {
                var end = FindNonCodeEnd(index);
                if (end == index)
                {
                    index++;
                    continue;
                }
                for (var i = index; i < end; i++)
                {
                    if (_masked[i] != '\n' && _masked[i] != '\r') _masked[i] = ' ';
                }
                index = end;
            }
        }

        // indexから始まるコメント・リテラルの終端（排他）を返す。コードならindexをそのまま返す
        // Returns the exclusive end of a comment or literal starting at index, or index itself when it is code
        private int FindNonCodeEnd(int index)
        {
            if (StartsWith(index, "//")) return FindLineEnd(index);
            if (StartsWith(index, "/*"))
            {
                var close = _source.IndexOf("*/", index + 2, System.StringComparison.Ordinal);
                return close < 0 ? _source.Length : close + 2;
            }
            if (_source[index] == '\'') return FindCharLiteralEnd(index);
            return FindStringLiteralEnd(index);
        }

        // $・@の接頭辞を読み、通常・逐語・補間の各文字列の終端を返す。文字列でなければindexを返す
        // Reads the $/@ prefix and returns the end of a regular, verbatim or interpolated string, or index when not a string
        private int FindStringLiteralEnd(int index)
        {
            var quote = index;
            var isVerbatim = false;
            var isInterpolated = false;
            while (quote < _source.Length && (_source[quote] == '$' || _source[quote] == '@'))
            {
                isVerbatim |= _source[quote] == '@';
                isInterpolated |= _source[quote] == '$';
                quote++;
            }
            if (quote >= _source.Length || _source[quote] != '"' || 2 < quote - index) return index;

            var position = quote + 1;
            while (position < _source.Length)
            {
                var current = _source[position];
                if (isVerbatim && current == '"' && StartsWith(position, "\"\""))
                {
                    position += 2;
                    continue;
                }
                if (current == '"') return position + 1;
                if (!isVerbatim && current == '\\')
                {
                    position += 2;
                    continue;
                }
                if (!isVerbatim && current == '\n') return position;
                if (isInterpolated && StartsWith(position, "{{"))
                {
                    position += 2;
                    continue;
                }
                position = isInterpolated && current == '{' ? FindInterpolationHoleEnd(position) : position + 1;
            }
            return _source.Length;
        }

        // 補間穴の中はコードなので、入れ子の文字列を飛ばしつつ対応する閉じ括弧まで進む
        // An interpolation hole is code, so walk to its matching close brace while skipping nested literals
        private int FindInterpolationHoleEnd(int openBrace)
        {
            var depth = 0;
            var position = openBrace;
            while (position < _source.Length)
            {
                var literalEnd = position == openBrace ? position : FindNonCodeEnd(position);
                if (literalEnd != position)
                {
                    position = literalEnd;
                    continue;
                }
                if (_source[position] == '{') depth++;
                if (_source[position] == '}') depth--;
                position++;
                if (depth == 0) return position;
            }
            return _source.Length;
        }

        private int FindCharLiteralEnd(int index)
        {
            var position = index + 1;
            while (position < _source.Length && _source[position] != '\'' && _source[position] != '\n')
            {
                position += _source[position] == '\\' ? 2 : 1;
            }
            return position < _source.Length && _source[position] == '\'' ? position + 1 : position;
        }

        private int FindLineEnd(int index)
        {
            var newline = _source.IndexOf('\n', index);
            return newline < 0 ? _source.Length : newline;
        }

        private bool StartsWith(int index, string value)
        {
            return string.CompareOrdinal(_source, index, value, 0, value.Length) == 0;
        }
    }
}
