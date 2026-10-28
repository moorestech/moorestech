# Task32 裁定D1–D12 / C15 実装報告

状態: DONE_WITH_CONCERNS（実装・非Unity検証・commit/push完了。Unityコンパイル/NUnit/録画は次のClaude QA waveで実施）

依頼文: `.superpowers/sdd/task32-pi-d-prompt.md`。裁定済み案Aを適用した。新しい裁定・仕様の再質問は行っていない。model capacity停止後は既存コミットと原ログを再利用し、残っていたdirty 2件を精査・検証・commit/pushした。この報告時点で実装の意図的スキップはない。ただし以下のUnity検証を通過したとの主張はしない。

## 対象とコミット

- BODY: `feat/tier9-integrated`、基点 `b0624f3549`、実装末尾 `8eeef04de4`。報告・証拠の追加コミットはこの末尾に続く。
- MASTER: `feat/tier9-integrated`、`744e2f102c8c72b75521d07ed4b4efcc8f4483bc..3a9e4af2673a301d87c34d4207b683f3b3158560`。
- MASTER既存PR89のheadは `3a9e4af2673a301d87c34d4207b683f3b3158560`。BODYの `.moorestech-external-revisions.json` も同値。
- MASTERを先にpushしPR89 headを確認してからBODYのピンを更新した。共有クローンのcheckout/編集、新規PR作成、BODY plan/Beads台帳編集、入れ子SDD/全レビューは行っていない。
- Unityは新規起動・実行していない。`.meta`手作成、UnityシリアライズYAML手編集も行っていない。

PR89: https://github.com/moorestech/moorestech_master/pull/89

## 件別の適用内容・証拠

表の「NUnit追加/更新」はコードの実装を指し、今回の実行成功を意味しない。コマンドと実行件数は後節にまとめる。

