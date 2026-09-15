// 辞書が読めない状況で出す文言のためt()を通さない。辞書経由にすると表示自体が壊れる
// These strings render while the dictionary is unavailable, so they bypass t() by design
export const DictionaryIndependentText = {
  dictionaryLoadFailed: "Failed to load language data. / 言語データの読み込みに失敗しました。",
  reload: "Reload / 再読み込み",
  languageListLoading: "Loading… / 読み込み中…",

  // 言語選択ゲートは選ばせる対象が辞書そのものなので、応答の結末4通りも辞書を通さない（ADR 0040）
  // The language gate chooses the dictionary itself, so its four answer outcomes bypass the dictionary too (ADR 0040)
  languageSelectFailed: "Could not start. Please press again. / 開始できませんでした。もう一度押してください。",
  languageSelectAccepted: "Starting… / 開始しています…",
  languageSelectDisconnected: "Disconnected. Please press again once it reconnects. / 接続が切れています。つながったらもう一度押してください。",
  languageSelectNotClosed: "The language was chosen but this screen did not close. Please press again. / 言語は選ばれましたが画面が閉じません。もう一度押してください。",
} as const;
