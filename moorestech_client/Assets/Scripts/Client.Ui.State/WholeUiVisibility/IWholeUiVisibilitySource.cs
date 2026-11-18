using System;

namespace Client.Ui.State.WholeUiVisibility
{
    /// <summary>
    /// 全体非表示の 2 つの源。どちらも購読時に今の値を流す
    /// The two sources of whole-UI hiding; both replay the current value on subscribe
    /// </summary>
    public interface IWholeUiVisibilitySource
    {
        IObservable<bool> ObserveUiRootVisible();
        IObservable<bool> ObserveCutScenePlaying();
    }
}
