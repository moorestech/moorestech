using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public static class TutorialAnchorIdMapper
    {
        // 動的アンカーIDのprefix。Web側TutorialAnchorDynamicPrefixesと対応する
        // Dynamic anchor ID prefix; must mirror Web's TutorialAnchorDynamicPrefixes
        public const string ItemAnchorPrefix = "recipe.item-";

        // マスタ側uiObjectIdの動的書式「種別:GUID」のprefix
        // Prefixes of the master-side dynamic uiObjectId form "kind:GUID"
        public const string BuildMenuBlockObjectIdPrefix = "buildMenuBlock:";
        public const string ResearchNodeObjectIdPrefix = "researchNode:";

        private static readonly IReadOnlyDictionary<string, string> UiAnchors =
            new Dictionary<string, string>
            {
                { "craftButton", "recipe.craft-button" },
                { "challengeHud", "challenge.current-hud" },
                { "hotbar", "hotbar.hud" },
            };

        public static string FromUiObjectId(string uiObjectId)
        {
            // 動的対象はGUIDを小文字化してWeb側の動的anchor生成規則へ揃える
            // Dynamic targets lower-case the GUID to match the web-side dynamic anchor rules
            if (uiObjectId.StartsWith(BuildMenuBlockObjectIdPrefix))
                return $"build-menu.entry-block-{uiObjectId.Substring(BuildMenuBlockObjectIdPrefix.Length).ToLowerInvariant()}";
            if (uiObjectId.StartsWith(ResearchNodeObjectIdPrefix))
                return $"research.node-{uiObjectId.Substring(ResearchNodeObjectIdPrefix.Length).ToLowerInvariant()}";
            return UiAnchors[uiObjectId];
        }

        public static string FromItemId(int itemId)
        {
            return $"{ItemAnchorPrefix}{itemId}";
        }

        // マスタ照合テスト用にマスタ側uiObjectIdの既知判定を公開する
        // Exposes known-key lookup for the master-data cross-check test
        public static bool IsKnownUiObjectId(string uiObjectId)
        {
            if (uiObjectId.StartsWith(BuildMenuBlockObjectIdPrefix))
                return Guid.TryParse(uiObjectId.Substring(BuildMenuBlockObjectIdPrefix.Length), out _);
            if (uiObjectId.StartsWith(ResearchNodeObjectIdPrefix))
                return Guid.TryParse(uiObjectId.Substring(ResearchNodeObjectIdPrefix.Length), out _);
            return UiAnchors.ContainsKey(uiObjectId);
        }

        // Web側フィクスチャとの突合テスト用に、静的マッピングの出力アンカーID全件を公開する
        // Exposes every statically mapped anchor ID for the parity test against the Web-side fixture
        public static IReadOnlyCollection<string> AllMappedAnchorIds => UiAnchors.Values.ToArray();
    }
}
