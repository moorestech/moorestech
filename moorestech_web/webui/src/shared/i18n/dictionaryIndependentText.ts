// 辞書が読めない状況で出す文言のためt()を通さない。辞書経由にすると表示自体が壊れる
// These strings render while the dictionary is unavailable, so they bypass t() by design
export const DictionaryIndependentText = {
  dictionaryLoadFailed: "Failed to load language data. / 言語データの読み込みに失敗しました。",
  uiErrorOccurred: "A UI error occurred / UIエラーが発生しました",
  renderFailed: "There was a problem rendering the screen. Please reload. / 画面の描画中に問題が発生しました。再読み込みしてください。",
  reload: "Reload / 再読み込み",
  languageListLoading: "Loading… / 読み込み中…",
  languageSelectFailed: "Could not start. Please press again. / 開始できませんでした。もう一度押してください。",

  // 開始ゲートは辞書を配る WebUiGameBinder より前に出るため、辞書が来るまでこの文言で描く
  // The start gates render before WebUiGameBinder publishes the dictionary, so these carry them until it arrives
  playtestConsentTitle: "About this playtest / このプレイテストについて",
  playtestConsentBody: "When you send a report we upload your description, the last 2 minutes of gameplay video, a world snapshot, the packet log, the Unity log and a screenshot. A session summary and an event list are sent automatically when you quit. Nothing else leaves your PC. / 報告を送ると、説明文・直前2分の録画・ワールドのスナップショット・パケットログ・Unityログ・スクリーンショットが送信されます。終了時にはセッションの要約とイベント列が自動送信されます。これ以外はPCから送信されません。",
  playtestConsentAgree: "Got it / 了解",
  playtestConsentFailed: "Could not answer. Please press again. / 応答できませんでした。もう一度押してください。",
  crashGateTitle: "The game did not close normally last time / 前回ゲームが正常に終了しませんでした",
  crashGateBody: "Send the last session's recording, world snapshot, packet log, Unity log and crash dump to the developer? / 前回セッションの録画・ワールドのスナップショット・パケットログ・Unityログ・クラッシュダンプを開発者へ送りますか？",
  crashGatePlaceholder: "What were you doing when it stopped? (optional) / 止まったとき何をしていましたか？（任意）",
  crashGateSend: "Send / 送る",
  crashGateSkip: "Do not send / 送らない",
  crashGateRespondFailed: "Could not answer. Please press again. / 応答できませんでした。もう一度押してください。",
} as const;
