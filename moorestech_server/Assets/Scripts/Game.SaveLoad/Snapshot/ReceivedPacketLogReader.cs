using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.PlayerIdentity;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    public static class ReceivedPacketLogReader
    {
        // 区間ファイルを与えられた順に読み、tick昇順で返す（同tick内は記録順を保つ安定ソート）
        // Read segment files in the given order and return records ordered by tick (stable sort keeps record order within a tick)
        public static List<ReceivedPacketRecord> ReadAll(IEnumerable<string> segmentFilePaths)
        {
            var result = new List<ReceivedPacketRecord>();
            foreach (var path in segmentFilePaths)
            {
                // ディスク読み出しは外部境界だが、読めない区間は再生の欠落として上位へ伝えるため握り潰さない
                // Disk reads are an external boundary, yet an unreadable segment must surface as a replay gap rather than be swallowed
                using var reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                if (!TryReadHeader(reader, path, out _)) continue;
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var tick = reader.ReadUInt64();
                    var kind = (ReceivedPacketRecordKind)reader.ReadByte();
                    if (kind != ReceivedPacketRecordKind.Packet && kind != ReceivedPacketRecordKind.Disconnect)
                    {
                        throw new InvalidDataException($"パケットログのレコード種別が不正です path:{path} tick:{tick} kind:{kind}");
                    }
                    var playerId = reader.ReadInt32();
                    var length = reader.ReadInt32();
                    if (length < 0) throw new InvalidDataException($"パケットログの長さが負です path:{path} tick:{tick} length:{length}");
                    var decodedPlayerId = ReceivedPacketLog.DecodeSenderPlayerId(playerId);
                    if (decodedPlayerId.HasValue && !PlayerIdSequence.IsValid(decodedPlayerId.Value))
                        throw new InvalidDataException($"パケットログの送り手IDが不正です path:{path} tick:{tick} playerId:{playerId}");
                    if (kind == ReceivedPacketRecordKind.Disconnect && length != 0)
                        throw new InvalidDataException($"パケットログの切断レコードが不正です path:{path} tick:{tick} playerId:{playerId} length:{length}");

                    // ReadBytes は足りない分を黙って短く返す。通すと壊れた末尾が別のパケットとして再生され、非決定性のバグに見える
                    // ReadBytes silently returns a short buffer; letting it through replays a corrupted tail as a different packet and looks like non-determinism
                    var payload = reader.ReadBytes(length);
                    if (payload.Length != length) throw new InvalidDataException($"パケットログのレコードが途中で切れています path:{path} tick:{tick} expected:{length} actual:{payload.Length}");

                    result.Add(new ReceivedPacketRecord(tick, decodedPlayerId, payload, kind));
                }
            }
            return result.OrderBy(record => record.Tick).ToList();
        }

        // 区間開始時点の接続中IDを読む。再生は世界を進める前にこれで接続を復元する
        // Reads the ids connected at a segment's start so replay can restore them before advancing the world
        public static IReadOnlyList<int> ReadConnectedPlayerIdsAt(IEnumerable<string> segmentFilePaths, ulong fromTick)
        {
            foreach (var path in segmentFilePaths)
            {
                using var reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                if (!TryReadHeader(reader, path, out var header)) continue;
                if (header.FromTick != fromTick) continue;
                return header.ConnectedPlayerIds;
            }

            // 見つからない区間を無言で空集合にすると、接続集合の欠落が非決定性のバグに見える
            // Silently treating a missing segment as an empty set would make the lost connection set look like non-determinism
            Debug.LogWarning($"開始tick {fromTick} の区間ヘッダが見つからないため、再生は接続集合を復元しません");
            return Array.Empty<int>();
        }

        private static bool TryReadHeader(BinaryReader reader, string path, out ReceivedPacketLogSegmentHeader header)
        {
            header = default;
            if (reader.BaseStream.Length < sizeof(int) * 2 ||
                reader.ReadInt32() != ReceivedPacketLog.SegmentMagic ||
                reader.ReadInt32() != ReceivedPacketLog.SegmentVersion)
            {
                Debug.LogWarning($"パケットログ区間の形式が現在版と異なるため読み飛ばします path:{path}");
                return false;
            }

            var fromTick = reader.ReadUInt64();
            var connectedCount = reader.ReadInt32();
            if (connectedCount < 0) throw new InvalidDataException($"パケットログ区間の接続中ID件数が負です path:{path} count:{connectedCount}");
            var connectedPlayerIds = new int[connectedCount];
            for (var i = 0; i < connectedCount; i++)
            {
                connectedPlayerIds[i] = reader.ReadInt32();
                if (!PlayerIdSequence.IsValid(connectedPlayerIds[i]))
                    throw new InvalidDataException($"パケットログ区間の接続中IDが不正です path:{path} playerId:{connectedPlayerIds[i]}");
            }
            header = new ReceivedPacketLogSegmentHeader(fromTick, connectedPlayerIds);
            return true;
        }
    }

    // 区間ヘッダの内容。開始tickとその時点の接続中ID
    // A segment header's content: the start tick and the ids connected at that tick
    public readonly struct ReceivedPacketLogSegmentHeader
    {
        public readonly ulong FromTick;
        public readonly IReadOnlyList<int> ConnectedPlayerIds;

        public ReceivedPacketLogSegmentHeader(ulong fromTick, IReadOnlyList<int> connectedPlayerIds)
        {
            FromTick = fromTick;
            ConnectedPlayerIds = connectedPlayerIds;
        }
    }
}
