# map-autogen-P2 (va:mapData Layout + mapObject実行時Instantiate化) レビュー記録 (2026-07-25)

<!-- One review run = one immutable file; only the post-merge outcome section may be appended later. -->

## 対象
- base: `f659d613b` / reviewed head: `b76b941db`（ゲート内fix適用後。レビュー時のコード対象patchは `f659d613b..86d313a2e` の.cs/.yml/.json、prefab/assetは per-task済で除外）
- ブランチ: feature/map-generator (tree2 worktree) / PR: #1061 (feature/map-generator→master・P2反映はT8で)
- context要約 — ゴール: mapObjectをシーンベイクから実行時Instantiate化・読み取り専用`va:mapData`(Layout)でMapInfoJson射影・addressablePath必須追加。非目標: vein視覚(Task6除外)・terrain(P3)・旧セーブ互換。許容: mapVeins未消費(契約テスト付)・lost-update MP限定・実行時検証T8繰延。制約: addressablePath必須(optional/??/prefill禁止)・Mode=enum+throw・読み取り専用=3点セット不要・partial/try-catch/デフォルト引数禁止・200行/10ファイル・UniRx。

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | なし(P2起因) | file-too-long(VanillaApiWithResponse401=努力目標)・dir-limit(PacketResponse50/PacketTest44=既存)のみ。comparison_operator0・schema_optional_true0(addressablePath必須確認)・region_internal0 |
| datastore-access-separation | なし | client GameObjectレジストリ前例に一致・public面はむしろ縮小 |
| hardcoded-content-enumeration | なし | 逆にベイク列挙を撤去しmaster駆動へ寄せる変更 |
| implicit-cardinality-assumption | Warning | MapObjectMasterがdup-guid未検証(Array.Find先頭勝ち)・P2が用途の重みを引上げ。MapVeinMaster同型でfail-loud化余地(P2非起因) |
| master-data-defense | Critical(データ) | v8 map.jsonにaddressablePath 7件欠落→boot時throw。スキーマ設計(必須+default=Editor専用)自体は正 |
| precedent-alignment(fable) | Critical(fixture) | EditMode fixture Stone/clay未登録address→instantiate中断予測。プロトコル/解決キャッシュ前例は完全一致 |
| redundant-member-duplication | なし | バッキング+素通し二重保持なし |
| server-state-sync | なし | 読み取り専用射影・可変状態は既存va:mapObjectInfo流用・3点セット健全 |
| set-once-dependency-injection | なし | SetRuntimeIdentityはMonoBehaviour値注入(正当)・protocolはctor注入 |
| type-driven-structure | なし | enum+throw・DTO配置とも正解形 |
| core-any-file-directory-organization | なし(Warning既存) | 3新規は各1ファイル別ディレクトリ・前例一致 |
| core-any-implicit-value-meaning | Warning | マジック100(→gate内でconst抽出済) |
| core-any-test-mutation-effectiveness | Warning | client実行時Instantiate移行にCIテスト0(T8 assert依存・合意繰延) |
| core-any-user-intent-fulfillment | Warning | snapshot直index(layout⊆snapshot前提が未文書) |
| core-cs-architecture-lifecycle | Warning×2+設計判断 | ①生成ループ非隔離truncation ②初期化ゲート未接続(IInitialEventApplyWaitTarget) |
| core-cs-bug-fix-intent | Warning×2+設計判断 | truncation・server/client失敗ポリシー非対称。skip-and-continue推奨 |
| core-cs-caller-orchestration-minimization | Warning+設計判断 | datastoreが構築手続き一式inline(Factory抽出議論)・element二重解決 |
| core-cs-centralization-duplication | 設計判断(現状維持推奨) | prefab解決+キャッシュ3箇所反復も前例指定・4個目まで抽出不要 |
| core-cs-dead-code-and-scope | Critical | HierarchyOrderUtility参照0(削除)。vein GameObject 3プロパティ孤児化(Warning) |
| core-cs-region-internal | Critical | 単一呼び出しInstantiateMapObjectsFromLayoutAsyncをConstruct内localize推奨 |
| core-cs-result-state-propagation | Warning×3+設計判断 | truncation・非対称・ガードoverclaim(A/B/C) |
| core-cs-schema-design | なし | addressablePath必須+default=Editor専用は教科書的に正 |
| core-cs-unidirectional-flow | なし | 読み取り専用DTO・ハブ/逆流/View戻り値なし |
| Codex外部監査 | Critical/High/Medium | Crit=v8 addressablePath欠落・High=EditMode Stone/clay・Med=.Forget()が部分マップ公開 |
| Fable全般 | Critical×3 | v8 addressablePath・EditMode fixture・**v8 world stale guid e76e6b65×12(新発見)** |
| comment-rationale-guard | なし | const移設で根拠喪失なし(両所に保全) |
| comment-convention-guard | ラベル分岐 | 名前重複0件。長さ目安超17件は情報コメント/根拠で残置(目安はソフト・per-task済) |

