using System.Collections.Generic;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Interact;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     ブロックの面は開ける機械のみ候補に
    ///     Verifies the block interact face only offers openable machines as candidates
    /// </summary>
    public class BlockInteractableTest
    {
        // UIパス有無のForUnitTestブロック
        // A ForUnitTest block with a UI path and one without
        private const string OpenableBlockName = "TestElectricMachine";
        private const string PlainBlockName = "TestBeltConveyor";

        private readonly List<GameObject> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ヒント文言のブロック名解決に辞書が要る
            // The hint's block name resolution needs the dictionary
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects) Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void 開けるブロックはFで開くアクションを1つ持つ()
        {
            var openable = CreateBlockInteractable(OpenableBlockName);

            Assert.IsTrue(openable.IsInteractAvailable);
            Assert.AreEqual(1, openable.Actions.Count);
            Assert.AreSame(InputManager.Playable.Interact, openable.Actions[0].Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractOpenBlock.Key, openable.Actions[0].HintKey.Key);
            var blockGuid = openable.GetComponent<BlockGameObject>().BlockMasterElement.BlockGuid;
            Assert.AreEqual(new[] { Localize.GetContent(ContentLocalizationKeys.BlockName(blockGuid)) }, openable.Actions[0].HintParams);

            var transit = openable.Actions[0].Execute();
            Assert.AreEqual(UIStateEnum.SubInventory, transit.TransitContext.NextStateEnum);
            Assert.IsInstanceOf<BlockSubInventorySource>(transit.TransitContext.GetContext<ISubInventorySource>());
        }

        [Test]
        public void 撤去済みのブロックは候補から外れる()
        {
            var openable = CreateBlockInteractable(OpenableBlockName);
            openable.GetComponent<BlockGameObject>().MarkUnsearchable();

            Assert.IsFalse(openable.IsInteractAvailable);
        }

        // BlockGameObject.Initializeはサーバ接続を伴い呼べないため、そこが付与判断に使う条件を実マスタで検証する
        // BlockGameObject.Initialize cannot run without a server, so verify the very condition it attaches on, against the real master
        [Test]
        public void インタラクト面は開けるブロックのマスタにだけ付与条件が立つ()
        {
            Assert.IsTrue(FindMaster(OpenableBlockName).IsBlockOpenable());
            Assert.IsFalse(FindMaster(PlainBlockName).IsBlockOpenable());
        }

        // BlockGameObject.Initializeはサーバ接続を伴うため、マスタだけ差し込んでインタラクト面を直接初期化する
        // BlockGameObject.Initialize talks to the server, so only the master is injected and the interact face is initialized directly
        private BlockInteractable CreateBlockInteractable(string blockName)
        {
            var master = FindMaster(blockName);
            var gameObject = new GameObject(blockName);
            _createdObjects.Add(gameObject);

            var blockGameObject = gameObject.AddComponent<BlockGameObject>();
            TestReflection.SetField(blockGameObject, "<BlockMasterElement>k__BackingField", master);
            TestReflection.SetField(blockGameObject, "<BlockPosInfo>k__BackingField", new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));

            var interactable = gameObject.AddComponent<BlockInteractable>();
            interactable.Initialize(blockGameObject);
            return interactable;
        }

        private static BlockMasterElement FindMaster(string blockName)
        {
            return MasterHolder.BlockMaster.Blocks.Data.First(block => block.Name == blockName);
        }
    }
}
