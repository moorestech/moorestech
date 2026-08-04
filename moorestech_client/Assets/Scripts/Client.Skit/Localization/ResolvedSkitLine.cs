namespace Client.Skit.Localization
{
    public readonly struct ResolvedSkitLine
    {
        public readonly string SpeakerName;
        public readonly string DisplayBody;
        public readonly string VoiceSourceBody;

        public ResolvedSkitLine(
            string speakerName,
            string displayBody,
            string voiceSourceBody)
        {
            SpeakerName = speakerName;
            DisplayBody = displayBody;
            VoiceSourceBody = voiceSourceBody;
        }
    }
}