| ID | 適用内容 | コミット | テスト・検証 / 未検証 |
|---|---|---|---|
| C15 | 未知Result=999/未知reasonはArgumentOutOfRangeExceptionを期待。不正JSON境界のDeserializeFailedは維持 | 963bc63af4 | SpaceElevatorConnectWireActionTestを更新。Web contract含む279件成功。該当NUnitは未実行 |
| D1 | V7→V8にElectricGeneratorFuelStateNormalizerを登録。activeFuelType欠落、Item/Fluidの識別GUID欠落・空値をNone/残秒0へ正規化し燃料識別を消去。inventory/tankは保持。全件prepare後apply、修復件数を通知 | 5102926414, c45eae1104 | SaveMigrationStepV7ToV8FuelTestに原子性・冪等性・Item/Fluid/種別欠落の実ディスク移行連鎖ロードを追加。NUnit未実行 |
| D2 | 現マスタと不適合な発射台積荷をIdleへ戻し、既知の実品を回収スロットへ保持。容量を復元前に計算し追加output枠を保存/DTOへ反映。回収理由・欠損を警告 | ba7da17acf, 579c9ea616 | RocketLaunchPadRetainedItemDiskTest、SaveCorruptionTest、WireContractTestを追加/更新。input/output/cargo実品保持・追加回収枠のwireを検査するNUnitは未実行 |
| D3 | 通常機械/清浄室の確定レシピ・継承レベル・確定出力列・TotalTicks・役割抽選記録を保存/復元。CycleCount巻戻しを廃止。清浄室抽選記録は専用保存器へ分離 | bf87a6944f | MachineRealizationSaveRoundTripTest、CleanRoomPendingDrawYieldInfoTestのreloadケース。モジュール除去/出力満杯/保存往復を対象とするNUnitは未実行 |
| D4 | consumedInputItemGuidsとジョブtotalTicksを通常機械/清浄室で保存。キャンセル時に消費した実品グレードを返却。旧stateの列欠落は基準品返却＋警告、時間欠落は算出＋警告 | bf87a6944f | LevelFamilySubstituteMachineTest.RecipeChangeRefundsConsumedVariantTest、CleanRoomJobSaveRoundTripTest（現行/旧V7移行/返却）を追加。NUnit未実行 |
| D5 | 役割分岐は既存CleanRoomYieldPreview.Evaluateのswitch一箇所へ統一。preview/確定処理が共有、新しい役割型は追加しない | bf87a6944f | 上記清浄室テストを更新。C#構文解析成功、Unity型検査/実行未検証 |
| D6 | 液体復元をBlockTemplateUtil.RestoreMachineFluidsへ集約。通常/清浄室の余剰液体および清浄室inventory overflowをエラーログで可視化。MachineSaveRestorerへ責務分離 | bf87a6944f | 呼出し/ログ経路を静的確認。Unity実行でのログ観測は未実施 |
| D7 | 詳細sectionの親種別7種類を親表へ集約し重複if列を除去 | 5f75dd3255 | Web全1359件、tsc成功 |
| D8 | 予約撤去でCanRemoveを撤去前に呼び、容量不足時に実品/予約を保持。DSL撤去にも同じガードを適用 | 5f75dd3255, b0056014fd | SpaceElevatorRemovalCapacityTestに容量不足→容量確保→再試行時の一回返却を追加。NUnit未実行 |
| D9 | 研究素材割当をサーバーと同じ2pass（単一許容素材を先、複数許容素材を宣言順）へ変更 | 5f75dd3255 | researchLogic.test.tsに回帰ケース。Web対象279件/全1359件成功 |
| D10 | 公開PlaytestDriver.ServerServiceを撤去。tier9以外を含む既存シナリオの直接service利用をtickキューRunOnServer/専用snapshotへ置換。研究/設計図/課題/清浄室/軌道/歯車/列車/ブロックの専用口を追加、可変コレクションはコピーを返す | b0056014fd, 579c9ea616 | ServerService残存・GetService呼出しのRunOnServer包含・非async lambda内awaitを静的掃引、asmdef依存循環なし。シナリオ実行/スレッド動作/録画は全て未検証 |
| D11 | 既存決定文の実施形態をユーザー裁定のMBP Unity Editor/world_1・ビルド不要へ訂正 | 963bc63af4 | 文面差分確認のみ。実際の通しプレイは未実施 |
| D12 | GearMachineからtank/fluidconnectorキーをschema/全同形JSON/SandBoxで削除。IMachineParamの必要な0値はinterfacePropertyConstantsから生成。自動生成コードを手編集せず正規build.shでDLL更新。MASTER spec/applyと回帰テストも変更し再applyで戻らない | BODY 07d8a622b7, 579c9ea616, 8eeef04de4 / MASTER 3a9e4af | 生成器303件、InterfaceConstantTest再確認5件成功。BODY+MASTERのGearMachine 50件掃引、MASTER再applyの97ファイルbyte冪等。Unityでの実マスタロードは未検証 |

D3/D4は明示裁定に従い新設V8内へ追加し、CurrentVersion=8を維持した。旧V7/旧stateの未確定扱い・基準品返却は警告付き契約であり、欠落を無音補完しない。

D2の保持対象は現マスタで識別できる実品である。削除済みマスタの未知GUIDは実品化できず警告で表明する。スタック上限違反などの破損入力を無条件で受理する変更ではない。

## 残っていたdirty 2件

- `mooresmaster/mooresmaster.SandBox/schema/blocks.yml`: GearMachine定数ブロックと次のcaseのインデントを修正。
- `mooresmaster/mooresmaster.Tests/DefineInterfaceTests/InterfaceConstantTest.cs`: SandboxBlockSchemaKeepsValidYamlIndentationを追加。実際のYamlParserでSandBox全体を読む。
- 最終全生成器テスト303成功、および再開後のInterfaceConstantTest 5成功を確認。`git diff --check`成功。`8eeef04de4`でcommit/push済み。

## 実行済み検証

原ログは同階層の `task32-pi-d-evidence/`。各ログ先頭にcwd・argv・exit codeを保存した。全て前景実行で、バックグラウンド監視やUnity起動はしていない。既存ログの再利用と再開後の再確認を区別するため、再確認ログは `resumed-interface-tests.txt` とした。

