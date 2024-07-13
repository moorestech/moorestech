namespace Game.Challenge.Config.TutorialParam
{
    public class UIHighLightTutorialParam : ITutorialParam
    {
        public const string TaskCompletionType = "uiHighLight";
        
        public readonly string HighLightUI;
        public readonly string HighLightText;
        
        public UIHighLightTutorialParam(string highLightUI, string highLightText)
        {
            HighLightUI = highLightUI;
            HighLightText = highLightText;
        }
        
        public static ITutorialParam Create(dynamic param)
        {
            string highLightUI = param.highLightUI;
            string highLightText = param.highLightText;
            
            return new UIHighLightTutorialParam(highLightUI, highLightText);
        }
    }
}