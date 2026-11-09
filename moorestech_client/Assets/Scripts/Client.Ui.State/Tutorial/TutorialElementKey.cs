namespace Client.Ui.State.Tutorial
{
    public static class TutorialElementKey
    {
        // Web の tutorialElement.ts と同じ「session:element」
        // Same "session:element" as Web tutorialElement.ts
        public static string Of(string sessionId, string elementId)
        {
            return $"{sessionId}:{elementId}";
        }
    }
}
