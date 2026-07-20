using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
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

        private static readonly ConcurrentDictionary<int, CachedIcon> _pngCache = new();

        // PNG とその ETag（内容ハッシュ）をペアで保持する
        // Holds a PNG together with its content-hash ETag
        private readonly struct CachedIcon
        {
            public readonly byte[] Png;
            public readonly string ETag;

            public CachedIcon(byte[] png, string etag)
            {
                Png = png;
                ETag = etag;
            }
        }

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

            if (!_pngCache.TryGetValue(itemIdValue, out var cached))
            {
                var png = await EncodePngOnMainThread();
                if (png == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                var etag = "\"" + Convert.ToBase64String(MD5.Create().ComputeHash(png)) + "\"";
                cached = new CachedIcon(png, etag);
                _pngCache[itemIdValue] = cached;
            }

            // ItemId は非永続のため長期キャッシュせず、ETag 再検証で内容変化に追随する
            // ItemIds are not persistent, so rely on ETag revalidation instead of long-lived caching
            context.Response.Headers["ETag"] = cached.ETag;
            context.Response.Headers["Cache-Control"] = "no-cache";

            if (context.Request.Headers["If-None-Match"].ToString() == cached.ETag)
            {
                context.Response.StatusCode = 304;
                return;
            }

            context.Response.ContentType = "image/png";
            await context.Response.Body.WriteAsync(cached.Png, 0, cached.Png.Length);

            #region Internal

            async UniTask<byte[]> EncodePngOnMainThread()
            {
                // EncodeToPNG は Unity API のためメインスレッドで実行する
                // EncodeToPNG is a Unity API and must run on the main thread
                await UniTask.SwitchToMainThread();
                var view = ClientContext.ItemImageContainer.GetItemView(new ItemId(itemIdValue));
                var png = view?.ItemTexture is Texture2D texture ? texture.EncodeToPNG() : null;
                await UniTask.SwitchToTaskPool();
                return png;
            }

            #endregion
        }
    }
}
