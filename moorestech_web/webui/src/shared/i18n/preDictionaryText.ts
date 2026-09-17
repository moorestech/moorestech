// 辞書が届く前に描かれる画面の文言表。キーとの対応はi18nがここ1本で持ち、t()の外へ出さない（公開barrelにも載せない）
// The copy for screens that render before the dictionary; i18n owns the key mapping here alone and never exposes it (not in the public barrel)
import { DictionaryIndependentText } from "./dictionaryIndependentText";
import type { VanillaLocalizationKey } from "./generated/localizationKeys";

// 開始ゲートは辞書を配る WebUiGameBinder より前に出る。値はlocalization.csvのenglish / japanese / germanを併記したもの
// The start gates render before WebUiGameBinder publishes the dictionary; each value joins the csv's english / japanese / german
export const PreDictionaryText: Partial<Record<VanillaLocalizationKey, string>> = {
  "ui.error.uiErrorOccurred": "A UI error occurred / UIエラーが発生しました",
  "ui.error.renderFailed": "There was a problem rendering the screen. Please reload. / 画面の描画中に問題が発生しました。再読み込みしてください。",
  "ui.error.reload": DictionaryIndependentText.reload,

  "ui.playtest.consent.title": "About this playtest / このプレイテストについて / Über diesen Playtest",
  "ui.playtest.consent.body": "When you send a report we upload your description, the last 2 minutes of gameplay video, a world snapshot, the packet log, the Unity log and a screenshot. A session summary and an event list are sent automatically when you quit. Nothing else leaves your PC. / 報告を送ると、説明文・直前2分の録画・ワールドのスナップショット・パケットログ・Unityログ・スクリーンショットが送信されます。終了時にはセッションの要約とイベント列が自動送信されます。これ以外はPCから送信されません。 / Wenn du eine Meldung sendest, werden dein Text, die letzten 2 Minuten Spielaufnahme, ein Welt-Snapshot, das Paketprotokoll, das Unity-Log und ein Screenshot übertragen. Beim Beenden werden automatisch eine Sitzungszusammenfassung und eine Ereignisliste gesendet. Sonst verlässt nichts deinen PC.",
  "ui.playtest.consent.agree": "Got it / 了解 / Verstanden",
  "ui.playtest.crashGate.title": "The game did not close normally last time / 前回ゲームが正常に終了しませんでした / Das Spiel wurde letztes Mal nicht normal beendet",
  "ui.playtest.crashGate.body": "Send the last session's recording, world snapshot, packet log, Unity log and crash dump to the developer? / 前回セッションの録画・ワールドのスナップショット・パケットログ・Unityログ・クラッシュダンプを開発者へ送りますか？ / Die Aufzeichnung, den Welt-Snapshot, das Paketprotokoll, das Unity-Log und den Absturzbericht der letzten Sitzung an den Entwickler senden?",
  "ui.playtest.crashGate.placeholder": "What were you doing when it stopped? (optional) / 止まったとき何をしていましたか？（任意） / Was hast du gemacht als es abstürzte? (optional)",
  "ui.playtest.crashGate.send": "Send / 送る / Senden",
  "ui.playtest.crashGate.skip": "Do not send / 送らない / Nicht senden",

  // 応答の結末はどちらの開始ゲートでも同じ4通りなので、文言も1組だけ持つ
  // Both start gates end an answer in the same four ways, so the copy exists as a single set
  "ui.playtest.gate.respondFailed": "Could not answer. Please press again. / 応答できませんでした。もう一度押してください。 / Antwort fehlgeschlagen. Bitte erneut drücken.",
  "ui.playtest.gate.answerAccepted": "Your answer was received. This closes shortly. / 応答を受け付けました。まもなく閉じます。 / Deine Antwort wurde empfangen. Dies schließt sich gleich.",
  "ui.playtest.gate.disconnected": "Disconnected. Please press again once it reconnects. / 接続が切れています。つながったらもう一度押してください。 / Verbindung getrennt. Bitte erneut drücken sobald sie wiederhergestellt ist.",
  "ui.playtest.gate.notClosed": "The answer arrived but this screen did not close. Please press again. / 応答は届きましたが画面が閉じません。もう一度押してください。 / Die Antwort kam an aber dieser Bildschirm schloss nicht. Bitte erneut drücken.",
};

// 表に無いキーは空文字。欠落マーカーで辞書未着の画面を埋めない
// A key absent from the table yields empty text rather than filling the pre-dictionary screen with markers
export function resolvePreDictionaryText(key: string): string {
  if (!Object.hasOwn(PreDictionaryText, key)) return "";
  return PreDictionaryText[key as VanillaLocalizationKey] ?? "";
}
