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
                if (reader.BaseStream.Length < sizeof(int) * 2 ||
                    reader.ReadInt32() != ReceivedPacketLog.SegmentMagic ||
                    reader.ReadInt32() != ReceivedPacketLog.SegmentVersion)
                {
                    Debug.LogWarning($"パケットログ区間の形式が現在版と異なるため読み飛ばします path:{path}");
                    continue;
                }
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
                    if (kind == ReceivedPacketRecordKind.Disconnect && (!decodedPlayerId.HasValue ||
                        !PlayerIdentityRegistry.IsValidPlayerId(decodedPlayerId.Value) || length != 0))
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
    }
}
