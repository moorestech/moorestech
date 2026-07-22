using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public static class TutorialAnchorIdMapper
    {
        // 動的アンカーIDのprefix。Web側TutorialAnchorDynamicPrefixesと対応する
        // Dynamic anchor ID prefix; must mirror Web's TutorialAnchorDynamicPrefixes
        public const string ItemAnchorPrefix = "recipe.item-";

        private static readonly IReadOnlyDictionary<string, string> UiAnchors =
            new Dictionary<string, string>
            {
                { "craftButton", "recipe.craft-button" },
            };

        public static string FromUiObjectId(string uiObjectId)
        {
            return UiAnchors[uiObjectId];
        }

        public static string FromItemId(int itemId)
        {
            return $"{ItemAnchorPrefix}{itemId}";
        }

        // マスタ照合テスト用にマスタ側uiHighLightObjectIdの既知判定を公開する
        // Exposes known-key lookup for the master-data cross-check test
        public static bool IsKnownUiObjectId(string uiObjectId)
        {
            return UiAnchors.ContainsKey(uiObjectId);
        }

        // Web側フィクスチャとの突合テスト用に、静的マッピングの出力アンカーID全件を公開する
        // Exposes every statically mapped anchor ID for the parity test against the Web-side fixture
        public static IReadOnlyCollection<string> AllMappedAnchorIds => UiAnchors.Values.ToArray();
    }
}
