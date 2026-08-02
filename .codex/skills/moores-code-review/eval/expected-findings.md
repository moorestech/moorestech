# リプレイ評価: 期待検出リスト（実レビュー22指摘）

各fixtureに対しレンズ/決定論チェックを走らせた際、検出されるべき指摘。
「検出器」列が本ハーネスの担当。リプレイで検出できなければ配管の退行（レンズ・selector・スクリプトを疑う）。
注意: レンズはこの22件から作られているため、全件検出は「配管が正しい」証明であって汎化の証明ではない。
汎化はレンズ作成に使っていないPRのブラインドリプレイ（README参照）で別途確認する。

## pr978-r1（review 4642535092）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 1 | BlockMasterのJSON挿入処理は不要 | BlockMaster.cs | master-data-defense（プリフィル） |
| 2 | アイドル判定のドメイン漏れ | GearEnergyTransformerComponent.cs:62 | domain-boundary（観点1） |
| 3 | _isActiveはドメイン境界越え | GearEnergyTransformerComponent.cs:43 | domain-boundary（観点1） |
| 4 | 何がアクティブかを知るべきでない | GearEnergyTransformerComponent.cs:32 | domain-boundary（観点1） |
| 5 | 発電機系のドメイン越境revert | SimpleGearGenerator/GearToElectric | domain-boundary（観点1） |
| 6 | テンプレートのラムダ判定 | VanillaGearMapObjectMinerTemplate.cs:49 | domain-boundary（観点1・Template） |
| 7 | UpdateTicksForSpeedChange適切か | VanillaBeltConveyorInventoryItem.cs:69 | （設計質問。検出対象外＝正当な人間判断） |

## pr978-r2（review 4642994960）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 8 | ??フォールバック全体不要 | BlockMaster.cs:24 | 決定論 master_default_fallback |
| 9 | optionalでなく必須 | VanillaSchema/blocks.yml:290 | 決定論 candidates.schema_optional_true → master-data-defense裁定 |
| 10 | 毎フレーム実行は非効率 | GearBeltConveyorComponent.cs:39 | domain-boundary（観点2） |
| 11 | 毎フレーム判定不要 | VanillaGearMapObjectMinerProcessorComponent.cs:142 | domain-boundary（観点2） |
| 12 | DefaultIdlePowerRate参照排除 | VanillaElectricPumpTemplate.cs:35 | 決定論 master_default_fallback |
| 13 | 不要な一時変数 | VanillaElectricPumpTemplate.cs | master-data-defense（一時変数） |

## pr988（review 4644402652）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 14 | イベントパケット新設+購読へ | ItemStackLevelEventHandler.cs | server-state-sync（3点セット/Applier） |
| 15 | Applier 2種は不要になる | Challenge/ResearchItemStackLevelApplier.cs | server-state-sync（Applier禁止） |
| 16 | 初期データで直接取得 | VanillaApiWithResponse.cs | server-state-sync（初期データ） |
| 17 | Lookup static公開/変更DI分離 | ItemStackLevelDataStore.cs | datastore-access-separation |

## pr987（comment r3537879954）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 18 | 共用体structの抽象化 | BuildMenuEntry.cs:20 | type-driven-structure（観点1） |

## pr996（review 4661573483）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 19 | Contextへのバカバカ入れ | IPlaceSystem.cs | type-driven-structure（観点2） |
| 20 | Selectorのプロパティ羅列 | PlaceSystemSelector.cs | type-driven-structure（観点2） |
| 21 | DTOをプロトコル階層に置くな | BlueprintPacketDto.cs | 決定論 packet_response_root / type-driven-structure（観点4） |

## pr997（review 4645972495）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 22 | 3interfaceを束ねる複合interface | ElectricWireConnectorComponent.cs | type-driven-structure（観点3） |
| 23 | ディレクトリ整理（依存可視化） | Util/ElectricWire/ | 決定論 dir_file_limit / type-driven-structure（観点4） |

## pr1000（comment r3556923781）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 24 | _gearServiceへ委譲し重複解消 | GearChainPoleComponent.cs:239 | domain-boundary（観点3）/ precedent-alignment |

## machine-recipe-selection（ProcessingMachineProcessState）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 25 | バッキングフィールド＋素通しプロパティの二重保持を自動プロパティに畳む | ProcessingMachineProcessState.cs | redundant-member-duplication（観点1） |

## pr1017（review 4686520443）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 26 | レール/チェーン/電線の`[0]`決め打ちが「1種類しか無い」を暗黙に規定 | ConnectToolCatalog.cs:53,59,65（SelectIconItemGuid） | implicit-cardinality-assumption |

