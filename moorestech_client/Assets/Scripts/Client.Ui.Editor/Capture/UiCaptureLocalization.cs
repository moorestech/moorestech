namespace Client.Ui.Editor.Capture
{
    /// <summary>
    /// 撮影の前に辞書を初期化し直すか。テストが足した辞書で撮るときは KeepCurrentDictionary
    /// Whether to reinitialize the dictionary before capturing; use KeepCurrentDictionary to capture with test-added entries
    /// </summary>
    public enum UiCaptureLocalization
    {
        InitializeDictionary,
        KeepCurrentDictionary,
    }
}
