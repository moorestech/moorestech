using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.Content
{
    /// <summary>
    /// アイテムを枠の中身へ写す工場の空き・個数・印の規則を確かめる L0
    /// L0: the empty-cell, count and mark rules of the factories mapping items to slot content
    /// </summary>
    public class SlotItemContentTest
    {
        private Texture2D _texture;
        private GameContentPresentation _content;

        [SetUp]
        public void SetUp()
        {
            // 名前解決のためテスト mod のマスタを読む
            // Load the test mod master for name resolution
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = new GameContentPresentation(new FakeContentTextureSource(_texture, null, null));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void 空きの枠は絵も個数も持たない()
        {
            var slot = SlotItemContent.From(_content, new SlotItem(ItemMaster.EmptyItemId, 0), false);
            Assert.IsNull(slot.Icon);
            Assert.IsNull(slot.Count);
            Assert.IsFalse(slot.Marks.Selected);
        }

        [Test]
        public void アイテムの枠は絵と個数と選択の印を持つ()
        {
            var slot = SlotItemContent.From(_content, new SlotItem(new ItemId(1), 5), true);
            Assert.AreSame(_texture, slot.Icon.Texture);
            Assert.AreEqual(5, slot.Count);
            Assert.IsTrue(slot.Marks.Selected);
            Assert.IsFalse(slot.Marks.Catalog);
            Assert.IsFalse(slot.Marks.Insufficient);
        }

        [Test]
        public void 機械の空きの枠も絵と個数を持たず所持必要とゴーストは添える()
        {
            var shortage = new ItemSlotShortage(0, 3, true, null);
            var ghost = new ItemSlotGhost(_content.Item(new ItemId(1)), 3);
            var slot = SlotItemContent.From(_content, new SlotItem(ItemMaster.EmptyItemId, 0), shortage, ghost);
            Assert.IsNull(slot.Icon);
            Assert.IsNull(slot.Count);
            Assert.AreSame(shortage, slot.Shortage);
            Assert.AreSame(ghost, slot.Ghost);
            Assert.IsFalse(slot.Marks.Insufficient);
        }

        [Test]
        public void 空のIDの絵だけの枠は空きになる()
        {
            Assert.IsNull(SlotItemContent.ForOptionalItem(_content, ItemMaster.EmptyItemId).Icon);
            var item = SlotItemContent.ForOptionalItem(_content, new ItemId(1));
            Assert.AreSame(_texture, item.Icon.Texture);
            Assert.IsNull(item.Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 素材の枠は不足のときだけ減光の印を持ちバッジの数は持たない(bool lacking)
        {
            var slot = SlotItemContent.ForMaterial(_content, new ItemId(1), new ItemSlotShortage(1, 3, lacking, null));
            Assert.AreEqual(lacking, slot.Marks.Insufficient);
            Assert.IsNull(slot.Count);
        }

        [Test]
        public void 一覧の枠はカタログの印と数を持つ()
        {
            var slot = SlotItemContent.ForCatalog(_content, new ItemId(1), 0);
            Assert.IsTrue(slot.Marks.Catalog);
            Assert.AreEqual(0, slot.Count);
        }
    }
}
