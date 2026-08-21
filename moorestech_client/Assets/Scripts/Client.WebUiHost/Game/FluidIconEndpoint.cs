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
    /// 液体Guidの画像をPNG配信する
    /// Serves fluid images (keyed by Guid) as PNG
    /// </summary>
    public static class FluidIconEndpoint
    {
        public const string PathPrefix = "/api/fluid-icons/";
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
            if (!Guid.TryParse(guidText, out var fluidGuid))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // ゲーム起動完了前は FluidImageContainer が未生成のため 503
            // FluidImageContainer is not yet created before game startup; return 503
            if (ClientContext.FluidImageContainer == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            // 外部入力のGuidは非投げAPIで解決し、未知IDは404にする
            // Resolve the externally supplied Guid via the non-throwing API; unknown ids become 404
            var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(fluidGuid);
            if (fluidId == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (!_pngCache.TryGetValue(fluidGuid, out var cached))
            {
                var png = await EncodePngOnMainThread(fluidId.Value);
                if (png == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                var etag = "\"" + Convert.ToBase64String(MD5.Create().ComputeHash(png)) + "\"";
                cached = new CachedIcon(png, etag);
                _pngCache[fluidGuid] = cached;
            }

            // ETag再検証で内容変化に追随
            // Follow content changes via ETag revalidation
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

            async UniTask<byte[]> EncodePngOnMainThread(FluidId targetFluidId)
            {
                // EncodeToPNG は Unity API のためメインスレッドで実行する
                // EncodeToPNG is a Unity API and must run on the main thread
                await UniTask.SwitchToMainThread();
                var view = ClientContext.FluidImageContainer.GetItemView(targetFluidId);
                var png = view?.FluidTexture is Texture2D texture ? texture.EncodeToPNG() : null;
                await UniTask.SwitchToTaskPool();
                return png;
            }

            #endregion
        }
    }
}
