using System.Collections.Concurrent;
using System.Threading.Tasks;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// GET /api/icons/{itemId}.png でアイテムアイコンを PNG 配信する
    /// Serves item icons as PNG at GET /api/icons/{itemId}.png
    /// </summary>
    public static class ItemIconEndpoint
    {
        public const string PathPrefix = "/api/icons/";
        public const string PathSuffix = ".png";

        private static readonly ConcurrentDictionary<int, byte[]> _pngCache = new();

        public static void ClearCache()
        {
            _pngCache.Clear();
        }

        public static async Task HandleAsync(HttpContext context, string path)
        {
            // パスから itemId を取り出す。不正なら 404
            // Extract itemId from the path; 404 if malformed
            var idText = path.Substring(PathPrefix.Length, path.Length - PathPrefix.Length - PathSuffix.Length);
            if (!int.TryParse(idText, out var itemIdValue))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // ゲーム起動完了前は ItemImageContainer が未生成のため 503
            // ItemImageContainer is not yet created before game startup; return 503
            if (ClientContext.ItemImageContainer == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            if (!_pngCache.TryGetValue(itemIdValue, out var png))
            {
                png = await EncodePngOnMainThread(itemIdValue);
                if (png == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                _pngCache[itemIdValue] = png;
            }

            context.Response.ContentType = "image/png";
            context.Response.Headers["Cache-Control"] = "public, max-age=86400";
            await context.Response.Body.WriteAsync(png, 0, png.Length);
        }

        private static async UniTask<byte[]> EncodePngOnMainThread(int itemIdValue)
        {
            // EncodeToPNG は Unity API のためメインスレッドで実行する
            // EncodeToPNG is a Unity API and must run on the main thread
            await UniTask.SwitchToMainThread();
            var view = ClientContext.ItemImageContainer.GetItemView(new ItemId(itemIdValue));
            var png = view?.ItemTexture is Texture2D texture ? texture.EncodeToPNG() : null;
            await UniTask.SwitchToTaskPool();
            return png;
        }
    }
}
