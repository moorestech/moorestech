# moores-code-review 記録: map-autogen P1

- 日付: 2026-07-24
- 対象: feature/map-generator ブランチ全体 (base fe9c5514e .. head 73a62674b、その後修正で 4b967d677)
- 範囲: P1 サーバー側生成基盤 14コミット368ファイル(dirtyなし)

## 系統別1行判定
- 決定論: confirmed 10 (default-arg1=pre-existing据置 / file-too-long5=努力目標 / dir-file-limit4=既存flat規約+Jobs既知)
- domain-boundary/server-state-sync/datastore-access-separation/set-once-DI/redundant-member/precedent-alignment/unidirectional-flow/result-state: Critical なし
- master-data-defense: Critical なし (optional:true=Noneの空variant正当 / CreateEmpty=CleanRoom前例)
- type-driven-structure: 設計判断(WorldDataDirectory 2モード型)→ユーザー現状維持裁定
- implicit-cardinality/precedent: Warning(MasterHolder index=0単一mod=Task6既知)
- 汎用reviewer14: region-internal Critical(テスト3ファイル#region除去→修正) / centralization Critical(path重複→ServerDataMapJsonPath集約) / implicit-value Critical(Seed sentinel+MapMode magic string→修正+裁定) / dead-code Critical(MapVeinMaster.All→前例保持で棄却) / bug-fix+test-mutation: NextClusterId静的(設計判断)
- 比較演算子verifier: 21件機械修正適用
- Codex: C3末尾スラッシュ(実バグ→修正) / C2 spawn WorldOffset破棄(潜在バグ→フォローアップ) / load-path未統合テスト・exporter名一意性(フォローアップ)
- Fable(fable上限→opus代替): マージハザード=origin/master save refactor(WorldSaverForJson→WorldSaveCoordinator)へrebase要

## 適用修正(2コミット)
- 3ee6da613: path集約 / MapMode SSOT / 末尾スラッシュ正規化(Codex C3) / region除去3 / 比較演算子21
- 4b967d677: Seed int?化(sentinel解消) / NextClusterId ref局所化

## AskUserQuestion裁定
- Seed→int?化(採用) / WorldDataDirectory型→現状維持 / NextClusterId→ref局所化(採用)

## 破棄した指摘
- dead-code MapVeinMaster.All削除: ConnectToolMaster前例+P2露頭で使用のため保持
- default-arg LoadMainGame: pre-existing(base既存)・据置
- file-too-long/dir-file-limit: 努力目標/既存flat規約

## フォローアップ(非ブロック・別課題)
Codex C2 spawn WorldOffset(useSpawnOffsetSearch=true) / generated-world DI load統合テスト / exporter名一意性 / マージ前rebase(save refactor) / prefab veinGuidオーファン(P2/P3) / 複数mod選択+cross-master整合(Task6) / generation.json空guid20+addressablePath138(prefab→GUIDマッピング)
