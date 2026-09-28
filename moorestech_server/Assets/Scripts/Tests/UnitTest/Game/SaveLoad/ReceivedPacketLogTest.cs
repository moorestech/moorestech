using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ReceivedPacketLogTest
    {
        [Test]
        public void 送り手のIDと未紐づけと同tick内の順序が読み戻せるTest()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(1, 2, new byte[] { 1 });
            log.Append(1, null, new byte[] { 2 });
            log.Rotate(2);
            log.Append(2, 7, new byte[] { 3 });
            log.Stop();

            // 区間を跨いでも送り手とペイロードの対応を崩さない
            // Keep sender and payload paired even across segment boundaries
            var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
            CollectionAssert.AreEqual(new int?[] { 2, null, 7 }, records.Select(record => record.PlayerId));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, records.Select(record => record.Payload[0]));
            Directory.Delete(dir, true);
        }

        [Test]
        public void 追記したレコードをtick付きで読み戻せる()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(1, 1, new byte[] { 1, 2, 3 });
            log.Append(3, 1, new byte[] { 9 });
            log.Rotate(4);
            log.Append(4, 1, new byte[] { 4, 4 });
            log.Flush();

            var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
            Assert.AreEqual(3, records.Count);
            Assert.AreEqual(1UL, records[0].Tick);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, records[0].Payload);
            Assert.AreEqual(3UL, records[1].Tick);
            Assert.AreEqual(4UL, records[2].Tick);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 切断レコードの種別と送り手を読み戻せる()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.AppendDisconnect(2, 3);
            log.Stop();

            var record = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths()).Single();
            Assert.AreEqual(ReceivedPacketRecordKind.Disconnect, record.Kind);
            Assert.AreEqual(3, record.PlayerId);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 負のプレイヤーIDを持つ切断レコードを拒否する()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.AppendDisconnect(2, -7);
            log.Stop();

            // 壊れた区間を無音で再生すると切断解除が空振りし、接続集合が分岐する
            // Replaying a corrupt segment silently would miss removal and diverge the connection set
            var exception = Assert.Throws<InvalidDataException>(() => ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths()));
            StringAssert.Contains("切断レコードが不正", exception.Message);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 旧形式の区間を理由付きで読み飛ばし現行区間を読む()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 2);
            log.Append(2, 1, new byte[] { 42 });
            log.Stop();
            var newPath = log.SegmentFilePaths()[0];
            var oldPath = Path.Combine(dir, "packets_1.bin");
            var previousVersionPath = Path.Combine(dir, "packets_0.bin");
            File.Copy(newPath, previousVersionPath);
            using (var previousVersion = new BinaryWriter(new FileStream(previousVersionPath, FileMode.Open)))
            {
                previousVersion.BaseStream.Position = sizeof(int);
                previousVersion.Write(1);
            }
            using (var old = new BinaryWriter(File.Create(oldPath)))
            {
                old.Write(1UL);
                old.Write(1);
                old.Write(1);
                old.Write((byte)7);
            }

            LogAssert.Expect(LogType.Warning, new Regex("パケットログ区間の形式が現在版と異なる"));
            LogAssert.Expect(LogType.Warning, new Regex("パケットログ区間の形式が現在版と異なる"));
            var records = ReceivedPacketLogReader.ReadAll(new[] { oldPath, previousVersionPath, newPath });
            Assert.AreEqual(1, records.Count);
            CollectionAssert.AreEqual(new byte[] { 42 }, records[0].Payload);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 最古スナップショット以前の区間だけ削除される()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Rotate(11);
            log.Rotate(21);
            log.Rotate(31);
            log.DeleteSegmentsBefore(20);

            var names = log.SegmentFilePaths().Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_21.bin", "packets_31.bin" }, names);
            Directory.Delete(dir, true);
        }

        // 記録のI/O失敗がゲームの受信処理を落としてはいけない。落とすと取り出し済みパケットが無応答で消える
        // A capture I/O failure must not break packet processing; it would silently drop already-dequeued packets
        [Test]
        public void 区間切り替えのI_O失敗は例外を投げず理由を出して記録だけ止める()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            Assert.IsTrue(log.IsActive);

            // 置き場ごと消して、区間ファイルを開けない状態を作る
            // Remove the directory so the segment file can no longer be opened
            Directory.Delete(dir, true);
            LogAssert.Expect(LogType.Error, new Regex("^パケットログの区間切り替えに失敗しました"));
            Assert.DoesNotThrow(() => log.Rotate(11), "区間切り替えの失敗が呼び出し元へ伝播している");
            Assert.IsFalse(log.IsActive, "記録が止まっていない");

            LogAssert.Expect(LogType.Log, new Regex("^パケットログは未開始のため記録しません"));
            Assert.DoesNotThrow(() => log.Append(12, 1, new byte[] { 1 }), "縮退後のAppendが呼び出し元へ伝播している");

            // ログだけに残すと取得結果は欠損を伝えられない。理由と停止tickは状態として持つ
            // Leaving it in the log alone keeps the gap out of the capture result, so the reason and the stop tick are held as state
            StringAssert.Contains("パケットログの区間切り替えに失敗しました", log.DegradeReason, "縮退の理由が状態として残っていない");
            Assert.AreEqual(11UL, log.DegradedAtTick, "記録を止めたtickが状態として残っていない");
        }

        [Test]
        public void 開始前のAppendは無視される()
        {
            var log = new ReceivedPacketLog();
            log.Append(1, 1, new byte[] { 1 });
            log.Append(2, 1, new byte[] { 1 });
            Assert.IsFalse(log.IsActive);
        }

        // 末尾が切れた区間を通すと、壊れたpayloadが別のパケットとして再生され「一致しない」を非決定性のバグと誤診断させる
        // Passing a truncated tail replays a corrupted payload as a different packet and misdiagnoses the mismatch as non-determinism
        [Test]
        public void 末尾が切れたレコードは読み飛ばさず落とす()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(1, 1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            log.Stop();

            // 宣言された長さより短いところでファイルを切る（書き込み中のプロセスが落ちた状態と同じ）
            // Truncate the file short of the declared length, exactly as a process killed mid-write leaves it
            var path = log.SegmentFilePaths()[0];
            var bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, bytes.Take(bytes.Length - 3).ToArray());

            var exception = Assert.Throws<InvalidDataException>(() => ReceivedPacketLogReader.ReadAll(new[] { path }));
            StringAssert.Contains("途中で切れています", exception.Message);
            Directory.Delete(dir, true);
        }
    }
}
