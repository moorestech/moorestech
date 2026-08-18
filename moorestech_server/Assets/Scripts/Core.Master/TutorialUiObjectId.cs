using System.Collections.Generic;

namespace Core.Master
{
    // チュートリアルuiObjectIdの語彙の正本。静的キー集合と動的prefixを一元管理する
    // Single source of truth for tutorial uiObjectId vocabulary: static keys and dynamic prefixes
    public static class TutorialUiObjectId
    {
        // 動的uiObjectIdの書式「種別prefix:GUID」に使うprefix
        // Prefixes used by the dynamic uiObjectId form "kindPrefix:GUID"
        public const string BuildMenuBlockPrefix = "buildMenuBlock:";
        public const string ResearchNodePrefix = "researchNode:";

        // 静的（GUIDを伴わない）uiObjectIdキーの全集合
        // The full set of static (non-GUID) uiObjectId keys
        public static readonly IReadOnlyCollection<string> StaticKeys = new[]
        {
            "craftButton",
            "challengeHud",
            "hotbar",
        };
    }
}
