namespace Client.Game.InGame.Tutorial
{
    // 同時currentになり得る複数challengeのsessionをまとめて公開する
    // Publish the sessions of every challenge that can be current at the same time
    public class TutorialPresentationData
    {
        public int Revision;
        public TutorialSessionData[] Sessions;
    }

    public class TutorialSessionData
    {
        public string TutorialSessionId;
        public string ChallengeId;
        public TutorialOverlayElementData[] Elements;
    }

    // kindを判別子にした単一要素列。Web側はdiscriminatedUnionで同じ形を受ける
    // A single element list discriminated by kind; the web side receives the same shape as a discriminatedUnion
    public abstract class TutorialOverlayElementData
    {
        public string Kind;
        public string ElementId;
    }

    public class TutorialOutlineElementData : TutorialOverlayElementData
    {
        public const string KindName = "outline";

        public TutorialOutlineElementData()
        {
            Kind = KindName;
        }

        public string AnchorId;
        public int PaddingPx;
        public bool BlocksPointerInput;
        // 枠線脇ラベルの文言キー元。nullなら枠線のみ（JSONではキーごと省略される）
        // Source GUID of the side label text; null means outline only (the key is omitted from JSON)
        public string LabelTutorialGuid;
    }

    public class TutorialDragGuideElementData : TutorialOverlayElementData
    {
        public const string KindName = "dragGuide";

        public TutorialDragGuideElementData()
        {
            Kind = KindName;
        }

        public string FromAnchorId;
        public string ToAnchorId;
    }

    // キー操作ヒント。uiState一致・skit中非表示の判定はWeb側が行う
    // Key-control hint; the web side decides uiState matching and hides it during skits
    public class TutorialKeyControlElementData : TutorialOverlayElementData
    {
        public const string KindName = "keyControl";

        public TutorialKeyControlElementData()
        {
            Kind = KindName;
        }

        public string TutorialGuid;
        public string KeyName;
        public string UiState;
    }
}
