import { Fragment } from "react";
import { useI18n, type TranslationKey } from "./i18nStore";

const SHORTCUT_MARKER = "\uE000";

// prefixはkbdを常に先頭へ置く様式、inlineは文言中のマーカー位置へ差し込む様式
// prefix always puts the kbd first; inline injects it at the marker position inside the sentence
type Props = {
  layout: "inline" | "prefix";
  shortcut: string;
  translationKey: TranslationKey;
};

// 完全文の翻訳順を保ったままショートカット部分だけkbdで描画する
// Preserve the translated sentence order while rendering only the shortcut as kbd
export function LocalizedShortcutHint({ layout, shortcut, translationKey }: Props) {
  const { t } = useI18n();
  const text = t(translationKey, { shortcut: SHORTCUT_MARKER });
  const markerIndex = layout === "prefix" ? -1 : text.indexOf(SHORTCUT_MARKER);

  // prefix様式の正規経路であり、辞書破損でマーカーを失ったinline文言を落とさない退避も兼ねる
  // The canonical route for the prefix layout, and also the fallback that keeps a marker-less inline text visible
  if (markerIndex < 0) {
    return (
      <Fragment>
        <kbd>{shortcut}</kbd>
        {text}
      </Fragment>
    );
  }

  return (
    <Fragment>
      {text.slice(0, markerIndex)}
      <kbd>{shortcut}</kbd>
      {text.slice(markerIndex + SHORTCUT_MARKER.length)}
    </Fragment>
  );
}
