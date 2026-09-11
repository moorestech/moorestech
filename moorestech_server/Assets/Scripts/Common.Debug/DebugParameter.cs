using System.Collections.Generic;

namespace Common.Debug
{
    /// <summary>
    /// クライアントとサーバーの両方から参照する共有デバッグキー
    /// Shared debug keys referenced by both the client and the server
    /// </summary>
    public static class DebugParameterKeys
    {
        // ブロック設置を無料化する（建設コストを消費しない）
        // Make block placement free (do not consume construction cost)
        public const string FreeBlockPlacement = "FreeBlockPlacement";

        // マップオブジェクトを一撃で採掘する（ダメージ最大・クールダウン無視）
        // Mine map objects in one hit (max damage, no cooldown)
        public const string MapObjectSuperMine = "MapObjectSuperMine";
    }

    /// <summary>
    /// デバッグ設定の読み書き窓口。ファイルIOは初回・書き込み時・解決ディレクトリ変化時だけに閉じ、読み取りは毎フレーム呼んでもキャッシュから返す
    /// The debug parameter gateway; file IO is confined to the first access, writes and resolved-directory changes, so per-frame reads come from the cache
    /// </summary>
    public static class DebugParameters
    {
        // クライアント（メインスレッド）と内蔵サーバー（ゲーム更新スレッド）が同じキャッシュを触るため全アクセスを直列化する
        // The client (main thread) and the in-process server (game update thread) share this cache, so every access is serialized
        private static readonly object Gate = new();
        private static DebugParametersFileCache _cache;

        #region Public Accessors

        public static bool GetValueOrDefaultBool(string key, bool defaultValue = false)
        {
            lock (Gate) return Current().Bools.GetValueOrDefault(key, defaultValue);
        }

        public static bool TryGetBool(string key, out bool value)
        {
            lock (Gate) return Current().Bools.TryGetValue(key, out value);
        }

        public static void SaveBool(string key, bool value)
        {
            lock (Gate)
            {
                var cache = Current();
                cache.Bools[key] = value;
                cache.Save();
            }
        }

        public static bool RemoveBool(string key)
        {
            lock (Gate)
            {
                var cache = Current();
                var result = cache.Bools.Remove(key);
                cache.Save();
                return result;
            }
        }

        public static bool ExistsBool(string key)
        {
            lock (Gate) return Current().Bools.ContainsKey(key);
        }

        public static int GetValueOrDefaultInt(string key, int defaultValue)
        {
            lock (Gate) return Current().Ints.GetValueOrDefault(key, defaultValue);
        }

        public static bool TryGetInt(string key, out int value)
        {
            lock (Gate) return Current().Ints.TryGetValue(key, out value);
        }

        public static void SaveInt(string key, int value)
        {
            lock (Gate)
            {
                var cache = Current();
                cache.Ints[key] = value;
                cache.Save();
            }
        }

        public static bool RemoveInt(string key)
        {
            lock (Gate)
            {
                var cache = Current();
                var result = cache.Ints.Remove(key);
                cache.Save();
                return result;
            }
        }

        public static bool ExistsInt(string key)
        {
            lock (Gate) return Current().Ints.ContainsKey(key);
        }

        public static string GetValueOrDefaultString(string key, string defaultValue)
        {
            lock (Gate) return Current().Strings.GetValueOrDefault(key, defaultValue);
        }

        public static bool TryGetString(string key, out string value)
        {
            lock (Gate) return Current().Strings.TryGetValue(key, out value);
        }

        public static void SaveString(string key, string value)
        {
            lock (Gate)
            {
                var cache = Current();
                cache.Strings[key] = value;
                cache.Save();
            }
        }

        public static bool RemoveString(string key)
        {
            lock (Gate)
            {
                var cache = Current();
                var result = cache.Strings.Remove(key);
                cache.Save();
                return result;
            }
        }

        public static bool ExistsString(string key)
        {
            lock (Gate) return Current().Strings.ContainsKey(key);
        }

        #endregion

        // 設定ファイルをこのクラス以外（ディレクトリ複製）が書き換えた後に呼び、次のアクセスでファイルから読み直させる
        // Called after something other than this class (a directory copy) rewrote the files, so the next access reloads from disk
        internal static void InvalidateCache()
        {
            lock (Gate) _cache = null;
        }

        // 解決先はアクセス毎に確かめ、変わっていれば読み直す。静的初期化時に固定すると環境変数による切替が効かないため
        // Check the resolved directory on every access and reload on change; fixing it at static init would defeat the env-var switch
        private static DebugParametersFileCache Current()
        {
            var directory = DebugParametersCacheDirectory.Resolve();
            if (_cache == null || _cache.DirectoryPath != directory) _cache = DebugParametersFileCache.Load(directory);
            return _cache;
        }
    }
}
