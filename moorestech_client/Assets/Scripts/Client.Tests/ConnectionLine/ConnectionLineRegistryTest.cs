using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Game.Block.Interface;
using NUnit.Framework;
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
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.ElectricWire, registry);

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
            // 接続線はブロックと別の破壊カテゴリーに属する
            // Connection lines belong to a destruction category separate from blocks
            var registry = new ConnectionLineRegistry();
            var line = CreateLine();
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.GearChain, registry);

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
            wire.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.ElectricWire, registry);
            chain.Initialize(new BlockInstanceId(3), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.GearChain, registry);
            Assert.AreEqual(2, registry.GetLinesAttachedTo(new BlockInstanceId(2)).Count);

            // 片方の登録解除で共有端点の他の線を消さない
            // Unregistering one line preserves the other at the shared endpoint
            registry.Unregister(wire);
            Assert.IsEmpty(registry.GetLinesAttachedTo(new BlockInstanceId(1)));
            Assert.AreSame(chain, registry.GetLinesAttachedTo(new BlockInstanceId(2))[0]);
            Assert.AreSame(chain, registry.GetLinesAttachedTo(new BlockInstanceId(3))[0]);
            Assert.AreEqual(1, registry.GetLinesAttachedTo(new BlockInstanceId(2)).Count);
        }

        [Test]
        public void HasLineBetweenChecksBothEndpointsAndKind()
        {
            // 読み取り面は端点順に依らず線種まで照合する
            // The read query checks line kind regardless of endpoint order
            var registry = new ConnectionLineRegistry();
            var line = CreateLine();
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(), ConnectionLineKind.ElectricWire, registry);

            Assert.IsTrue(registry.HasLineBetween(new BlockInstanceId(1), new BlockInstanceId(2), ConnectionLineKind.ElectricWire));
            Assert.IsTrue(registry.HasLineBetween(new BlockInstanceId(2), new BlockInstanceId(1), ConnectionLineKind.ElectricWire));
            Assert.IsFalse(registry.HasLineBetween(new BlockInstanceId(1), new BlockInstanceId(2), ConnectionLineKind.GearChain));
            Assert.IsFalse(registry.HasLineBetween(new BlockInstanceId(1), new BlockInstanceId(3), ConnectionLineKind.ElectricWire));
        }

        private ConnectionLineDeleteTarget CreateLine()
        {
            var gameObject = new GameObject("Line");
            _createdLines.Add(gameObject);
            return gameObject.AddComponent<ConnectionLineDeleteTarget>();
        }
    }
}
