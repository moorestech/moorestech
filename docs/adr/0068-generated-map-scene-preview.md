# 現行生成マップをPlay不要の一時プレビューで確認する

## ユーザー裁定

- 現行ゲームの生成結果をUnityのSceneビューでPlayせず確認する。生成設定の編集UIは今回含めない。
  出所: ユーザー裁定 2026-09-22 原文「tmp/MapMaking にあるみたいに、生成マップのプレビューをシーンでチェックできるようにしてほしい」→ 選択「生成結果の確認」への回答「1」。
- 生成したプレビューは一時的なものとし、生成物をシーンとして保存して開き直す機能は含めない。
  出所: ユーザー裁定 2026-09-22 質問「生成したプレビューは、Unityを閉じた後もシーンとして開き直せる必要がありますか？ 保存する場合は地形データや配置物も保存対象になります。」への回答「一時プレビューでok」。
- 自明な確認質問をしない。
  出所: ユーザー裁定 2026-09-22「自明すぎる質問しないで」。これは下記agent前提の内容への承認ではない。

実際に提示した代替案は、生成設定の編集まで含める案と、生成物を保存する案。どちらも今回採らない。

## agent前提と実装の境界

- 既定の生成条件と全域を使う。seedはDefaultGeneratedWorldProvisioner.DefaultGeneratedSeed、マスタの読込先はServerDirectory.GetDirectory()に従う。任意seed/既存セーブの選択UIは足さない。
  出所: agent前提（Server.Boot/DefaultGeneratedWorldProvisioner.cs、既存MapAuthoringImporterのマスタ読込。閲覧範囲という裁定から追加設定UIを増やさない）。
- 地形の起伏・地表テクスチャ・detailの草・mapObjectの木/岩/露頭を表示する。生成されたスポーン位置も特定可能にする。
  出所: agent前提（CONTEXT.mdの「生成結果」、MapMakingの生成物、現行TerrainRuntimeBuilderとMapAuthoringImporter）。地形だけへ縮小しない。
- 生成/再生成、閉じるの操作を提供する。Sceneビューで通常の移動・俯瞰ができることを実装検証する。
  出所: agent前提（一時プレビュー裁定、MapMakingの生成ボタン）。
- ゲーム用の生成処理と生成結果の描画処理を再利用する。MapMakingの生成ロジックのコピーや新しい生成仕様は作らない。生成マスタや既存セーブは変更しない。
  出所: agent前提（ADR 0025、WorldTerrainSession、WorldSnapshotBundler）。キャッシュの利用判断は既存生成システム内部に閉じる。
- 編集時の配置はMapAuthoringImporterと同役割であり、プレイヤー状態・通信・採掘中の状態を初期化しない。全量の配置と姿勢を再現し、失敗は欠損件数とログに残す。
  出所: agent前提（MapAuthoringImporterとMapObjectLayoutInstantiatorの役割比較）。
- 通常の景観を確認できる照明を用意する。地下鉱脈やバイオーム等の新しい可視化モード、ゲーム起動への導線、マップ編集/書き出しは追加しない。
  出所: agent前提（今回の閲覧目的へのスコープ限定）。照明の厳密なゲーム同一性はユーザー裁定ではない。

## 呼び出し側の線引き

Editor側は生成入口への依頼、公開された生成結果の表示、プレビュー資源の寿命管理を担う。seedの再定義、生成パラメータの組み直し、キャッシュのヒット判定、配置の再抽選は行わない。地形や配置が生成途中で欠損している状態を完了として表示しない。

## 未裁定の表示場所

専用の一時シーンで見るか、作業中のゲームシーンへ一時配置して既存の照明や背景と合わせて見るかは未裁定。一時プレビューという回答だけから専用シーンを採択した扱いにはしない。

専用シーンの場合はPreviewSceneStageが候補。既存のMapObjectWrapperGeneratorMenuはNewPreviewSceneで作業シーンを汚さず生成する前例だが、ユーザーにSceneビューで見せる用途ではStageへの切り替えまで必要になる。[Unity公式のPreviewSceneStage](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.PreviewSceneStage.html)を参照。ゲームシーンへの追加の場合は既存の地形と照明との重なり、閉じた後の復元を設計する。

## 独立質問の照合

2026-09-22にgenerateの14問をfilterで照合。Q1/Q2/Q5/Q12がユーザー裁定で解決し、10問は未裁定と判定された。agent前提をユーザー裁定として消し込んでいない。設定編集・試遊・既存セーブ復元等の追加機能を求める質問は「自明すぎる質問しないで」という指示に従い繰り返さず、上記のagent前提として分離する。表示場所は既存シーンとの合成結果が変わるため質問する。writing-plansにはまだ接続していない。

## 検証対象

EditModeで地形/草/配置物が出ること、同じ条件で本番生成結果と位置/姿勢/個数が一致すること、再生成で重複しないこと、終了と失敗後に一時資源が残らないこと、作業シーンとセーブが変わらないことを確認する。現時点では実測していない。

## リンク

- [.decisions](../../.decisions/2026-09-22-生成マップはPlay不要の一時シーンプレビューで確認する.md)
- [既存の生成境界](0025-generation-system-exposes-results-only.md)
- bd: moorestech-uvrb