## 適用した修正
- HierarchyOrderUtility.cs+.meta削除(R-deadcode Critical・全ブランチ参照0裏取り) → コミット `b76b941db`
- マジック100→const `FrameYieldObjectInterval`抽出(R-implicitval Warning) → コミット `db89d3c32`(fix waveに同梱)

## 設計判断（AskUserQuestion裁定）
- Q1: 生成ループのper-item失敗処理(6系統一致でtruncationをバグ判定) / 選択肢: skip+continue / boot伝播fail-fast / 現状+T8データ / **裁定: A skip+continue** / 適用: `ResolvePrefabOrNull`+失敗4点(prefab/component/snapshot/dup)LogError+continue・サーバMapObjectDatastore対称 → `db89d3c32`
- Q2: 初期化完了ゲート(現状.Forget()で生成途中に起動完了発火・実害実証なし) / 選択肢: ungated / IInitialEventApplyWaitTarget / **裁定: B gate** / 適用: interface実装+`IsInitialEventApplied`をループ完走後true+DI `.AsSelf().As<IInitialEventApplyWaitTarget>()` → `db89d3c32`（★InitializeScenePipeline無限待機なのでQ1=A完走保証と噛み合う）
- Q3: 非同期helper構造(3系統是認/R-region localize/R-callerorch Factory・非両立) / 選択肢: 現状/Construct内localize/Factory抽出 / **裁定: B localize** / 適用: Construct内ローカル関数化・ResolvePrefabOrNullは兄弟フラット → `db89d3c32`

## 破棄した指摘
- (二値潰しなし・3段階契約で運用) Warning級は全て報告に保持しトリアージ。破棄=false-positiveは無し。R-central設計判断(Factory抽出)はR-callerorchと同根でQ3に集約・R-centralの結論は現状維持だがユーザーはlocalizeを選択。
- 既存負債(VanillaApiWithResponse401行・dir-limit・MapObjectMaster dup検証・vein孤児プロパティ)はP2非起因・報告のみでAskUserQuestion非載せ(規約)。

## 事後結果（マージ後追記可）
- fix wave回帰テスト(GetMapDataProtocol)はUnity uloop MCP pump wedgeで未取得(client専用変更でserver protocol非接触ゆえ静的に非影響・compile green二重確認)。Unity再起動後1回で確定可。
- T8必須申し送り: EditMode fixture(EditModeInPlayingTestMod/master/map.json)のStone/clay/Bush未登録addressはskip+continue化後もLogError→Unity Test Runner赤化。fixture実在address化 or エントリ除去 or LogAssert.Expect要。v8 world map.json: addressablePath 7件追加+stale guid e76e6b65×12除去+Bush(廃止対象)対応。

## メタ
- セッションID: 99456c56-ddb3-4e80-aebc-f86b189f059a / スキップ系統: なし(Codex/Fable含む全5系統実行・APIアウテージで一度中断→全再実行で完遂) / 備考: 23サブエージェント(レンズ9+reviewer13+Fable1)を20並列上限で分割発火・post-guard2・fix wave1(Opus)。決定論comparison_operator0でverifier非起動。
