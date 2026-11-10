using Client.Game.InGame.BlockInfoPolling;
using Client.Localization;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 照会が取れていない欄の 1 行
    /// The one line for a section whose query is unavailable (Web BlockInfoUnavailableLine); {target} carries the name
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

        // 文を描いて host の唯一の子にする。取得済みが来たら空ラベルを載せず host を空にする
        // Render the text and mount as host's only child; on a fetched state clear host instead of mounting an empty label
        public void RenderInto(VisualElement host, BlockInfoFetchState fetchState)
        {
            // 取得済みで来るのは呼び手の取り違え
            // Reaching here while fetched is a caller mix-up; log it and show nothing
            if (!BlockInfoFetchStateLogic.TryGetUnavailableKey(fetchState, out var key))
            {
                Debug.LogError($"[BlockInfoUnavailableLine] {name}: no unavailable text for {fetchState}");
                host.Clear();
                return;
            }
            // 欄の名前は描いた時点の言語で解く
            // The section name resolves in the current language; the one-second redraw follows language changes
            SetText(new UiText(key, new[] { new LocalizationTextParam("target", new UiText(_targetKey).Resolve()) }));
            ContextRowList.MountOnly(host, this);
        }
    }
}
