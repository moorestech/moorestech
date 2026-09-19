using System.Net;
using System.Text.RegularExpressions;

namespace Client.PlaytestReceiver.Http.Responses
{
    // R2（S3互換）のエラー本文 <Error><Code>…</Code><Message>…</Message></Error> から Code と Message を読む
    // Reads Code and Message from an R2 (S3-compatible) error body <Error><Code>…</Code><Message>…</Message></Error>
    public static class R2ErrorBody
    {
        // 本文は外部入力。XMLパーサの例外境界を持ち込まず、要る2要素だけを正規表現で拾う
        // The body is external input; rather than bringing in an XML parser's exception boundary, only the two needed elements are picked by regex
        private static readonly Regex CodePattern = new(@"<Code>([^<]*)</Code>");
        private static readonly Regex MessagePattern = new(@"<Message>([^<]*)</Message>");

        public static bool TryRead(string body, out string code, out string message)
        {
            var codeMatch = CodePattern.Match(body);
            var messageMatch = MessagePattern.Match(body);
            code = codeMatch.Success ? WebUtility.HtmlDecode(codeMatch.Groups[1].Value) : "";
            message = messageMatch.Success ? WebUtility.HtmlDecode(messageMatch.Groups[1].Value) : "";
            return codeMatch.Success;
        }
    }
}
