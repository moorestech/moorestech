namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// PlaytestStartVerdict.RefusedWithNotice に紐づくテスター向けの文言。他の判定では読まない。
    /// The tester-facing text tied to PlaytestStartVerdict.RefusedWithNotice; it is not read for any other verdict.
    /// </summary>
    public readonly struct PlaytestStartRefusal
    {
        public readonly string NoticeText;

        internal PlaytestStartRefusal(string noticeText)
        {
            NoticeText = noticeText;
        }
    }
}
