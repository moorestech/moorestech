using System;
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// 表示の直前に現在の言語へ解く文。辞書キー・コンテンツ辞書キー・解決済みの文の 3 種を運ぶ（設計書 A-40）
    /// A sentence resolved right before display; carries a dictionary key, a content key or already-resolved text (design A-40)
    /// </summary>
    public sealed class UiText
    {
        public UiTextSource Source { get; }

        // Source が Dictionary のときだけ意味を持つ
        // Meaningful only when Source is Dictionary
        public LocalizationKey Key { get; }
        public IReadOnlyList<LocalizationTextParam> Params { get; }

        // Source が Content のときだけ意味を持つ
        // Meaningful only when Source is Content
        public ContentLocalizationKey ContentKey { get; }

        // Source が Verbatim のときだけ意味を持つ
        // Meaningful only when Source is Verbatim
        public string VerbatimText { get; }

        public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)
            : this(UiTextSource.Dictionary, key, textParams, default, null)
        {
        }

        public UiText(LocalizationKey key) : this(key, Array.Empty<LocalizationTextParam>())
        {
        }

        private UiText(UiTextSource source, LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams, ContentLocalizationKey contentKey, string verbatimText)
        {
            Source = source;
            Key = key;
            Params = textParams;
            ContentKey = contentKey;
            VerbatimText = verbatimText;
        }

        // アイテム名・研究名・チャレンジのカテゴリ名など、マスタの GUID から導くキー
        // Keys derived from master GUIDs, such as item, research and challenge-category names
        public static UiText FromContent(ContentLocalizationKey key)
        {
            return new UiText(UiTextSource.Content, default, Array.Empty<LocalizationTextParam>(), key, null);
        }

        // サーバーのモーダル要求の題・本文・ボタン文言など、送り手が解決済みの文。訳さない
        // Text the sender already resolved (server modal titles, bodies, button labels); never translated
        public static UiText FromVerbatim(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text), "[UiText] verbatim text must not be null");
            return new UiText(UiTextSource.Verbatim, default, Array.Empty<LocalizationTextParam>(), default, text);
        }

        public string Resolve()
        {
            // 種ごとの口で解く。言語変更での解き直しは LocalizedLabel と LocalizedTextBinding が呼ぶ
            // Resolve through the matching path; LocalizedLabel and LocalizedTextBinding call this again on language change
            switch (Source)
            {
                case UiTextSource.Dictionary:
                    return Params.Count == 0 ? Localize.Get(Key) : Localize.GetFormatted(Key, Params);
                case UiTextSource.Content:
                    return Localize.GetContent(ContentKey);
                case UiTextSource.Verbatim:
                    return VerbatimText;
                default:
                    throw new InvalidOperationException($"[UiText] unknown source {Source}");
            }
        }
    }

    public enum UiTextSource
    {
        Dictionary,
        Content,
        Verbatim,
    }
}
