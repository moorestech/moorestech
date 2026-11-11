using Client.Ui.State.Train.Platform.Edit;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainStationNameDraftTest
    {
        [Test]
        public void 手を付けていない入力は同期した名前に従い拒否で同期済みへ戻る()
        {
            var draft = new TrainStationNameDraft();
            draft.Sync("北駅");
            Assert.AreEqual("北駅", draft.Input);

            // 編集中は同期で上書きしない
            // While edited, a sync does not overwrite
            draft.Edit("南駅");
            draft.Sync("東駅");
            Assert.AreEqual("南駅", draft.Input);

            // 拒否で同期値へ戻り以後は同期に従う
            // Rejection reverts to synced name, then follows syncs
            draft.Reject();
            Assert.AreEqual("東駅", draft.Input);
            draft.Sync("西駅");
            Assert.AreEqual("西駅", draft.Input);

            // 決定名が同期されれば以後も同期に従う
            // Applied name synced: later syncs followed
            draft.Edit("中央駅");
            draft.Sync("中央駅");
            draft.Sync("中央駅2");
            Assert.AreEqual("中央駅2", draft.Input);
        }
    }
}
