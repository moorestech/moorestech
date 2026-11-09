using System;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻のエラー行（文言の対応・列車の各エラー・列車の本文の未登録・エラーからの復帰）の L1
    /// L1 of the shell error row (text mapping, each train error, a missing train body and recovery from an error)
    /// </summary>
    public class BlockInventoryPanelErrorTest : BlockInventoryScreenTestHost
    {
        [Test]
        public void None以外のエラーはすべて文言のキーを持つ()
        {
            foreach (BlockInventoryPanelError error in Enum.GetValues(typeof(BlockInventoryPanelError)))
            {
                var found = BlockInventoryPanelErrorText.TryGetLocalizationKey(error, out var key);
                Assert.AreEqual(error != BlockInventoryPanelError.None, found, error.ToString());
                if (found) Assert.AreNotEqual(default(LocalizationKey), key, error.ToString());
            }
        }

        [TestCase(BlockInventoryPanelError.ContainerMissing)]
        [TestCase(BlockInventoryPanelError.TrainCarMissing)]
        [TestCase(BlockInventoryPanelError.OpenFailed)]
        public void 列車のエラーは列車のエラー行に各キーの文を出す(BlockInventoryPanelError error)
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Train(error)));
            simulate.FrameUpdate();

            BlockInventoryPanelErrorText.TryGetLocalizationKey(error, out var key);
            var row = Find<Label>(shell.View, "train-inventory-error");
            Assert.AreEqual(new UiText(key).Resolve(), row.text);
            Assert.IsNull(shell.View.Q("block-inventory-error"));
            // Web の素の div と同じ本文の既定（16px・左の余白 0）で題の左端に揃う
            // Same body default as the Web bare div (16px, no left inset), aligned with the title's left edge
            Assert.AreEqual(16f, row.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(0f, row.resolvedStyle.marginLeft + row.resolvedStyle.paddingLeft, 0.01f);
        }

        [Test]
        public void 列車の本文の登録が無いときは題とエラー行だけで警告を1回出す()
        {
            // 警告は組み立て時の情報ログより後に来るよう、閉から開く
            // Open from closed so the warning follows the assembly-time info log
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var shell = BlockInventoryTestShell.Create(this, state);
            LogAssert.Expect(LogType.Warning, new Regex("列車の本文の登録が無い"));
            state.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.None);
            simulate.FrameUpdate();

            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.TrainInventory).Resolve(), shell.TitleText());
            Assert.IsNull(shell.View.Q("train-inventory-error"));
            // 本文の器には空の列車の部分の Root だけが入る
            // The body holder holds only the empty train part's Root
            var body = Find<VisualElement>(shell.View, "block-inventory-body");
            Assert.AreEqual(1, body.childCount);
            Assert.AreEqual(0, body[0].childCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 正常からエラーへ移ると本文を外しエラーが解けると本文が戻る()
        {
            var chest = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(new ItemId(1), 1));
            var state = new ReactiveProperty<BlockInventoryPanelState>(chest);
            var shell = BlockInventoryTestShell.Create(this, state);
            simulate.FrameUpdate();
            Assert.IsNotNull(shell.View.Q("chest-grid"));
            Assert.IsNull(shell.View.Q("block-inventory-error"));

            state.Value = BlockInventoryTestPanels.OpenFailedChest();
            simulate.FrameUpdate();
            Assert.IsNull(shell.View.Q("chest-grid"));
            Assert.AreEqual(DisplayStyle.Flex, Find<Label>(shell.View, "block-inventory-error").resolvedStyle.display);

            // 一度作ったエラー行は外さず隠す
            // A built error row is hidden, not removed
            state.Value = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(new ItemId(1), 1));
            simulate.FrameUpdate();
            Assert.IsNotNull(shell.View.Q("chest-grid"));
            Assert.AreEqual(DisplayStyle.None, Find<Label>(shell.View, "block-inventory-error").resolvedStyle.display);
        }
    }
}
