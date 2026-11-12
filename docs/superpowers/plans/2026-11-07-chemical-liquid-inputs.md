# 化学プラント液体入力専用化 Implementation Plan

> For the controller: moores-subagent-driven-developmentで実装を委譲する。焦点外のUI操作・他機械の録画を追加しない。

**Goal:** 指定材料でロケット燃料を作り、現行シリコン工程を維持し、離れた液体入力口を化学プラントに用意する。
**Architecture:** JSONの既存データとChemical Plants.prefabのみを変更する。既存ElectricMachineの2入力タンク機構を使う。
**Tech Stack:** Unity6000.3.8f1、v8 master、uloop。

## Requirements
- ロケット燃料: inputItems=[]、inputFluids=ガソリン100＋ナフサ200、outputItems=1、time=8。
- 高純度シリコンは最新6f693a6で削除済み。存在しない旧アイテム/レシピを復活させず、現行シリコン工程を変更しない。
- 化学プラントのinventory inputConnects=[]、outputConnects維持。inputSlotCount=1も維持して保存済み在庫容量を削らない。
- 液体入力0=(3,1,0)、入力1=(1,1,0)、双方-Z。タンクindex0/1と出力(2,1,10)+Zは維持。
- Chemical PlantsモデルをPR1615前96d3fdf082の版へ戻した上、2入力/1出力表示だけを論理セルへ合わせる。アイテム入口を追加せず、旧版の排出ベルトは残す。
- レシピ・論理口・表示を焦点検証。1マス離れた実パイプへ別液を送り混線なしとロケット1個/IPA出力を確認する。旧高純度シリコンを復活させていないことも確認。

## Global Constraints
- 既存worktreeを再使用、mainのdirty pinを触らない。マスタorigin/master6f693a6のユーザーバランス変更を維持。
- Prefabのプロパティ変更はUnity経由、既存版へのgit restore以外のYAML直接編集は禁止。meta手動作成禁止。
- C#変更を想定しない。必要になれば事前に報告しcompile必須。セーブ形式変更なし。
- マスタ変更は別PRを作り、push済みcommitへ本体pinを揃える。

## 配置と前例
各機械Prefabが自分の口表示を持つ既存方式。液体はVanillaElectricMachineTemplate/BlockTemplateUtilの既存2入力機構、レシピ移動は電気精密組立機への移管と同様にGUID維持。

## 設計検査記録
- 配置検査: 実施済み / 既存JSON・Prefabの責務内。新型/API/イベント0。
- Phase2.6: 実施済み / 強0・弱0・第3バケツ0、独立確認済み。

## Task 1: データと模型を修正
Files: ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json、machineRecipes.json、moorestech_client/Assets/AddressableResources/Block/Chemical Plants.prefab、.moorestech-external-revisions.json。
Interfaces: 現行マスタ/Prefabを消費し、同じGUIDの修正データと表示を出す。新規APIなし。
- [ ] masterの2JSONを対象要素のみ更新しcommitする。
- [ ] Chemical Plants.prefabのみ既存96d3fdf082へgit restoreしUnityで再import、旧入力口を保持、追加入口を離れたセルへ、液体出口も表示。全新設描画部品はBlockレイヤーにする。
- [ ] 素材レシピとポート座標/空セル、Item入力0接続、モデルのセル中心・向きをassertし再ロード。モデル本体は旧版へのrevert対象なので前回の位置補正を再導入しない。
- [ ] マスタ新commitへ本体pinを固定し、両repoの変更をcommit。

## Task 2: 焦点検証
- [ ] マスタをUnityでロードしバリデーション成功、該当レシピ2種のタンク数/slot数適合を確認。
- [ ] 独立した焦点シナリオでRocket/IPAの各1回加工と、離れた配管2系統の液体IDを確認。旧高純度Siのデータが増えていないことも照合。必要な給電と供給だけ、クリーンルーム・精密なカメラ角・全機械UI/削除は不要。
- [ ] unity-playmode-recorded-playtestでこの焦点シナリオ1本だけを録画し、数値と非空動画を確認。モデルの口も実画像で確認。

## Task 3: レビュー・PR
- [ ] moores-code-reviewで変更範囲をレビュー。目的を渡した独立評価2体で実物を判定。
- [ ] pr-createで両repoのPRを作成、全作業commit/push、ピン対応・コンフリクトなしを確認。記録はsdd-archiveへ退避。

## 判断記録（ADR）
ADR0098と.decisionsの今回ユーザー裁定を参照。口の配置は既存口維持と空セル1つを満たすagent前提。新しい型/APIなし、保存容量は減らさない。実際の液体流れに触れるので焦点録画1本を含める。
