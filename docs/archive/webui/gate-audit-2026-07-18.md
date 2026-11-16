# 全 uGUI ビュー ゲート化監査記録（2026-07-18）

Phase D タスク3の実施記録。`archive/ui-completeness-reaudit-plan.md` の全件 triage を、
**機械監査（`Client.Tests/WebUi/Gate/WebUiGateAuditTest`）+ 本記録** の形で恒久化した。

## 監査手法

1. **決定論チェック（テスト3本・green）**
   - `AllScreenSpaceUiFilesAreClassified`: 走査root配下の全 .cs が分類済みであること（未分類=新規スクリーンスペースuGUI追加=テスト失敗）
   - `GatedRootsContainGateToken`: 全ゲートルートが実際に `WebUiScreenGate.IsWebUiMode` を参照していること
   - `AllRulesMatchExistingFiles`: 分類ルールの腐敗（削除/リネーム追従漏れ）検出
2. 分類の正は `WebUiGateClassification.cs`（本監査の台帳を兼ねる）。以降の追加・変更はテストが強制する

## 走査root（スクリーンスペースUIが存在し得る領域）

`Client.Game/InGame/UI` / `InGame/Presenter/PauseMenu` / `InGame/BackgroundSkit` / `Client.Game/Skit` /
`InGame/Mining` / `InGame/Tutorial` / `Client.Skit` / `Client.CutScene`

## ゲートルート（webモード中に自己抑止する23ファイル）

インベントリ・常駐ホットバー・レシピビューア・研究ツリー・ビルドメニュー・BP名入力・
チャレンジリスト/HUD・コンテキストメニュー・クロスヘア・キーヒント・ツールチップ・削除バー・
ポーズメニュー/切断表示・列車インベントリ・スクリーン進捗バー・BGスキット（文字のみ。音声維持）・
通常スキット（UI Toolkit）・UIハイライト2種・キーガイドチュートリアル

## 状態外オーバーレイの別軸確認（UIStateEnum走査では拾えないもの）

| オーバーレイ | 処遇 |
|---|---|
| 常駐ホットバーHUD | HotBarView自身のゲートで抑止・Web側は常時表示 |
| 進行中チャレンジHUD | CurrentChallengeHudView ゲート（C1） |
| クロスヘア | CrosshairView ゲート（C2） |
| Ctrl+U 全UI非表示 | C#主権キー検知→ui.visibility Topic→Web全体unmount（C2） |
| バックグラウンドスキット | 文字表示のみゲート（音声はUnity再生のためルート維持・C4） |
| カットシーン退避 | game_state.current=CutScene でWeb全レイヤ退避（Ctrl+Uと独立判定・C4） |
| カーソル追従オーバーレイ | 個別移植不要と判定（grab=GrabOverlay・ContextMenu=Webポインタ座標へ吸収済み。uGUIフォールバック用に残置・C2棚卸し） |
| スクリーン進捗バー | 本監査で**二重表示ゲート漏れを検出し修正**（論理状態はProgressTopicデータ源として分離維持） |

## 対象外 allowlist（ゲート不要の根拠）

- **ワールド空間UI（uGUI維持方針）**: MapObjectPin / HudArrow / BlockPlacePreview / Mining採掘進捗のワールド表示 /
  マップオブジェクトHPバー / ブロック進捗バー / 電線設置ラベル
- **メインメニュー**: `Client.MainMenu` 別シーン（旧D3維持・スコープ外。走査root外）
- **デバッグUI**: 非出荷（UIStateEnum.Debug 含む）
- **Infra**: UIState状態機械・ゲート本体・Tutorial lifecycle基盤

## 結論

webモード中に表示され得る平面uGUIビューは全てゲート化済み。今後の新規uGUIは分類必須の
決定論チェックが監視する。残る確認は実機（PlayMode）での目視スモークのみ（TODO Phase D 最終検証）。
