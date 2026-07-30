using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Client.Localization;
using Client.WebUiHost.Common;
using Microsoft.AspNetCore.Http;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// uGUI と同じ Localize 辞書を locale ごとに配信する。
    /// Serves the same Localize dictionary used by uGUI for each locale.
    /// </summary>
    public static class LocalizationDictionaryEndpoint
    {
        public const string PathPrefix = "/api/i18n/";

        public static async Task HandleAsync(HttpContext context, string path)
        {
            var locale = path.Substring(PathPrefix.Length);

            // locale は辞書コードだけを許可し、階層や空値をHTTP境界で拒否する
            // Accept only dictionary codes and reject nested or empty paths at the HTTP boundary
            if (string.IsNullOrEmpty(locale) || 0 <= locale.IndexOf("/", StringComparison.Ordinal))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("locale not found", CancellationToken.None);
                return;
            }

            // queryの期待世代を必須化し、欠落や不正値を外部境界で拒否する
            // Require the expected query generation and reject missing or malformed values at the boundary
            var revisionText = context.Request.Query["revision"].ToString();
            if (!long.TryParse(
                    revisionText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var expectedRevision) ||
                expectedRevision < 0)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("dictionary revision required", CancellationToken.None);
                return;
            }

            if (!Localize.TryGetDictionary(locale, expectedRevision, out var dictionary))
            {
                var revisionChanged = expectedRevision != Localize.GetDictionaryRevision();
                context.Response.StatusCode = revisionChanged
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(
                    revisionChanged ? "dictionary revision changed" : "locale not found",
                    CancellationToken.None);
                return;
            }

            // キャッシュ再検証を許可しつつ、Mod切替後の辞書を永続キャッシュさせない
            // Allow revalidation without persisting a dictionary across mod changes
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-cache";
            await context.Response.WriteAsync(WebUiJson.Serialize(dictionary), CancellationToken.None);
        }
    }
}
