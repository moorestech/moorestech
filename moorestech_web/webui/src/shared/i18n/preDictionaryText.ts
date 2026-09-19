// 辞書が届く前に描かれる画面の文言表。キーとの対応はi18nがここ1本で持ち、t()の外へ出さない（公開barrelにも載せない）
// The copy for screens that render before the dictionary; i18n owns the key mapping here alone and never exposes it (not in the public barrel)
import { DictionaryIndependentText } from "./dictionaryIndependentText";
import type { VanillaLocalizationKey } from "./generated/localizationKeys";

// 値はlocalization.csvのenglish / japanese / germanを併記
// Each value joins the csv's english / japanese / german
export const PreDictionaryText: Partial<Record<VanillaLocalizationKey, string>> = {
  "ui.error.uiErrorOccurred": "A UI error occurred / UIエラーが発生しました",
  "ui.error.renderFailed": "There was a problem rendering the screen. Please reload. / 画面の描画中に問題が発生しました。再読み込みしてください。",
  "ui.error.reload": DictionaryIndependentText.reload,
};

// 表に無いキーは空文字。欠落マーカーで辞書未着の画面を埋めない
// A key absent from the table yields empty text rather than filling the pre-dictionary screen with markers
export function resolvePreDictionaryText(key: string): string {
  if (!Object.hasOwn(PreDictionaryText, key)) return "";
  return PreDictionaryText[key as VanillaLocalizationKey] ?? "";
}