## pr1027（レビュー会話由来・SetEventSink）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 27 | set-once依存EventSinkの可変setter注入（生成直後1回のみ・不変条件がコメント頼み）をコンストラクタ注入へ | PacketResponseContext.cs:53（SetEventSink）/ ServerListenAcceptor.cs:36 | set-once-dependency-injection |

## pr1045（人間レビューコメント 2026-07-22・未マージのためbase直指定fixture）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 28 | マーカーinterface＋型switchでなくUndoAsyncの多態化 | BuildUndoService.cs / IBuildOperationRecord.cs | type-driven-structure（基準5）。「spec記載＝合意」シールドで抑制されないこと（合意ガード） |
| 29 | CommitDelete戻り値廃止・履歴記録をCommitDelete内部へ（最終形はRemoveOperationRecord.CreateFrom化＝5f1876cda。「ビュー結合増」却下より兄弟型対称が優先） | DragDeleteSelection.cs / DeleteObjectService.cs | core-cs-caller-orchestration-minimization（基準4＋移動先判定手続き） |
| 30 | DI分割コミットのrevert | MainGameStarter.cs | （検出対象外＝スコープ規律。検出系統が未実装） |
| 31 | _isUndoing解放が復元観測前（連続Undo競合） | RemoveOperationRecord.cs:47 | （検出対象外＝CodeRabbit検出。非同期再入reviewerは未実装） |
| 32 | 兄弟型API対称化: RemoveOperationRecordをCreateFrom(List\<IDeleteTarget\>)+HasCells形式へ（fixture: pr1045-r2） | RemoveOperationRecord.cs:21 / DragDeleteSelection.cs:116-131 | precedent-alignment（兄弟型間API形状非対称基準） |

注: #28/#29は最低ラインが `設計判断: あり`（AskUserQuestion送り）。Criticalでも可。備考落ち・シロ判定はリプレイ失敗。

## replace-family（セッション指摘 2026-07-23・当時ブランチdiff直指定fixture）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 33 | リプレースファミリーのBlockTypeConst 3種コード内列挙→buildMenuマスタ定義へ（最終形=3ad0cd5c0のreplaceFamilies＋ReplaceFamilyValidator） | BlockReplaceFamilyUtil.cs:29-32 | hardcoded-content-enumeration（基準1+2。同dirマスタ駆動前例BeltConveyorPlaceFamilyUtilからの無言乖離）。precedent-alignment（fable）の設計判断出口は補助線 |

注: #33はhardcoded-content-enumeration（opus）の**Critical**が最低ライン（fable依存の検知をopus側へ降ろした第1号。二値時代のリプレイでopus/sonnet 9系統が素通しした実績あり）。

