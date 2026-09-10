using System;
using Core.Master.Validator;
using Game.MapGeneration.Transfer;

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

        // 見た目導出変更ごとにbumpする現在値
        // The running value, bumped whenever the visual derivation changes
        // 据え置くと旧鍵で焼いたキャッシュファイルが新鍵と衝突する可能性が残る
        // Holding it back would risk cache files baked under the old key colliding with the new one
        public const int FormatVersion = 11;

        // キーはSHA256の16進64文字固定。可変長にすると壊れたファイルで読み出し長が暴れる
        // The key is a fixed 64-char SHA256 hex; a variable length would let a broken file dictate how much is read
        public const int CacheKeyByteLength = 64;
        public const int PayloadChecksumByteLength = 32;
        public const int HeaderByteLength = 4 + 4 + CacheKeyByteLength + 4 * 5 + PayloadChecksumByteLength;

        // 読み手が破損headerで巨大な配列を確保しないための形式上限。実データの最大値ではない
        // The format bounds stop a corrupt header from allocating enormous arrays; they are not gameplay data limits
        public const int MaximumHeightmapResolution = 8193;
        public const int MaximumAlphamapResolution = 4096;
        public const int MaximumLayerCount = 64;
        public const int MaximumDetailResolution = 4096;
        public const int MaximumDetailMapCount = 64;

        // splatmapの重みは1画素1バイトに量子化する。Unityがalphamapを8bitテクスチャへ焼くため精度は落ちない
        // Splat weights are quantized to one byte per pixel: Unity bakes alphamaps into 8-bit textures, so no precision is lost
        public const float WeightQuantizeScale = byte.MaxValue;

        // 高さはr16と同じushort量子化で持つ。木の摂動後の表示高さはこの刻みに載っていないため、保存で最大1LSB丸められる
        // Heights are held at the same ushort quantization as r16; post-perturbation display heights do not sit on that step, so storing rounds them by up to one LSB
        public const float HeightQuantizeScale = ushort.MaxValue;
        public const int HeightBytesPerPixel = 2;

        public const int DetailBytesPerCell = 2;

        // ヘッダの5つの寸法はこの順で並ぶ
        // The header's five dimensions sit in this order
        public const int HeightmapResolutionOffset = 8 + CacheKeyByteLength;
        public const int AlphamapResolutionOffset = HeightmapResolutionOffset + 4;
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

        // 各区画のバイト長。書き手と読み手が同じ式を共有し、区画境界の取り違えを構造的に無くす
        // Each section's byte length; writer and reader share one formula so a section boundary cannot drift between them
        public static long HeightsByteLength(int heightmapResolution)
        {
            return (long)heightmapResolution * heightmapResolution * HeightBytesPerPixel;
        }

        public static long AlphamapPlaneByteLength(int alphamapResolution)
        {
            return (long)alphamapResolution * alphamapResolution * TileAlphamap.AlphamapPlaneBytesPerPixel;
        }

        private static long DetailMapByteLength(int detailResolution)
        {
            return (long)detailResolution * detailResolution * DetailBytesPerCell;
        }

        public static bool TryCalculatePayloadByteLength(
            int heightmapResolution, int alphamapResolution, int layerCount, int detailResolution, int detailMapCount,
            out long payloadByteLength)
        {
            payloadByteLength = 0;
            if (heightmapResolution <= 0 || MaximumHeightmapResolution < heightmapResolution ||
                alphamapResolution <= 0 || MaximumAlphamapResolution < alphamapResolution ||
                layerCount <= 0 || MaximumLayerCount < layerCount ||
                detailResolution < 0 || MaximumDetailResolution < detailResolution ||
                detailMapCount < 0 || MaximumDetailMapCount < detailMapCount ||
                detailMapCount == 0 && detailResolution != 0 ||
                0 < detailMapCount && !GenerationMasterUtil.IsValidDetailResolution(detailResolution, heightmapResolution)) return false;

            payloadByteLength = checked(
                HeightsByteLength(heightmapResolution) +
                TileAlphamap.AlphamapPlaneCount(layerCount) * AlphamapPlaneByteLength(alphamapResolution) +
                detailMapCount * DetailMapByteLength(detailResolution));
            return payloadByteLength <= int.MaxValue;
        }
    }
}
