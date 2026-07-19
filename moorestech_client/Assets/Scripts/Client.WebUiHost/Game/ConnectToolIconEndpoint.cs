using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// 接続ツールGuidのアイコンをPNG配信する
    /// Serves connect-tool icons (keyed by Guid) as PNG
    /// </summary>
    public static class ConnectToolIconEndpoint
    {
        public const string PathPrefix = "/api/connect-tool-icons/";
        public const string PathSuffix = ".png";

        private static readonly ConcurrentDictionary<Guid, CachedIcon> _pngCache = new();

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
            var guidText = path.Substring(PathPrefix.Length, path.Length - PathPrefix.Length - PathSuffix.Length);
            if (!Guid.TryParse(guidText, out var connectToolGuid))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // ゲーム起動完了前は ConnectToolImageContainer が未生成のため 503
            // ConnectToolImageContainer is not yet created before game startup; return 503
            if (ClientContext.ConnectToolImageContainer == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            if (!_pngCache.TryGetValue(connectToolGuid, out var cached))
            {
                var png = await EncodePngOnMainThread();
                if (png == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                var etag = "\"" + Convert.ToBase64String(MD5.Create().ComputeHash(png)) + "\"";
                cached = new CachedIcon(png, etag);
                _pngCache[connectToolGuid] = cached;
            }

            // アイコン内容の変化に ETag 再検証で追随する
            // Follow icon-content changes via ETag revalidation
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
                var view = ClientContext.ConnectToolImageContainer.GetConnectToolView(connectToolGuid);
                var png = view?.ItemTexture is Texture2D texture ? texture.EncodeToPNG() : null;
                await UniTask.SwitchToTaskPool();
                return png;
            }

            #endregion
        }
    }
}
