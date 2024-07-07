namespace Game.Challenge
{
    public class TutorialConfig
    {
        public string TutorialType { get; }
        public ITutorialParam Param { get; }
        
        public TutorialConfig(string tutorialType, ITutorialParam param)
        {
            TutorialType = tutorialType;
            Param = param;
        }
    }
    
    public delegate ITutorialParam TutorialParamLoader(dynamic param);
    
    public interface ITutorialParam
    {
    }
}