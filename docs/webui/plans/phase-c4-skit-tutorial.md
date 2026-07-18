# Phase C4 実行計画: スキット・チュートリアル・カットシーン（再設計から）

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-SKIT-1/2 / TUT-1 / CUT-1 相当。**単純移植不可 — 各系統とも再設計文書を先に書き、
ユーザー合意後に writing-plans 詳細計画 → 実装の3段階で進める。**
依存: A2（入力）。要素 ID 規約は全画面に波及するため C1〜C3 完了後の着手を推奨。

## 前提: Web UI 要素 ID 規約（A5 で策定済みの前提）

チュートリアルの UI ハイライトは uGUI 階層走査
（`FindObjectsOfType<UIHighlightTutorialTargetObject>()`）で対象を見つける仕組みのため、
対象が DOM 化すると原理的に走査不能。**規約（`data-tutorial-anchor`）は A5 で策定し、
C1〜C3 の各画面は実装時に付与済み**の前提。本 Phase で行うのは:
1. それ以前に実装済みだった10画面への anchor 付与の棚卸し
2. **anchor registry の実装**: ID が存在するだけでは SPA 再レンダーに耐えない
   （対象が未 mount・画面外・Portal 内・仮想化リスト内・Mantine 再構築直後で消失/誤配置する）。
   Web 側で mount/unmount・可視性を Mutation/Resize/scroll 追従で監視し、Unity へ
   `ready / not-found / hidden` を ack する**宣言的ハイライト状態**として実装する
※ `data-testid` とは分離する（A5 規約。チュートリアル ID はゲーム契約・test ID はテスト都合）

## 系統1: スキット（FEAT-SKIT-1）— 再設計対象の本丸

- 現状: `Client.Skit/` 約36ファイル。`SkitUI.cs`（**UI Toolkit/UIDocument** 製・162行）は描画ガワで、
  実体はストーリーコマンドインタプリタ（ShowText/Transition/Camerawork/Selection/Emote/Motion/Voice 等
  20+ コマンド）+ `SkitCharacterAnimator` 等。`SkitState` で全画面ブロッキング
- 再設計文書で決めること:
  1. **責務分割**: コマンドインタプリタ・カメラ・キャラ制御は Unity 残置。Web が担うのは
     テキストボックス・選択肢・スキップ/オート/非表示 UI のみ（薄い表示層）とするのが有力案
  2. **同期方式**: 「コマンド進行イベントの逐次配信」ではなく**完全 snapshot 配信**とする —
     `{sessionId, sceneRevision, presentationState(本文/選択肢/auto/skip状態), allowedIntents}` を
     状態 Topic として配信し、WS 切断中に進行しても再接続で最新 snapshot から復元できる形にする
     （A4 規約準拠）。タイプライター演出をどちら側で持つかも決める
  3. **Action の冪等化**: 会話送り・選択肢はダブルクリック/キーリピート/再接続後の遅延 Action で
     二重進行し得る。全操作に `sessionId + revision（+選択 id）` を付け、Unity 側で現在コマンドと
     照合して古い要求を破棄する
  4. **ボイス**: CEF の音声専有問題（旧 INFRA-10）の**決定責務は本 Phase**。音声を Unity 側再生に
     寄せて回避できるか検証し、方式・試験・DoD を再設計文書に明記する
  5. 立ち絵は Unity 側描画に残すか、画像配信（A3 の汎用アセット配信規約）で Web に出すか
- **バックグラウンドスキット（FEAT-SKIT-2）は先行実装可**: `InGame/BackgroundSkit/` は
  Text コマンドのみ（キャラ名 + 本文 + ボイス）の GameScreen オーバーレイで遥かに軽い。
  スキット再設計の同期方式の実証台として最初に作る

## 系統2: チュートリアル（FEAT-TUT-1）

- 現状: `InGame/Tutorial/` 約800行。`TutorialManager` 配下に UIHighlight / KeyControl /
  ItemViewHighLight / BlockPlacePreview の各 Manager + MapObjectPin / HudArrow。チャレンジ進行で発火
- 再設計文書で決めること:
  1. DOM ハイライトの実現方式（要素 ID 規約 + Web 側ハイライトオーバーレイ。発火は C# 主権で
     Topic に「ハイライト対象 ID + 種別」を流す）
  2. キーガイド系チュートリアルの Web 表示（C2 のキーヒント基盤と統合）
  3. **MapObjectPin / HudArrow / BlockPlacePreview はワールド空間系のため uGUI/Unity 残置**
     （方針どおり移行対象外。Web 側と発火タイミングの整合だけ取る）
- 依存: チャレンジ Topic（C1）が発火元

## 系統3: カットシーン（FEAT-CUT-1）

- 現状: `Client.CutScene/TimelinePlayer.cs` + `GameStateType.CutScene`（UIState とは別の第2状態機械）。
  Timeline 再生中は HUD を一括退避
- 作業: **GameStateType の Topic 化**（旧 INFRA-6 残タスク）→ Web 側でカットシーン中は
  全 UI レイヤーを退避。スキップ UI があれば Action 化。映像/3D は Unity 残置
- 規模は小さい。GameStateType Topic は全 UI 非表示（C2）とも共用できるため先行実装してよい

## 進め方

1. 既存10画面への anchor 付与棚卸し + anchor registry 実装（独立コミット。規約自体は A5 が前提）
2. GameStateType Topic 化 + カットシーン退避（小・先行）
3. バックグラウンドスキット実装（同期方式の実証）
4. スキット再設計文書 → 合意 → 詳細計画 → 実装
5. チュートリアル再設計文書 → 合意 → 詳細計画 → 実装

## 完了条件

- スキット/バックグラウンドスキットの会話・選択肢・スキップが Web 表示で従来内容を再生できる
- チュートリアルが Web UI 要素をハイライトし、ワールド系ピン/矢印は従来通り
- カットシーン中に Web UI が退避する
- 各系統の PlayMode 実機確認（該当チャレンジ/ストーリーを進行させる録画付きプレイテスト）
