using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 帯の高さと大型の幅は舞台の寸法から解き直す（L1。Web の calc() の C# 版。A-47）
    /// The band height and large width are re-solved from the stage size (L1; the C# take on the Web calc(); A-47)
    /// </summary>
    public class BlockInventoryPanelBandTest : BlockInventoryScreenTestHost
    {
        private const float InventoryPanelWidth = 473.891247f;
        private const float StageColumnGap = 16f;

        [Test]
        public void 舞台の大きさが変わると次のフレームで帯と大型の幅を当て直す()
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest()), new FakeLargePanelRule(true));
            simulate.FrameUpdate();

            // 舞台を縮め、期待値は舞台の解決済みの寸法から同じ式で出す
            // Shrink the stage and derive the expectation from its resolved size with the same formula
            shell.Stage.style.width = 1100f;
            shell.Stage.style.height = 600f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var stage = shell.Stage.resolvedStyle;
            Assert.AreEqual(1100f, stage.width, 0.01f);
            var band = stage.height - stage.paddingTop - BlockInventoryTestShell.MenuLowerSafeArea;
            var largeWidth = stage.width - stage.paddingLeft - stage.paddingRight - InventoryPanelWidth - StageColumnGap;
            Assert.AreEqual(band, shell.View.style.height.value.value, 0.01f);
            Assert.AreEqual(largeWidth, shell.View.style.width.value.value, 0.01f);

            // 配置は倍率 1 で物理画素へ丸まるので、実寸は半画素以内を見る
            // Layout snaps to device pixels at scale 1, so the laid-out size is checked within half a pixel
            Assert.AreEqual(band, shell.View.layout.height, 0.5f);
            Assert.AreEqual(largeWidth, shell.View.layout.width, 0.5f);
        }

        [Test]
        public void 舞台が安全帯より狭い間は警告を1回だけ出し_回復後に狭まると再び出す()
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest()), new FakeLargePanelRule(true));
            simulate.FrameUpdate();

            // 安全帯（上の padding + 下 70）より低く縮める。入ったフレームで 1 回
            // Shrink below the safe areas (top padding + 70 bottom); one warning on entry
            shell.Stage.style.height = 40f;
            LogAssert.Expect(LogType.Warning, new Regex("below the safe areas"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // さらに縮めても同じ異常なので出ない（未期待のログがあれば失敗する）
            // Shrinking further is the same anomaly, so nothing more is logged (an unexpected log fails the test)
            shell.Stage.style.height = 30f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 回復で旗が戻り、再び狭めると出る
            // Recovery clears the flag, and narrowing again warns again
            shell.Stage.style.height = 600f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            shell.Stage.style.height = 40f;
            LogAssert.Expect(LogType.Warning, new Regex("below the safe areas"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
