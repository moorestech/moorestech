// 辞書が読めない状況で出す文言のためt()を通さない。辞書経由にすると表示自体が壊れる
// These strings render while the dictionary is unavailable, so they bypass t() by design
export const DictionaryIndependentText = {
  dictionaryLoadFailed: "Failed to load language data. / 言語データの読み込みに失敗しました。",
  uiErrorOccurred: "A UI error occurred / UIエラーが発生しました",
  renderFailed: "There was a problem rendering the screen. Please reload. / 画面の描画中に問題が発生しました。再読み込みしてください。",
  reload: "Reload / 再読み込み",
  languageListLoading: "Loading… / 読み込み中…",
  languageSelectFailed: "Could not start. Please press again. / 開始できませんでした。もう一度押してください。",
} as const;