| cwd / コマンド | 結果 | 原ログ |
|---|---|---|
| webui: `pnpm exec vitest run src/features/research/researchLogic.test.ts src/bridge/contract` | 18ファイル279件成功 | web-focused.txt |
| webui: `pnpm exec tsc -b --pretty false` | exit 0 | web-tsc.txt |
| webui: `pnpm test` | 165ファイル1359件成功 | web-suite.txt |
| BODY: `env PATH=$HOME/.dotnet:/usr/local/bin:/usr/bin:/bin bash mooresmaster/build.sh` | 正規生成器ビルド・DLL配置成功 | generator-deploy.txt |
| mooresmaster: `~/.dotnet/dotnet test mooresmaster.Tests/mooresmaster.Tests.csproj --no-restore --verbosity quiet` | 303成功、0失敗/skip | generator-sandbox-final.txt |
| mooresmaster: 同testへ `--no-build --no-restore --filter FullyQualifiedName~InterfaceConstantTest --verbosity quiet` | 再開後5成功 | resumed-interface-tests.txt |
| MASTER: `python3 -m unittest discover -s design/tier9-balance/apply/tests -v` | 7成功 | master-apply-tests-fixed.txt |
| MASTER: `python3 -m unittest discover -s tools/tier9_balance/tests -v` | 115成功 | master-balance-tests.txt |
| MASTER: `python3 -m unittest discover -s tools/progression/tests -v` | 56成功 | master-progression-tests.txt |
| MASTER: `python3 tools/progression/check.py` | G2成功、55/55nodes、24waves、0fail/19allow | master-g2.txt |
| MASTER: `python3 tools/tier9_balance/run.py` | G6成功、398checks/0fail、全体17.054h | master-g6.txt |
| MASTER: `python3 tools/nodegraph_sync/sync_nodegraph.py --check` | 0fail | master-nodegraph.txt |
| MASTER: `python3 design/tier9-balance/apply/apply_spec.py --mod-dir server_v8/mods/moorestechAlphaMod_8 --spec-dir design/tier9-balance/spec --ledger design/tier9-balance/guids.json` | 11spec適用、新規GUID 0 | master-reapply.txt |
| BODY: `python3 .superpowers/sdd/task32-pi-d-evidence/static_checks.py` | 50歯車JSON、105 C#行数、追加先dir10ファイル制約、禁止構文追加なし、asmdef循環なし、DLL一致、MASTER再apply97ファイルbyte冪等 | static-verified.txt |
| BODY: `~/.dotnet/dotnet run --project .superpowers/sdd/task32-pi-d-evidence/syntaxcheck/syntaxcheck.csproj -- .superpowers/sdd/task32-pi-d-evidence/csharp-files.txt` | Roslyn構文解析105ファイル0失敗。シナリオの直接GetService呼出しがRunOnServer内であることも検査 | csharp-final.txt |

生成器DLLはclient/serverともSHA256 `8bb2a580687ae790b8f4f56343b1370dac8703e013525ab0eff68ccf3b01dc85`。無関係なLocalizationCsv生成DLLのバイト差分は戻した。SourceGeneratorはビルドして配布済みだが、利用側Unityプロジェクトで生成コードが型検査を通るかは次waveで確認が必要。

生成器テストのNU1701/MSB3052/nullable等の警告、Webテストの既存翻訳欠落stderrは原ログに残している。成功件数にはスキップを成功換算していない。生成器の.NETテストはUnity NUnitとは別物である。Roslyn構文解析は参照解決・型検査を行うUnityコンパイルの代替ではない。

## 検証途中の失敗と修正

失敗ログを成功結果で上書きしていない。

- `generator-tests.txt`: Activator.CreateInstanceのbool引数が非公開ctor検索overloadへ解決された試験不備。object[]引数へ修正し最終303成功。
- `master-apply-tests.txt`: 新規試験fixtureのblockType欠落。fixtureに必須値を明記して7成功。本番側の欠落fallbackは追加していない。
- `csharp-syntax.txt`: SDK10 Roslynに対して検査器net8を用いた参照不一致。検査器をnet10へ揃え105ファイル成功。
- `static-final.txt` / `static-final-fixed.txt`: 検査用PyYAML依存不足とインデント固定の照合式を修正。PyYAML不要の検査と実YamlParser試験で最終成功。
- 最初のrestore前の `dotnet test --no-restore` は件数を出さず完了したため検証証拠に採用していない。restore後の実件数303を採用。

