import { Fragment } from "react";
import { useI18n, type TranslationKey } from "./i18nStore";

const SHORTCUT_MARKER = "\uE000";

type Props = {
  shortcut: string;
  translationKey: TranslationKey;
};

// 完全文の翻訳順を保ったままショートカット部分だけkbdで描画する
// Preserve the translated sentence order while rendering only the shortcut as kbd
export function LocalizedShortcutHint({ shortcut, translationKey }: Props) {
  const { t } = useI18n();
  const text = t(translationKey, { shortcut: SHORTCUT_MARKER });
  const markerIndex = text.indexOf(SHORTCUT_MARKER);

  // 辞書破損時も文言を失わずショートカットを先頭へ露出する
  // Keep the text visible and surface the shortcut first if a dictionary loses the marker
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
