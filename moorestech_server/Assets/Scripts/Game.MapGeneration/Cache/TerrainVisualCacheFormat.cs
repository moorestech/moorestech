using System;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     見た目キャッシュファイルの並びを定義する唯一の場所。書き手と読み手はここの定数だけを共有する
    ///     The single definition of the visual cache file's layout; the writer and reader share only these constants
    /// </summary>
    public static class TerrainVisualCacheFormat
    {
        // "MTVC" = Moorestech Terrain Visual Cache。別形式のファイルを誤読しないための識別子
        // "MTVC" = Moorestech Terrain Visual Cache; the identifier keeping a foreign file from being misread
        public const int MagicNumber = 0x4D545643;

        // 見た目の導出が変わるたびに1ずつbumpしてきた現在値。直近の10はキー鍵の導出元を生成マスタ指紋方式へ刷新した回
        // The running value, bumped by one whenever the visual derivation changed; the latest step to 10 moved the key's inputs to the generation master fingerprint scheme
        // 据え置くと旧鍵で焼いたキャッシュファイルが新鍵と衝突する可能性が残る
        // Holding it back would risk cache files baked under the old key colliding with the new one
        public const int FormatVersion = 10;

        // キーはSHA256の16進64文字固定。可変長にすると壊れたファイルで読み出し長が暴れる
        // The key is a fixed 64-char SHA256 hex; a variable length would let a broken file dictate how much is read
        public const int CacheKeyByteLength = 64;
        public const int PayloadChecksumByteLength = 32;
        public const int HeaderByteLength = 4 + 4 + CacheKeyByteLength + 4 * 4 + PayloadChecksumByteLength;

        // 読み手が破損headerで巨大な配列を確保しないための形式上限。実データの最大値ではない
        // The format bounds stop a corrupt header from allocating enormous arrays; they are not gameplay data limits
        public const int MaximumAlphamapResolution = 4096;
        public const int MaximumLayerCount = 64;
        public const int MaximumDetailResolution = 4096;
        public const int MaximumDetailMapCount = 64;

        // splatmapの重みは1画素1バイトに量子化する。Unityがalphamapを8bitテクスチャへ焼くため精度は落ちない
        // Splat weights are quantized to one byte per pixel: Unity bakes alphamaps into 8-bit textures, so no precision is lost
        public const float WeightQuantizeScale = byte.MaxValue;

        public const int DetailBytesPerCell = 2;

        // ヘッダの4つの寸法はこの順で並ぶ
        // The header's four dimensions sit in this order
        public const int AlphamapResolutionOffset = 8 + CacheKeyByteLength;
        public const int LayerCountOffset = AlphamapResolutionOffset + 4;
        public const int DetailResolutionOffset = LayerCountOffset + 4;
        public const int DetailMapCountOffset = DetailResolutionOffset + 4;
        public const int PayloadChecksumOffset = DetailMapCountOffset + 4;

        public static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public static int ReadInt(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
        }

        public static bool TryCalculatePayloadByteLength(
            int alphamapResolution, int layerCount, int detailResolution, int detailMapCount, out long payloadByteLength)
        {
            payloadByteLength = 0;
            if (alphamapResolution <= 0 || MaximumAlphamapResolution < alphamapResolution ||
                layerCount <= 0 || MaximumLayerCount < layerCount ||
                detailResolution < 0 || MaximumDetailResolution < detailResolution ||
                detailMapCount < 0 || MaximumDetailMapCount < detailMapCount) return false;

            payloadByteLength = checked(
                (long)alphamapResolution * alphamapResolution * layerCount +
                (long)detailMapCount * detailResolution * detailResolution * DetailBytesPerCell);
            return payloadByteLength <= int.MaxValue;
        }
    }
}