## 次のClaude QA waveへの必須引継ぎ

今回の実装をUnity検証済みとして扱わないこと。Unityコンパイル、今回追加/更新したNUnit、PlayMode実行、実ワールド通しプレイ/録画、G1/G3、独立評価2体は未実行。ユーザーの禁止に従い今回のセッションでは実行していない。全レビュー/PRはcontroller Task33の担当。

1. QA worktreeを本BODY末尾とMASTERピン `3a9e4af` へ揃える。共有クローンは編集しない。新規C#の.metaは次回Unityに生成させる。今回追加したserverコードを含めUnityを正規に再読込し、クライアントprojectでコンパイルする。
2. 新規/更新NUnitを優先実行する。C15未知Result/未知reasonと不正JSON、D1実ディスク移行/原子性、D2回収品input/output/cargoと拡張output wire、D3/D4通常機械/清浄室の確定抽選・時間・実品返却・旧V7、D8予約撤去容量不足と再試行、D12 GearMachineIoTest/GearMachineSaveLoadTestをテストmod/実マスタで確認する。
3. 回帰NUnitは例として `^(?!.*(PlaytestReceiver|RealReceiver|DeveloperReport)).*(Rocket|SpaceElevator|OrbitalArrival|LaunchPad|SaveMigration|V7ToV8|Machine|CleanRoom|BlockInventory|ItemInitialEquipment|Playtest)` のように実receiver起動系を除外して絞る。件数・0failを原ログで確認する。
4. D10は全既存シナリオの呼出し側まで変更したため、Tier9のstage0 smoke、1a、1b、2～7を録画込みで確認する。特にD3–5のstage1–3、D2のstage4、D8/D10のstage6、全段階の研究準備を重点確認する。
5. Tier9以外の変更シナリオも実行は未検証。既存の「broken」旧uGUI型参照シナリオはUI移植まで完了したという主張をしない。今回のD10静的掃引はservice取得/操作キューを対象としており、実行可能性や全ての既存専用getterのスレッド安全性の証明ではない。
6. D11はMBPのUnity Editor/world_1で実施し、ビルドを作る裁定へ戻さない。終了時の独立評価とPR手続きはcontrollerで行う。

追加/更新NUnitの主な識別名:

- SpaceElevatorConnectWireActionTest.RequestCarriesIdentityAndPreservesEveryResult
- SaveMigrationStepV7ToV8FuelTest（LegacyFuelLoadsThroughDiskMigrationChain含む）
- RocketLaunchPadRetainedItemDiskTest.MasterChangeRetainsExistingItemsOnDiskLoad
- RocketLaunchPadSaveCorruptionTest / RocketLaunchPadWireContractTest
- MachineRealizationSaveRoundTripTest.PendingOutputsAndTimeSurviveModuleRemovalAndReload
- LevelFamilySubstituteMachineTest.RecipeChangeRefundsConsumedVariantTest
- CleanRoomPendingDrawYieldInfoTest.RemovingQualityModuleWhileOutputFullKeepsDrawTimeRatesAndProcessingTimeTest
- CleanRoomJobSaveRoundTripTest.SaveReloadPreservesJobTimeAndConsumedGradeOrWarnsForLegacy
- SpaceElevatorRemovalCapacityTest.ReservedRemovalChecksCapacityAndRefundsOnlyOnceAfterRetry

## 完了証拠

実装履歴は `body-commits.txt`、変更対象は `body-changed-files.txt`、MASTER履歴は `master-commits.txt`。原ログと検査スクリプトのソースを報告と一緒にコミットした。検査器のbin/obj等の一時生成物は含めない。実装・原ログの保持を完了したため、この実装セッションは終了可能。Unity QA/全レビュー未実施は次waveへ明示して引き継ぐ。
