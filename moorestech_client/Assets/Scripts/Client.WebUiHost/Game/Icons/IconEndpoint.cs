using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// Item/Block/TrainCar/ConnectTool/Fluid アイコン配信の共通実装（キャッシュ・ETag・304・PNGエンコード）
    /// Shared delivery logic for Item/Block/TrainCar/ConnectTool/Fluid icons (cache, ETag, 304, PNG encoding)
    /// </summary>
    public static class IconEndpoint
    {
        public const string PathSuffix = ".png";

        private static readonly IIconTextureSource[] _sources =
        {
            new ItemIconSource(),
            new BlockIconSource(),
            new TrainCarIconSource(),
            new ConnectToolIconSource(),
            new FluidIconSource()
        };

        // PathPrefix + キー文字列をキーにして source 横断で1本にまとめる
        // Keyed by PathPrefix + key text, shared across all sources in one dictionary
        private static readonly ConcurrentDictionary<string, CachedIcon> _pngCache = new();

        // PNG とその ETag（内容ハッシュ）をペアで保持する。Png が null なら既知の欠落を表す負キャッシュ
        // Holds a PNG together with its content-hash ETag; a null Png is a negative cache entry for a known miss
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

        public static void ClearAllCaches()
        {
            _pngCache.Clear();
        }

        /// <summary>
        /// アイコン経路なら配信まで行い true を返す。判定と実行を1本にして呼び違いを起こせなくする
        /// Serves the icon and returns true when the path is an icon route; routing and delivery are one call so they cannot be mispaired
        /// </summary>
        public static async Task<bool> TryHandleAsync(HttpContext context, string path)
        {
            if (!TryGetSource(path, out var source)) return false;

            await HandleAsync(context, path, source);
            return true;
        }

        private static bool TryGetSource(string path, out IIconTextureSource source)
        {
            source = null;
            if (!path.EndsWith(PathSuffix, StringComparison.Ordinal)) return false;

            foreach (var candidate in _sources)
            {
                if (!path.StartsWith(candidate.PathPrefix, StringComparison.Ordinal)) continue;
                source = candidate;
                return true;
            }
            return false;
        }

        private static async Task HandleAsync(HttpContext context, string path, IIconTextureSource source)
        {
            var ct = context.RequestAborted;
            var keyText = path.Substring(source.PathPrefix.Length, path.Length - source.PathPrefix.Length - PathSuffix.Length);

            // ID書式として解釈できないキーは解決も負キャッシュもせず即404（任意文字列でキャッシュが際限なく増えるのを防ぐ）
            // A key that is not a valid id gets an immediate 404 with no resolve and no negative cache entry, so arbitrary strings cannot grow the cache
            if (!source.IsValidKey(keyText))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // ゲーム起動完了前は対応する ImageContainer が未生成のため 503
            // Return 503 while game startup has not yet created the backing ImageContainer
            if (!source.IsReady)
            {
                context.Response.StatusCode = 503;
                return;
            }

            var cacheKey = source.PathPrefix + keyText;
            if (!_pngCache.TryGetValue(cacheKey, out var cached))
            {
                var png = await EncodePngOnMainThread();
                var etag = png == null ? null : "\"" + Convert.ToBase64String(ComputeMd5Hash(png)) + "\"";
                cached = new CachedIcon(png, etag);
                _pngCache[cacheKey] = cached;
            }

            // 欠落は負キャッシュ済みなのでメインスレッド往復なしで即 404
            // A miss is already negative-cached, so return 404 without another main-thread round trip
            if (cached.Png == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            // キーは非永続のため長期キャッシュせず、ETag 再検証で内容変化に追随する
            // Keys are not persistent, so rely on ETag revalidation instead of long-lived caching
            context.Response.Headers["ETag"] = cached.ETag;
            context.Response.Headers["Cache-Control"] = "no-cache";

            if (context.Request.Headers["If-None-Match"].ToString() == cached.ETag)
            {
                context.Response.StatusCode = 304;
                return;
            }

            context.Response.ContentType = "image/png";
            await context.Response.Body.WriteAsync(cached.Png, 0, cached.Png.Length, ct);

            #region Internal

            async UniTask<byte[]> EncodePngOnMainThread()
            {
                // EncodeToPNG は Unity API のためメインスレッドで実行する
                // EncodeToPNG is a Unity API and must run on the main thread
                await UniTask.SwitchToMainThread(ct);
                var texture = source.ResolveOrNull(keyText);
                var png = texture != null ? texture.EncodeToPNG() : null;
                await UniTask.SwitchToTaskPool();
                return png;
            }

            byte[] ComputeMd5Hash(byte[] png)
            {
                using var md5 = MD5.Create();
                return md5.ComputeHash(png);
            }

            #endregion
        }
    }
}
