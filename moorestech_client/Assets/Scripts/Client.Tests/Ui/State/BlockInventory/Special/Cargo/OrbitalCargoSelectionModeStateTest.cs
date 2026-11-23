using Client.Ui.State.BlockInventory.Special.Cargo;
using NUnit.Framework;
using static Client.Tests.Ui.State.BlockInventory.Special.Cargo.OrbitalCargoSamples;

namespace Client.Tests.Ui.State.BlockInventory.Special.Cargo
{
    public class OrbitalCargoSelectionModeStateTest
    {
        [Test]
        public void 未選択は解放済みが無くても一覧を出す()
        {
            var state = new OrbitalCargoSelectionModeState();
            Assert.IsTrue(state.ResolveShowsSelection(Pad(null, null)));
        }

        [Test]
        public void 見出しを押すと一覧を出し選んだ種類がサーバーに届くと閉じる()
        {
            var state = new OrbitalCargoSelectionModeState();
            Assert.IsFalse(state.ResolveShowsSelection(Pad(ChipLv1, null, ChipLv1, ChipLv2)));
            state.OpenSelection();
            Assert.IsTrue(state.ResolveShowsSelection(Pad(ChipLv1, null, ChipLv1, ChipLv2)));
            state.RememberRequest(ChipLv2);
            Assert.IsTrue(state.ResolveShowsSelection(Pad(ChipLv1, null, ChipLv1, ChipLv2)), "届くまでは一覧のまま（拒否されても一覧に残る）");
            Assert.IsFalse(state.ResolveShowsSelection(Pad(ChipLv2, null, ChipLv1, ChipLv2)));
        }

        [Test]
        public void 閉じた後にほかの人が選び直しても一覧へ戻らない()
        {
            var state = new OrbitalCargoSelectionModeState();
            state.OpenSelection();
            state.RememberRequest(ChipLv2);
            Assert.IsFalse(state.ResolveShowsSelection(Pad(ChipLv2, null, ChipLv1, ChipLv2)));
            Assert.IsFalse(state.ResolveShowsSelection(Pad(ChipLv1, null, ChipLv1, ChipLv2)));
        }

        [Test]
        public void 今選んでいる行を押しても閉じる()
        {
            // 同じ種類の選択はサーバーが成功で何もしない（A）ので、状態はすでに一致している
            // Selecting the same kind is a no-op success on the server (A), so the state already matches
            var state = new OrbitalCargoSelectionModeState();
            state.OpenSelection();
            state.RememberRequest(ChipLv1);
            Assert.IsFalse(state.ResolveShowsSelection(Pad(ChipLv1, null, ChipLv1)));
        }
    }
}
