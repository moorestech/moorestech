namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 機械以外の文脈区画を出すか（Web SectionStackView の hasContextSections の写し。機械は専用の区画で出すので偽）
    /// Whether the non-machine context section shows (port of Web SectionStackView's hasContextSections; machines show their own, so false)
    /// </summary>
    public static class ContextSectionVisibility
    {
        public static bool Shows(bool isMachine, BlockContextViewModel context)
        {
            if (isMachine) return false;
            return context.HasAny;
        }
    }
}
