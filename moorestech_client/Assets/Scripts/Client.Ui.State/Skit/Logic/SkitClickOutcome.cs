namespace Client.Ui.State.Skit.Logic
{
    /// <summary>
    /// 会話窓のクリックと Enter の帰結（Web の SkitClickOutcome）
    /// Outcome of a dialogue-window click or Enter (Web SkitClickOutcome)
    /// </summary>
    public enum SkitClickOutcome
    {
        None,
        Reveal,
        Advance,
    }
}
