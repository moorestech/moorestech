using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// サーバーの地形バイナリをローカルキャッシュへ取得する。キャッシュが最新なら通信しない
    /// Fetches the server's terrain binaries into the local cache, skipping all traffic when the cache is current
    /// </summary>
    public class TerrainDataFetcher
    {
        private readonly VanillaApiWithResponse _vanillaApiWithResponse;

        public TerrainDataFetcher(VanillaApiWithResponse vanillaApiWithResponse)
        {
            _vanillaApiWithResponse = vanillaApiWithResponse;
        }

        // 戻り値は実際に取得したチャンク数。0はキャッシュヒットまたは地形なしワールドを意味する
        // Returns how many chunks were actually fetched; 0 means a cache hit or a terrain-less world
        public async UniTask<int> RunAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout)
        {
            // モード解釈はToTerrainTransferMeta1本。未知モードもそこで例外になる
            // ToTerrainTransferMeta is the only mode interpreter, and it is also where unknown modes throw
            var wireMeta = mapLayout.TerrainMeta;
            var terrainMeta = wireMeta.ToTerrainTransferMeta();

            // templateモードのワールドは地形バイナリを持たないので取得対象が無い
            // A template-mode world owns no terrain binary, so there is nothing to fetch
            if (terrainMeta is not GeneratedTerrainTransferMeta generatedMeta) return 0;

            var cacheWorldDirectory = WorldDataDirectory.ForWorldCache(generatedMeta.WorldId);
            var segments = TerrainTransferMeta.EnumerateStreamSegments(cacheWorldDirectory, generatedMeta.TerrainTileCount, generatedMeta.TerrainResolution).ToList();
            var totalStreamByteLength = segments.Sum(segment => segment.ByteLength);

            // 欠損・不一致は区別せず、サーバーのハッシュと一致しなければ全チャンクを取り直す
            // Missing and mismatching are not distinguished: anything but a hash match triggers a full re-fetch
            if (IsCacheMatchingServer())
            {
                Debug.Log($"[TerrainDataFetcher] 地形キャッシュを再利用します worldId={generatedMeta.WorldId}");
                return 0;
            }

            Debug.Log($"[TerrainDataFetcher] 地形チャンク取得開始 worldId={generatedMeta.WorldId} total={generatedMeta.TerrainChunkTotal}");
            await DownloadAllChunks();

            // 書き込み後に再ハッシュして転送破損を検出する。壊れた地形をキャッシュヒット扱いで持ち越さない
            // Re-hash after writing to catch transfer corruption instead of carrying broken terrain forward as a cache hit
            var restoredHash = TerrainStreamHasher.Compute(cacheWorldDirectory, generatedMeta);
            if (restoredHash != wireMeta.TerrainHash)
                throw new InvalidOperationException(
                    $"Restored terrain hash '{restoredHash}' does not match the server hash '{wireMeta.TerrainHash}'.");

            return generatedMeta.TerrainChunkTotal;

            #region Internal

            bool IsCacheMatchingServer()
            {
                if (segments.Any(segment => !File.Exists(segment.FilePath))) return false;
                return TerrainStreamHasher.Compute(cacheWorldDirectory, generatedMeta) == wireMeta.TerrainHash;
            }

            async UniTask DownloadAllChunks()
            {
                // 途中まで残った前回の断片を混ぜないよう、terrainディレクトリを作り直してから復元する
                // Rebuild the terrain directory first so leftovers from an aborted fetch never mix into the restore
                if (Directory.Exists(cacheWorldDirectory.TerrainDirectory))
                    Directory.Delete(cacheWorldDirectory.TerrainDirectory, true);
                Directory.CreateDirectory(cacheWorldDirectory.TerrainDirectory);

                using var fileWriter = new TerrainStreamFileWriter(segments);
                for (var chunkIndex = 0; chunkIndex < generatedMeta.TerrainChunkTotal; chunkIndex++)
                {
                    fileWriter.Write(await FetchChunk(chunkIndex));
                }
                fileWriter.ThrowIfTruncated();
            }

            async UniTask<byte[]> FetchChunk(int chunkIndex)
            {
                var response = await _vanillaApiWithResponse.GetTerrainChunk(chunkIndex, default);

                // サーバーが例外を投げた場合は応答が届かず、PacketExchangeManagerのタイムアウトでnullが返る
                // When the server throws, no response arrives and PacketExchangeManager's timeout surfaces it as null
                if (response == null)
                    throw new InvalidOperationException($"Terrain chunk {chunkIndex} was not answered by the server (timed out or undeserializable).");

                // 別チャンクの応答を取り違えるとファイル内容が静かに入れ替わる
                // Mixing up another chunk's response would silently swap file contents
                if (response.ChunkIndex != chunkIndex)
                    throw new InvalidOperationException($"Terrain chunk index mismatch: requested {chunkIndex} but received {response.ChunkIndex}.");

                return Decompress(response.Payload, ExpectedChunkByteLength(chunkIndex));
            }

            int ExpectedChunkByteLength(int chunkIndex)
            {
                if (chunkIndex + 1 < generatedMeta.TerrainChunkTotal) return TerrainTransferMeta.ChunkByteSize;
                return checked((int)(totalStreamByteLength - (long)chunkIndex * TerrainTransferMeta.ChunkByteSize));
            }

            #endregion
        }

        private static byte[] Decompress(byte[] compressedBytes, int expectedByteLength)
        {
            using var compressedStream = new MemoryStream(compressedBytes);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            var rawBytes = new byte[expectedByteLength];
            var readOffset = 0;
            while (readOffset < rawBytes.Length)
            {
                var readLength = gzipStream.Read(rawBytes, readOffset, rawBytes.Length - readOffset);
                if (readLength == 0)
                    throw new InvalidOperationException(
                        $"Terrain chunk decompressed to {readOffset} bytes but expected {expectedByteLength}.");
                readOffset += readLength;
            }

            // 契約長より1byteでも多い応答は、余剰をメモリへ展開せずその場で拒否する
            // Reject even one byte beyond the contract without expanding the surplus into memory
            if (gzipStream.ReadByte() != -1)
                throw new InvalidOperationException(
                    $"Terrain chunk decompressed beyond its expected {expectedByteLength} bytes.");
            return rawBytes;
        }
    }
}
