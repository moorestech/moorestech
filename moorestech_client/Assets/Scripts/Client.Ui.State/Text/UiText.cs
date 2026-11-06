using System;
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// 現在の言語へ解く文（3種）
    /// Text resolved to the current language (3 kinds)
    /// </summary>
    public sealed class UiText
    {
        internal UiTextSource Source { get; }

        // Dictionary時のみ
        // Dictionary only
        internal LocalizationKey Key { get; }
        internal IReadOnlyList<LocalizationTextParam> Params { get; }

        // Content時のみ
        // Content only
        private ContentLocalizationKey ContentKey { get; }

        // Verbatim時のみ
        // Verbatim only
        private string VerbatimText { get; }

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

        // 引数を先頭に 1 つ足した写しを返す。同名の引数は置き換える
        // Returns a copy with one param prepended; a same-named param is replaced
        public UiText WithLeadingParam(string name, string value)
        {
            // 引数は辞書キーの文にだけ効く。ほかの種で呼ぶのは呼び出し側の誤りなので作る時点で止める（設計書 A-40）
            // Params apply only to dictionary-key text; any other source is a caller bug, so stop here (design A-40)
            if (Source != UiTextSource.Dictionary) throw new InvalidOperationException($"[UiText] WithLeadingParam needs a dictionary key, got {Source}");
            var textParams = new List<LocalizationTextParam>(Params.Count + 1) { new(name, value) };
            foreach (var param in Params)
            {
                if (param.Name != name) textParams.Add(param);
            }
            return new UiText(Key, textParams);
        }

        public string Resolve()
        {
            // 種ごとの口で解く。再解決はLocalizedLabelが呼ぶ
            // Resolve per kind; LocalizedLabel re-resolves
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

    internal enum UiTextSource
    {
        Dictionary,
        Content,
        Verbatim,
    }
}
