using System;
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// 辞書キーと名前付き引数だけを運ぶ文。表示の直前に Resolve で現在の言語へ解く
    /// A sentence carried as a dictionary key plus named params; Resolve turns it into the current language right before display
    /// </summary>
    public sealed class UiText
    {
        public LocalizationKey Key { get; }
        public IReadOnlyList<LocalizationTextParam> Params { get; }

        public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)
        {
            Key = key;
            Params = textParams;
        }

        public UiText(LocalizationKey key) : this(key, Array.Empty<LocalizationTextParam>())
        {
        }

        public string Resolve()
        {
            return Params.Count == 0 ? Localize.Get(Key) : Localize.GetFormatted(Key, Params);
        }
    }
}
