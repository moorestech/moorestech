using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Microsoft.AspNetCore.Http;
using Mooresmaster.Localization.Generated;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// 選択可能な言語コードと表示名を配信する
    /// Serves selectable locale codes and display names
    /// </summary>
    public static class LocalizationLanguagesEndpoint
    {
        public const string Path = "/api/i18n-languages";

        public static async Task HandleAsync(HttpContext context)
        {
            // generator埋め込みメタだけをWeb公開形へ射影する
            // Project only generator-embedded metadata into the public web shape
            var languages = LanguageCatalog.Languages
                .Select(language => new LanguageDto
                {
                    Code = language.Code,
                    DisplayName = language.DisplayName,
                })
                .ToList();

            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(
                WebUiJson.Serialize(languages),
                CancellationToken.None);
        }
    }

    public class LanguageDto
    {
        public string Code;
        public string DisplayName;
    }
}
