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
    /// 空きは絵と個数を持たず選択の印だけ渡す L0
    /// L0: empty cells carry only the selection mark
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
    }
}
