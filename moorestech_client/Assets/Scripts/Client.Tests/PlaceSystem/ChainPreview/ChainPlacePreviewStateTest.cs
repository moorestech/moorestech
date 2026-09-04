using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結レイアウト共有状態の登録ガードを検証
    ///     Verifies the registration guard of the shared chain layout state
    /// </summary>
    public class ChainPlacePreviewStateTest
    {
        private static readonly Guid FirstTutorialGuid = Guid.Parse("44444444-0000-0000-0000-000000000001");
        private static readonly Guid SecondTutorialGuid = Guid.Parse("44444444-0000-0000-0000-000000000002");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 別guidが同じ設置ブロックを取ると代表選択が列挙順任せになるため例外にする
        // Two guids claiming one placing block would leave the pick to enumeration order, so it throws
        [Test]
        public void 別チュートリアルが同じ設置ブロックを登録すると例外になる()
        {
            var state = new ChainPlacePreviewState();
            state.SetChain(FirstTutorialGuid, ForUnitTestModBlockId.ChestId, CreateChain(Vector3Int.forward));

            Assert.Throws<InvalidOperationException>(() => state.SetChain(SecondTutorialGuid, ForUnitTestModBlockId.ChestId, CreateChain(Vector3Int.right)));
        }

        [Test]
        public void 同一チュートリアルの再登録は上書きされる()
        {
            var state = new ChainPlacePreviewState();
            state.SetChain(FirstTutorialGuid, ForUnitTestModBlockId.ChestId, CreateChain(Vector3Int.forward));
            state.SetChain(FirstTutorialGuid, ForUnitTestModBlockId.ChestId, CreateChain(Vector3Int.right));

            Assert.IsTrue(state.TryGetChain(ForUnitTestModBlockId.ChestId, out var chain, out var tutorialGuid));
            Assert.AreEqual(FirstTutorialGuid, tutorialGuid);
            Assert.AreEqual(1, chain.Count);
            Assert.AreEqual(Vector3Int.right, chain[0].Offset);
        }

        [Test]
        public void 解除後は別チュートリアルが同じ設置ブロックを登録できる()
        {
            var state = new ChainPlacePreviewState();
            state.SetChain(FirstTutorialGuid, ForUnitTestModBlockId.ChestId, CreateChain(Vector3Int.forward));
            state.Clear(FirstTutorialGuid);

            state.SetChain(SecondTutorialGuid, ForUnitTestModBlockId.ChestId, CreateChain(Vector3Int.right));

            Assert.IsTrue(state.TryGetChain(ForUnitTestModBlockId.ChestId, out _, out var tutorialGuid));
            Assert.AreEqual(SecondTutorialGuid, tutorialGuid);
        }

        private static List<ChainGhost> CreateChain(Vector3Int offset)
        {
            return new List<ChainGhost> { new(ForUnitTestModBlockId.ChestId, offset, BlockDirection.North) };
        }
    }
}
