using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Client.Tests.UIState.DragDelete
{
    /// <summary>
    ///     巻き込み赤表示（R9）の要求・追従・解除。線自身のホバーの赤は巻き込み解除で消えない
    ///     Cascade red preview (R9): request, follow and release; the line's own hover red survives a cascade release
    /// </summary>
    public class CascadePreviewRedTest
    {
        private readonly List<GameObject> _created = new();
        private ConnectionLineRegistry _registry;
        private BlockAttachedConnectionResolver _resolver;
        private Material _originalMaterial;

        [SetUp]
        public void SetUp()
        {
            _registry = new ConnectionLineRegistry();
            _resolver = new BlockAttachedConnectionResolver(_registry, RailGraphClientCache.CreateForEditorTest());
            _originalMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _created)
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }
            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_originalMaterial);
        }

        [Test]
        public void AttachedLineTurnsRedOnRequestAndReturnsOnRelease()
        {
            var block = CreateBlock(1);
            var line = CreateLine(1, 2, out var renderer);

            _resolver.RequestCascadePreview(block);
            AssertRed(renderer);

            _resolver.ReleaseCascadePreview(block);
            Assert.AreSame(_originalMaterial, renderer.sharedMaterial);
            Assert.IsNotNull(line);
        }

        [Test]
        public void LineOwnHoverKeepsRedWhileCascadeIsReleased()
        {
            var block = CreateBlock(1);
            var line = CreateLine(1, 2, out var renderer);

            // 線のホバー中は赤が残る
            // Red stays while the line is hovered
            _resolver.RequestCascadePreview(block);
            line.SetRemovePreviewing();
            _resolver.ReleaseCascadePreview(block);
            AssertRed(renderer);
        }

        [Test]
        public void LineAddedWhileRequestingFollowsRed()
        {
            var block = CreateBlock(1);
            _resolver.RequestCascadePreview(block);

            // 要求中に増えた線へ赤表示が追従する
            // The red preview follows a line added while requesting
            CreateLine(1, 3, out var renderer);
            AssertRed(renderer);
        }

        // リニア色空間ではマテリアル色がガンマ⇔リニア変換を往復し末尾の桁がずれるため、近似で比べる
        // In linear color space the material color round-trips through gamma/linear conversion and loses trailing digits, so compare approximately
        private static void AssertRed(MeshRenderer renderer)
        {
            var actual = renderer.sharedMaterial.GetColor(MaterialConst.PreviewColorPropertyName);
            Assert.That(actual, Is.EqualTo(MaterialConst.NotPlaceableColor).Using(ColorEqualityComparer.Instance));
        }

        private BlockGameObject CreateBlock(int instanceId)
        {
            var gameObject = new GameObject($"Block{instanceId}");
            _created.Add(gameObject);
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockInstanceId)).GetSetMethod(true).Invoke(block, new object[] { new BlockInstanceId(instanceId) });
            return block;
        }

        private ConnectionLineDeleteTarget CreateLine(int fromId, int toId, out MeshRenderer renderer)
        {
            var gameObject = new GameObject($"Line{fromId}-{toId}");
            _created.Add(gameObject);
            renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _originalMaterial;
            var line = gameObject.AddComponent<ConnectionLineDeleteTarget>();
            line.Initialize(new BlockInstanceId(fromId), new BlockInstanceId(toId), Guid.NewGuid(), _registry, new StubEndpoints(), new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire));
            return line;
        }

        private sealed class StubEndpoints : IConnectionLineEndpointQuery
        {
            public bool TryGetPosition(BlockInstanceId instanceId, out Vector3Int position)
            {
                position = Vector3Int.zero;
                return true;
            }
        }
    }
}
