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
}
