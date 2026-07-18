# Phase C4 実行計画: スキット・チュートリアル・カットシーン（再設計から）

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-SKIT-1/2 / TUT-1 / CUT-1 相当。**単純移植不可 — 各系統とも再設計文書を先に書き、
ユーザー合意後に writing-plans 詳細計画 → 実装の3段階で進める。**
依存: A2（入力）。要素 ID 規約は全画面に波及するため C1〜C3 完了後の着手を推奨。

## 前提タスク: Web UI 要素 ID 規約（旧 INFRA-12）

チュートリアルの UI ハイライトは uGUI 階層走査
（`FindObjectsOfType<UIHighlightTutorialTargetObject>()`）で対象を見つける仕組みのため、
対象が DOM 化すると原理的に走査不能。先に **DOM 側の安定 ID 規約**（`data-tutorial-id` 等）を定め、
既存全 feature のハイライト対象になり得る要素へ付与する。
e2e セレクタとの共用も設計に含める（テスト安定化の副次効果）。

## 系統1: スキット（FEAT-SKIT-1）— 再設計対象の本丸

- 現状: `Client.Skit/` 約36ファイル。`SkitUI.cs`（**UI Toolkit/UIDocument** 製・162行）は描画ガワで、
  実体はストーリーコマンドインタプリタ（ShowText/Transition/Camerawork/Selection/Emote/Motion/Voice 等
  20+ コマンド）+ `SkitCharacterAnimator` 等。`SkitState` で全画面ブロッキング
- 再設計文書で決めること:
  1. **責務分割**: コマンドインタプリタ・カメラ・キャラ制御は Unity 残置。Web が担うのは
     テキストボックス・選択肢・スキップ/オート/非表示 UI のみ（薄い表示層）とするのが有力案
  2. **同期方式**: コマンド進行 → 表示内容を Topic 配信、クリック/選択/スキップを Action 返し。
     タイプライター表示等の演出をどちら側で持つか
  3. **ボイス**: CEF の音声専有問題（旧 INFRA-10）との関係。音声は Unity 側再生に寄せれば回避可能か検証
  4. 立ち絵は Unity 側描画に残すか、画像配信（アイコン配信エンドポイントの拡張）で Web に出すか
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

1. 要素 ID 規約の設計 + 既存 feature への適用（独立コミット）
2. GameStateType Topic 化 + カットシーン退避（小・先行）
3. バックグラウンドスキット実装（同期方式の実証）
4. スキット再設計文書 → 合意 → 詳細計画 → 実装
5. チュートリアル再設計文書 → 合意 → 詳細計画 → 実装

## 完了条件

- スキット/バックグラウンドスキットの会話・選択肢・スキップが Web 表示で従来内容を再生できる
- チュートリアルが Web UI 要素をハイライトし、ワールド系ピン/矢印は従来通り
- カットシーン中に Web UI が退避する
- 各系統の PlayMode 実機確認（該当チャレンジ/ストーリーを進行させる録画付きプレイテスト）
