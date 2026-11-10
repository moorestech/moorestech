using Client.Game.InGame.BlockInfoPolling;
using Client.Localization;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 照会が取れていない欄の 1 行（Web BlockInfoUnavailableLine）。文言の {target} に欄の名前を差す
    /// The one line for a section whose query is unavailable (Web BlockInfoUnavailableLine); {target} carries the section name
    /// </summary>
    public sealed class BlockInfoUnavailableLine : LocalizedLabel
    {
        private readonly LocalizationKey _targetKey;

        public BlockInfoUnavailableLine(string testid, LocalizationKey targetKey)
        {
            name = testid;
            _targetKey = targetKey;
            AddToClassList(UiTypographyClass.TextXs);
            AddToClassList(UiTypographyClass.TextDefault);
        }

        public void Render(BlockInfoFetchState fetchState)
        {
            // 取得済みでここへ来るのは呼び手の取り違え。開発者ログへ出して描かない
            // Reaching here while fetched is a caller mix-up; log it and draw nothing
            if (!BlockInfoFetchStateLogic.TryGetUnavailableKey(fetchState, out var key))
            {
                Debug.LogError($"[BlockInfoUnavailableLine] {name}: no unavailable text for {fetchState}");
                return;
            }
            // 欄の名前は描いた時点の言語で解く（1 秒ごとの再描画で言語の変化にも追従する）
            // The section name resolves in the current language (the one-second redraw follows language changes)
            SetText(new UiText(key, new[] { new LocalizationTextParam("target", new UiText(_targetKey).Resolve()) }));
        }
    }
}
