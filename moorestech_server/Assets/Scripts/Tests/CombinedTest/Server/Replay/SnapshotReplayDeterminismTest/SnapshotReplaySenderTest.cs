using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Replay;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.Replay.SnapshotReplayDeterminismTest
{
    public class SnapshotReplaySenderTest
    {
        [Test]
        public void 切断後は同じ身元の再接続を再生でも受理する()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-replay-reconnect-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var snapshotPath = Path.Combine(root, "save.json");
            var directory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, snapshotPath);
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) { worldDataDirectory = directory });
            var log = new ReceivedPacketLog();
            var warnings = new List<string>();
            Application.logMessageReceived += ObserveLog;
            try
            {
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                GameUpdater.RestoreCurrentTick(0);
                File.WriteAllText(snapshotPath, provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
                log.Start(Path.Combine(root, "packets"), 1, Array.Empty<int>());
                var handshake = MessagePackSerializer.Serialize(
                    new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack("steam:1"));
                log.Append(1, null, handshake);
                log.AppendDisconnect(2, 1);
                log.Append(3, null, handshake);
                log.Stop();

                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, snapshotPath, log.SegmentFilePaths(), 3));
                Assert.AreEqual(2, result.ReplayedPacketCount);
                Assert.AreEqual("steam:1", (string)JObject.Parse(result.SnapshotJson)["players"]["entries"][0]["identity"]);
                Assert.IsFalse(warnings.Any(message => message.Contains("接続中のため後からの接続を拒否")));
            }
            finally
            {
                Application.logMessageReceived -= ObserveLog;
                log.Stop();
                Directory.Delete(root, true);
            }

            #region Internal

            void ObserveLog(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Warning) warnings.Add(message);
            }

            #endregion
        }

        [Test]
        public void 再生は送り手別に操作しハンドシェイク後も未紐づけを拒否するTest()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-replay-senders-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var snapshotPath = Path.Combine(root, "save.json");
            var directory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, snapshotPath);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = directory,
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var log = new ReceivedPacketLog();
            var target = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;

            try
            {
                // 操作前の世界だけ保存し、再生結果を送り手ごとの割当で検査する
                // Save only the initial world and inspect replayed assignments for each sender
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                GameUpdater.RestoreCurrentTick(0);
                provider.GetRequiredService<IGameUnlockStateDataController>().UnlockBlock(target);
                File.WriteAllText(snapshotPath, provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
                log.Start(Path.Combine(root, "packets"), 1, Array.Empty<int>());

                // 最初の未紐づけ接続は確定するが、次の未紐づけ接続は引き継がない
                // The first unbound connection binds, but the next unbound connection must not inherit it
                log.Append(1, null, MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack("steam:1")));
                AppendAssignment(2, null, 0);
                AppendAssignment(3, 1, 1);
                AppendAssignment(3, 2, 2);
                AppendAssignment(4, 1, 3);
                log.Stop();

                LogAssert.Expect(LogType.Warning, "[PacketResponseCreator] 未紐づけの接続からの要求を無視しました tag:va:hotbar");
                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory,
                    directory, snapshotPath, log.SegmentFilePaths(), 4));
                Assert.AreEqual(5, result.ReplayedPacketCount);
                var assignments = (JArray)JObject.Parse(result.SnapshotJson)["hotbarAssignments"];
                var first = assignments.Single(entry => (int)entry["PlayerId"] == 1)["Assignments"];
                var second = assignments.Single(entry => (int)entry["PlayerId"] == 2)["Assignments"];

                // 未紐づけ操作は拒否し、二人の同tick操作と一人目の再操作は別々に残す
                // Reject the unbound operation and preserve same-tick operations plus the first sender's later operation
                Assert.AreEqual(Guid.Empty.ToString(), (string)first[0]);
                Assert.AreEqual(target.ToString(), (string)first[1]);
                Assert.AreEqual(Guid.Empty.ToString(), (string)first[2]);
                Assert.AreEqual(target.ToString(), (string)first[3]);
                Assert.AreEqual(target.ToString(), (string)second[2]);
                Assert.AreEqual(Guid.Empty.ToString(), (string)second[1]);

            }
            finally
            {
                log.Stop();
                Directory.Delete(root, true);
            }

            #region Internal

            void AppendAssignment(ulong tick, int? sender, int slot)
            {
                var request = HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(slot, target);
                log.Append(tick, sender, MessagePackSerializer.Serialize(request));
            }

            #endregion
        }
    }
}
