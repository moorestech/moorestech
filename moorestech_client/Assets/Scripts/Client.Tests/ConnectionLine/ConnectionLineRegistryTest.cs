using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Game.Block.Interface;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.ConnectionLine
{
    /// <summary>
    ///     接続線索引が両端ブロックから線を引けることを検証する
    ///     Verifies the connection-line registry resolves lines from either endpoint block
    /// </summary>
    public class ConnectionLineRegistryTest
    {
        private readonly List<GameObject> _createdLines = new();

        [TearDown]
        public void TearDown()
        {
            // 途中の検証失敗でも生成物を残さない
            // Clean generated objects even when an assertion fails
            foreach (var line in _createdLines)
            {
                if (line != null) UnityEngine.Object.DestroyImmediate(line);
            }
            _createdLines.Clear();
        }

        [Test]
        public void LineIsAttachedToBothEndpointsUntilUnregistered()
        {
            // 1本の線を登録すると両端どちらからも引ける
            // A registered line is reachable from both endpoints
            var registry = new ConnectionLineRegistry();
            var line = CreateLine();
            InitializeLine(line, new BlockInstanceId(1), new BlockInstanceId(2), ConnectionLineKind.ElectricWire, registry);

            Assert.AreSame(line, registry.GetLinesAttachedTo(new BlockInstanceId(1))[0]);
            Assert.AreSame(line, registry.GetLinesAttachedTo(new BlockInstanceId(2))[0]);
            Assert.AreEqual(0, registry.GetLinesAttachedTo(new BlockInstanceId(3)).Count);

            // 登録解除で索引から外れる（EditModeではOnDestroyが呼ばれないため経路を直接叩く）
            // Unregistering removes the line; EditMode skips OnDestroy, so call the path directly
            registry.Unregister(line);
            Assert.AreEqual(0, registry.GetLinesAttachedTo(new BlockInstanceId(1)).Count);
            Assert.AreEqual(0, registry.GetLinesAttachedTo(new BlockInstanceId(2)).Count);
        }

        [Test]
        public void LineCategoryIsConnectionLine()
        {
            // 接続線はブロックと別カテゴリー
            // Connection lines use a category separate from blocks
            var registry = new ConnectionLineRegistry();
            var line = CreateLine();
            InitializeLine(line, new BlockInstanceId(1), new BlockInstanceId(2), ConnectionLineKind.GearChain, registry);

            Assert.AreEqual(ConnectionLineKind.GearChain, line.Kind);
            Assert.AreEqual(Client.Game.Common.BlockMasterElementExtension.ConnectionLineDestructionCategory, line.GetDestructionCategory());
            Assert.IsTrue(line.IsRemovable(out var reason));
            Assert.IsFalse(reason.HasValue);
            UnityEngine.Object.DestroyImmediate(line.gameObject);
        }
        [Test]
        public void RemovingOneLineKeepsOtherLinesAtSharedEndpoint()
        {
            // 同じ端点に電線とチェーンを索引付けする
            // Index a wire and a chain at the same endpoint
            var registry = new ConnectionLineRegistry();
            var wire = CreateLine();
            var chain = CreateLine();
            InitializeLine(wire, new BlockInstanceId(1), new BlockInstanceId(2), ConnectionLineKind.ElectricWire, registry);
            InitializeLine(chain, new BlockInstanceId(3), new BlockInstanceId(2), ConnectionLineKind.GearChain, registry);
            Assert.AreEqual(2, registry.GetLinesAttachedTo(new BlockInstanceId(2)).Count);

            // 片方の登録解除で他の線を消さない
            // Unregistering one line keeps the other
            registry.Unregister(wire);
            Assert.IsEmpty(registry.GetLinesAttachedTo(new BlockInstanceId(1)));
            Assert.AreSame(chain, registry.GetLinesAttachedTo(new BlockInstanceId(2))[0]);
            Assert.AreSame(chain, registry.GetLinesAttachedTo(new BlockInstanceId(3))[0]);
            Assert.AreEqual(1, registry.GetLinesAttachedTo(new BlockInstanceId(2)).Count);
        }

        [Test]
        public void RegisterAndUnregisterNotifyBothEndpoints()
        {
            // 登録・解除のたびに両端ブロックIdが流れる（巻き込み赤表示が追従する契機）
            // Each register and unregister emits both endpoint ids (the cue for the cascade red preview to follow)
            var registry = new ConnectionLineRegistry();
            var changed = new List<int>();
            registry.OnLineAttachmentChanged.Subscribe(id => changed.Add(id.AsPrimitive()));
            var line = CreateLine();

            InitializeLine(line, new BlockInstanceId(1), new BlockInstanceId(2), ConnectionLineKind.ElectricWire, registry);
            CollectionAssert.AreEqual(new[] { 1, 2 }, changed);

            registry.Unregister(line);
            CollectionAssert.AreEqual(new[] { 1, 2, 1, 2 }, changed);
        }

        private ConnectionLineDeleteTarget CreateLine()
        {
            var gameObject = new GameObject("Line");
            _createdLines.Add(gameObject);
            return gameObject.AddComponent<ConnectionLineDeleteTarget>();
        }

        private static void InitializeLine(ConnectionLineDeleteTarget line, BlockInstanceId fromId, BlockInstanceId toId, ConnectionLineKind kind, ConnectionLineRegistry registry)
        {
            line.Initialize(fromId, toId, Guid.NewGuid(), registry, new StubEndpoints(), new StubCommands(kind));
        }

        private sealed class StubEndpoints : IConnectionLineEndpointQuery
        {
            public bool TryGetPosition(BlockInstanceId instanceId, out Vector3Int position)
            {
                position = Vector3Int.zero;
                return true;
            }
        }

        private sealed class StubCommands : IConnectionLineCommands
        {
            public ConnectionLineKind Kind { get; }
            public StubCommands(ConnectionLineKind kind) { Kind = kind; }
            public void SendDisconnect(Vector3Int posA, Vector3Int posB) { }
            public void SendRestore(IRemovalRestoreSender sender, Vector3Int posA, Vector3Int posB, Guid connectToolGuid) { }
        }
    }
}
