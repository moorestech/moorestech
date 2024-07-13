namespace Game.Challenge.Config.TutorialParam
{
    public class UIHighLightTutorialParam : ITutorialParam
    {
        public const string TaskCompletionType = "uiHighLight";
        
        public readonly string HighLightUIObjectId;
        public readonly string HighLightText;
        
        public UIHighLightTutorialParam(string highLightUIObjectId, string highLightText)
        {
            HighLightUIObjectId = highLightUIObjectId;
            HighLightText = highLightText;
        }
        
        public static ITutorialParam Create(dynamic param)
        {
            string highLightObjectId = param.highLightUIObjectId;
            string highLightText = param.highLightText;
            
            return new UIHighLightTutorialParam(highLightObjectId, highLightText);
        }
    }
}