| 34 | 4カテゴリcontextの「許容するトレードオフ」「目指さない」行の出所ラベル欠落（散文・箇条書きとも）・カテゴリ見出し欠落・偽`[ADR:]`参照（台帳に非実在/参照先がagent前提）は confirmed（context-source-label）で検出される | synthetic/*-context.md | deterministic_checks --context（checks_context.py。LLM不要） |
| 35 | トレードオフ合致の指摘は「指摘しない」でなく suppressed 節（`- [Critical|Warning] … / suppressed-by: <トレードオフ, 出所ラベル>`）で返る。沈黙（無出力で落とす）は失格。`[agent前提]`出所での suppressed 化も失格（通常Critical/Warningが正） | 全レンズ・reviewer共通契約 | 各観点の依頼動詞優先ガード＋integration-rules §2.6 |

## PR1095 reconcile由来（人間レビュー 4829833297・2026-08-01「Cluadeの検知漏れ」）

見逃し17件から起票した改善（`pr-independent-review/records/improvement-queue.md` Q1〜Q12）の期待検出。
由来PRのdiffは巨大なためfixture化せず、**ブラインド合成fixture（下記synthetic 3ペア）を一次の検証コーパスとする**。

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 36 | 実装が1つしかない新設interface・固有の中身を持たない汎用ラッパー・空Disposeの`IDisposable`・存在意義のない新設public メンバー・不要な新設型（5件を束ねた「受益者なき抽象」） | IBlueprintCatalogSource.cs / BuildToolPlacementTarget.cs / EquipmentHeldItemModel.cs / LocalPlayerEquipment.cs / EquipmentProtocol.cs | speculative-abstraction（opus・新設）。synthetic: `speculative-abstraction-positive/negative` |
| 37 | 新設publicメンバーの公開範囲過剰（参照元が自ファイルだけ／デバッグ専用呼び出し元だけ） | MapObjectMiningService.cs（経過ティック）/ MapObjectAcquisitionProtocol.cs（デバッグ用破棄API） | core-cs-dead-code-and-scope §5（sonnet）。synthetic: `sweep-and-scope-positive/negative` |
| 38 | 到達不能な失敗経路（不能フォールバック）と受け側のsilent skip。1件だけ挙げて同型を落とさないこと | PlacementTargetFactory.TryCreate（検出済）/ MapObjectMiningMiningState（同型・見逃し） | core-cs-result-state-propagation §5＋全数掃引。synthetic: `sweep-and-scope-positive/negative` |
| 39 | 重複集約後に素通しになる中継メソッドまで掃引すること（カスケードの端まで） | BuildMenuEntryCatalog/WebBuildMenuEntryCatalog（検出済）/ PlacementTargetFactory.CreateEntry直返し（見逃し） | core-cs-centralization-duplication §6。synthetic: `sweep-and-scope-positive/negative` |
| 40 | 受け取れる`CancellationToken`を渡していない・CTSの作りっぱなし・`async void` | EquipmentHeldItemModel.cs（AddressableLoader.LoadAsyncへ未伝搬） | core-cs-async-cancellation（opus・新設）。synthetic: `async-cancellation-positive/negative` |
| 41 | 根拠コメントの実在をtry-catch免除にしない。許可された境界3種を主張しないtry-catchは`confirmed`のまま | EquipmentHeldItemModel.cs:99（「Addressableロードは外部境界」＝3種のどれでもない） | 決定論 try-catch-forbidden（較正後）＋`candidates.try_catch_boundary` → try-catch-boundary-verifier |
| 42 | サーバのゲームロジックで実時間API（`Time.deltaTime`/`Stopwatch`/`Environment.TickCount`）を使わない | MapObjectMiningService.cs:85,88（Stopwatch.GetTimestamp・head 74ba6e8） | 決定論 server-realtime-api（confirmed） |
| 42b | サーバGame配下の`DateTime.Now/UtcNow`＋経過計測痕跡（TimeSpan/Total*/DateTime辞書）は候補化し、用途（ゲート=Critical／セーブ用実世界時刻記録=正当）をverifierが裁定 | MapObjectMiningService.cs:79-80（DateTimeクールダウン・head 463a56d時点の形） | 決定論 `candidates.server_elapsed_time` → server-elapsed-time-verifier（sonnet） |
| 43 | 初期化メソッドの命名（厳密名の揺れ Init/Setup/Construct/Initialise・override除外・テスト除外） | 合成（PR1095実diffは厳密名0件＝誤爆なしを確認） | 決定論 init-method-naming（confirmed）。`tests/test_init_method_naming.py` 10件が回帰を守る |
| 44 | 初期化役メソッドの意味的な別名（`ApplyInitial`等）とctor→Initialize記述順・ガード節の一箇所集約 | LocalPlayerEquipment.cs:88（ApplyInitial・下部配置）/ MapObjectMiningService.cs:90（クールダウンガードのローカル関数埋没） | core-cs-region-internal §6/§7（sonnet）。synthetic: `init-structure-positive/negative` |
| 45 | 無内容なイベント名（OnChanged）と実処理と乖離した総称名（プロトコル） | LocalPlayerEquipment.cs:33（OnChanged 3種混流）/ EquipmentProtocol.cs:13,36 | core-cs-centralization-duplication §1命名（opus）。synthetic: `event-naming-positive/negative` |
| 46 | オーバーロード置換（新ctor追加で旧ctorの生存者がテスト/デバッグのみ化）。テスト参照は§1の免除にならない | Responses.cs:17（PlayerInventoryResponse生引数ctor） | core-cs-dead-code-and-scope §1（sonnet）。synthetic: `overload-replacement-positive/negative` |

注: #36〜#40 の最低ラインは各観点の **Critical**。#41/#42/#42b は決定論チェックなのでLLM不要（`tests/test_try_catch_boundary.py` が回帰を守る。#42bのverifier裁定のみsonnet）。
注: 段階4実diffバックテスト実測（2026-08-02・head 74ba6e8）: #40/#41/#42/#42b は完全検出。#36 は由来surfaceの3件が観点自身のガード（asmdef反転・兄弟規約・解放対象実在）で抑止され**不合格**、#37 は担当reviewerの4分割subagent委任で希釈され**全滅**、#38 は由来surfaceを到達性裁定で対象外化（同§5の別3件は検出）、#39 は部分。合成fixture緑（段階3）が段階4を保証しない実証例＝ガード較正・委任禁止が残タスク（改善キューQ1/Q2/Q3/Q4/Q11）。
注: DateTimeを`server-realtime-api`のconfirmedに含めてはならない — セーブメタデータ（`WorldSettingsDatastore`等）で正当使用が実在し、confirmed化すると誤検知する（2026-08-02実測・AGENTS.md例外に成文化済み）。
#38/#39 は「1件検出」では不合格 — 同型の全インスタンスが修正方針に列挙されていることまでが合格条件（integration-rules §2.7）。
