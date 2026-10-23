# Tier9 計算データループ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ロケット発射台でGPU衛星を打ち上げて計算データを得る仕組み（スキーマ・軌道到着サービス・発射台・合図・表示・Web UI・セーブ）を、テスト用マスタで確かめた状態で入れる。moorestech_master の中身（GPU・GPU衛星・積荷表の行・発射台の値・レシピ・研究）と、ループ全体を遊べるかの unityプレイ録画テストは、S1〜S3 の実装がそろってからの統合タスク（bd `moorestech-xyuaf.2`）で入れる。この plan の成果は、統合タスクの後に遊べる状態になる。

**Architecture:** 軌道に届いた後の処理（打ち上げ成果の決定と到着通知）を新設アセンブリ `Game.Orbit` の `IOrbitalArrivalService` に集め、発射台（`Game.Block`）とS3のエレベーター拠点はそれを呼ぶだけにする（ADR 0088）。発射台は資材スロット・積荷スロット・出力スロットを持つブロックで、電力と時間でロケットを建造し、初打ち上げだけプレイヤーのボタン操作（Web UI → プロトコル）を待つ。初打ち上げ済みかはワールド単位の状態 `IRocketLaunchHistory` としてセーブに入れる（版6→7）。

**Tech Stack:** Unity C#（moorestech_server/client）、UniRx、MessagePack、Newtonsoft.Json、マスタYAMLスキーマ＋SourceGenerator、React/TypeScript（moorestech_web/webui、zod、vitest）、moorestech_master（JSON）。

## Requirements

1. **R1 発射台ブロック:** blockType `RocketLaunchPad` のブロックが、資材スロット（`launchMaterials` 1件につき1スロット・そのアイテム専用）、積荷スロット（積荷表1行につき1スロット・その積荷専用）、出力スロット（`outputSlotCount` 個・取り出し専用）を持つ。受け入れ: ベルトとプレイヤー操作のどちらでも、合わないアイテムは入らず、出力スロットへは置けない。開いたまま整理（ソート）しても各スロットの中身は動かない。
2. **R2 建造:** 全資材が1便ぶん揃い、どれか1種の積荷が1便ぶん揃い、その積荷の成果（`IOrbitalArrivalService.PreviewArrivalOutputs`）が今の出力スロットに入りきるとき建造を始め、資材と積荷をその時点で取り込む。建造は `buildSeconds` を電力の供給率に応じて進め、電力ゼロでは止まって理由を出す。受け入れ: 供給率0.5では建造時間が約2倍になる。
3. **R3 初打ち上げの手動操作:** ワールドで初打ち上げが済むまでは、揃った台は「建造の合図待ち」で止まり、建造ボタンで建造を始め、建造後は「発射の合図待ち」で止まり、発射ボタンで打ち上げる。初打ち上げ後は全ての台（合図待ちで止まっていた台を含む）が自動で建造・打ち上げする。合図と初打ち上げの通知は段階を「打ち上げ待ち」へ移すだけで、打ち上げはその台自身の tick で始まる。受け入れ: 別の台の初打ち上げで、発射の合図待ちの台が同じtickか次のtickに打ち上がる。
4. **R4 積荷の選び方:** 1便に積むのは1種類。揃った積荷が複数あれば、前回の次の積荷から順に、成果が出力スロットに入りきる最初の積荷を選ぶ。受け入れ: GPU衛星とグラフェンケーブルの両方が常に揃っている台で、交互に打ち上がる。出力が満杯でも、成果の無いグラフェンケーブルは打ち上がる。
5. **R5 軌道到着と成果:** 打ち上げ開始の時点で軌道到着とし、`IOrbitalArrivalService.Arrive` が積荷表から成果を返し、`OnOrbitalArrival` を流す。成果が出力スロットに入りきるかを、建造を始める前と打ち上げる直前の両方で確かめ、入りきらなければ待たせて理由（出力満杯）を出す。打ち上げた成果はそのまま出力スロットへ入り、出力スロットからは接続先（ベルト）へ流れる。打ち上げ後に成果を預かる保留は持たない。受け入れ: 出力が満杯のとき次の便の資材が取り込まれず、取り出すと再開する。
6. **R6 積荷表:** items.yml のトップレベル配列 `orbitalPayloads`（積荷アイテム・1便の個数・成果の並び）を必須で追加する。repo 内の全 items.json と、moorestech_master で読み込まれる `server_v8/mods/moorestechAlphaMod_8/master/items.json` へ空配列を足す（土台で空になっているスタックテーブル7件への Default と合わせ、読み込みが止まらない最小限。R12・D14）。テスト用 mod には積荷2行を入れる。GPU衛星などの実際の行は統合タスクで入れる。受け入れ: 積荷表に無いアイテムは積荷スロットを持たない。ピン先の moorestech_master が読込エラー無しで読める。
7. **R7 マスタ検証:** 積荷表と発射台パラメータの不整合（存在しないアイテム・重複・1便の個数がスタック上限を超える・資材と積荷が同じアイテム・秒数や電力が0以下・発射台があるのに積荷表が空・1便ぶんの成果がレベル1のスタック上限で数えて `outputSlotCount` を超える）はマスタ読込時にエラーで止まる。
8. **R8 セーブ:** 発射台の段階（int）・残り秒・積んだロケットの中身・次に探す積荷・入れ口と出力口の中身をブロックのセーブに入れ、`rocketHasLaunchedOnce` をワールドのセーブのトップレベルに入れる（版7で必須。欠ければロードを止める）。版を6→7に上げ、旧版セーブに `rocketHasLaunchedOnce:false` を足す移行ステップを同梱する。ロード時、今の積荷表で打ち上げられない積荷（表に無い・個数が今の1便ぶんと違う・欠損マスタの刈り込みで空）は警告して捨てて待機に戻し、置き場の無いスロットの品は警告して捨て、負の「次に探す積荷」は範囲内へ直す。受け入れ: 建造中・発射の合図待ち・出力満杯の打ち上げ待ちでセーブ→ロードして同じ状態から続く。壊れた積荷のセーブはロードが止まらず待機から始まる。
9. **R9 撤去返却:** 撤去時、スロットの中身（出力スロットの成果を含む）に加えて、建造中・発射の合図待ち・打ち上げ待ちのロケットに積んだ資材と積荷を返却する。打ち上げ中のロケットの中身は消費済みなので返さない。
10. **R10 見た目:** 既存の `Rocket.prefab`（発射台 base とロケット本体 body）を使う。base は常に表示、body は建造完了（発射の合図待ち・打ち上げ待ち）から表示し、打ち上げで `launchSeconds` かけて上昇し、終わると消える。演出・音は付けない。
11. **R11 Web UI:** 発射台を開くと、資材・積荷・出力のスロット群に見出しを付けて並べ、段階と待ち理由の文、建造・打ち上げの進み具合のゲージ、初打ち上げ前だけ「建造」「発射」ボタンを出す。
12. **R12 マスタの中身は統合タスク:** 研究「ロケット技術」・「半導体のアップグレード研究」、ロケット筐体素材・ロケット燃料・GPU衛星のレシピ、計算データ・GPU・GPU衛星のアイテムと levelFamily、積荷表の行、発射台の blocks 行の値、ビルドメニュー「宇宙」は、この plan では入れない。下書きを末尾の「統合タスクへ引き継ぐマスタの中身」節に残し、統合タスク（bd `moorestech-xyuaf.2`）が使う。受け入れ: この PR の moorestech_master の差分は items.json の `"orbitalPayloads": []` と、スタックテーブルが空の7件への Default（作業ブランチの土台 `b2149a0` で読込が止まるのを防ぐ最小限。D14）だけ。
13. **R13 録画テストは統合タスク:** ループ全体の unityプレイ録画テストは、マスタに発射台が入る統合タスクで撮る。この PR はサーバーの CombinedTest（テスト用マスタで建造→打ち上げ→成果→セーブ）・プロトコルテスト・web UI の vitest と wire 契約テストで確かめる。
14. **R14 S3への口:** `Game.Orbit` の API（S3 へ送付済み。`PreviewArrivalOutputs` の追加は 2026-10-23 に連絡済み）は以後変えない。Game.Orbit 一式と積荷表のスキーマ（repo 内の全JSONと master の空配列）を最初の2タスクで作って push する。グラフェンケーブルの行と「軌道エレベーターの建設」の研究変更は、統合タスクで入れる（S3 へ 2026-10-23 連絡済み）。
15. **R15 撤去返却の取りこぼし:** `RemoveBlockProtocol` は `IGetRefundItemsInfo` を先頭1件しか読まないため、電線（`ElectricWireConnectorComponent` も `IGetRefundItemsInfo`）と発射台の進行を併せ持つ発射台では片方の返却が消える。全件を合算する。受け入れ: 電線つきの発射台を建造中に撤去すると、積んだ資材と積荷がプレイヤーのインベントリへ戻る。

**やらないこと:**
- S1 の領域（クリーンルーム・チップ抽選・モジュール・GPU のアイテム定義とレベルの効き方）のコードとマスタを変えない。
- 宇宙エレベーター拠点・グラフェンケーブルの行・軌道エレベーターの建設の研究ノードは触らない（S3）。
- moorestech_master の中身（R12 の一覧）と unityプレイ録画テストは入れない（統合タスク bd `moorestech-xyuaf.2`。`.decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md`）。
- 打ち上げの演出（炎・煙・音）、カメラ演出は作らない。
- UI Toolkit への移行はしない（新UIは webui に作る）。

## Global Constraints

- 1ファイル200行以下。新規ファイルを置くディレクトリのコードファイルは10個まで（既に10個を超えているディレクトリには足さず、サブディレクトリを作る）。
- partial・`Func<>`・デフォルト引数・try-catch（外部境界以外）・単純な getter/setter プロパティは使わない。`{ get; private set; }` は可。値の設定は `SetHoge`。
- コメントは日本語1行＋英語1行の2行セットを主要処理ごとに入れる。
- サーバーの経過時間は `GameUpdater` のtickだけで測る。秒の換算は `GameUpdater.SecondsToTicks`。
- fail-closed で拒否・保留する経路は理由を `Debug.LogWarning` 以上で残す。意図して黙って続行する行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く。
- イベントは UniRx（`Subject<T>` を private、`IObservable<T>` を公開）。
- `.meta` を手で作らない。Prefab・シーンは `uloop execute-dynamic-code` 経由でだけ変える。Library を消さない。
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を通す。新規 `.cs` を足した直後は Unity の再読込が要ることがある（`uloop` が古い結果を返したら `docs/development/uloop-troubleshooting.md`）。
- YAMLスキーマを触る前に `edit-schema` スキルを読む。`optional: true` を足さない。プロトコルを作る前に `creating-server-protocol` スキル、セーブ移行を書く前に `moorestech-save-migration` スキル、テストを書く前に `creating-server-tests` スキル、webui を触る前に `ref-webui-design` スキルを読む。
- moorestech_master の作業場所は、このworktreeのルートから見て `../../moorestech_master/tier9-compute-data-loop`（ブランチ `feat/tier9-compute-data-loop`）。以下のコマンドはどれもこのworktreeのルートで実行し、master 側は `MASTER_WT=../../moorestech_master/tier9-compute-data-loop` を置いて `git -C "$MASTER_WT"` で操作する。`../moorestech_master` は全worktree共有のsymlinkなので、そこで checkout・編集しない。マスタを変えたら master worktree でコミット・push し、本repoの `.moorestech-external-revisions.json` の `moorestech_master.commitHash` をそのコミットへ向ける。`moores-wt status` はこの master worktree を「UNUSED master pin」と表示するが、S2 の作業場所なので `moores-wt rm --prune-master` 等で消さない。
- 名前（S3へ送付済み・変更不可）: asmdef/名前空間 `Game.Orbit`、`IOrbitalArrivalService`（`IObservable<OrbitalArrival> OnOrbitalArrival { get; }`・`bool IsOrbitalPayload(ItemId itemId)`・`IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)`（S3 へ 2026-10-23 連絡済み）・`IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)`）、`OrbitalArrival`（`public readonly ItemId PayloadItemId; public readonly int PayloadCount;`）、取得は `ServerContext.GetService<IOrbitalArrivalService>()`。
- GUID（統合タスクで使う。S3へ送付済み・変更不可。この plan のコードとテストは参照しない）: 研究「ロケット技術」`01a167cf-af9d-7f31-817c-3069b4f6e499`／計算データ `01a167cf-af88-74d3-9cd6-7d61b71052bb`／GPU衛星 Lv1 `01a167cf-af8b-7894-8de6-593ee30289c0`・Lv2 `01a167cf-af8e-7e95-b299-09eb712bee12`・Lv3 `01a167cf-af91-7f62-93d1-e499b06f2398`・Lv4 `01a167cf-af94-7e25-9654-825374c3923b`・Lv5 `01a167cf-af97-7f67-bf53-7833b8d17a15`／GPU衛星 levelFamily `01a167cf-af9a-78e5-9203-1c18ce94cdc6`／ロケット筐体素材レシピ `01a167cf-afa0-7add-8407-eec71456f4f3`／ロケット燃料レシピ `01a167cf-afa3-7c4f-82b7-c0d4f6d513c6`。
- S1 の GPU（S1 からの連絡 2026-10-23。統合タスクで使う）: levelFamily `01a167ed-2700-7371-9f95-168966ff60bc`、Lv1 `01a167ed-2703-7014-98e4-11bd15f38571`・Lv2 `01a167ed-2706-7bfd-8361-a430dd96a569`・Lv3 `01a167ed-2709-74f0-a5c6-25026c7479d9`・Lv4 `01a167ed-270c-7873-a83a-5cf02ead6277`・Lv5 `01a167ed-270f-7276-8700-469ab54a26a2`。
- 既存マスタGUID（統合タスクで使う）: ロケット発射台 `01a162b0-7769-7419-af24-bb35528b93d2`、ロケット筐体素材 `01a162a4-9334-7286-a8ba-7daf9c0f407a`、ロケット燃料 `01a162a8-e435-73d0-bddb-08533819b80e`、コンピュータ `019f0ca4-a544-723e-8ff2-feb15f99c84e`、鋼鉄板 `019e3a98-cfe5-73eb-bf6a-a564400bdb4f`、プラスチック `019e3aaa-af6f-7527-8689-01f4ebd65426`、固形燃料 `019eded2-f06b-70fd-b8aa-960690cb2b0f`、ガソリン（流体）`019eded2-7454-76da-8943-84c4e7a59179`、化学プラント `019ee880-7f68-705e-b846-7b05036e2ca8`、電気汎用工作装置 `019e1225-f49b-7283-8835-428d903852f1`、研究「半導体製造」`019e3ad9-46e2-73fd-9555-8c1d0245d26d`、スタックテーブル Default `9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa`。
- セーブ版: S2 が 6→7（`SaveMigrationStepV6ToV7`）。S1（クリーンルーム）も 6→7（モジュール・継承）と 7→8（クリーンルーム機械の液体）、S3 は 7→8 を使う（S1 からの連絡 2026-10-23）。番号は3本とも重なるので、後からマージする側が番号を詰め直す（ステップの FromVersion・`CurrentVersion`・テストの版の数字を、先に入った版の続きへ繰り上げる）。この PR では 6→7 のまま実装する。
- moorestech は PR 作成までで止める（マージしない）。moorestech_master も PR を作る（中身は items.json の空配列と、スタックテーブルが空の7件への Default だけ）。
- S3 は本ブランチ（moorestech・moorestech_master とも `feat/tier9-compute-data-loop`）を merge で取り込んでエレベーター拠点を作る。Task 2 の最後で両repoを push する（S3 の着手条件）。以後どちらのブランチも force-push・rebase・履歴の書き換えをしない。
- ビルドメニュー「生産 / 宇宙」（`subCategoryGuid: d2000000-0000-4000-8000-000200000005`、`entrySource: blocks`。S3 合意）は統合タスクで入れる。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 初回 違反6件・修正6件、再検査 違反3件・修正3件 / 初回: #1 通信の語彙をプロトコルへ（D24）・#2 整理の除外（D31）・#3 セーブを秒と int に（D32）・#4 10ファイル超のディレクトリを避ける（D33。録画シナリオの1件は末尾の「統合タスクへ引き継ぐマスタの中身」節で `scenarios/rocket/` に置く）・#5 検証の連鎖を `ItemMasterUtil` へ（D22）・#6 webui の型を `payloadTypes.ts` へ（D33）。再検査: N1 `ItemMasterUtil.cs` の200行超を `Validator/Item/` への切り出しで解消（D22）・N2 初打ち上げ済みを前例 `currentTick` と同じ `bool?` のトップレベル値に（注目点3）・N3 Web UI の3ファイルを親の名前空間に（Task 8）。再検査の注目点1・2・7は D39 で反映。注目点は判断記録の末尾
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 初回 強5・弱5・第3バケツ2、再検査 強1（後の裁定 D36 で解消済み）・弱3・第3バケツ4 / 初回: 強2はユーザー裁定で段階ごとの型（D26）、強1・3・4は前例どおり（D34）、強5は ADR 0088 を直した（D35）。弱の5件はユーザー裁定の4つの選択（進み具合と読込時のガード・打ち上げ開始の口・判定と導出の重複・契約型）で全て直した（D25・D27・D28・D29）。第3バケツの2件は Task 10 の PR 本文「本PR外のリファクタ提案」。再検査: 強1（マスタの中身の担当と時期の食い違い）は D36 と ADR 0088 の更新で解消。弱3件（重複6か所・Task 7 の cast・出力口の空き待ちの無音）は D39 の前提宣言で扱った。第3バケツの4件（新規3件と継続1件）は同じ PR 本文の節に1行ずつ入れた

---

## File Structure

| ファイル | 責務 |
|---|---|
| `VanillaSchema/items.yml` | `orbitalPayloads`（積荷表）を追加 |
| `VanillaSchema/blocks.yml` | blockType `RocketLaunchPad` と `RocketLaunchPadBlockParam` を追加 |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs` | 積荷表の検証（新規） |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs` | 発射台パラメータの検証（新規） |
| `moorestech_server/Assets/Scripts/Game.Orbit/*` | 軌道到着サービス・初打ち上げ履歴（新規asmdef） |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/*.cs`・`Stage/`・`Save/` | 発射台の部品の外殻・段階の進行・段階の型・電力・インベントリ・積み込み・出力口・セーブ変換 |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/RocketLaunchPad/*.cs`・`State/RocketLaunchPad/*.cs` | 発射台の契約型（合図の口と結果・スロット並び・段階・待ち理由・状態詳細）。プロトコルとクライアントが参照する |
| `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs` | 発射台の組み立て |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/...` | ワールドセーブへの `rocketHasLaunchedOnce`・V6→V7 移行 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Rocket/RocketLaunchPadCommandProtocol.cs` | 建造・発射の合図（操作の種類と通信の失敗理由はプロトコルにネスト） |
| `moorestech_server/Assets/Scripts/Server.Boot/Composition/OrbitServiceRegistration.cs` | Game.Orbit の DI 登録 |
| `moorestech_server/Assets/Scripts/Tests/Util/Rocket/RocketLaunchPadTestUtil.cs` | 発射台のテスト共有ヘルパ |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Rocket/RocketLaunchPadStateChangeProcessor.cs` | ロケット本体の表示と上昇 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/...` | DTO・アクション・API |
| `moorestech_web/webui/src/...` | 発射台パネル |
| moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/items.json` | `"orbitalPayloads": []` と、スタックテーブルが空の7件への Default だけ（中身は統合タスク） |

---

### Task 1: 積荷表と発射台のスキーマ・マスタ検証・テスト用マスタ

**Files:**
- Modify: `VanillaSchema/items.yml`（`levelFamilies` の直後に `orbitalPayloads`）
- Modify: `VanillaSchema/blocks.yml`（blockType enum の末尾に `RocketLaunchPad`、`blockParam.cases` の末尾に case）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json`
- Modify: `mooresmaster/mooresmaster.SandBox/TestMod/items.json`（edit-schema スキルが SandBox の更新を求める場合のみ。`schema/items.json` はスキーマ生成物なら同スキルの手順で再生成）
- Modify: moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/items.json`（`"orbitalPayloads": []` と、`stackLevelTableGuid` が空の7件へ Default。どちらも読み込みに要る最小限で、中身は統合タスク）
- Modify: `.moorestech-external-revisions.json`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ItemMasterUtil.cs`（S1 の領域でない2つの検証を下の2ファイルへ移して200行以下にし、`Validate` の連鎖に積荷表の1行を足す。`ItemMaster.Validate` は委譲の1行のまま）
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Item/ItemStackLevelTableValidator.cs`（`ItemMasterUtil` のローカル関数 `StackLevelTableValidation` の移し先）
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Item/ItemInitialEquipmentValidator.cs`（同 `InitialEquipmentValidation`・中の `InitialLevelMaxStackValidation` の移し先）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`（`Validate` に1行）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`、`ForUnitTestItemId.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/OrbitalPayloadMasterValidatorTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/RocketLaunchPadParamValidatorTest.cs`

**Interfaces:**
- Produces:
  - 生成型 `Mooresmaster.Model.ItemsModule.OrbitalPayloadMasterElement`（`Guid PayloadItemGuid`・`int CountPerLaunch`・`OrbitalArrivalOutputElement[] ArrivalOutputs`）、`OrbitalArrivalOutputElement`（`Guid ItemGuid`・`int Count`）、`Items.OrbitalPayloads`
  - 生成型 `Mooresmaster.Model.BlocksModule.RocketLaunchPadBlockParam`（`float RequiredPower`・`float BuildSeconds`・`float LaunchSeconds`・`RocketLaunchMaterialElement[] LaunchMaterials`・`int OutputSlotCount`・`InventoryConnectors`・`MaxWireConnectionCount`・`ConnectionRange`・`ConnectionHeightRange`）、`RocketLaunchMaterialElement`（`Guid ItemGuid`・`int Count`）
  - `internal static string OrbitalPayloadMasterValidator.Validate(Items items)`
  - `internal static string RocketLaunchPadParamValidator.Validate(Blocks blocks)`
  - テストID: `ForUnitTestModBlockId.RocketLaunchPadId`、`ForUnitTestItemId.RocketCaseId`・`RocketFuelId`・`RocketComputerId`・`SatellitePayloadId`・`CablePayloadId`・`ComputeDataId`
  - 生成された数値型（`float`/`double`）は SourceGenerator の出力に合わせる。上の表記と違えば以降のタスクのコードも出力側に合わせる

- [x] **Step 0: 環境を確かめる**

Run:
```bash
pwd   # worktree tier9-compute-data-loop のルートであること
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" branch --show-current   # feat/tier9-compute-data-loop であること
moores-wt status
cd moorestech_web/webui && pnpm install --frozen-lockfile && cd ../..
uloop compile --project-path ./moorestech_client
```
Expected: 自分のworktreeのEditorが居なければ `uloop launch --project-path ./moorestech_client` で起動してからcompile。ErrorCount 0。

- [x] **Step 1: items.yml に積荷表を足す**

`levelFamilies` ブロックの直後（`itemStackLevelTables` の直前）に追加する:

```yaml
# 軌道へ運べる積荷の表。打ち上げ口（発射台・エレベーター拠点）はここに載る積荷だけを扱い、軌道到着で打ち上げ成果を出す
# Table of cargo that can be sent to orbit; launch sites only handle cargo listed here and yield the arrival outputs on orbital arrival
- key: orbitalPayloads
  type: array
  overrideCodeGeneratePropertyName: OrbitalPayloadMasterElement
  items:
    type: object
    properties:
    - key: payloadItemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: countPerLaunch
      type: integer
      default: 1
    - key: arrivalOutputs
      type: array
      overrideCodeGeneratePropertyName: OrbitalArrivalOutputElement
      items:
        type: object
        properties:
        - key: itemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: count
          type: integer
          default: 1
```

- [x] **Step 2: blocks.yml に発射台を足す**

blockType の `options` の末尾（`CleanRoomMachine` の次）に `- RocketLaunchPad` を足し、`blockParam.cases` の末尾に追加する:

```yaml
      - when: RocketLaunchPad
        type: object
        implementationInterface:
        - IInventoryConnectors
        - IElectricWireConnectParam
        properties:
        # 建造中だけ要求する電力。供給率に応じて建造が進む
        # Power requested only while building; building advances by the supply rate
        - key: requiredPower
          type: number
          default: 100
        - key: buildSeconds
          type: number
          default: 60
        # 打ち上げ開始から本体が昇り切るまでの秒数。この間は次の便を建造しない
        # Seconds from launch start until the rocket body finishes rising; no next build meanwhile
        - key: launchSeconds
          type: number
          default: 10
        # 1便ごとに消費する資材。1件につき専用スロットを1つ持つ
        # Materials consumed per launch; each entry owns one dedicated slot
        - key: launchMaterials
          type: array
          overrideCodeGeneratePropertyName: RocketLaunchMaterialElement
          items:
            type: object
            properties:
            - key: itemGuid
              type: uuid
              foreignKey:
                schemaId: items
                foreignKeyIdPath: /data/[*]/itemGuid
                displayElementPath: /data/[*]/name
            - key: count
              type: integer
              default: 1
        - key: outputSlotCount
          type: integer
          default: 4
        - key: inventoryConnectors
          ref: inventoryConnects
        - key: maxWireConnectionCount
          type: integer
          default: 2
        - key: connectionRange
          type: integer
          default: 30
        - key: connectionHeightRange
          type: integer
          default: 20
```

- [x] **Step 3: 全 items.json に `"orbitalPayloads": []` を足し、テストマスタに発射台と積荷を足す**

`git ls-files | grep -E "items\.json$"` の全ファイル（本repoの4件。`mooresmaster.SandBox/schema/items.json` がスキーマ生成物なら edit-schema スキルの手順で再生成）と、moorestech_master で読み込まれる `server_v8/mods/moorestechAlphaMod_8/master/items.json` の1件へ `"orbitalPayloads": []` を足す（edit-schema スキルの「全JSON一括更新」手順に従う）。moorestech_master の `server/`・`server_v4`〜`server_v7` の旧 mod は今のゲームが読まないので触らない。

moorestech_master の作業ブランチは origin/master の `b2149a0`（tier9基板）の上にあり、そこで `stackLevelTableGuid` が空の7件（グラフェンケーブル・グラフェンシート・使用済み銅板・高純度炭素・ロケット筐体素材・ロケット燃料・反物質爆弾）が入っている。今のピン `0d129c8` には空の行が無く、ピンを作業ブランチへ進めると、スタックテーブルの参照検証（今の `ItemMasterUtil.StackLevelTableValidation`。Step 4 で `ItemStackLevelTableValidator` へ移す）で読込が止まる。そこで同じ items.json の7件へ Default `9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa` を入れる（読み込みに要る最小限。S3 が同じ7件を重ねて直さないよう、Task 2 Step 8 の bd note で伝える）。

moorestech_master へ入れるのはこの空配列と7件の Default だけで、アイテム・積荷表の行・ビルドメニュー・発射台の値・レシピ・研究は統合タスクで入れる（末尾の「統合タスクへ引き継ぐマスタの中身」節）。そのうえで ForUnitTest の items.json の `data` に次の6件を足し、`orbitalPayloads` を2行にする:

```json
{ "name": "TestRocketCase", "itemGuid": "5c8e0000-0000-4000-8000-000000000001", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestRocketFuel", "itemGuid": "5c8e0000-0000-4000-8000-000000000002", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestRocketComputer", "itemGuid": "5c8e0000-0000-4000-8000-000000000003", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestSatellitePayload", "itemGuid": "5c8e0000-0000-4000-8000-000000000004", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestCablePayload", "itemGuid": "5c8e0000-0000-4000-8000-000000000005", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" },
{ "name": "TestComputeData", "itemGuid": "5c8e0000-0000-4000-8000-000000000006", "imagePath": "Test1", "sortPriority": 100, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111" }
```

```json
"orbitalPayloads": [
  { "payloadItemGuid": "5c8e0000-0000-4000-8000-000000000004", "countPerLaunch": 1, "arrivalOutputs": [ { "itemGuid": "5c8e0000-0000-4000-8000-000000000006", "count": 150 } ] },
  { "payloadItemGuid": "5c8e0000-0000-4000-8000-000000000005", "countPerLaunch": 5, "arrivalOutputs": [] }
]
```

（成果150はスタック上限100を超え、分割と出力満杯の検証に使う。出力2スロットに1便ぶん（100＋50）が入りきるので、発射台パラメータの検証は通る。）

ForUnitTest の blocks.json の `data` に発射台を足す。共通キーはテスト機械 `TestElectricMachine`（`00000000-0000-0000-0000-000000000001`）と同じ値にし、`inventoryConnectors` は同じ形で `connectorGuid` だけ新しくする（テストでは 1×1×1 で足りる）:

```json
{
  "blockGuid": "5c8e0000-0000-4000-8000-000000000100",
  "name": "TestRocketLaunchPad",
  "blockType": "RocketLaunchPad",
  "openable": true,
  "maxStack": 100,
  "modelTransform": { "scale": [1, 1, 1], "rotation": [0, 0, 0], "position": [0, 0, 0] },
  "blockSize": [1, 1, 1],
  "category": "テスト",
  "subCategory": "RocketLaunchPad",
  "placementsPerCost": 1,
  "blockParam": {
    "requiredPower": 100,
    "buildSeconds": 1,
    "launchSeconds": 1,
    "launchMaterials": [
      { "itemGuid": "5c8e0000-0000-4000-8000-000000000001", "count": 2 },
      { "itemGuid": "5c8e0000-0000-4000-8000-000000000002", "count": 3 },
      { "itemGuid": "5c8e0000-0000-4000-8000-000000000003", "count": 1 }
    ],
    "outputSlotCount": 2,
    "inventoryConnectors": {
      "inputConnects": [ { "offset": [0, 0, 0], "directions": [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1]], "connectorGuid": "607ee58d-6dc6-4647-a882-7213cdd1334a" } ],
      "outputConnects": [ { "offset": [0, 0, 0], "directions": [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1]], "connectorGuid": "6fa4b379-b0ea-4b4e-8484-2dabea65fe2d" } ]
    },
    "maxWireConnectionCount": 2,
    "connectionRange": 30,
    "connectionHeightRange": 20
  }
}
```

`ForUnitTestModBlockId.cs` に `public static BlockId RocketLaunchPadId => GetBlock("5c8e0000-0000-4000-8000-000000000100");`、`ForUnitTestItemId.cs` に既存の書式で6件（`RocketCaseId`=…0001、`RocketFuelId`=…0002、`RocketComputerId`=…0003、`SatellitePayloadId`=…0004、`CablePayloadId`=…0005、`ComputeDataId`=…0006）を足す。

- [x] **Step 4: 積荷表の検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.OrbitalPayload
{
    // 積荷表の行がアイテム定義と矛盾しないことを読込時に確かめる
    // Validates at load that orbital payload rows agree with the item definitions
    internal static class OrbitalPayloadMasterValidator
    {
        internal static string Validate(Items items)
        {
            var logs = "";
            var itemByGuid = items.Data.ToDictionary(item => item.ItemGuid);
            var tableByGuid = items.ItemStackLevelTables.ToDictionary(table => table.TableGuid);
            var seenPayloads = new HashSet<Guid>();
            foreach (var payload in items.OrbitalPayloads)
            {
                logs += ValidatePayloadItem(payload);
                foreach (var output in payload.ArrivalOutputs) logs += ValidateOutput(payload, output);
            }
            return logs;

            #region Internal

            string ValidatePayloadItem(OrbitalPayloadMasterElement payload)
            {
                // 積荷は実在し、表に1回だけ載り、1便ぶんが1スロットに収まる
                // A payload must exist, appear once, and fit one launch into a single slot
                if (!itemByGuid.TryGetValue(payload.PayloadItemGuid, out var item)) return $"[ItemMaster.orbitalPayloads] unknown payloadItemGuid:{payload.PayloadItemGuid}\n";
                if (!seenPayloads.Add(payload.PayloadItemGuid)) return $"[ItemMaster.orbitalPayloads] duplicate payload:{item.Name}\n";
                if (payload.CountPerLaunch < 1) return $"[ItemMaster.orbitalPayloads] {item.Name} countPerLaunch must be >= 1 (was {payload.CountPerLaunch})\n";

                // 発射台は積荷1種につき1スロットなので、レベル1のスタック上限を超える1便は積めない
                // A launch pad has one slot per payload, so a launch above the level-1 stack limit can never be loaded
                // silent-ok: テーブル欠落・段数0のテーブルは ItemStackLevelTableValidator が報告する / マスタ読込時のエラーログで開発者が気づく
                if (!tableByGuid.TryGetValue(item.StackLevelTableGuid, out var table) || table.StackCounts.Length == 0) return "";
                var firstLevelStack = table.StackCounts[0];
                return payload.CountPerLaunch > firstLevelStack ? $"[ItemMaster.orbitalPayloads] {item.Name} countPerLaunch {payload.CountPerLaunch} exceeds level-1 stack {firstLevelStack}\n" : "";
            }

            string ValidateOutput(OrbitalPayloadMasterElement payload, OrbitalArrivalOutputElement output)
            {
                if (!itemByGuid.ContainsKey(output.ItemGuid)) return $"[ItemMaster.orbitalPayloads] payload:{payload.PayloadItemGuid} has unknown arrival output:{output.ItemGuid}\n";
                return output.Count < 1 ? $"[ItemMaster.orbitalPayloads] payload:{payload.PayloadItemGuid} arrival output count must be >= 1\n" : "";
            }

            #endregion
        }
    }
}
```

（スタックテーブル欠落と段数0のテーブルは既存のスタックテーブル検証（下で `ItemStackLevelTableValidator` へ移す）が報告するので、ここでは二重に出さない。段数0を見送るのは既存の `InitialLevelMaxStackValidation`（下で `ItemInitialEquipmentValidator` へ移す）と同じ扱い。）

`ItemMaster.Validate` は委譲の1行のまま残し、連鎖は `ItemMasterUtil.Validate` に足す（前例 `BlockMasterUtil.Validate` の `errorLogs +=` 連結）。`ItemMasterUtil.cs` は今244行で、1行足すと200行の規則にさらに外れる。そこで先に、S1 の領域ではない2つの検証（スタックテーブルと初期装備）を `Core.Master/Validator/Item/` へ移す（前例: `Validator/Block/` が `BlockMasterUtil` から検証を切り出した形。名前空間はその過半と同じサブ名前空間）。モジュールとレベルファミリーの検証（`ModulesValidation`・`LevelFamiliesValidation`）は S1 の領域なので動かさない。移す2つの本体は今のコードを1文字も変えずに写す（`allItemGuids` を使うのは初期装備の検証だけなので引数で渡す）。

`moorestech_server/Assets/Scripts/Core.Master/Validator/Item/ItemStackLevelTableValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.Item
{
    // スタックテーブルの中身と、各アイテムからの参照先テーブルの実在を検証する
    // Validates stack level table contents and that each item references an existing table
    internal static class ItemStackLevelTableValidator
    {
        internal static string Validate(Items items)
        {
            // 空配列・1未満のスタック数・アイテムからの参照先テーブル実在を検証
            // Validate empty arrays, sub-1 counts, and that each item references an existing table
            var logs = "";
            if (items.ItemStackLevelTables == null) return logs;

            var tableGuids = new HashSet<Guid>();
            foreach (var table in items.ItemStackLevelTables)
            {
                // TableGuid重複はInitializeのDictionary.Addが生の例外で落ちるため検証で弾く
                // Duplicate TableGuid would throw raw in Initialize's Dictionary.Add, so reject it here
                if (!tableGuids.Add(table.TableGuid))
                    logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} has duplicate TableGuid:{table.TableGuid}\n";
                if (table.StackCounts.Length == 0)
                    logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} has empty StackCounts\n";
                foreach (var count in table.StackCounts)
                    if (count < 1)
                        logs += $"[ItemMaster.itemStackLevelTables] Name:{table.Name} has invalid StackCount:{count} (must be >= 1)\n";
            }
            foreach (var item in items.Data)
                if (!tableGuids.Contains(item.StackLevelTableGuid))
                    logs += $"[ItemMaster.data] Name:{item.Name} references missing StackLevelTableGuid:{item.StackLevelTableGuid}\n";

            return logs;
        }
    }
}
```

`moorestech_server/Assets/Scripts/Core.Master/Validator/Item/ItemInitialEquipmentValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.Item
{
    // 初期装備は未定義アイテムを指せない。装備スロットを超える分は投入時に捨てられるためここで弾く
    // Initial equipment must reference defined items; entries beyond the equipment slot count would be dropped on grant, so reject them here
    internal static class ItemInitialEquipmentValidator
    {
        internal static string Validate(Items items, HashSet<Guid> allItemGuids)
        {
            var logs = "";

            // 初期レベルの最大スタック数はItemStack生成が生の例外で落ちるため、ここで先に弾く
            // ItemStack creation throws raw past the level-1 max stack, so it is rejected here first
            var stackLevelTableByGuid = items.ItemStackLevelTables.ToDictionary(table => table.TableGuid);
            var itemByGuid = items.Data.ToDictionary(item => item.ItemGuid);

            foreach (var initial in items.InitialEquipmentItems)
            {
                if (!allItemGuids.Contains(initial.ItemGuid))
                {
                    logs += $"[ItemMaster.initialEquipmentItems] has invalid ItemGuid:{initial.ItemGuid}\n";
                    continue;
                }
                if (initial.ItemCount <= 0)
                    logs += $"[ItemMaster.initialEquipmentItems] ItemGuid:{initial.ItemGuid} has non-positive ItemCount:{initial.ItemCount}\n";
                logs += InitialLevelMaxStackValidation(initial.ItemGuid, initial.ItemCount);
            }
            if (items.EquipmentSlotCount < items.InitialEquipmentItems.Length)
                logs += $"[ItemMaster.initialEquipmentItems] count:{items.InitialEquipmentItems.Length} exceeds EquipmentSlotCount:{items.EquipmentSlotCount}\n";
            // 装備選択は「未装備なし」で常に1枠を指すため、枠0は選択インデックスのクランプが成立せず起動不能になる
            // Equipment selection always points at a slot (no unequipped state), so zero slots breaks the index clamp and the boot
            if (items.EquipmentSlotCount < 1)
                logs += $"[ItemMaster.equipmentSlotCount] must be at least 1 but was {items.EquipmentSlotCount}\n";

            return logs;

            #region Internal

            // 参照先テーブルが壊れている件は ItemStackLevelTableValidator の担当なので、ここでは黙って見送る
            // A broken table reference is ItemStackLevelTableValidator's job, so it is skipped silently here
            string InitialLevelMaxStackValidation(Guid itemGuid, int itemCount)
            {
                if (!itemByGuid.TryGetValue(itemGuid, out var item)) return "";
                if (!stackLevelTableByGuid.TryGetValue(item.StackLevelTableGuid, out var table)) return "";
                if (table.StackCounts.Length == 0) return "";

                var initialLevelMaxStack = table.StackCounts[0];
                if (itemCount <= initialLevelMaxStack) return "";
                return $"[ItemMaster.initialEquipmentItems] ItemGuid:{itemGuid} has ItemCount:{itemCount} exceeding initial level MaxStack:{initialLevelMaxStack}\n";
            }

            #endregion
        }
    }
}
```

`ItemMasterUtil.cs` では、ローカル関数 `StackLevelTableValidation`（112〜137行）と `InitialEquipmentValidation`（139〜182行。前のコメント2行を含む）を消し、`using Core.Master.Validator.Item;` と `using Core.Master.Validator.OrbitalPayload;` を足して、`Validate` の連鎖を次にする（`Initialize` は触らない。約176行になる）:

```csharp
            errorLogs = "";
            errorLogs += ModulesValidation();
            errorLogs += LevelFamiliesValidation();
            errorLogs += ItemStackLevelTableValidator.Validate(items);
            errorLogs += ItemInitialEquipmentValidator.Validate(items, allItemGuids);
            errorLogs += OrbitalPayloadMasterValidator.Validate(items);
            return string.IsNullOrEmpty(errorLogs);
```

（移した2つの検証の既存テストは、Step 8 のフィルタ `ItemMaster` が拾う。ログの文面を変えていないので、既存テストの期待値はそのまま通る。）

- [x] **Step 5: 発射台パラメータの検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    // 発射台が建造・打ち上げを進められないパラメータを読込時に止める
    // Stops at load any launch pad parameters that could never build or launch
    internal static class RocketLaunchPadParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            var payloads = MasterHolder.ItemMaster.Items.OrbitalPayloads;
            var payloadGuids = new HashSet<Guid>(payloads.Select(payload => payload.PayloadItemGuid));
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not RocketLaunchPadBlockParam param) continue;
                logs += ValidateScalars(block.Name, param);
                logs += ValidateMaterials(block.Name, param);
                logs += ValidateOutputCapacity(block.Name, param);

                // 積荷表が空だと揃う積荷が無く、永遠に建造しない
                // With an empty payload table nothing can ever be loaded, so the pad would never build
                if (payloadGuids.Count == 0) logs += $"[BlockMaster] {block.Name} is a RocketLaunchPad but items.orbitalPayloads is empty\n";
            }
            return logs;

            #region Internal

            string ValidateScalars(string name, RocketLaunchPadBlockParam param)
            {
                var result = "";
                if (param.RequiredPower <= 0) result += $"[BlockMaster] {name} requiredPower must be > 0\n";
                if (param.BuildSeconds <= 0) result += $"[BlockMaster] {name} buildSeconds must be > 0\n";
                if (param.LaunchSeconds <= 0) result += $"[BlockMaster] {name} launchSeconds must be > 0\n";
                if (param.OutputSlotCount < 1) result += $"[BlockMaster] {name} outputSlotCount must be >= 1\n";
                if (param.LaunchMaterials.Length == 0) result += $"[BlockMaster] {name} launchMaterials must not be empty\n";
                return result;
            }

            string ValidateMaterials(string name, RocketLaunchPadBlockParam param)
            {
                // 資材は実在・重複なし・積荷と別アイテム・1便ぶんがレベル1のスタックに収まる
                // Materials must exist, be unique, differ from payloads, and fit one launch in a level-1 stack
                var result = "";
                var seen = new HashSet<Guid>();
                foreach (var material in param.LaunchMaterials)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(material.ItemGuid);
                    if (itemId == null) { result += $"[BlockMaster] {name} has unknown launch material:{material.ItemGuid}\n"; continue; }
                    if (!seen.Add(material.ItemGuid)) result += $"[BlockMaster] {name} has duplicate launch material:{material.ItemGuid}\n";
                    if (payloadGuids.Contains(material.ItemGuid)) result += $"[BlockMaster] {name} launch material {material.ItemGuid} is also an orbital payload\n";
                    if (material.Count < 1) result += $"[BlockMaster] {name} launch material count must be >= 1\n";

                    var firstLevelStack = FirstLevelStackOrNull(itemId.Value);
                    if (firstLevelStack != null && material.Count > firstLevelStack) result += $"[BlockMaster] {name} launch material {material.ItemGuid} count {material.Count} exceeds level-1 stack {firstLevelStack}\n";
                }
                return result;
            }

            string ValidateOutputCapacity(string name, RocketLaunchPadBlockParam param)
            {
                // 空の出力口に1便ぶんの成果が入りきらない積荷は、その台で永遠に出力満杯のまま打ち上がらない
                // A payload whose single launch outputs cannot fit empty output slots would stay output-full forever on this pad
                var result = "";
                foreach (var payload in payloads)
                {
                    var stackCount = 0;
                    foreach (var output in payload.ArrivalOutputs)
                    {
                        var outputItemId = MasterHolder.ItemMaster.GetItemIdOrNull(output.ItemGuid);
                        var firstLevelStack = outputItemId == null ? null : FirstLevelStackOrNull(outputItemId.Value);

                        // silent-ok: 未知の成果アイテムは積荷表の検証、段数0のテーブルは ItemStackLevelTableValidator が報告する / マスタ読込時のエラーログで開発者が気づく
                        if (firstLevelStack == null) continue;
                        stackCount += (output.Count + firstLevelStack.Value - 1) / firstLevelStack.Value;
                    }
                    if (stackCount > param.OutputSlotCount) result += $"[BlockMaster] {name} outputSlotCount {param.OutputSlotCount} cannot hold one launch of payload {payload.PayloadItemGuid} ({stackCount} stacks at level-1 stack)\n";
                }
                return result;
            }

            int? FirstLevelStackOrNull(ItemId itemId)
            {
                var element = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var stackCounts = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts;

                // silent-ok: 段数0のテーブルは ItemStackLevelTableValidator が報告する / マスタ読込時のエラーログで開発者が気づく
                return stackCounts.Length == 0 ? null : stackCounts[0];
            }

            #endregion
        }
    }
}
```

成果のスタック数はレベル1のスタック上限で数える。実行時の分割（`CreateSplitStacks`）はその時点のスタック上限を使い、レベルが上がるほど上限は大きくなるので、レベル1で入りきれば実行時も空の出力口に入りきる。

`BlockMasterUtil.Validate` の `errorLogs += BeltSpeedPerSecondValidator.Validate(blocks);` の次に `errorLogs += RocketLaunchPadParamValidator.Validate(blocks);` を足す。

- [x] **Step 6: 検証のテストを書く**

検証クラスは `internal` のまま、テストは公開された集約点 `ItemMasterUtil.Validate`・`BlockMasterUtil.Validate` を呼ぶ（前例 `Tests/UnitTest/Game/BeltSpeedPerSecondMasterTest.cs` の `BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs)`）。

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/OrbitalPayloadMasterValidatorTest.cs`:

```csharp
using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.OrbitalPayload
{
    public class OrbitalPayloadMasterValidatorTest
    {
        [Test]
        public void テストマスタの積荷表は検証を通る()
        {
            // DIの生成でマスタが読み込まれ、検証エラーなら例外で落ちる
            // Building the container loads the master and would throw on a validation error
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(2, MasterHolder.ItemMaster.Items.OrbitalPayloads.Length);
        }

        [TestCase("countPerLaunch", 101, "exceeds level-1 stack")]
        [TestCase("countPerLaunch", 0, "must be >= 1")]
        public void 積荷の個数の不正を止める(string key, int value, string expected)
        {
            var json = LoadTestItemsJson();
            json["orbitalPayloads"][0][key] = value;

            var isValid = ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains(expected, errorLogs);
        }

        [Test]
        public void 重複した積荷を止める()
        {
            var json = LoadTestItemsJson();
            ((JArray)json["orbitalPayloads"]).Add(json["orbitalPayloads"][0].DeepClone());

            var isValid = ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains("duplicate payload", errorLogs);
        }

        private static JObject LoadTestItemsJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "items.json");
            return JObject.Parse(File.ReadAllText(path));
        }
    }
}
```

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/RocketLaunchPadParamValidatorTest.cs`（`BeltSpeedPerSecondMasterTest` の `PrepareBlocksJson`・`FindBlockParam` を同ファイル内に写す。`PrepareBlocksJson` が DI を作るので `MasterHolder.ItemMaster` は読込済み）:
- `出力口に1便ぶんの成果が入りきらない台を止める`: `TestRocketLaunchPad` の `outputSlotCount` を1にして `BlockMasterUtil.Validate` が false、ログに `cannot hold one launch of payload 5c8e0000-0000-4000-8000-000000000004 (2 stacks at level-1 stack)` を含む（成果150はレベル1のスタック100で2スタック）。
- `資材の個数がスタックを超える台を止める`: `launchMaterials[0].count` を101にして `exceeds level-1 stack 100` を含む。
- `テストマスタの発射台は検証を通る`: 変更しないJSONで `BlockMasterUtil.Validate` が true。

(`TestModDirectory.ForUnitTestModDirectory` の実際のパス構成は前例 `BeltSpeedPerSecondMasterTest.PrepareBlocksJson` と同じ組み立てにする。)

- [x] **Step 7: マスタを push しピンを向ける**

```bash
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" diff --stat   # items.json 1件だけであること
git -C "$MASTER_WT" add server_v8/mods/moorestechAlphaMod_8/master/items.json
git -C "$MASTER_WT" commit -m "data(v8): 積荷表の空配列を足し、スタックテーブル未設定のTier9アイテムへDefaultを入れる"
git -C "$MASTER_WT" push origin feat/tier9-compute-data-loop
git -C "$MASTER_WT" rev-parse HEAD   # この値を .moorestech-external-revisions.json の moorestech_master.commitHash へ
```

- [x] **Step 8: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OrbitalPayloadMasterValidatorTest|RocketLaunchPadParamValidatorTest|ItemMaster|BlockMaster"`
Expected: ErrorCount 0 / 全 PASS。Editor でマスタを読み直し（RepositorySync がピンへ checkout する）、`uloop get-logs --project-path ./moorestech_client --log-type Error` に `[ItemMaster]` `[BlockMaster]` `MooresmasterLoaderException` が無い（ピン先のマスタが読めている）。土台の `b2149a0` に由来する別の読込エラーが出たら、直さずに内容を bd note と PR 本文に書く（S2 の範囲外）。

- [x] **Step 9: コミットする**

```bash
git add VanillaSchema/items.yml VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData .moorestech-external-revisions.json mooresmaster
git commit -m "feat(master): 積荷表と発射台のスキーマと読込時検証を足す"
```

---

### Task 2: 軌道到着サービスと初打ち上げ履歴（Game.Orbit）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/Game.Orbit.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/OrbitalArrival.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/IOrbitalArrivalService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/OrbitalArrivalService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/IRocketLaunchHistory.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Orbit/RocketLaunchHistory.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Composition/OrbitServiceRegistration.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`SaveAndEventServiceRegistration.Register(...)` の直前に1行）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`、`moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef`、`moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`、テストの asmdef（`Tests` 配下で `Game.CleanRoom` を参照している asmdef すべて）の `references` に `"Game.Orbit"`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/OrbitalArrivalServiceTest.cs`

**Interfaces:**
- Consumes: Task 1 の `Items.OrbitalPayloads`、`OrbitalPayloadMasterElement`
- Produces:
  - `Game.Orbit.OrbitalArrival`（`public readonly ItemId PayloadItemId; public readonly int PayloadCount;`、ctor `(ItemId payloadItemId, int payloadCount)`）
  - `Game.Orbit.IOrbitalArrivalService`（`IObservable<OrbitalArrival> OnOrbitalArrival { get; }`・`bool IsOrbitalPayload(ItemId itemId)`・`IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)`・`IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)`）
  - `Game.Orbit.IRocketLaunchHistory`（`bool HasLaunchedOnce { get; }`・`IObservable<Unit> OnFirstLaunch { get; }`・`void RecordLaunch()`・`void LoadSaveData(bool hasLaunchedOnce)`。セーブへは `HasLaunchedOnce` をそのまま書く）
  - DI: `IRocketLaunchHistory`→`RocketLaunchHistory`、`IOrbitalArrivalService`→`OrbitalArrivalService`（どちらもシングルトン）

- [x] **Step 1: asmdef を作る**

`Game.Orbit.asmdef`（書式は `Game.CleanRoom.asmdef` と同じ）:

```json
{
  "name": "Game.Orbit",
  "rootNamespace": "",
  "references": [
    "Core.Master",
    "Core.Item.Interface",
    "Game.Context",
    "UniRx"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [x] **Step 2: 到着通知とサービスを書く**

`OrbitalArrival.cs`:

```csharp
using Core.Master;

namespace Game.Orbit
{
    // 軌道に届いた積荷の通知。S3はこれでケーブルの累計を数える
    // Notification for cargo that reached orbit; S3 counts its cable total from it
    public class OrbitalArrival
    {
        public readonly ItemId PayloadItemId;
        public readonly int PayloadCount;

        public OrbitalArrival(ItemId payloadItemId, int payloadCount)
        {
            PayloadItemId = payloadItemId;
            PayloadCount = payloadCount;
        }
    }
}
```

`IOrbitalArrivalService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.Orbit
{
    // 打ち上げ口が軌道到着を伝える窓口。成果の量や積荷の種類による分岐はここだけが持つ
    // The single entry launch sites use to report orbital arrival; only this owns output amounts and per-payload branching
    public interface IOrbitalArrivalService
    {
        IObservable<OrbitalArrival> OnOrbitalArrival { get; }

        bool IsOrbitalPayload(ItemId itemId);

        // 到着させずに成果だけを返す。打ち上げ口は出力口に入りきるかをこれで確かめてから打ち上げる
        // Returns the outputs without arriving; launch sites check output space with it before launching
        IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount);

        // 成果を新しいスタックの並びで返し、到着を通知する。表に無い積荷・1便の整数倍でない個数は呼び出し側のバグとして例外
        // Returns the outputs as fresh stacks and publishes the arrival; unknown payloads or non-multiple counts are caller bugs and throw
        IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount);
    }
}
```

`OrbitalArrivalService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.ItemsModule;
using UniRx;

namespace Game.Orbit
{
    // 積荷表から打ち上げ成果を決め、軌道到着を流す
    // Decides arrival outputs from the payload table and publishes orbital arrivals
    public class OrbitalArrivalService : IOrbitalArrivalService
    {
        public IObservable<OrbitalArrival> OnOrbitalArrival => _onOrbitalArrival;

        private readonly Subject<OrbitalArrival> _onOrbitalArrival = new();
        private readonly Dictionary<ItemId, OrbitalPayloadMasterElement> _payloadByItemId = new();

        public OrbitalArrivalService()
        {
            // 積荷表を積荷アイテムで引けるように索引する
            // Index the payload table by payload item
            foreach (var payload in MasterHolder.ItemMaster.Items.OrbitalPayloads)
                _payloadByItemId.Add(MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid), payload);
        }

        public bool IsOrbitalPayload(ItemId itemId)
        {
            return _payloadByItemId.ContainsKey(itemId);
        }

        public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)
        {
            var payload = FindPayload();

            // 成果は1便ぶん×便数。スタック上限を超える量は分割して返す
            // Outputs are per-launch amounts times launches, split when above the stack limit
            var launchCount = payloadCount / payload.CountPerLaunch;
            var outputs = new List<IItemStack>();
            foreach (var output in payload.ArrivalOutputs)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(output.ItemGuid);
                outputs.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(outputItemId, output.Count * launchCount));
            }
            return outputs;

            #region Internal

            OrbitalPayloadMasterElement FindPayload()
            {
                if (!_payloadByItemId.TryGetValue(payloadItemId, out var found))
                    throw new InvalidOperationException($"積荷表に無いアイテムの打ち上げ成果を求められました ItemId:{payloadItemId}");
                if (payloadCount <= 0 || payloadCount % found.CountPerLaunch != 0)
                    throw new ArgumentException($"積荷の個数が1便の個数の正の整数倍ではありません ItemId:{payloadItemId} Count:{payloadCount} CountPerLaunch:{found.CountPerLaunch}");
                return found;
            }

            #endregion
        }

        public IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)
        {
            // 先に知らせた成果と同じものを返し、到着を流す
            // Return the same outputs as the preview and publish the arrival
            var outputs = PreviewArrivalOutputs(payloadItemId, payloadCount);
            _onOrbitalArrival.OnNext(new OrbitalArrival(payloadItemId, payloadCount));
            return outputs;
        }
    }
}
```

- [x] **Step 3: 初打ち上げ履歴を書く**

`IRocketLaunchHistory.cs`:

```csharp
using System;
using UniRx;

namespace Game.Orbit
{
    // ワールドで初打ち上げが済んだか。済むまで全ての発射台は建造と発射の合図を待つ
    // Whether the world has had its first launch; until then every launch pad waits for build and launch commands
    public interface IRocketLaunchHistory
    {
        bool HasLaunchedOnce { get; }
        IObservable<Unit> OnFirstLaunch { get; }

        void RecordLaunch();
        void LoadSaveData(bool hasLaunchedOnce);
    }
}
```

`RocketLaunchHistory.cs`:

```csharp
using System;
using UniRx;

namespace Game.Orbit
{
    public class RocketLaunchHistory : IRocketLaunchHistory
    {
        public bool HasLaunchedOnce { get; private set; }
        public IObservable<Unit> OnFirstLaunch => _onFirstLaunch;

        private readonly Subject<Unit> _onFirstLaunch = new();

        public void RecordLaunch()
        {
            // 通知はワールドで最初の1回だけ。2回目以降は状態が変わらない
            // Notify only on the world's first launch; later launches change nothing
            if (HasLaunchedOnce) return;
            HasLaunchedOnce = true;
            _onFirstLaunch.OnNext(Unit.Default);
        }

        public void LoadSaveData(bool hasLaunchedOnce)
        {
            HasLaunchedOnce = hasLaunchedOnce;
        }
    }
}
```

- [x] **Step 4: DI に登録する**

`Server.Boot/Composition/OrbitServiceRegistration.cs`（`GameplayServiceRegistration.cs` は202行で既に上限超えのため足さない）:

```csharp
using Game.Orbit;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    // 軌道到着と初打ち上げ履歴をワールドに1つずつ置く
    // Registers one orbital arrival service and one launch history per world
    internal static class OrbitServiceRegistration
    {
        internal static void Register(ServiceCollection services)
        {
            services.AddSingleton<IRocketLaunchHistory, RocketLaunchHistory>();
            services.AddSingleton<IOrbitalArrivalService, OrbitalArrivalService>();
        }
    }
}
```

`MoorestechServerDIContainerGenerator.cs` の `GameplayServiceRegistration.Register(...)` の次の行に `OrbitServiceRegistration.Register(services);` を足す。各 asmdef に `"Game.Orbit"` を足す。

- [x] **Step 5: テストを書く**

`Tests/CombinedTest/Game/RocketLaunchPad/OrbitalArrivalServiceTest.cs`:

```csharp
using System.Collections.Generic;
using Game.Context;
using Game.Orbit;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class OrbitalArrivalServiceTest
    {
        [Test]
        public void 衛星の到着で成果を分割して返し到着を通知する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 成果150はスタック100を超えるので2スタックに分かれる
            // An output of 150 exceeds the stack of 100, so it splits into two stacks
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 1);

            Assert.AreEqual(2, outputs.Count);
            Assert.AreEqual(150, outputs[0].Count + outputs[1].Count);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, outputs[0].Id);
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, arrivals[0].PayloadItemId);
        }

        [Test]
        public void 成果の無い積荷は空を返し通知はする()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            Assert.AreEqual(0, service.Arrive(ForUnitTestItemId.CablePayloadId, 10).Count);
            Assert.AreEqual(10, arrivals[0].PayloadCount);
        }

        [Test]
        public void 成果の先読みは到着と同じ量を返し通知しない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 先読みは通知せず、到着と同じスタックの並びを返す
            // The preview publishes nothing and returns the same stacks as the arrival
            var preview = service.PreviewArrivalOutputs(ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.AreEqual(0, arrivals.Count);
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.AreEqual(outputs.Count, preview.Count);
            for (var i = 0; i < outputs.Count; i++)
            {
                Assert.AreEqual(outputs[i].Id, preview[i].Id);
                Assert.AreEqual(outputs[i].Count, preview[i].Count);
            }
            Assert.Throws<System.ArgumentException>(() => service.PreviewArrivalOutputs(ForUnitTestItemId.CablePayloadId, 3));
        }

        [Test]
        public void 表に無い積荷と半端な個数は例外()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();

            Assert.IsFalse(service.IsOrbitalPayload(ForUnitTestItemId.ComputeDataId));
            Assert.Throws<System.InvalidOperationException>(() => service.Arrive(ForUnitTestItemId.ComputeDataId, 1));
            Assert.Throws<System.ArgumentException>(() => service.Arrive(ForUnitTestItemId.CablePayloadId, 3));
        }

        [Test]
        public void 初打ち上げの通知は1回だけ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var history = ServerContext.GetService<IRocketLaunchHistory>();
            var count = 0;
            history.OnFirstLaunch.Subscribe(_ => count++);

            history.RecordLaunch();
            history.RecordLaunch();

            Assert.IsTrue(history.HasLaunchedOnce);
            Assert.AreEqual(1, count);
        }
    }
}
```

（`ForUnitTestItemId` の各IDが `ItemId` を返す形かは既存の書式に合わせる。`MoorestechServerDIContainerGenerator().Create` の戻り値の使い方は既存 CombinedTest に合わせる。）

- [x] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Game.RocketLaunchPad.OrbitalArrivalServiceTest"`
Expected: ErrorCount 0 / 5件 PASS

- [x] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Orbit moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef moorestech_server/Assets/Scripts/Tests
git commit -m "feat(orbit): 軌道到着サービスと初打ち上げ履歴を足す"
```

- [x] **Step 8: 両repoを push して S3 へ渡す**

S3 はこの push を取り込んでからエレベーター拠点に着手する（Global Constraints）。moorestech_master 側に入っているのは Task 1 Step 7 の items.json の変更（空配列と7件の Default）だけ。

```bash
git push origin feat/tier9-compute-data-loop
MASTER_WT=../../moorestech_master/tier9-compute-data-loop
git -C "$MASTER_WT" push origin feat/tier9-compute-data-loop
git rev-parse HEAD
git -C "$MASTER_WT" rev-parse HEAD
bd note moorestech-xyuaf.1 "Game.Orbit（PreviewArrivalOutputs を含む）と積荷表のスキーマをpush済み（S3取り込み可）: moorestech <上のハッシュ> / moorestech_master <上のハッシュ>。master はorbitalPayloadsの空配列と、土台b2149a0で空だったstackLevelTableGuid 7件へのDefaultだけ（読込に要る最小限。S3は同じ7件を重ねて直さない）。積荷表の行・グラフェンケーブル・ビルドメニュー宇宙・研究は統合タスク moorestech-xyuaf.2"
```

Expected: 両方の push が成功し、`.moorestech-external-revisions.json` の `moorestech_master.commitHash` が moorestech_master 側の HEAD と一致する。

---

### Task 3: ワールドセーブへの初打ち上げ履歴と V6→V7 移行

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（`CurrentVersion = 7`、ctor 引数 `bool? rocketHasLaunchedOnce` を `cleanRoomRooms` の次に、プロパティ `[JsonProperty("rocketHasLaunchedOnce")] public bool? RocketHasLaunchedOnce { get; }`。前例 `currentTick` の `ulong?`＋`HasValue` と同じく、欠損を null で検出するため nullable にする）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`（ctor に `IRocketLaunchHistory`、`Capture()` で `_rocketLaunchHistory.HasLaunchedOnce`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveDataRestorer.cs`（ctor に `IRocketLaunchHistory`、`LoadUnlockState` の次で `LoadSaveData`、必須欠損の検査へ `rocketHasLaunchedOnce`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaveRequiredFieldGuard.cs`（`WorldSaveDataRestorer` の `ThrowIfRequiredFieldMissing` を移す。194行のファイルを200行以内に保つため。同じ役割の前例 `PlayerSaveReferenceValidator` に合わせて `internal static`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`（`SectionsWithoutMasterPruning` の「マスタguidを持たない」行に `"rocketHasLaunchedOnce"`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV6ToV7.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs:116`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs:37`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs:137`（配列の末尾に `new SaveMigrationStepV6ToV7()`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV6ToV7Test.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchHistorySaveLoadTest.cs`

**Interfaces:**
- Consumes: Task 2 の `IRocketLaunchHistory`
- Produces: セーブのトップレベル `rocketHasLaunchedOnce: bool`（版7で必須）、`SaveMigrationStepV6ToV7`（`FromVersion => 6`）

- [x] **Step 1: moorestech-save-migration スキルを読み、移行ステップを書く**

`SaveMigrationStepV6ToV7.cs`:

```csharp
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V6→V7移行: ロケットの初打ち上げ履歴を未打ち上げで足す（ADR 0088）
    // V6→V7 migration: add the rocket launch history as not yet launched (ADR 0088)
    public sealed class SaveMigrationStepV6ToV7 : ISaveMigrationStep
    {
        public int FromVersion => 6;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            // 版6に履歴は無い。あるなら想定外の形なので変換しない
            // V6 has no launch history; its presence is an unexpected shape, so refuse
            if (save["rocketHasLaunchedOnce"] is JToken existing && existing.Type != JTokenType.Null) return Fail("版6のセーブに rocketHasLaunchedOnce が既にある");

            // 版6以前に発射台は存在しないので、未打ち上げが唯一の正しい値
            // No launch pad existed before V6, so not-yet-launched is the only correct value
            save["rocketHasLaunchedOnce"] = false;
            Debug.Log("セーブを版6から版7へ変換しました。ロケットの初打ち上げ履歴=未打ち上げ");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string reason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版6から版7へ変換できません: {reason}");
                return SaveMigrationStepResult.Failed(reason);
            }

            #endregion
        }
    }
}
```

3か所の配列の末尾へ `new SaveMigrationStepV6ToV7()` を足す。

- [x] **Step 2: 必須欠損の検査を移して `rocketHasLaunchedOnce` を足す**

`WorldSaveRequiredFieldGuard.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.SaveLoad.Json.WorldVersions;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    // 現在版のセーブに必須の節が欠けていれば理由付きで止める
    // Stops with a reason when a current-version save lacks a required section
    internal static class WorldSaveRequiredFieldGuard
    {
        internal static void ThrowIfMissing(WorldSaveAllInfo load)
        {
            var missing = new List<string>();
            if (!load.CurrentTick.HasValue) missing.Add("currentTick");
            if (load.RandomState == null) missing.Add("randomState");
            if (load.MiningCooldowns == null) missing.Add("miningCooldowns");
            if (load.BackfilledFields == null) missing.Add("backfilledFields");
            if (load.Players == null) missing.Add("players");
            if (!load.RocketHasLaunchedOnce.HasValue) missing.Add("rocketHasLaunchedOnce");
            if (missing.Count == 0) return;

            var reason = $"セーブに {string.Join(" / ", missing)} がありません。版が古いセーブはマイグレーション連鎖（Game.SaveLoad/Migration）が補填するため、現在版のセーブで欠けているのは手編集による破損です";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
```

`WorldSaveDataRestorer.Restore` の `ThrowIfRequiredFieldMissing();` を `WorldSaveRequiredFieldGuard.ThrowIfMissing(load);` に置き換え、ローカル関数 `ThrowIfRequiredFieldMissing` を消す。`_gameUnlockStateDataController.LoadUnlockState(...)` の次に追加する:

```csharp
            // 発射台がロード直後の初回Updateで参照するので、ブロックより先に戻す
            // Launch pads read it on their first update after load, so restore it before blocks
            _rocketLaunchHistory.LoadSaveData(load.RocketHasLaunchedOnce.Value);
```

`WorldSaveAllInfo` の ctor 引数・代入・プロパティ、`AssembleSaveJsonText` の ctor・`Capture()` の引数（`_cleanRoomDatastore.GetSaveData(),` の次に `_rocketLaunchHistory.HasLaunchedOnce,`）を足す。`WorldSaveAllInfo` を `new` している他の箇所（`git grep -n "new WorldSaveAllInfo("`）も引数を足す。`MissingMasterPruner` の一覧に `"rocketHasLaunchedOnce"` を足す。

- [x] **Step 3: テストを書く**

`Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV6ToV7Test.cs`:

```csharp
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV6ToV7Test
    {
        [Test]
        public void 版6のセーブに未打ち上げの履歴が足され他の節は残る()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"world\":[{\"blockGuid\":\"x\"}],\"cleanRoomRooms\":[]}");

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
            Assert.AreEqual("x", result.Save["world"][0]["blockGuid"].Value<string>());
        }

        [Test]
        public void 履歴が既にある版6は変換しない()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"rocketHasLaunchedOnce\":true}");
            LogAssert.Expect(LogType.Warning, new Regex("版6から版7へ変換できません"));

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsFalse(result.IsConverted);
        }
    }
}
```

`Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchHistorySaveLoadTest.cs`: `CleanRoomSaveLoadTest` と `SaveLoadJsonTestHelper` の書き方に合わせ、(a) `RecordLaunch()` 後にセーブ→新しいDIでロードして `HasLaunchedOnce == true`、(b) 版6のセーブJSON（`SaveLoadJsonTestHelper` が作る現在版のJSONから `rocketHasLaunchedOnce` を消し `worldVersion` を6にしたもの）をロードして `HasLaunchedOnce == false` になることを確かめる2件を書く。

既存テストやフィクスチャが「現在版=6」を前提にしていれば（`git grep -n "worldVersion\\\\\":6\|CurrentVersion" -- moorestech_server/Assets/Scripts/Tests`）、7へ直す。

- [x] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoad|RocketLaunchHistory|MissingMasterPruner"`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "feat(save): 初打ち上げ履歴をセーブに入れ版6から7への移行を足す"
```

---

### Task 4: 発射台の契約型（インターフェース層）・インベントリ・段階の型

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/RocketLaunchPad/IRocketLaunchPadComponent.cs`（合図を受ける口と結果 enum。前例 `Game.Block.Interface/Component/IMachineRecipeSelectorComponent.cs`）
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/RocketLaunchPad/RocketLaunchPadSlotLayout.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/RocketLaunchPad/RocketLaunchPadPhase.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/RocketLaunchPad/RocketLaunchPadWaitReason.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/RocketLaunchPad/RocketLaunchPadStateDetail.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadInventoryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketCargo.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/Stage/RocketLaunchPadStage.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadInventoryTest.cs`（Task 5 のテンプレート登録後に実行する。このタスクではコンパイルまで）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/RocketLaunchPad/RocketLaunchPadStageTest.cs`

配置: 契約型（合図の口と結果・段階・待ち理由・状態詳細・スロット並び）はサーバーの部品・プロトコル・クライアントの表示が共有するので `Game.Block.Interface` に置く（判断記録 D27）。`Game.Block.Interface/Component/` と `State/` の直下には足さず、前例 `Component/CleanRoom/` と同じくドメインのサブディレクトリを作る。名前空間はディレクトリによらず `Game.Block.Interface.Component`・`Game.Block.Interface.State`（前例どおり）。`Game.Block.Interface.asmdef` は `Core.Master` と `MessagePack` を既に参照しており、生成型 `Mooresmaster.Model.*` も `IMachineRecipeSelectorComponent` が使っている。

**Interfaces:**
- Consumes: Task 1 の `RocketLaunchPadBlockParam`・`Items.OrbitalPayloads`
- Produces:
  - `Game.Block.Interface.Component.IRocketLaunchPadComponent : IBlockComponent`（`RocketLaunchPadCommandResult RequestBuild()`・`RocketLaunchPadCommandResult RequestLaunch()`）
  - enum `Game.Block.Interface.Component.RocketLaunchPadCommandResult { Accepted, NotAwaitingBuildCommand, NotAwaitingLaunchCommand, CargoNotReady, OutputFull }`
  - `Game.Block.Interface.Component.RocketLaunchSlotRequirement`（readonly struct: `public readonly ItemId ItemId; public readonly int Count;`）
  - `Game.Block.Interface.Component.RocketLaunchPadSlotLayout`（ctor `(RocketLaunchPadBlockParam param)`、`IReadOnlyList<RocketLaunchSlotRequirement> Materials`・`Payloads`、`IReadOnlyList<int> MaterialSlots`・`PayloadSlots`・`OutputSlots`（群ごとの絶対添字）、`int OutputSlotStart`・`OutputSlotCount`・`TotalSlotCount`、`int PayloadSlot(int payloadIndex)`、`bool IsOutputSlot(int slot)`、`bool TryGetInputSlot(ItemId itemId, out int slot)`、`bool TryGetPayloadRequirement(ItemId itemId, out RocketLaunchSlotRequirement requirement)`、`RocketLaunchSlotRequirement RequirementAt(int inputSlot)`）
  - enum `Game.Block.Interface.State.RocketLaunchPadPhase { Idle = 0, AwaitingBuildCommand = 1, Building = 2, AwaitingLaunchCommand = 3, ReadyToLaunch = 4, Launching = 5 }`（セーブに int で入るので値を固定する）
  - enum `Game.Block.Interface.State.RocketLaunchPadWaitReason { None = 0, MissingMaterials = 1, MissingPayload = 2, OutputFull = 3, NoPower = 4 }`
  - `Game.Block.Interface.State.RocketLaunchPadStateDetail`（`const string BlockStateDetailKey = "RocketLaunchPad"`、`[Key(0)] Phase`・`[Key(1)] WaitReason`・`[Key(2)] float Progress`、`static BlockStateDetail CreateState(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)`）
  - `Game.Block.Blocks.RocketLaunchPad.RocketLaunchPadInventoryComponent : IOpenableBlockInventoryComponent, ISortExcludedSlots`（ctor `(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)`）
  - `Game.Block.Blocks.RocketLaunchPad.RocketCargo`（`public readonly IItemStack Payload; public readonly IReadOnlyList<IItemStack> Materials; bool IsLoaded`、ctor `(IItemStack payload, IReadOnlyList<IItemStack> materials)`、`IEnumerable<IItemStack> AllItems()`）
  - 段階の型（名前空間 `Game.Block.Blocks.RocketLaunchPad.Stage`）: 抽象 `RocketLaunchPadStage`（`abstract RocketLaunchPadPhase Phase`）、抽象 `LoadedRocketStage : RocketLaunchPadStage`（`public readonly RocketCargo Cargo`）、`IdleStage.Instance`、`AwaitingBuildCommandStage.Instance`、`BuildingStage(RocketCargo cargo, uint remainingTicks)`、`AwaitingLaunchCommandStage(RocketCargo cargo)`、`ReadyToLaunchStage(RocketCargo cargo)`、`LaunchingStage(uint remainingTicks)`、interface `IProgressingStage`（`uint RemainingTicks { get; }`。`BuildingStage`・`LaunchingStage` が実装）。積荷は `LoadedRocketStage` の3種だけが持ち、残りtickは `IProgressingStage` の2種だけが持つ。「積荷を持つ段階」「進行中の段階」の集合はこの型の階層から引き、enum の集合で書き直さない（再検査 弱(e)）

- [x] **Step 1: 合図の口と結果を書く**

`Game.Block.Interface/Component/RocketLaunchPad/IRocketLaunchPadComponent.cs`:

```csharp
namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     建造・発射の合図を受ける発射台の部品。合図が意味を持つのはワールドの初打ち上げ前だけ
    ///     Launch pad component accepting build and launch commands; they only matter before the world's first launch
    /// </summary>
    public interface IRocketLaunchPadComponent : IBlockComponent
    {
        RocketLaunchPadCommandResult RequestBuild();
        RocketLaunchPadCommandResult RequestLaunch();
    }

    public enum RocketLaunchPadCommandResult
    {
        Accepted,
        NotAwaitingBuildCommand,
        NotAwaitingLaunchCommand,
        CargoNotReady,
        OutputFull,
    }
}
```

- [x] **Step 2: スロット並びを書く**

`Game.Block.Interface/Component/RocketLaunchPad/RocketLaunchPadSlotLayout.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Component
{
    // 発射台のスロット並び。資材→積荷→出力の順で、入れ口は1アイテムにつき1スロット。サーバーと表示が同じ並びを使う
    // Slot order of a launch pad: materials, payloads, outputs, one input slot per item; the server and the display share it
    public class RocketLaunchPadSlotLayout
    {
        public readonly IReadOnlyList<RocketLaunchSlotRequirement> Materials;
        public readonly IReadOnlyList<RocketLaunchSlotRequirement> Payloads;
        public readonly IReadOnlyList<int> MaterialSlots;
        public readonly IReadOnlyList<int> PayloadSlots;
        public readonly IReadOnlyList<int> OutputSlots;
        public readonly int OutputSlotStart;
        public readonly int OutputSlotCount;
        public int TotalSlotCount => OutputSlotStart + OutputSlotCount;

        public RocketLaunchPadSlotLayout(RocketLaunchPadBlockParam param)
        {
            // 資材はブロックパラメータ、積荷は積荷表の並びのまま並べる
            // Materials follow the block parameter and payloads follow the payload table order
            Materials = param.LaunchMaterials.Select(material => new RocketLaunchSlotRequirement(MasterHolder.ItemMaster.GetItemId(material.ItemGuid), material.Count)).ToList();
            Payloads = MasterHolder.ItemMaster.Items.OrbitalPayloads.Select(payload => new RocketLaunchSlotRequirement(MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid), payload.CountPerLaunch)).ToList();
            OutputSlotStart = Materials.Count + Payloads.Count;
            OutputSlotCount = param.OutputSlotCount;

            // 群ごとの絶対添字。表示側はこれをそのまま使い、並びを計算し直さない
            // Absolute indices per group; the display uses them as-is and never recomputes the order
            MaterialSlots = Enumerable.Range(0, Materials.Count).ToList();
            PayloadSlots = Enumerable.Range(Materials.Count, Payloads.Count).ToList();
            OutputSlots = Enumerable.Range(OutputSlotStart, OutputSlotCount).ToList();
        }

        public int PayloadSlot(int payloadIndex)
        {
            return PayloadSlots[payloadIndex];
        }

        public bool IsOutputSlot(int slot)
        {
            return slot >= OutputSlotStart;
        }

        public RocketLaunchSlotRequirement RequirementAt(int inputSlot)
        {
            return inputSlot < Materials.Count ? Materials[inputSlot] : Payloads[inputSlot - Materials.Count];
        }

        // 資材と積荷が同じアイテムにならないことはマスタ検証が保証する
        // Master validation guarantees no item is both a material and a payload
        public bool TryGetInputSlot(ItemId itemId, out int slot)
        {
            for (var i = 0; i < OutputSlotStart; i++)
            {
                if (RequirementAt(i).ItemId != itemId) continue;
                slot = i;
                return true;
            }
            slot = 0;
            return false;
        }

        // 積荷表に載る積荷の1便ぶんの要求を引く。資材は対象にしない
        // Looks up one launch worth of a listed payload; materials are not considered
        public bool TryGetPayloadRequirement(ItemId itemId, out RocketLaunchSlotRequirement requirement)
        {
            foreach (var payload in Payloads)
            {
                if (payload.ItemId != itemId) continue;
                requirement = payload;
                return true;
            }
            requirement = default;
            return false;
        }
    }

    public readonly struct RocketLaunchSlotRequirement
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public RocketLaunchSlotRequirement(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
```

- [x] **Step 3: 段階・待ち理由・状態詳細を書く**

`Game.Block.Interface/State/RocketLaunchPad/RocketLaunchPadPhase.cs`（名前空間 `Game.Block.Interface.State`。値は Interfaces 節のとおり固定し、各値に日英1行ずつ「`AwaitingBuildCommand`・`AwaitingLaunchCommand` は初打ち上げ前だけ」「`ReadyToLaunch` は打ち上げが許され出力口の空きを待つ」を書く）、`RocketLaunchPadWaitReason.cs`（同じく値を固定）。

`Game.Block.Interface/State/RocketLaunchPad/RocketLaunchPadStateDetail.cs`（`TrainPlatformTransferStateDetail` と同形）:

```csharp
using System;
using MessagePack;

namespace Game.Block.Interface.State
{
    // 発射台の段階・待ち理由・進み具合をクライアントへ送る
    // Sends the launch pad's phase, wait reason and progress to the client
    [MessagePackObject]
    public class RocketLaunchPadStateDetail
    {
        public const string BlockStateDetailKey = "RocketLaunchPad";

        [Key(0)] public RocketLaunchPadPhase Phase { get; set; }
        [Key(1)] public RocketLaunchPadWaitReason WaitReason { get; set; }
        [Key(2)] public float Progress { get; set; }

        public RocketLaunchPadStateDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
        }

        public static BlockStateDetail CreateState(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)
        {
            var stateDetail = new RocketLaunchPadStateDetail(phase, waitReason, progress);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RocketLaunchPadStateDetail() { }
    }
}
```

（`{ get; set; }` は MessagePack の前例 `TrainPlatformTransferStateDetail` どおり。）

- [x] **Step 4: インベントリを書く**

`Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadInventoryComponent.cs`。スロットごとに入れてよい品が決まっているという役割は機械と同じなので、`VanillaMachineBlockInventoryComponent` と同じく `ISortExcludedSlots` で全スロットを整理の対象から外す（外さないと、開いたまま整理ボタンを押したとき `InventorySortService` が ItemId 順に先頭から詰め直し、燃料が筐体素材のスロットへ、資材が出力スロットへ移って建造が止まる）。`GetItem`・`SetItem`・`ReplaceItem`・`GetSlotSize`・`InventoryItems` などの残りのメンバーは `CleanRoomAirFilterInventoryComponent` と同じく `OpenableInventoryItemDataStoreService` へ委譲し、各メンバーの先頭で `CheckDestroy(this)` を呼ぶ。前例と違うメンバーは次のとおり:

```csharp
        // 入れ口も出力口も束縛済みのスロットなので、どれも整理の対象にしない（機械のインベントリと同じ）
        // Every input and output slot is bound, so none takes part in sorting (same as machine inventories)
        public IReadOnlyCollection<int> SortExcludedSlots
        {
            get
            {
                CheckDestroy(this);
                return Enumerable.Range(0, GetSlotSize()).ToList();
            }
        }

        // 入れ口は割り当てたアイテムだけ、出力口は取り出し専用
        // Input slots accept only their assigned item; output slots are take-out only
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            if (itemStack.Count == 0) return true;
            if (_layout.IsOutputSlot(slot)) return false;
            return _layout.RequirementAt(slot).ItemId == itemStack.Id;
        }

        public IItemStack InsertItem(IItemStack itemStack) => InsertIntoInputSlots(new List<IItemStack> { itemStack })[0];
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) => InsertItem(itemStack);
        public IItemStack InsertItem(ItemId itemId, int count) => InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return InsertIntoInputSlots(itemStacks).Where(remainder => remainder.Count > 0).ToList();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            return SimulateInsert(itemStacks).remainders.All(remainder => remainder.Count == 0);
        }

        private List<IItemStack> InsertIntoInputSlots(IReadOnlyList<IItemStack> itemStacks)
        {
            CheckDestroy(this);

            // 仮挿入の結果をそのまま書き込む。可否判定と実挿入で規則が食い違わないようにするため
            // Write the simulated result as-is so the check and the real insert never disagree
            var (slots, remainders) = SimulateInsert(itemStacks);
            foreach (var pair in slots) _slots.SetItem(pair.Key, pair.Value);
            return remainders;
        }

        // 書き込まずに、各スタックを自分の入れ口へ順に仮挿入する（前例 VanillaMachineInputSimulationUtil.SimulateInsert）
        // Simulates inserting each stack into its own input slot in order without writing (precedent: VanillaMachineInputSimulationUtil.SimulateInsert)
        private (Dictionary<int, IItemStack> slots, List<IItemStack> remainders) SimulateInsert(IReadOnlyList<IItemStack> itemStacks)
        {
            var simulated = new Dictionary<int, IItemStack>();
            var remainders = new List<IItemStack>(itemStacks.Count);
            foreach (var itemStack in itemStacks)
            {
                if (!_layout.TryGetInputSlot(itemStack.Id, out var slot))
                {
                    // silent-ok: 入れ口の無い品は余りとして呼び出し元へ返り、ベルト上や手元に残る / プレイヤーが入らないことを目視で気づく
                    remainders.Add(itemStack);
                    continue;
                }
                var result = (simulated.TryGetValue(slot, out var stack) ? stack : _slots.GetItem(slot)).AddItem(itemStack);
                simulated[slot] = result.ProcessResultItemStack;
                remainders.Add(result.RemainderItemStack);
            }
            return (simulated, remainders);
        }
```

ctor は `(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)` で2つを保持する。`IsDestroy`/`Destroy()` は前例どおり。仮挿入 `SimulateInsert` は1本だけ置き、`InsertionCheck` と実挿入の両方がこれを通る（前例 `VanillaMachineInputInventory` の `InsertItem`・`InsertionCheck` が `VanillaMachineInputSimulationUtil.SimulateInsert` を共有する形。再検査 弱(d)）。

- [x] **Step 5: 積荷と段階の型を書く**

`Game.Block/Blocks/RocketLaunchPad/RocketCargo.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // ロケットに積んだ積荷と資材。打ち上げで消費され、打ち上げ前の撤去では返る
    // Payload and materials loaded into a rocket; consumed by the launch and refunded on removal before it
    public class RocketCargo
    {
        public readonly IItemStack Payload;
        public readonly IReadOnlyList<IItemStack> Materials;
        public bool IsLoaded => Payload.Count > 0;

        public RocketCargo(IItemStack payload, IReadOnlyList<IItemStack> materials)
        {
            Payload = payload;
            Materials = materials;
        }

        // 欠損マスタの刈り込みで空になった資材は返さない
        // Materials emptied by missing-master pruning are not refunded
        public IEnumerable<IItemStack> AllItems()
        {
            return Materials.Prepend(Payload).Where(item => item.Count > 0);
        }
    }
}
```

`Game.Block/Blocks/RocketLaunchPad/Stage/RocketLaunchPadStage.cs`:

```csharp
using System;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    // 発射台の段階。段階ごとに意味を持つ値だけを持ち、作った後は変えない
    // A launch pad stage; it holds only the values meaningful to that stage and never changes after creation
    public abstract class RocketLaunchPadStage
    {
        public abstract RocketLaunchPadPhase Phase { get; }
    }

    // ロケットを積んでいる段階。撤去すると中身が返る
    // Stages holding a loaded rocket; removing the pad refunds its contents
    public abstract class LoadedRocketStage : RocketLaunchPadStage
    {
        public readonly RocketCargo Cargo;

        protected LoadedRocketStage(RocketCargo cargo)
        {
            // 空のロケットを積んだ段階は作れない
            // A loaded stage cannot be created with an empty rocket
            if (!cargo.IsLoaded) throw new ArgumentException("積荷の無いロケットで積載中の段階を作ろうとしました", nameof(cargo));
            Cargo = cargo;
        }
    }

    // 残りtickで進む段階（建造中・打ち上げ中）。進行中かと残りtickはこの型で引く
    // Stages advancing by remaining ticks (building, launching); progress and remaining ticks are queried through this type
    public interface IProgressingStage
    {
        uint RemainingTicks { get; }
    }

    // 1便ぶんの資材と積荷、成果を入れる出力口の空きを待つ
    // Waits for one launch worth of materials and payload and for output space for the arrival outputs
    public sealed class IdleStage : RocketLaunchPadStage
    {
        public static readonly IdleStage Instance = new();
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Idle;

        private IdleStage() { }
    }

    // 初打ち上げ前だけ: 揃った台がプレイヤーの建造の合図を待つ
    // Before the first launch only: a ready pad waits for the player's build command
    public sealed class AwaitingBuildCommandStage : RocketLaunchPadStage
    {
        public static readonly AwaitingBuildCommandStage Instance = new();
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.AwaitingBuildCommand;

        private AwaitingBuildCommandStage() { }
    }

    public sealed class BuildingStage : LoadedRocketStage, IProgressingStage
    {
        public uint RemainingTicks { get; }
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Building;

        public BuildingStage(RocketCargo cargo, uint remainingTicks) : base(cargo)
        {
            // 残り0の建造中は作れない。建造が終わったら次の段階へ移るため
            // A building stage with zero ticks left cannot exist; a finished build moves to the next stage
            if (remainingTicks == 0) throw new ArgumentOutOfRangeException(nameof(remainingTicks), "建造中の段階は残りtickが1以上です");
            RemainingTicks = remainingTicks;
        }
    }

    // 初打ち上げ前だけ: 完成したロケットがプレイヤーの発射の合図を待つ
    // Before the first launch only: a finished rocket waits for the player's launch command
    public sealed class AwaitingLaunchCommandStage : LoadedRocketStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.AwaitingLaunchCommand;

        public AwaitingLaunchCommandStage(RocketCargo cargo) : base(cargo) { }
    }

    // 打ち上げが許され、成果が出力口に入りきるのを待つ。打ち上げはこの段階からだけ始まる
    // Launch is permitted and waits until the outputs fit the output slots; launches start only from this stage
    public sealed class ReadyToLaunchStage : LoadedRocketStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.ReadyToLaunch;

        public ReadyToLaunchStage(RocketCargo cargo) : base(cargo) { }
    }

    public sealed class LaunchingStage : RocketLaunchPadStage, IProgressingStage
    {
        public uint RemainingTicks { get; }
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Launching;

        public LaunchingStage(uint remainingTicks)
        {
            if (remainingTicks == 0) throw new ArgumentOutOfRangeException(nameof(remainingTicks), "打ち上げ中の段階は残りtickが1以上です");
            RemainingTicks = remainingTicks;
        }
    }
}
```

（`RemainingTicks` は `IProgressingStage` の実装なので get 専用のプロパティにし、ctor でだけ代入する。）

- [x] **Step 6: テストを書く（実行は Task 5 Step 8）**

`Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadInventoryTest.cs`。`ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.RocketLaunchPadId, ...)` で台を置き（既存 CombinedTest の設置書式に合わせる）、`IBlockInventory` と `IOpenableBlockInventoryComponent` を取って次を確かめる:
- `InsertItem(TestRocketFuel×5)` が資材スロット1（2番目）に入り、余りが空。
- `InsertItem(TestComputeData×1)` はそのまま返る（入れ口が無い）。
- `IsAllowedToPlace(出力スロット, 任意の品)` が false、`IsAllowedToPlace(0, TestRocketFuel)` が false（スロット0は筐体素材専用）、`IsAllowedToPlace(0, TestRocketCase)` が true。
- `InsertionCheck([TestRocketCase×99, TestRocketCase×2])` が false（スタック100を超える）、`[TestRocketCase×99, TestSatellitePayload×1]` が true。
- スロット数が `3（資材）+2（積荷）+2（出力）=7`。`RocketLaunchPadSlotLayout` の `MaterialSlots`・`PayloadSlots`・`OutputSlots` が `[0,1,2]`・`[3,4]`・`[5,6]`。
- 整理しても中身が動かない: 資材スロット1へ燃料、積荷スロット3へ衛星を入れ、`SortInventoryProtocol` で開いているブロックの整理を送る（`SortInventoryProtocol` の既存テストの書き方に合わせる。無ければ `InventorySortService.Sort` を `ISortExcludedSlots` 付きで直接呼ぶ）。各スロットの中身が整理前と同じ。

`Tests/UnitTest/Game/RocketLaunchPad/RocketLaunchPadStageTest.cs`（DIを作って `ServerContext.ItemStackFactory` を使う。最小構成の段階を作れないことを固定する）:
- `new BuildingStage(空の積荷, 10)` は `ArgumentException`、`new BuildingStage(積荷, 0)` と `new LaunchingStage(0)` は `ArgumentOutOfRangeException`。
- `ReadyToLaunchStage` と `AwaitingLaunchCommandStage` は `LoadedRocketStage` で、`IdleStage.Instance`・`LaunchingStage` は違う。`BuildingStage` と `LaunchingStage` は `IProgressingStage` で、`ReadyToLaunchStage`・`IdleStage.Instance` は違う。

- [x] **Step 7: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [x] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/RocketLaunchPad moorestech_server/Assets/Scripts/Game.Block.Interface/State/RocketLaunchPad moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad moorestech_server/Assets/Scripts/Tests/UnitTest/Game/RocketLaunchPad
git commit -m "feat(block): 発射台の契約型とインベントリと段階の型を足す"
```

---

### Task 5: 発射台の進行（外殻と段階の進行）・出力口・電力・セーブとテンプレート

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadCargoLoader.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadOutputSlots.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadLaunchSequence.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadProcessorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadElectricComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/Save/RocketLaunchPadSaveJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/Save/RocketLaunchPadSaveConverter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`（`BlockTypesDictionary` に `RocketLaunchPad`）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:121-126`（`IGetRefundItemsInfo` を全件合算。ファイルはちょうど200行なので、6行を6行で置き換え行を増やさない）
- Create: `moorestech_server/Assets/Scripts/Tests/Util/Rocket/RocketLaunchPadTestUtil.cs`（`Tests/Util/` 直下は19件あるのでサブディレクトリ。前例 `Tests/Util/EnergySystem/ElectricWireTestUtil.cs`）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadRemoveRefundTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadLaunchFlowTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadOutputFullTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadSaveLoadTest.cs`

`Game.Block/Blocks/RocketLaunchPad/` の直下は Task 4 と合わせて .cs が7個（インベントリ・積荷・積み込み・出力口・段階の進行・外殻・電力）と、サブディレクトリ `Stage/`・`Save/` になる。

外殻と段階の進行の分け方: 外殻 `RocketLaunchPadProcessorComponent` はブロック部品としての口（tick・合図・初打ち上げ通知の購読・状態の通知・セーブ・撤去返却）だけを持ち、段階の遷移はすべて `RocketLaunchPadLaunchSequence` が持つ。どちらも200行以内。打ち上げを始めるのは `RocketLaunchPadLaunchSequence.AdvanceReadyToLaunch` の1か所だけで、発射の合図と初打ち上げ通知は段階を `ReadyToLaunchStage` へ移すだけ（判断記録 D25）。打ち上げは自分の Update の中でしか起きないので、別の台の `RecordLaunch` の呼び出しの中から再入で打ち上げることはなく、「段階を先に変えて二重打ち上げを防ぐ」回避策も要らない。

**Interfaces:**
- Consumes: Task 2 の `IOrbitalArrivalService`（`PreviewArrivalOutputs`・`Arrive`）・`IRocketLaunchHistory`、Task 4 の全型
- Produces:
  - `RocketLaunchPadCargoLoader`（`int NextPayloadIndex { get; private set; }`・`void SetNextPayloadIndex(int nextPayloadIndex)`・`bool HasMaterials()`・`IEnumerable<int> ReadyPayloadIndices()`・`RocketCargo LoadCargo(int payloadIndex)`）
  - `RocketLaunchPadOutputSlots`（`bool CanStore(IReadOnlyList<IItemStack> outputs)`・`void Store(IReadOnlyList<IItemStack> outputs)`・`void PushToConnected()`）
  - `RocketLaunchPadLaunchSequence`（`RocketLaunchPadStage Stage { get; private set; }`・`RocketLaunchPadWaitReason WaitReason { get; private set; }`・`float RequestPower`・`void SetStage(RocketLaunchPadStage stage)`・`void Advance(float tickPower)`・`RocketLaunchPadCommandResult RequestBuild()`・`RocketLaunchPadCommandResult RequestLaunch()`・`void ReleaseLaunchCommandWait()`・`float CalculateProgress()`）
  - `RocketLaunchPadProcessorComponent : IRocketLaunchPadComponent, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo`（`RocketLaunchPadPhase Phase`・`RocketLaunchPadWaitReason WaitReason`・`float RequestPower`・`void SupplyPower(float power)`）
  - `RocketLaunchPadElectricComponent : IElectricConsumer, IElectricTickPostHandler`
  - `Save.RocketLaunchPadSaveJsonObject`・`Save.RocketLaunchPadSaveConverter`（`Capture`・`RestoreSlots`・`RestoreStage`）
  - `VanillaRocketLaunchPadTemplate : IBlockTemplate`
  - テスト用 `Tests.Util.Rocket.RocketLaunchPadTestUtil`（`static IBlock PlacePoweredPad(Vector3Int position, float generatePower)`・`static void FillMaterials(IBlock pad)`・`static void FillOneLaunch(IBlock pad, ItemId payloadItemId, int payloadCount)`・`static void FillOutputSlots(IBlock pad, int countPerSlot)`・`static bool RunUntilPhase(IBlock pad, RocketLaunchPadPhase until, int maxTicks)`。Task 6 のテストも使う）

- [x] **Step 1: 積み込みを書く**

`RocketLaunchPadCargoLoader.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 入れ口の資材と積荷を数え、選ばれた1便ぶんを取り出してロケットに積む
    // Counts materials and payloads in the input slots and takes the chosen launch worth into a rocket
    public class RocketLaunchPadCargoLoader
    {
        public int NextPayloadIndex { get; private set; }

        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;

        public RocketLaunchPadCargoLoader(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            _layout = layout;
            _slots = slots;
        }

        public void SetNextPayloadIndex(int nextPayloadIndex)
        {
            // 積荷表の行数がセーブ時から変わっても、負の値でも範囲内に収める（表が空でないことはマスタ検証が保証する）
            // Keep it in range for a changed table size or a negative value (master validation guarantees a non-empty table)
            var count = _layout.Payloads.Count;
            NextPayloadIndex = (nextPayloadIndex % count + count) % count;
        }

        public bool HasMaterials()
        {
            for (var i = 0; i < _layout.Materials.Count; i++)
                if (!HasRequirement(_layout.MaterialSlots[i], _layout.Materials[i])) return false;
            return true;
        }

        // 1便ぶん揃った積荷の番号を前回の次から順に返し、特定の積荷だけが飛び続けないようにする
        // Yields ready payload indices starting after the previous launch so no single payload starves the others
        public IEnumerable<int> ReadyPayloadIndices()
        {
            var count = _layout.Payloads.Count;
            for (var offset = 0; offset < count; offset++)
            {
                var index = (NextPayloadIndex + offset) % count;
                if (IsPayloadReady(index)) yield return index;
            }
        }

        public RocketCargo LoadCargo(int payloadIndex)
        {
            // 揃っていないのに呼ばれたら呼び出し側の段階管理の誤り
            // Being called while not ready is a stage-management bug in the caller
            if (!HasMaterials() || !IsPayloadReady(payloadIndex)) throw new InvalidOperationException("1便ぶんの資材と積荷が揃っていない発射台で建造が始まりました");

            var materials = new List<IItemStack>();
            for (var i = 0; i < _layout.Materials.Count; i++) materials.Add(TakeRequirement(_layout.MaterialSlots[i], _layout.Materials[i]));
            var payload = TakeRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);
            NextPayloadIndex = (payloadIndex + 1) % _layout.Payloads.Count;
            return new RocketCargo(payload, materials);
        }

        // 積荷が1便ぶん揃ったかの判定はここだけが持ち、巡回とガードの両方が呼ぶ
        // The single readiness check for one launch worth of a payload, used by both the scan and the guard
        private bool IsPayloadReady(int payloadIndex)
        {
            return HasRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);
        }

        private bool HasRequirement(int slot, RocketLaunchSlotRequirement requirement)
        {
            var stack = _slots.GetItem(slot);
            return stack.Id == requirement.ItemId && stack.Count >= requirement.Count;
        }

        private IItemStack TakeRequirement(int slot, RocketLaunchSlotRequirement requirement)
        {
            _slots.SetItem(slot, _slots.GetItem(slot).SubItem(requirement.Count));
            return ServerContext.ItemStackFactory.Create(requirement.ItemId, requirement.Count);
        }
    }
}
```

- [x] **Step 2: 出力口を書く**

`RocketLaunchPadOutputSlots.cs`（前例 `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs` の `CanStoreOutputs`・`InsertOutputSlot`・`InsertConnectInventory`）:

```csharp
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 出力口へ成果を入れ、中身を接続先へ流す。入りきるかは機械の出力と同じく書き込まずに試す
    // Stores outputs into the output slots and pushes them onward; the fit check simulates without writing, like machine outputs
    public class RocketLaunchPadOutputSlots
    {
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly IBlockInventoryInserter _beltOutput;

        public RocketLaunchPadOutputSlots(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput)
        {
            _layout = layout;
            _slots = slots;
            _beltOutput = beltOutput;
        }

        public bool CanStore(IReadOnlyList<IItemStack> outputs)
        {
            // 出力口の写しへ順に詰め、余りが出たら入りきらない
            // Fill a copy of the output slots in order; any remainder means they do not fit
            var simulated = CopyOutputSlots();
            foreach (var output in outputs)
                if (FillInOrder(simulated, output).Count > 0) return false;
            return true;
        }

        public void Store(IReadOnlyList<IItemStack> outputs)
        {
            // 同じtickの直前に CanStore を通したので余りは出ない。出たら判定と書き込みの食い違いとして残す（機械の出力と同じ）
            // CanStore passed earlier in the same tick, so no remainder is expected; log a mismatch if one appears (as machine outputs do)
            var current = CopyOutputSlots();
            foreach (var output in outputs)
            {
                var remainder = FillInOrder(current, output);
                if (remainder.Count > 0) Debug.LogError($"発射台の出力口に成果が入りきりませんでした ItemId:{remainder.Id} Count:{remainder.Count}");
            }
            for (var i = 0; i < _layout.OutputSlots.Count; i++) _slots.SetItem(_layout.OutputSlots[i], current[i]);
        }

        public void PushToConnected()
        {
            // 出力口の中身は機械の出力と同じく毎tick接続先へ流す
            // Push output slot contents to connected inventories every tick, as machine outputs do
            foreach (var slot in _layout.OutputSlots) _slots.SetItem(slot, _beltOutput.InsertItem(_slots.GetItem(slot)));
        }

        private List<IItemStack> CopyOutputSlots()
        {
            var copy = new List<IItemStack>();
            foreach (var slot in _layout.OutputSlots) copy.Add(_slots.GetItem(slot));
            return copy;
        }

        // 写しのスロットへ先頭から詰め、余りを返す
        // Fills the copied slots from the first one and returns the remainder
        private static IItemStack FillInOrder(List<IItemStack> slots, IItemStack item)
        {
            var rest = item;
            for (var i = 0; i < slots.Count && rest.Count > 0; i++)
            {
                var result = slots[i].AddItem(rest);
                slots[i] = result.ProcessResultItemStack;
                rest = result.RemainderItemStack;
            }
            return rest;
        }
    }
}
```

- [x] **Step 3: 段階の進行を書く**

`RocketLaunchPadLaunchSequence.cs`（段階の遷移はここだけが持つ。遷移の持ち主の置き方は新規パターン（D26 の裁定による。機械の State ハンドラ方式とは遷移の持ち主が違う）。残りtickと電力の進め方の前例は機械の処理状態 `Game.Block/Blocks/Machine/State/ProcessingMachineProcessState.cs`）:

```csharp
using Core.Master;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Blocks.Util;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 発射台の段階を1tickずつ進める。建造は成果が出力口に入りきるときだけ始め、打ち上げは ReadyToLaunch からだけ始める
    // Advances the pad stage one tick at a time; builds start only when outputs fit, launches only from ReadyToLaunch
    public class RocketLaunchPadLaunchSequence
    {
        public RocketLaunchPadStage Stage { get; private set; } = IdleStage.Instance;
        public RocketLaunchPadWaitReason WaitReason { get; private set; }
        public float RequestPower => Stage is BuildingStage ? _requiredPower : 0f;

        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly RocketLaunchPadCargoLoader _cargoLoader;
        private readonly RocketLaunchPadOutputSlots _outputSlots;
        private readonly IOrbitalArrivalService _arrivalService;
        private readonly IRocketLaunchHistory _launchHistory;
        private readonly float _requiredPower;
        private readonly uint _buildTotalTicks;
        private readonly uint _launchTotalTicks;

        public RocketLaunchPadLaunchSequence(RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, RocketLaunchPadCargoLoader cargoLoader, RocketLaunchPadOutputSlots outputSlots, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
        {
            _layout = layout;
            _cargoLoader = cargoLoader;
            _outputSlots = outputSlots;
            _arrivalService = arrivalService;
            _launchHistory = launchHistory;
            _requiredPower = param.RequiredPower;
            _buildTotalTicks = GameUpdater.SecondsToTicks(param.BuildSeconds);
            _launchTotalTicks = GameUpdater.SecondsToTicks(param.LaunchSeconds);
        }

        public void SetStage(RocketLaunchPadStage stage)
        {
            Stage = stage;
        }

        public void Advance(float tickPower)
        {
            // 発射の合図待ちは tick では進めない。合図か初打ち上げの通知でだけ段階が移る
            // The launch command wait does not advance by tick; only the command or the first-launch notice moves it
            if (Stage is IdleStage or AwaitingBuildCommandStage) AdvanceWaitingForCargo();
            else if (Stage is BuildingStage building) AdvanceBuilding(building, tickPower);
            else if (Stage is ReadyToLaunchStage ready) AdvanceReadyToLaunch(ready);
            else if (Stage is LaunchingStage launching) AdvanceLaunching(launching);
        }

        public RocketLaunchPadCommandResult RequestBuild()
        {
            if (Stage is not AwaitingBuildCommandStage) return RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
            var reason = CheckBuildable(out var payloadIndex);
            if (reason == RocketLaunchPadWaitReason.OutputFull) return RocketLaunchPadCommandResult.OutputFull;
            if (reason != RocketLaunchPadWaitReason.None) return RocketLaunchPadCommandResult.CargoNotReady;
            StartBuilding(payloadIndex);
            return RocketLaunchPadCommandResult.Accepted;
        }

        public RocketLaunchPadCommandResult RequestLaunch()
        {
            // 合図は段階を移すだけ。打ち上げは次の Advance の AdvanceReadyToLaunch が行う
            // The command only moves the stage; the next Advance performs the launch in AdvanceReadyToLaunch
            if (Stage is not AwaitingLaunchCommandStage awaiting) return RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
            Stage = new ReadyToLaunchStage(awaiting.Cargo);
            return RocketLaunchPadCommandResult.Accepted;
        }

        // 初打ち上げの後は発射の合図を待たない。段階を移すだけで、打ち上げは次の Advance が行う。建造の合図待ちは AdvanceWaitingForCargo が解く
        // No launch command wait after the first launch; only the stage moves and the next Advance launches. AdvanceWaitingForCargo releases the build wait
        public void ReleaseLaunchCommandWait()
        {
            if (Stage is AwaitingLaunchCommandStage awaiting) Stage = new ReadyToLaunchStage(awaiting.Cargo);
        }

        public float CalculateProgress()
        {
            // 建造中・打ち上げ中は0〜1、完成したロケットが待つ段階は1。マスタ変更で残りが総量を超えても0〜1に収める
            // Building and launching report 0..1 and a finished waiting rocket reports 1; clamp even if remaining exceeds the total after a master change
            return Stage switch
            {
                BuildingStage building => Mathf.Clamp01(1f - building.RemainingTicks / (float)_buildTotalTicks),
                LaunchingStage launching => Mathf.Clamp01(1f - launching.RemainingTicks / (float)_launchTotalTicks),
                AwaitingLaunchCommandStage or ReadyToLaunchStage => 1f,
                _ => 0f,
            };
        }

        private void AdvanceWaitingForCargo()
        {
            WaitReason = CheckBuildable(out var payloadIndex);
            if (WaitReason != RocketLaunchPadWaitReason.None)
            {
                Stage = IdleStage.Instance;
                // silent-ok: 出力口の空き待ちは通常の待機（資材・積荷の待ちも同じ） / プレイヤーは発射台 UI の待ち理由で気づく
                return;
            }

            // 初打ち上げ前のワールドではプレイヤーの建造の合図を待つ。済んでいれば建造の合図待ちの台もここで建造を始める
            // Before the world's first launch wait for the build command; afterwards pads awaiting it start building here
            if (_launchHistory.HasLaunchedOnce) StartBuilding(payloadIndex);
            else Stage = AwaitingBuildCommandStage.Instance;
        }

        private void AdvanceBuilding(BuildingStage building, float tickPower)
        {
            // 電力の供給率だけ建造を進める
            // Advance the build by the power supply rate
            WaitReason = tickPower <= 0f ? RocketLaunchPadWaitReason.NoPower : RocketLaunchPadWaitReason.None;
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(tickPower, _requiredPower);
            if (subTicks < building.RemainingTicks)
            {
                Stage = new BuildingStage(building.Cargo, building.RemainingTicks - subTicks);
                return;
            }

            // 初打ち上げ前は発射の合図を待ち、以後は打ち上げ待ちへ進む
            // Before the first launch wait for the launch command, otherwise move on to ready-to-launch
            WaitReason = RocketLaunchPadWaitReason.None;
            if (_launchHistory.HasLaunchedOnce) Stage = new ReadyToLaunchStage(building.Cargo);
            else Stage = new AwaitingLaunchCommandStage(building.Cargo);
        }

        private void AdvanceReadyToLaunch(ReadyToLaunchStage ready)
        {
            // 打ち上げを始める唯一の口。成果が出力口に入りきらない間は待たせる
            // The only place a launch starts; wait while the outputs do not fit the output slots
            var payload = ready.Cargo.Payload;
            if (!CanStoreArrivalOutputs(payload.Id, payload.Count))
            {
                WaitReason = RocketLaunchPadWaitReason.OutputFull;
                // silent-ok: 出力口の空き待ちは通常の待機 / プレイヤーは発射台 UI の待ち理由で気づく
                return;
            }

            // 打ち上げ開始が軌道到着（CONTEXT.md）。成果を出力口へ入れ、ワールドの打ち上げを記録する
            // Launch start is orbital arrival (CONTEXT.md); store the outputs and record the world's launch
            WaitReason = RocketLaunchPadWaitReason.None;
            Stage = new LaunchingStage(_launchTotalTicks);
            _outputSlots.Store(_arrivalService.Arrive(payload.Id, payload.Count));
            _launchHistory.RecordLaunch();
        }

        private void AdvanceLaunching(LaunchingStage launching)
        {
            if (launching.RemainingTicks > 1) Stage = new LaunchingStage(launching.RemainingTicks - 1);
            else Stage = IdleStage.Instance;
        }

        // 揃った積荷のうち、成果が出力口に入りきる最初の1つを選ぶ。成果の無い積荷は出力口が満杯でも選べる
        // Picks the first ready payload whose outputs fit; payloads without outputs can go even when the outputs are full
        private RocketLaunchPadWaitReason CheckBuildable(out int payloadIndex)
        {
            payloadIndex = 0;
            if (!_cargoLoader.HasMaterials()) return RocketLaunchPadWaitReason.MissingMaterials;

            var anyReady = false;
            foreach (var index in _cargoLoader.ReadyPayloadIndices())
            {
                anyReady = true;
                var payload = _layout.Payloads[index];
                if (!CanStoreArrivalOutputs(payload.ItemId, payload.Count)) continue;
                payloadIndex = index;
                return RocketLaunchPadWaitReason.None;
            }
            return anyReady ? RocketLaunchPadWaitReason.OutputFull : RocketLaunchPadWaitReason.MissingPayload;
        }

        // 先読みした成果が出力口に入りきるか。建造前と打ち上げ直前の判定がこの1本を使う
        // Whether the previewed outputs fit the output slots; both the pre-build and pre-launch checks use this
        private bool CanStoreArrivalOutputs(ItemId payloadItemId, int payloadCount)
        {
            return _outputSlots.CanStore(_arrivalService.PreviewArrivalOutputs(payloadItemId, payloadCount));
        }

        private void StartBuilding(int payloadIndex)
        {
            Stage = new BuildingStage(_cargoLoader.LoadCargo(payloadIndex), _buildTotalTicks);
            WaitReason = RocketLaunchPadWaitReason.None;
        }
    }
}
```

（約189行。実装で200行を超えたら `CheckBuildable` と `StartBuilding` を `RocketLaunchPadBuildStarter` へ出す。電力の進め方が `ProcessingMachineProcessState` と食い違えば前例に合わせる。）

- [x] **Step 4: 外殻を書く**

`RocketLaunchPadProcessorComponent.cs`（段階は持たず、段階の進行へ渡すだけ。セーブ形の組み立ては Step 5 の `RocketLaunchPadSaveConverter`）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.RocketLaunchPad.Save;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    /// <summary>
    ///     発射台の部品としての口。tick・合図・初打ち上げ通知を段階の進行へ渡し、状態の通知・セーブ・撤去返却を受け持つ
    ///     The pad's component surface: routes ticks, commands and the first-launch notice to the sequence, and owns state publishing, saving and refunds
    /// </summary>
    public class RocketLaunchPadProcessorComponent : IRocketLaunchPadComponent, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        public RocketLaunchPadPhase Phase => _sequence.Stage.Phase;
        public RocketLaunchPadWaitReason WaitReason => _sequence.WaitReason;
        public float RequestPower => _sequence.RequestPower;
        public IObservable<Unit> OnChangeBlockState => _changeState;
        public string SaveKey { get; } = typeof(RocketLaunchPadProcessorComponent).FullName;
        public bool IsDestroy { get; private set; }

        private readonly Subject<Unit> _changeState = new();
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly RocketLaunchPadCargoLoader _cargoLoader;
        private readonly RocketLaunchPadOutputSlots _outputSlots;
        private readonly RocketLaunchPadLaunchSequence _sequence;
        private readonly IDisposable _firstLaunchSubscription;
        private float _suppliedPower;

        public RocketLaunchPadProcessorComponent(RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
        {
            _layout = layout;
            _slots = slots;
            _cargoLoader = new RocketLaunchPadCargoLoader(layout, slots);
            _outputSlots = new RocketLaunchPadOutputSlots(layout, slots, beltOutput);
            _sequence = new RocketLaunchPadLaunchSequence(param, layout, _cargoLoader, _outputSlots, arrivalService, launchHistory);

            // ワールドの初打ち上げで発射の合図待ちを解く。建造の合図待ちと打ち上げそのものは各台の次の Update が行う
            // On the world's first launch release the launch command wait; each pad's next update handles the build wait and the launch
            _firstLaunchSubscription = launchHistory.OnFirstLaunch.Subscribe(_ => OnWorldFirstLaunch());
        }

        public RocketLaunchPadProcessorComponent(Dictionary<string, object> componentStates, RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
            : this(param, layout, slots, beltOutput, arrivalService, launchHistory)
        {
            // スロット・次に探す積荷・段階を戻す。今の積荷表で打ち上げられない積荷はコンバーターが警告して捨てる
            // Restore slots, the next payload index and the stage; the converter warns about and drops cargo the current table cannot launch
            var saveData = BlockComponentStateReader.Read<RocketLaunchPadSaveJsonObject>(componentStates, SaveKey);
            RocketLaunchPadSaveConverter.RestoreSlots(saveData, _layout, _slots);
            _cargoLoader.SetNextPayloadIndex(saveData.NextPayloadIndex);
            _sequence.SetStage(RocketLaunchPadSaveConverter.RestoreStage(saveData, _layout));

            // 初打ち上げ済みのワールドで発射の合図待ちのまま保存された台は、通知を受けた台と同じく打ち上げ待ちへ移す（履歴はブロックより先にロードされる）
            // Pads saved awaiting the launch command in an already-launched world move to ready-to-launch like notified pads (history loads before blocks)
            if (launchHistory.HasLaunchedOnce) _sequence.ReleaseLaunchCommandWait();
        }

        public void SupplyPower(float power)
        {
            // 複数の電力網から供給され得るため加算し、次のUpdateで使い切る
            // Accumulate since several networks may supply this pad, then consume it on the next update
            _suppliedPower += power;
        }

        public void Update()
        {
            CheckDestroy(this);
            var previousPhase = Phase;
            var previousReason = WaitReason;
            var tickPower = _suppliedPower;
            _suppliedPower = 0f;

            // 出力口の中身を接続先へ流してから段階を進める（機械の出力と同じ順）
            // Push output contents onward before advancing the stage (same order as machine outputs)
            _outputSlots.PushToConnected();
            _sequence.Advance(tickPower);

            // 段階か待ち理由が変わったとき、進行中は毎tick状態を流す（機械の進捗と同じ）
            // Publish on a phase or reason change, and every tick while progressing (as machine progress does)
            var isProgressing = _sequence.Stage is IProgressingStage;
            if (previousPhase != Phase || previousReason != WaitReason || isProgressing) _changeState.OnNext(Unit.Default);
        }

        public RocketLaunchPadCommandResult RequestBuild()
        {
            CheckDestroy(this);
            return PublishCommandResult(_sequence.RequestBuild());
        }

        public RocketLaunchPadCommandResult RequestLaunch()
        {
            CheckDestroy(this);
            return PublishCommandResult(_sequence.RequestLaunch());
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            CheckDestroy(this);
            return new[] { RocketLaunchPadStateDetail.CreateState(Phase, WaitReason, _sequence.CalculateProgress()) };
        }

        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            // 積んだまま打ち上げていないロケットの中身を返す。出力口の成果は開けるスロットとして撤去処理が返す
            // Refund the contents of a not-yet-launched rocket; output slot contents are refunded by removal as openable slots
            return _sequence.Stage is LoadedRocketStage loaded ? loaded.Cargo.AllItems().ToList() : new List<IItemStack>();
        }

        public object GetSaveState()
        {
            CheckDestroy(this);
            return RocketLaunchPadSaveConverter.Capture(_sequence.Stage, _cargoLoader.NextPayloadIndex, _layout, _slots);
        }

        public void Destroy()
        {
            IsDestroy = true;
            _firstLaunchSubscription.Dispose();
        }

        private void OnWorldFirstLaunch()
        {
            var previousPhase = Phase;
            _sequence.ReleaseLaunchCommandWait();
            if (previousPhase != Phase) _changeState.OnNext(Unit.Default);
        }

        private RocketLaunchPadCommandResult PublishCommandResult(RocketLaunchPadCommandResult result)
        {
            // 受け付けた合図は段階を変えるので状態を流し、拒否は理由を開発者向けに残す
            // Accepted commands change the stage so publish it; rejections leave their reason for developers
            if (result == RocketLaunchPadCommandResult.Accepted) _changeState.OnNext(Unit.Default);
            else Debug.LogWarning($"ロケット発射台への操作を拒否しました 理由:{result} 段階:{Phase} 待ち理由:{WaitReason}");
            return result;
        }
    }
}
```

- [x] **Step 5: セーブ形と変換を書く**

残り時間は秒、段階は int で保存する（前例 `Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorSaveJsonObject.cs` の `state`・`remainingSeconds`、`VanillaMachineProcessorComponent` の「tickを秒数に変換して保存」）。打ち上げ後の保留は無いので、セーブにも持たない。

`Save/RocketLaunchPadSaveJsonObject.cs`:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    // 入れ口はアイテムで、出力口は並びで保存する。積荷表が変わってもスロットの意味がずれないようにするため
    // Inputs are saved by item and outputs by order, so slot meaning survives a payload table change
    public class RocketLaunchPadSaveJsonObject
    {
        [JsonProperty("phase")] public int Phase;
        [JsonProperty("remainingSeconds")] public double RemainingSeconds;
        [JsonProperty("cargoPayload")] public ItemStackSaveJsonObject CargoPayload;
        [JsonProperty("cargoMaterials")] public List<ItemStackSaveJsonObject> CargoMaterials;
        [JsonProperty("nextPayloadIndex")] public int NextPayloadIndex;
        [JsonProperty("inputItems")] public List<ItemStackSaveJsonObject> InputItems;
        [JsonProperty("outputItems")] public List<ItemStackSaveJsonObject> OutputItems;
    }
}
```

`Save/RocketLaunchPadSaveConverter.cs`（前例 `CleanRoomMachineProcessorSaveState.cs` の static 変換）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    // 発射台のセーブ形と段階の型を相互に変換する。壊れた値は段階を作る前に弾き、警告して待機へ戻す
    // Converts between the save shape and stage types; broken values are rejected before building a stage and fall back to idle with a warning
    public static class RocketLaunchPadSaveConverter
    {
        public static RocketLaunchPadSaveJsonObject Capture(RocketLaunchPadStage stage, int nextPayloadIndex, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            var cargo = stage is LoadedRocketStage loaded ? loaded.Cargo : new RocketCargo(ServerContext.ItemStackFactory.CreatEmpty(), new List<IItemStack>());
            var items = slots.InventoryItems;
            return new RocketLaunchPadSaveJsonObject
            {
                Phase = (int)stage.Phase,
                RemainingSeconds = GameUpdater.TicksToSeconds(RemainingTicksOf(stage)),
                CargoPayload = new ItemStackSaveJsonObject(cargo.Payload),
                CargoMaterials = cargo.Materials.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                NextPayloadIndex = nextPayloadIndex,
                InputItems = items.Take(layout.OutputSlotStart).Where(item => item.Count > 0).Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputItems = layout.OutputSlots.Select(slot => new ItemStackSaveJsonObject(items[slot])).ToList(),
            };
        }

        public static void RestoreSlots(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            // スロットをイベント無しで戻す。マスタ変更で置き場が無くなった品は警告して捨てる（打ち上げ後の保留は持たない）
            // Restore slots without events; items that lost their place through a master change are dropped with a warning (no pending store)
            var droppedCount = 0;
            foreach (var input in saveData.InputItems.Select(item => item.ToItemStack()).Where(item => item.Count > 0))
            {
                if (layout.TryGetInputSlot(input.Id, out var slot)) slots.SetItemWithoutEvent(slot, input);
                else droppedCount += input.Count;
            }
            var outputs = saveData.OutputItems.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputs.Count; i++)
            {
                if (i < layout.OutputSlotCount) slots.SetItemWithoutEvent(layout.OutputSlots[i], outputs[i]);
                else droppedCount += outputs[i].Count;
            }
            if (droppedCount > 0) Debug.LogWarning($"発射台のスロット構成がセーブ時と違うため、置き場の無い品を {droppedCount} 個捨てました");
        }

        public static RocketLaunchPadStage RestoreStage(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadSlotLayout layout)
        {
            var phase = (RocketLaunchPadPhase)saveData.Phase;
            var cargo = new RocketCargo(saveData.CargoPayload.ToItemStack(), saveData.CargoMaterials.Select(item => item.ToItemStack()).ToList());
            var isLaunchableCargo = IsLaunchableCargo(cargo, layout);

            // 積載中の型は、積荷が今の並びの1便ぶんのときだけ作る（欠損マスタの刈り込みで空になった積荷もここで弾く）
            // Loaded stage types are built only when the cargo is one launch worth under the current layout (pruned empty cargo is rejected too)
            RocketLaunchPadStage stage = phase switch
            {
                RocketLaunchPadPhase.Idle => IdleStage.Instance,
                RocketLaunchPadPhase.AwaitingBuildCommand => AwaitingBuildCommandStage.Instance,
                RocketLaunchPadPhase.Building when isLaunchableCargo => new BuildingStage(cargo, RestoreRemainingTicks(saveData, phase)),
                RocketLaunchPadPhase.AwaitingLaunchCommand when isLaunchableCargo => new AwaitingLaunchCommandStage(cargo),
                RocketLaunchPadPhase.ReadyToLaunch when isLaunchableCargo => new ReadyToLaunchStage(cargo),
                RocketLaunchPadPhase.Launching => new LaunchingStage(RestoreRemainingTicks(saveData, phase)),
                _ => null,
            };

            // どの型にも当たらないのは、積載中の段階で積荷が合わないときか、不明な段階の値のとき
            // No type matches only for a loaded phase with mismatched cargo or an unknown phase value
            if (stage == null && Enum.IsDefined(typeof(RocketLaunchPadPhase), phase)) return FallBackToIdle($"積荷が今の1便ぶんと合わず打ち上げられないため積荷を捨てます 段階:{phase} 積荷:{cargo.Payload.Id}×{cargo.Payload.Count}");
            if (stage == null) return FallBackToIdle($"段階の値が不明です 段階:{saveData.Phase}");

            // 積荷を持つかは段階の型の階層が決める。持たない型に積荷が残っていたら捨てる
            // The stage type hierarchy decides whether cargo is held; cargo left on a type that holds none is dropped
            if (stage is not LoadedRocketStage && cargo.IsLoaded) Debug.LogWarning($"発射台のセーブで積荷を持たない段階に積荷があったため捨てます 段階:{phase}");
            return stage;
        }

        // 積んだロケットは常に1便ぶん（入れ口の要求と同じ品・同じ個数）。積荷表の受け付けは軌道到着サービスの Arrive が持ち、ここでは写さない
        // A loaded rocket always holds one launch worth (same item and count as the input requirement); table acceptance stays with Arrive
        private static bool IsLaunchableCargo(RocketCargo cargo, RocketLaunchPadSlotLayout layout)
        {
            return layout.TryGetPayloadRequirement(cargo.Payload.Id, out var requirement) && cargo.Payload.Count == requirement.Count;
        }

        private static uint RestoreRemainingTicks(RocketLaunchPadSaveJsonObject saveData, RocketLaunchPadPhase phase)
        {
            // 進行中の段階は残り1tick以上。壊れた0以下の値は警告して1tickにする
            // Progressing stages have at least one tick left; a broken non-positive value is set to one tick with a warning
            var ticks = GameUpdater.SecondsToTicks(saveData.RemainingSeconds);
            if (ticks > 0) return ticks;
            Debug.LogWarning($"発射台のセーブの残り時間が0以下のため1tickにします 段階:{phase} 残り秒:{saveData.RemainingSeconds}");
            return 1;
        }

        private static uint RemainingTicksOf(RocketLaunchPadStage stage)
        {
            return stage is IProgressingStage progressing ? progressing.RemainingTicks : 0;
        }

        private static RocketLaunchPadStage FallBackToIdle(string reason)
        {
            Debug.LogWarning($"発射台のセーブを待機として読み込みます {reason}");
            return IdleStage.Instance;
        }
    }
}
```

（約125行。`ItemStackSaveJsonObject.ToItemStack()` は `Game.Context` の拡張メソッド。積荷を持つかは `LoadedRocketStage`、残りtickは `IProgressingStage` で引き、段階の enum の集合を別に書かない（再検査 弱(e)）。打ち上げられるかの判定は発射台の並びの要求（`TryGetPayloadRequirement` と個数の一致）だけで行い、積荷表の受け付け規則は `IOrbitalArrivalService.Arrive` に残す（再検査 弱(b)）。）

- [x] **Step 6: 電力とテンプレートと撤去返却を書く**

`RocketLaunchPadElectricComponent.cs`（`VanillaElectricMachineComponent` と同形）:

```csharp
using Game.Block.Interface;
using Game.EnergySystem;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 建造中だけ電力を要求し、供給された量を進行へ渡す
    // Requests power only while building and hands the supplied amount to the processor
    public class RocketLaunchPadElectricComponent : IElectricConsumer, IElectricTickPostHandler
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_processor.RequestPower);
        public bool IsDestroy { get; private set; }

        private readonly RocketLaunchPadProcessorComponent _processor;

        public RocketLaunchPadElectricComponent(BlockInstanceId blockInstanceId, RocketLaunchPadProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _processor = processor;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            CheckDestroy(this);
            _processor.SupplyPower(_processor.RequestPower * statistics.PowerRate);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

`Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs`:

```csharp
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.Service;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Rocket
{
    /// <summary>
    ///     ロケット発射台（資材・積荷・出力スロットと電力）のテンプレート
    ///     Template for the rocket launch pad with material, payload and output slots and power
    /// </summary>
    public class VanillaRocketLaunchPadTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static IBlock Create(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (RocketLaunchPadBlockParam)blockMasterElement.BlockParam;
            var layout = new RocketLaunchPadSlotLayout(param);

            // 全スロットを1つの保管に持ち、入れ口と出力口の制限はインベントリ側で掛ける
            // Keep every slot in one store and apply input/output restrictions in the inventory component
            var slots = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, layout.TotalSlotCount);
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var beltOutput = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector);
            var arrivalService = ServerContext.GetService<IOrbitalArrivalService>();
            var launchHistory = ServerContext.GetService<IRocketLaunchHistory>();

            var processor = componentStates == null
                ? new RocketLaunchPadProcessorComponent(param, layout, slots, beltOutput, arrivalService, launchHistory)
                : new RocketLaunchPadProcessorComponent(componentStates, param, layout, slots, beltOutput, arrivalService, launchHistory);
            var electric = new RocketLaunchPadElectricComponent(blockInstanceId, processor);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            var components = new List<IBlockComponent>
            {
                connector,
                new RocketLaunchPadInventoryComponent(layout, slots),
                electric,
                wireConnector,
                processor,
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void InvokeEvent(int slot, IItemStack itemStack)
            {
                var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
                blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(blockInstanceId, slot, itemStack));
            }

            #endregion
        }
    }
}
```

（`using` の名前空間は既存テンプレート `VanillaCleanRoomAirFilterTemplate`・機械テンプレートの実物に合わせる。`ConnectingInventoryListPriorityInsertItemService` の名前空間は `Game.Block.Blocks.Service`。プロトコルは部品を `IRocketLaunchPadComponent` で引くので、テンプレートの部品一覧に processor が入っていれば足りる。）

`VanillaIBlockTemplates.cs` の `BlockTypesDictionary` 構築に、既存の追加行と同じ書式で `RocketLaunchPad` → `new VanillaRocketLaunchPadTemplate()` を足す。

`RemoveBlockProtocol.cs` の `GetRefundItems` ローカル関数で、`TryGetComponent(out IGetRefundItemsInfo refundInfo)` の if ブロック（121〜126行の6行）を次の6行へ置き換える。発射台は電線（`ElectricWireConnectorComponent` も `IGetRefundItemsInfo`）と進行の両方が返却を持つため、先頭1件だけでは片方が消える。既存テンプレートで返却を持つ部品が2つ以上あるブロックは無い（電線つきテンプレートとベルト・チェーンポール・フィルター分配器・クリーンルーム境界のテンプレートは重ならない）ので、既存ブロックの返却量は変わらない:

```csharp
                // 返却を持つ部品すべてを合算する。電線と発射台の進行のように1ブロックに複数あり得る
                // Sum every component that holds refunds; a block may have several, such as wires and a launch pad processor
                foreach (var refundInfo in block.ComponentManager.GetComponents<IGetRefundItemsInfo>())
                {
                    result.AddRange(refundInfo.GetRefundItems());
                }
```

- [x] **Step 7: テストを書く**

`Tests/Util/Rocket/RocketLaunchPadTestUtil.cs`（テスト用。`ElectricWireTestUtil.WirePower(consumerPos, polePos, generatePower)` で給電する）:

```csharp
using Core.Master;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using UnityEngine;

namespace Tests.Util.Rocket
{
    // 発射台のテストで使う設置・搬入・tick送り
    // Placement, loading and tick helpers shared by launch pad tests
    public static class RocketLaunchPadTestUtil
    {
        public static IBlock PlacePoweredPad(Vector3Int position, float generatePower)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.RocketLaunchPadId, position, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var pad);
            ElectricWireTestUtil.WirePower(position, position + new Vector3Int(3, 0, 0), generatePower);
            return pad;
        }

        public static void FillMaterials(IBlock pad)
        {
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.InsertItem(ForUnitTestItemId.RocketCaseId, 2);
            inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 3);
            inventory.InsertItem(ForUnitTestItemId.RocketComputerId, 1);
        }

        public static void FillOneLaunch(IBlock pad, ItemId payloadItemId, int payloadCount)
        {
            FillMaterials(pad);
            pad.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(payloadItemId, payloadCount);
        }

        // 出力口を計算データで埋める。出力口は取り出し専用なので、置き場の制限を通らない SetItem で書く
        // Fill the output slots with compute data; they are take-out only, so write with SetItem which skips the placement check
        public static void FillOutputSlots(IBlock pad, int countPerSlot)
        {
            var layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId).BlockParam);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            foreach (var slot in layout.OutputSlots) inventory.SetItem(slot, ForUnitTestItemId.ComputeDataId, countPerSlot);
        }

        // 指定の段階になるまで tick を回し、なったかを返す
        // Run ticks until the given phase and report whether it was reached
        public static bool RunUntilPhase(IBlock pad, RocketLaunchPadPhase until, int maxTicks)
        {
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            for (var i = 0; i < maxTicks; i++)
            {
                if (processor.Phase == until) return true;
                GameUpdater.RunFrames(1);
            }
            return processor.Phase == until;
        }
    }
}
```

（`TryAddBlock` の引数・`ElectricWireTestUtil.WirePower` の引数は既存 CombinedTest の実物に合わせる。`GameUpdater.RunFrames(uint)` は実在を確認済み。）

`RocketLaunchPadLaunchFlowTest.cs` に次のテストを書く（各テストは DI を作り直す。`IRocketLaunchHistory` は `ServerContext.GetService` で取る）:

1. `初打ち上げ前は建造と発射の合図を待つ`: 給電した台に1便ぶん入れて `RunUntilPhase(AwaitingBuildCommand, 5)` が true、資材が減っていない。`RequestBuild()` が `Accepted`、資材と積荷が消える。`RunUntilPhase(AwaitingLaunchCommand, 40)` が true。`RequestLaunch()` が `Accepted` で、直後の `Phase` は `ReadyToLaunch`（合図は段階を移すだけ）。`RunUntilPhase(Launching, 2)` が true、`HasLaunchedOnce` が true、出力スロット2つに TestComputeData が合計150（100＋50）入る。
2. `合図待ちでない台への操作は拒否する`: 空の台で `RequestBuild()` が `NotAwaitingBuildCommand`、`RequestLaunch()` が `NotAwaitingLaunchCommand`（`LogAssert.Expect(LogType.Warning, new Regex("ロケット発射台への操作を拒否しました"))` を2回）。合図待ちの台から資材を1つ抜いて `RequestBuild()` が `CargoNotReady`。資材を戻し、`FillOutputSlots(pad, 100)` で出力口を埋めてから `RequestBuild()` が `OutputFull`（拒否の警告をそれぞれ `LogAssert.Expect`）。
3. `初打ち上げ後は自動で建造し打ち上げる`: `history.RecordLaunch()` してから1便ぶん入れ、`RunUntilPhase(Launching, 40)` が true。毎tickの `Phase` を記録し、`AwaitingBuildCommand` と `AwaitingLaunchCommand` を一度も経ない。
4. `別の台の初打ち上げで発射の合図待ちの台が次のtickまでに打ち上がる`: 台Aと台Bを置き、両方を `AwaitingLaunchCommand` まで進め、Aで `RequestLaunch()`。`RunUntilPhase(A, Launching, 2)` が true になった時点で、Bは `ReadyToLaunch` か `Launching`。続けて `RunUntilPhase(B, Launching, 1)` が true（Bの打ち上げは自分の Update の中で、Aの打ち上げと同じtickか次のtick）。`AwaitingBuildCommand` で止めておいた台C（1便ぶん入れておく）は、Aの打ち上げと同じtickか次のtickに `Building` に入る（建造の合図待ちは通知ではなく C 自身の Update の判定が解く）。
5. `電力が半分なら建造が倍かかる`: 自動モード（`RecordLaunch()` 済み）で、要求100に対し発電50の台と発電100の台の`Building` から `ReadyToLaunch` までのtick数の比が 1.6〜2.4（確率的な丸めのため幅を持たせる）。発電0では40tick後も `Building` のまま `WaitReason == NoPower`。
6. `揃った積荷は交互に選ぶ`: 自動モードで資材2便ぶん・衛星1便ぶん・ケーブル1便ぶん（5個）を同時に入れ、1便目と2便目で `OnOrbitalArrival` の積荷が異なる（1便目の成果150が出力口に入りきり、2便目のケーブルは成果が無いので入りきる）。
7. `撤去で積んだ資材を返す`: 合図待ちで `RequestBuild()` した台の `GetRefundItems()` に資材3種と積荷が入る。`ReadyToLaunch` の台（初打ち上げ前に出力口を埋めてから `RequestLaunch()` し、`OutputFull` で待たせた台）も同じ。打ち上げ中（`Launching`）の台の `GetRefundItems()` は空。
8. `資材が無いときの最小構成`: 空の台は `Idle`・`MissingMaterials`。資材だけ入れると `MissingPayload`。

`RocketLaunchPadOutputFullTest.cs`（出力口の満杯。ユーザー裁定 D2）:

1. `成果が出力口に入りきらないときは建造を始めない`: 自動モードで資材2便ぶんと衛星2機を入れる。1便目の打ち上げで出力が100＋50になり、`Launching` の後は `Idle` かつ `WaitReason == OutputFull` で、2便目の資材と衛星が入れ口に残る（成果150は空き50に入りきらない）。出力スロット1つを空にすると次のtickで `Building` に入る。
2. `初打ち上げ前でも出力口が満杯なら建造の合図を待たない`: `FillOutputSlots(pad, 100)` の後に1便ぶん入れ、5tick回しても `AwaitingBuildCommand` へ進まず `Idle`・`OutputFull`。
3. `出力口が満杯でも成果の無い積荷は打ち上げる`: 自動モードで `FillOutputSlots(pad, 100)` の後に資材1便ぶん・衛星1機・ケーブル5個を入れ、最初の `OnOrbitalArrival` の積荷がケーブル。続けて資材をもう1便ぶん入れると `Idle`・`OutputFull`（衛星が待つ）。
4. `打ち上げ直前に出力口が埋まったら発射を待たせる`: 初打ち上げ前に `AwaitingLaunchCommand` まで進め、`FillOutputSlots(pad, 100)` してから `RequestLaunch()`。5tick回しても `ReadyToLaunch`・`OutputFull` のままで、`HasLaunchedOnce` は false。出力口を空にすると次のtickで `Launching`。

`Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadRemoveRefundTest.cs`（`RemoveBlockRefundTest` の `CreateServer`・`GetInventory`・`GetItemCount`・`CreateRemovePayload` を同ファイル内に写す）:
- `建造中の台を撤去すると積んだ資材と積荷が戻る`: `PlacePoweredPad`・`FillOneLaunch(pad, SatellitePayloadId, 1)` → `RunUntilPhase(AwaitingBuildCommand, 5)` → `RequestBuild()` の後、`RemoveBlockProtocol` で撤去し、プレイヤーのインベントリに TestRocketCase×2・TestRocketFuel×3・TestRocketComputer×1・TestSatellitePayload×1 が入る。テンプレートの部品順は電線が進行より前なので、修正前の `TryGetComponent`（先頭1件）では電線の返却しか読まれずこのテストが落ちる。
- `出力口の成果も撤去で戻る`: 自動モードで1便打ち上げた台（出力100＋50）を撤去し、TestComputeData×150 がインベントリに入る（開けるスロットの既存の返却経路。インベントリの空きが足りるよう、テスト前にプレイヤーのインベントリを空にしておく）。

`RocketLaunchPadSaveLoadTest.cs`（`CleanRoomSaveLoadTest` の書き方。保存した JSON を書き換えるケースは `SaveLoadJsonTestHelper` を使う）:
- (a) `Building` 途中、(b) `AwaitingLaunchCommand`、(c) 出力口が満杯で待つ `ReadyToLaunch`、の3状態でセーブ→ロードし、段階・残り秒から戻した残りtick・積荷・入れ口・出力口・`NextPayloadIndex` が同じで、ロード後に進行が続く。保存した state の `phase` が int、`remainingSeconds` が秒であること。
- (d) 保存した `inputItems` に入れ口の無いアイテム（TestComputeData）を書き足すと、そのアイテムは捨てられ `LogAssert.Expect(LogType.Warning, new Regex("置き場の無い品"))`。
- (e) `phase` が `Building` のまま `cargoPayload` を空・TestComputeData（積荷表に無い）・TestCablePayload×3 と ×10（1便5個と違う）にしたJSONは、どれもロードで `Idle` になり `LogAssert.Expect(LogType.Warning, new Regex("打ち上げられないため積荷を捨てます"))`。
- (f) `nextPayloadIndex` を -1 にすると `NextPayloadIndex` が1（積荷表2行）。
- (g) `phase` を99にすると `Idle` で `段階の値が不明` の警告。`Launching` で `remainingSeconds` を0にすると残り1tickで警告。
- (h) `Building` で `remainingSeconds` を100（建造1秒の台）にしたJSONをロードすると、状態の `Progress` が0（負にならない）。
- (i) `AwaitingLaunchCommand` の台を置いたワールドを保存し、JSON の `rocketHasLaunchedOnce` を true に書き換えてロードすると、ロード直後の `Phase` が `ReadyToLaunch`。同じく1便ぶん入れたまま `AwaitingBuildCommand` で保存して書き換えると、ロード直後は `AwaitingBuildCommand` のままで、1tick回すと `Building`（建造の合図待ちはロード時に移さず、Update の判定が解く。D9）。

`Tests/UnitTest/Game/RocketLaunchPad/` の段階の型のテスト（Task 4）もここで実行する。

- [x] **Step 8: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.CombinedTest\.Game\.RocketLaunchPad\.|Tests\.UnitTest\.Game\.RocketLaunchPad\.|RocketLaunchPadRemoveRefundTest|RemoveBlockRefundTest|RemoveBlockProtocolTest|BeltConveyorRemovalTest"`
Expected: ErrorCount 0 / Task 4・5 の全テストと既存の撤去返却テストが PASS

- [x] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/Util/Rocket moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket
git commit -m "feat(block): ロケット発射台の建造と打ち上げとセーブを足し、撤去返却を全部品で合算する"
```

---

### Task 6: 建造・発射の合図のプロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Rocket/RocketLaunchPadCommandProtocol.cs`（`PacketResponse/` 直下は .cs が52個あるのでサブディレクトリ。名前空間は前例 `PacketResponse/TrainStation/SetTrainStationNameProtocol.cs` どおり `Server.Protocol.PacketResponse` のまま）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（`SetTrainPlatformTransferModeProtocol` の登録の次）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Requests/BlockRequestApi.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadCommandProtocolTest.cs`

通信の語彙（操作の種類・座標からブロックを引く失敗）はプロトコルにネストし、発射台の部品が返すのはドメインの結果 `RocketLaunchPadCommandResult` だけにする（前例 `MachineRecipeSelectionProtocol` のネストした `MachineRecipeSelectionOperation`・`MachineRecipeSelectionFailureReason` と `ToFailureReason`、ドメインの結果は `IMachineRecipeSelectorComponent.cs` の `MachineRecipeSelectionResult`）。部品は具象型でなくインターフェース層の `IRocketLaunchPadComponent` で引くので、`Server.Protocol.asmdef` に参照を足す必要は無い（`Game.Block.Interface` は参照済み）。

**Interfaces:**
- Consumes: Task 4 の `IRocketLaunchPadComponent`・`RocketLaunchPadCommandResult`
- Produces:
  - `RocketLaunchPadCommandProtocol.ProtocolTag = "va:rocketLaunchPadCommand"`
  - ネストした enum `RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation { Build = 0, Launch = 1 }`
  - ネストした enum `RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason { None = 0, BlockNotFound = 1, NotRocketLaunchPad = 2, UnknownOperation = 3, InvalidRequest = 4, NotAwaitingBuildCommand = 5, NotAwaitingLaunchCommand = 6, CargoNotReady = 7, OutputFull = 8 }`
  - `RocketLaunchPadCommandProtocol.RocketLaunchPadCommandRequest`（`[Key(2)] Vector3IntMessagePack Position`・`[Key(3)] RocketLaunchPadCommandOperation Operation`、ctor `(Vector3Int position, RocketLaunchPadCommandOperation operation)`）
  - `RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse`（`[Key(2)] bool Success`・`[Key(3)] RocketLaunchPadCommandFailureReason FailureReason`）
  - `BlockRequestApi.SendRocketLaunchPadCommand(Vector3Int position, RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation operation, CancellationToken ct)` → `UniTask<RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse>`

- [x] **Step 1: creating-server-protocol スキルを読み、プロトコルを書く**

```csharp
using System;
using Game.Block.Interface.Component;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     発射台への建造・発射の合図。結果の表示はブロック状態の更新を正とする
    ///     Build/launch commands for a launch pad; the displayed result follows the block state update
    /// </summary>
    public class RocketLaunchPadCommandProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:rocketLaunchPadCommand";

        public RocketLaunchPadCommandProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RocketLaunchPadCommandRequest>(payload);
            if (request.Position == null) return Fail(RocketLaunchPadCommandFailureReason.InvalidRequest);

            // 指定座標の発射台を引く。無ければ理由を残して拒否する
            // Look up the launch pad at the position, rejecting with a logged reason when absent
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return Fail(RocketLaunchPadCommandFailureReason.BlockNotFound);
            if (!block.ComponentManager.TryGetComponent<IRocketLaunchPadComponent>(out var launchPad)) return Fail(RocketLaunchPadCommandFailureReason.NotRocketLaunchPad);

            // 操作の種類ごとに発射台の入口へ渡し、ドメインの結果を通信の失敗理由へ写す
            // Hand each operation to the pad's entry point and map the domain result to a wire failure reason
            switch (request.Operation)
            {
                case RocketLaunchPadCommandOperation.Build:
                    return ToResponse(launchPad.RequestBuild());
                case RocketLaunchPadCommandOperation.Launch:
                    return ToResponse(launchPad.RequestLaunch());
                default:
                    return Fail(RocketLaunchPadCommandFailureReason.UnknownOperation);
            }

            #region Internal

            RocketLaunchPadCommandResponse ToResponse(RocketLaunchPadCommandResult result)
            {
                // 部品側の拒否は部品が理由をログに出している
                // Component-side rejections are already logged by the component
                if (result == RocketLaunchPadCommandResult.Accepted) return new RocketLaunchPadCommandResponse(true, RocketLaunchPadCommandFailureReason.None);
                return new RocketLaunchPadCommandResponse(false, ToFailureReason(result));
            }

            RocketLaunchPadCommandResponse Fail(RocketLaunchPadCommandFailureReason reason)
            {
                Debug.LogWarning($"ロケット発射台への合図を拒否しました 理由:{reason} 座標:{request.Position?.Vector3Int}");
                return new RocketLaunchPadCommandResponse(false, reason);
            }

            static RocketLaunchPadCommandFailureReason ToFailureReason(RocketLaunchPadCommandResult result)
            {
                return result switch
                {
                    RocketLaunchPadCommandResult.NotAwaitingBuildCommand => RocketLaunchPadCommandFailureReason.NotAwaitingBuildCommand,
                    RocketLaunchPadCommandResult.NotAwaitingLaunchCommand => RocketLaunchPadCommandFailureReason.NotAwaitingLaunchCommand,
                    RocketLaunchPadCommandResult.CargoNotReady => RocketLaunchPadCommandFailureReason.CargoNotReady,
                    RocketLaunchPadCommandResult.OutputFull => RocketLaunchPadCommandFailureReason.OutputFull,
                    _ => throw new ArgumentOutOfRangeException(nameof(result), result, "通信の失敗理由へ写していない発射台の結果です"),
                };
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class RocketLaunchPadCommandRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public RocketLaunchPadCommandOperation Operation { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RocketLaunchPadCommandRequest() { Tag = ProtocolTag; }

            public RocketLaunchPadCommandRequest(Vector3Int position, RocketLaunchPadCommandOperation operation)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Operation = operation;
            }
        }

        [MessagePackObject]
        public class RocketLaunchPadCommandResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public RocketLaunchPadCommandFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RocketLaunchPadCommandResponse() { }

            public RocketLaunchPadCommandResponse(bool success, RocketLaunchPadCommandFailureReason failureReason)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
            }
        }

        public enum RocketLaunchPadCommandOperation
        {
            Build = 0,
            Launch = 1,
        }

        public enum RocketLaunchPadCommandFailureReason
        {
            None = 0,
            BlockNotFound = 1,
            NotRocketLaunchPad = 2,
            UnknownOperation = 3,
            InvalidRequest = 4,
            NotAwaitingBuildCommand = 5,
            NotAwaitingLaunchCommand = 6,
            CargoNotReady = 7,
            OutputFull = 8,
        }

        #endregion
    }
}
```

（`ToFailureReason` の既定分岐は、ドメインの結果に値を足して写し忘れたときに落とすための例外。`Accepted` は `ToResponse` が先に返すのでここへ来ない。）

`PacketResponseCreator.cs` に `_packetResponseDictionary.Add(RocketLaunchPadCommandProtocol.ProtocolTag, new RocketLaunchPadCommandProtocol(serviceProvider));`。

`BlockRequestApi.cs` に追加（前例は同ファイルの `SendMachineRecipeSelectionRequest`。`using Server.Protocol.PacketResponse;` は既存）:

```csharp
        // 発射台へ建造・発射の合図を送る
        // Send a build or launch command to a rocket launch pad
        public async UniTask<RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse> SendRocketLaunchPadCommand(
            Vector3Int position, RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation operation, CancellationToken ct)
        {
            var request = new RocketLaunchPadCommandProtocol.RocketLaunchPadCommandRequest(position, operation);
            return await _packetExchangeManager.GetPacketResponse<RocketLaunchPadCommandProtocol.RocketLaunchPadCommandResponse>(request, ct);
        }
```

- [x] **Step 2: テストを書く**

`Tests/CombinedTest/Server/PacketTest/Rocket/RocketLaunchPadCommandProtocolTest.cs`。`SetTrainPlatformTransferModeProtocolTest` と同じ書式で、`packet.GetPacketResponse(payload, BoundPacketContext.Bind(1))[0]` を使う（パケット送受の実物は既存 PacketTest に合わせる）。台は `Tests.Util.Rocket.RocketLaunchPadTestUtil.PlacePoweredPad`・`FillOneLaunch` で用意する:
- 合図待ちの台へ `Build` → `Success == true`・`FailureReason == None`、`Phase == Building`。
- `AwaitingLaunchCommand` まで進めて `Launch` → `Success == true`、直後の `Phase == ReadyToLaunch`（合図は段階を移すだけ）、1tick後に `Launching`。
- 空き座標 → `BlockNotFound`、チェストへ → `NotRocketLaunchPad`、空の台へ `Build` → `NotAwaitingBuildCommand`、`Operation` に99を入れた要求 → `UnknownOperation`（それぞれ `Success == false`、`LogAssert.Expect(LogType.Warning, ...)`）。
- 合図待ちの台の出力口を `FillOutputSlots(pad, 100)` で埋めて `Build` → `OutputFull`。

- [x] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RocketLaunchPadCommandProtocolTest"`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Rocket moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs moorestech_client/Assets/Scripts/Client.Network moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Rocket
git commit -m "feat(protocol): 発射台の建造と発射の合図を足す"
```

---

### Task 7: ロケット本体の表示と上昇

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Rocket/RocketLaunchPadStateChangeProcessor.cs`
- Modify（uloop経由）: `moorestech_client/Assets/AddressableResources/Block/Rocket.prefab`（ルートへ `RocketLaunchPadStateChangeProcessor` を追加し `body` を割り当てる）

**Interfaces:**
- Consumes: Task 4 の `RocketLaunchPadStateDetail`・`RocketLaunchPadPhase`（名前空間 `Game.Block.Interface.State`）、Task 1 の `RocketLaunchPadBlockParam.LaunchSeconds`
- Produces: なし（見た目のみ）

- [ ] **Step 1: 状態処理を書く**

```csharp
using Client.Game.InGame.Block;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.Rocket
{
    // 発射台の上のロケット本体を建造完了で出し、打ち上げで昇らせて消す
    // Shows the rocket body on build completion, raises it during launch, and hides it afterwards
    public class RocketLaunchPadStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private Transform body;
        [SerializeField] private float riseHeight = 80f;

        private Vector3 _bodyRestLocalPosition;
        private float _launchSeconds;
        private float _launchElapsedSeconds;
        private bool _isLaunching;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _bodyRestLocalPosition = body.localPosition;
            _launchSeconds = ((RocketLaunchPadBlockParam)blockGameObject.BlockMasterElement.BlockParam).LaunchSeconds;
            body.gameObject.SetActive(false);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var detail = blockState.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);

            // 本体は建造が終わってから昇り切るまで（発射の合図待ち・打ち上げ待ち・打ち上げ中）だけ見せる
            // Show the body from build completion until it finishes rising (awaiting launch, ready to launch, launching)
            body.gameObject.SetActive(detail.Phase is RocketLaunchPadPhase.AwaitingLaunchCommand or RocketLaunchPadPhase.ReadyToLaunch or RocketLaunchPadPhase.Launching);

            // 打ち上げの途中から受け取っても、サーバーの進み具合から上昇位置を合わせる
            // Even when received mid-launch, align the rise with the server's progress
            var wasLaunching = _isLaunching;
            _isLaunching = detail.Phase == RocketLaunchPadPhase.Launching;
            if (_isLaunching && !wasLaunching) _launchElapsedSeconds = detail.Progress * _launchSeconds;
            if (!_isLaunching) body.localPosition = _bodyRestLocalPosition;
        }

        private void Update()
        {
            if (!_isLaunching) return;

            // 加速して昇って見えるよう経過割合の二乗で上げる
            // Raise by the square of elapsed ratio so the ascent reads as accelerating
            _launchElapsedSeconds = Mathf.Min(_launchElapsedSeconds + Time.deltaTime, _launchSeconds);
            var ratio = _launchElapsedSeconds / _launchSeconds;
            body.localPosition = _bodyRestLocalPosition + Vector3.up * (riseHeight * ratio * ratio);
        }
    }
}
```

（`Initialize` のシグネチャ・`BlockGameObject.BlockMasterElement` の参照は `GearStateChangeProcessor` と `IBlockGameObjectInnerComponent` の実物に合わせる。）

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 3: Prefab に付ける（uloop execute-dynamic-code）**

`Initialize` の `RocketLaunchPadBlockParam` への cast は、前例 `Client.Game/InGame/BlockSystem/StateProcessor/SteamEngineEffectStateChangeProcessor.cs:21`（`(FuelGearGeneratorBlockParam)blockGameObject.BlockMasterElement.BlockParam`）と同じ形のまま残し、型での保証は足さない（判断記録 D39）。到達しない根拠は次の実測で、読み込まれる mod で `Rocket.prefab` を使うのは（統合タスクでパスを入れた後の）発射台だけになる。`Rocket.prefab`（アドレス `Vanilla/Block/Rocket`）を見た目に使う他のブロックは、plan 作成時の実測（2026-10-23）では moorestech_master `server/mods/moorestechAlphaMod_3/master/blocks.json` の「ロケット」（`aa312f32-35d4-4dae-9e3e-a292e1a70a70`、今のゲームが読まない旧 mod）だけで、読み込まれる `server_v8/mods/moorestechAlphaMod_8/master/blocks.json` には無い（発射台の行も今は空のパスで、統合タスクで `Vanilla/Block/Rocket` を入れる）。他のブロックが使っていると `Initialize` の `RocketLaunchPadBlockParam` への cast で落ちるので、着手時に `grep -rn "Block/Rocket\"" ../../moorestech_master/tier9-compute-data-loop/server_v8/mods` で同じことを確かめる。居れば、`Initialize` の cast を `is` の判定に変え、発射台以外では何もせず理由を `Debug.LogWarning` で出す。

階層を読む:

```csharp
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/AddressableResources/Block/Rocket.prefab");
foreach (var t in prefab.GetComponentsInChildren<UnityEngine.Transform>(true)) UnityEngine.Debug.Log(UnityEditor.AnimationUtility.CalculateTransformPath(t, prefab.transform));
```

`Rocket.fbx` のノードは `Rocket`（モデルの根）・`base`・`body` なので、期待するパスは `Rocket/body`。ログのパスが違えば下の `Find` の引数をログの値に直してから、ルートへコンポーネントを足して割り当てる:

```csharp
var path = "Assets/AddressableResources/Block/Rocket.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var processor = root.AddComponent<Client.Game.InGame.BlockSystem.StateProcessor.Rocket.RocketLaunchPadStateChangeProcessor>();
var serialized = new UnityEditor.SerializedObject(processor);
serialized.FindProperty("body").objectReferenceValue = root.transform.Find("Rocket/body");
serialized.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
```

保存後にもう一度読み、`body` が null でないことをログで確かめる。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Rocket moorestech_client/Assets/AddressableResources/Block/Rocket.prefab
git commit -m "feat(client): 発射台のロケット本体の表示と上昇を足す"
```

（Unity が生成した `.meta` は一緒にコミットしてよい。）

---

### Task 8: 発射台の Web UI

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Rocket/RocketLaunchPadDetailDto.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Rocket/RocketLaunchPadDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`（`public RocketLaunchPadDetailDto RocketLaunchPad;`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`（`TrainStationDetailDtoBuilder.Apply` の次に1行）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Rocket/RocketLaunchPadCommandActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`（`MachineRecipeSelectActionHandler` の登録の次に1行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_rocket_launch_pad.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/RocketLaunchPadWireContractTest.cs`
- Modify: `Localization/localization.csv`
- Create: `moorestech_web/webui/src/bridge/contract/schemas/train/index.ts`（`inventory.ts` から列車の schema を移す）
- Create: `moorestech_web/webui/src/bridge/contract/schemas/beltThroughput/index.ts`（`inventory.ts` からベルト統計の schema を移す）
- Create: `moorestech_web/webui/src/bridge/contract/schemas/rocketLaunchPad/index.ts`（schema だけ。型は置かない）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`、`schemas/index.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`（`RocketLaunchPadData` 型を導出する。公開型の定義元はここだけ）
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`（fixture 一覧）
- Create: `moorestech_web/webui/src/features/blockInventory/views/RocketLaunchPadInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/rocket/RocketLaunchPadSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/rocket/rocketLaunchPadLogic.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/rocket/rocketLaunchPadLogic.test.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/registry/registeredBlockTypes.ts`、`registry/blockComponentRegistry.ts`

スロットの並び（資材→積荷→出力の絶対添字）は C# の `RocketLaunchPadSlotLayout` が計算した値を DTO にそのまま載せ、TS では計算し直さない（判断記録 D29）。`RocketLaunchPadSlotLayout` はインターフェース層の型なので、クライアントがこれを生成するのはサーバーと並びの規則を共有するための正規の参照になる。

**Interfaces:**
- Consumes: Task 4 の `RocketLaunchPadStateDetail`・`RocketLaunchPadSlotLayout`（`Game.Block.Interface.State`・`Game.Block.Interface.Component`）、Task 6 の `BlockRequestApi.SendRocketLaunchPadCommand`・`RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation`
- Produces:
  - wire `rocketLaunchPad`: `{ phase: "idle"|"awaitingBuildCommand"|"building"|"awaitingLaunchCommand"|"readyToLaunch"|"launching", waitReason: "none"|"missingMaterials"|"missingPayload"|"outputFull"|"noPower", progress: number(0..1), materialSlots: int[], payloadSlots: int[], outputSlots: int[] }`
  - action `rocket_launch_pad.command`、payload `{ command: "build" | "launch" }`
  - testid: `rocket-launch-pad-view`・`rocket-launch-pad-material-slots`・`rocket-launch-pad-payload-slots`・`rocket-launch-pad-output-slots`・`rocket-launch-pad-status`・`rocket-launch-pad-progress`・`rocket-launch-pad-build`・`rocket-launch-pad-launch`

- [ ] **Step 1: ref-webui-design スキルを読み、inventory.ts を分ける**

`inventory.ts`（215行）から、`TrainPlatformModeSchema`〜`TrainStationDetailSchema`（124〜152行）を `schemas/train/index.ts` へ、`BeltThroughputWindowDataSchema`・`BeltThroughputDataSchema`（156〜169行）を `schemas/beltThroughput/index.ts` へそのまま移し（`const` だった `TrainTimetableStateSchema`・`TrainStationDetailSchema` は `export const` にする）、`inventory.ts` は import に置き換える。`schemas/index.ts` に `export * from "./train";`・`export * from "./beltThroughput";`・`export * from "./rocketLaunchPad";` を足す。挙動は変えない。

Run: `cd moorestech_web/webui && pnpm vitest run src/bridge` → 全 PASS（移しただけで壊れていないこと）

- [ ] **Step 2: wire schema とアクション契約を足す**

`schemas/rocketLaunchPad/index.ts`（schema だけを置く。前例 `schemas/energyConversion/index.ts`。`schemas/` の下には `z.infer` を置かない）:

```ts
import { z } from "zod";

// 発射台の段階・待ち理由・進み具合と、C# が並べたスロット群の絶対添字。C# の RocketLaunchPadDetailDtoBuilder と1対1
// Launch pad phase, wait reason, progress and the slot group indices laid out by C#; mirrors RocketLaunchPadDetailDtoBuilder one-to-one
export const RocketLaunchPadPhaseSchema = z.enum(["idle", "awaitingBuildCommand", "building", "awaitingLaunchCommand", "readyToLaunch", "launching"]);
export const RocketLaunchPadWaitReasonSchema = z.enum(["none", "missingMaterials", "missingPayload", "outputFull", "noPower"]);
const SlotIndexListSchema = z.array(z.number().int().nonnegative());
export const RocketLaunchPadDataSchema = z.object({
  phase: RocketLaunchPadPhaseSchema,
  waitReason: RocketLaunchPadWaitReasonSchema,
  progress: z.number().min(0).max(1),
  materialSlots: SlotIndexListSchema,
  payloadSlots: SlotIndexListSchema,
  outputSlots: SlotIndexListSchema,
}).strict();
```

`payloadTypes.ts` の `from "./schemas"` の import に `RocketLaunchPadDataSchema` を足し、`TrainPlatformMode` の型の並びに `export type RocketLaunchPadData = z.infer<typeof RocketLaunchPadDataSchema>;` を足す（L66「公開DTO型は実行時スキーマから導出し、wire shapeの定義元を一つに保つ」。feature 層は `@/bridge` の `export type * from "./contract/payloadTypes"` からだけ型を見る）。

`inventory.ts` の `BlockInventoryOpenSchema` に `rocketLaunchPad: RocketLaunchPadDataSchema.optional(),` を足し、`superRefine` に「`rocketLaunchPad` は `blockType === "RocketLaunchPad"` のときだけ」を既存の変換機と同じ書式で足す。

`actionContract.ts` の payload 型表に `"rocket_launch_pad.command": { command: "build" | "launch" };`、`ACTION_TYPES` に `"rocket_launch_pad.command",` を足す。`action_names.json` にも同じ名前を足す。

- [ ] **Step 3: C# の DTO・アクションを書く**

名前空間はサブディレクトリによらず親のまま置く（前例: `Topics/BlockDetail/Train/`・`Belt/` の DtoBuilder は `Client.WebUiHost.Game.Topics.BlockDetail`、`Actions/Train/` のハンドラは `Client.WebUiHost.Game.Actions`。再検査 N3）。そのため `BlockInventoryDtos.cs`・`BlockDetailDtoBuilder.cs`・`WebUiGameBinder.cs` に using は足さない。

`RocketLaunchPadDetailDto.cs`（名前空間 `Client.WebUiHost.Game.Topics.BlockDetail`。既存DTOと同じ public フィールド）:

```csharp
using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public class RocketLaunchPadDetailDto
    {
        public string Phase;
        public string WaitReason;
        public float Progress;
        public List<int> MaterialSlots;
        public List<int> PayloadSlots;
        public List<int> OutputSlots;
    }
}
```

`RocketLaunchPadDetailDtoBuilder.cs`:

```csharp
using System;
using System.Linq;
using Client.Game.InGame.Block;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 発射台の状態とスロット群の絶対添字をDTOへ変換する
    /// Converts the launch pad state and the absolute indices of its slot groups into the DTO
    /// </summary>
    public static class RocketLaunchPadDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not RocketLaunchPadBlockParam padParam) return;
            var state = block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
            // silent-ok: 初回の状態を受け取る前だけ詳細を省き、受信後の再配信で埋まる / ボタンと段階表示が出ないことでパネル上で気づく
            if (state == null) return;

            // スロット群はサーバーと同じ並びの型が計算した添字をそのまま載せる
            // Carry the indices computed by the layout type the server also uses
            var layout = new RocketLaunchPadSlotLayout(padParam);
            dto.RocketLaunchPad = new RocketLaunchPadDetailDto
            {
                Phase = ToWirePhase(state.Phase),
                WaitReason = ToWireWaitReason(state.WaitReason),
                Progress = state.Progress,
                MaterialSlots = layout.MaterialSlots.ToList(),
                PayloadSlots = layout.PayloadSlots.ToList(),
                OutputSlots = layout.OutputSlots.ToList(),
            };
        }

        private static string ToWirePhase(RocketLaunchPadPhase phase)
        {
            return phase switch
            {
                RocketLaunchPadPhase.Idle => "idle",
                RocketLaunchPadPhase.AwaitingBuildCommand => "awaitingBuildCommand",
                RocketLaunchPadPhase.Building => "building",
                RocketLaunchPadPhase.AwaitingLaunchCommand => "awaitingLaunchCommand",
                RocketLaunchPadPhase.ReadyToLaunch => "readyToLaunch",
                RocketLaunchPadPhase.Launching => "launching",
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知の発射台の段階"),
            };
        }

        private static string ToWireWaitReason(RocketLaunchPadWaitReason reason)
        {
            return reason switch
            {
                RocketLaunchPadWaitReason.None => "none",
                RocketLaunchPadWaitReason.MissingMaterials => "missingMaterials",
                RocketLaunchPadWaitReason.MissingPayload => "missingPayload",
                RocketLaunchPadWaitReason.OutputFull => "outputFull",
                RocketLaunchPadWaitReason.NoPower => "noPower",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の発射台の待ち理由"),
            };
        }
    }
}
```

`BlockDetailDtoBuilder.Apply` の `TrainStationDetailDtoBuilder.Apply(dto, block, param);` の次に `RocketLaunchPadDetailDtoBuilder.Apply(dto, block, param);`。

`Actions/Rocket/RocketLaunchPadCommandActionHandler.cs`（失敗コードは `ActionErrorCodeWireContractTest` の既存集合から選ぶ。操作の種類はプロトコルにネストした enum を使う。using は前例 `Actions/TrainPlatformActions.cs` と同じく `SubInventoryState` の `Client.Game.InGame.UI.UIState.State` と `BlockSubInventorySource` の `.SubInventory` の両方を入れる）:

```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 開いている発射台へ建造・発射の合図を送る
    /// Sends a build or launch command to the open rocket launch pad
    /// </summary>
    public class RocketLaunchPadCommandActionHandler : IActionHandler
    {
        public string ActionType => "rocket_launch_pad.command";

        private readonly SubInventoryState _subInventoryState;

        public RocketLaunchPadCommandActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            var operation = ParseOperation(payload["command"]);
            if (operation == null) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");
            if (!source.TryGetBlockIdentity(out _, out var blockTypeName) || blockTypeName != "RocketLaunchPad") return ActionResult.Fail("block_not_open");

            // 表示の正本は後続のブロック状態。ここでは受理されたかだけ返す
            // The later block state is the display source of truth; only acceptance is reported here
            var response = await ClientContext.VanillaApi.Response.Block.SendRocketLaunchPadCommand(source.BlockPosition, operation.Value, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("invalid_state");
            return ActionResult.Success();
        }

        private static RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation? ParseOperation(JToken token)
        {
            if (token is not JValue { Type: JTokenType.String } value) return null;
            return (string)value switch
            {
                "build" => RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation.Build,
                "launch" => RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation.Launch,
                _ => null,
            };
        }
    }
}
```

（`ActionResult.Fail` に失敗理由を残す口が既存にあれば、`response.FailureReason` をそこへ渡す。サーバー側の拒否はサーバーのログに理由が残る。）

`WebUiGameBinder.cs` の `hub.RegisterAction(new MachineRecipeSelectActionHandler(subInventoryState, unlockStateData));` の次に `hub.RegisterAction(new RocketLaunchPadCommandActionHandler(subInventoryState));` を足す（前例: 開いているブロックへ合図を送るハンドラを `subInventoryState` 付きで直に登録する同じ行。`TrainPlatformSetTransferModeActionHandler` は `Actions/Train/TrainWebUiActionRegistration.cs` 経由の登録で、この1行の前例にならない）。ハンドラは名前空間 `Client.WebUiHost.Game.Actions` なので、既存の `using Client.WebUiHost.Game.Actions;` で足りる。

- [ ] **Step 4: 文言を足す**

`Localization/localization.csv` の `ui.blockInventory.trainPlatformUnload` の次に追加し（列: key,Source,english,japanese,german,korean）、`cd moorestech_web/webui && pnpm gen:i18n` を実行する:

```csv
ui.blockInventory.rocketMaterials,Materials,Materials,資材,Materialien,자재
ui.blockInventory.rocketPayloads,Payloads,Payloads,積荷,Nutzlasten,적재물
ui.blockInventory.rocketOutputs,Output,Output,出力,Ausgabe,출력
ui.blockInventory.rocketBuild,Build,Build,建造,Bauen,건조
ui.blockInventory.rocketLaunch,Launch,Launch,発射,Starten,발사
ui.blockInventory.rocketStatusIdle,Waiting for cargo,Waiting for cargo,積み込み待ち,Wartet auf Fracht,적재 대기
ui.blockInventory.rocketStatusAwaitingBuild,Ready to build,Ready to build,建造できます,Bereit zum Bauen,건조 가능
ui.blockInventory.rocketStatusBuilding,Building rocket,Building rocket,ロケットを建造中,Rakete wird gebaut,로켓 건조 중
ui.blockInventory.rocketStatusAwaitingLaunch,Ready to launch,Ready to launch,発射できます,Startbereit,발사 가능
ui.blockInventory.rocketStatusReadyToLaunch,Launching soon,Launching soon,まもなく打ち上げ,Start in Kürze,곧 발사
ui.blockInventory.rocketStatusLaunching,Launching,Launching,打ち上げ中,Startet,발사 중
ui.blockInventory.rocketWaitMissingMaterials,Not enough materials,Not enough materials,資材が足りません,Nicht genug Material,자재 부족
ui.blockInventory.rocketWaitMissingPayload,No payload loaded,No payload loaded,積荷がありません,Keine Nutzlast,적재물 없음
ui.blockInventory.rocketWaitOutputFull,Output is full,Output is full,出力がいっぱいです,Ausgabe ist voll,출력이 가득 참
ui.blockInventory.rocketWaitNoPower,No power,No power,電力がありません,Kein Strom,전력 없음
```

- [ ] **Step 5: パネルを書く**

`details/rocket/rocketLaunchPadLogic.ts`（表示の分岐を純関数に集める。スロットの並びは DTO の添字を使うので、ここでは計算しない。文言キーは前例 `details/detailLogic.ts:37` の `stopReasonTranslationKey` と同じく `TranslationKey` で返し、`RocketLaunchPadSection` の `group` の `labelKey` も `TranslationKey` にする。キーの打ち間違いを型エラーにするため（判断記録 D34））:

```ts
import type { RocketLaunchPadData } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 段階と待ち理由から状態文のキーを選ぶ。待ち理由があればそちらを優先する
// Choose the status text key from phase and wait reason, preferring the wait reason when present
export function rocketStatusKey(detail: RocketLaunchPadData): TranslationKey {
  switch (detail.waitReason) {
    case "missingMaterials": return L.ui.blockInventory.rocketWaitMissingMaterials;
    case "missingPayload": return L.ui.blockInventory.rocketWaitMissingPayload;
    case "outputFull": return L.ui.blockInventory.rocketWaitOutputFull;
    case "noPower": return L.ui.blockInventory.rocketWaitNoPower;
    case "none": break;
  }
  switch (detail.phase) {
    case "idle": return L.ui.blockInventory.rocketStatusIdle;
    case "awaitingBuildCommand": return L.ui.blockInventory.rocketStatusAwaitingBuild;
    case "building": return L.ui.blockInventory.rocketStatusBuilding;
    case "awaitingLaunchCommand": return L.ui.blockInventory.rocketStatusAwaitingLaunch;
    case "readyToLaunch": return L.ui.blockInventory.rocketStatusReadyToLaunch;
    case "launching": return L.ui.blockInventory.rocketStatusLaunching;
  }
}

// 合図のボタンは初打ち上げ前の合図待ちだけに出る
// Command buttons appear only while awaiting a command before the first launch
export function rocketCommandButton(detail: RocketLaunchPadData): "build" | "launch" | null {
  if (detail.phase === "awaitingBuildCommand") return "build";
  if (detail.phase === "awaitingLaunchCommand") return "launch";
  return null;
}
```

`details/rocket/RocketLaunchPadSection.tsx`（`TrainPlatformSection`・`GeneratorSection` の書式。スロット描画は `MachineInventoryBody` の `slotAt` と同じく絶対添字で `useBlockSlotGestures` に渡す）:

```tsx
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n, type TranslationKey } from "@/shared/i18n";
import { GaugeBar, ItemSlot, PanelActionButton, SlotGrid } from "@/shared/ui";
import { useBlockSlotGestures } from "../../useBlockSlotGestures";
import { rocketCommandButton, rocketStatusKey } from "./rocketLaunchPadLogic";

export default function RocketLaunchPadSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const gestures = useBlockSlotGestures();
  const detail = data.rocketLaunchPad;
  if (!detail) return null;
  const command = rocketCommandButton(detail);

  // 絶対添字のスロットをジェスチャ付きで描く
  // Render a slot by absolute index with gestures attached
  const slotAt = (index: number) => {
    const slot = data.itemSlots[index];
    return (
      <ItemSlot
        key={index}
        itemId={slot.itemId}
        count={slot.count}
        onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
        onRightDown={() => gestures.onRightDown(index)}
        onDoubleClick={() => gestures.onDoubleClick(index)}
      />
    );
  };
  const group = (labelKey: TranslationKey, indices: number[], testId: string) => (
    <Stack gap={4}>
      <Text size="sm">{t(labelKey)}</Text>
      <SlotGrid cols={Math.min(9, Math.max(1, indices.length))} testId={testId}>{indices.map(slotAt)}</SlotGrid>
    </Stack>
  );

  return (
    <Stack gap="sm">
      {group(L.ui.blockInventory.rocketMaterials, detail.materialSlots, "rocket-launch-pad-material-slots")}
      {group(L.ui.blockInventory.rocketPayloads, detail.payloadSlots, "rocket-launch-pad-payload-slots")}
      <Text size="sm" data-testid="rocket-launch-pad-status">{t(rocketStatusKey(detail))}</Text>
      <GaugeBar value={detail.progress} testId="rocket-launch-pad-progress" />
      {command === "build" ? (
        <PanelActionButton testId="rocket-launch-pad-build" onClick={() => { void dispatchAction("rocket_launch_pad.command", { command: "build" }); }}>
          {t(L.ui.blockInventory.rocketBuild)}
        </PanelActionButton>
      ) : null}
      {command === "launch" ? (
        <PanelActionButton testId="rocket-launch-pad-launch" onClick={() => { void dispatchAction("rocket_launch_pad.command", { command: "launch" }); }}>
          {t(L.ui.blockInventory.rocketLaunch)}
        </PanelActionButton>
      ) : null}
      {group(L.ui.blockInventory.rocketOutputs, detail.outputSlots, "rocket-launch-pad-output-slots")}
    </Stack>
  );
}
```

（ボタンの置き場は ref-webui-design スキル components.md §8.6 の titleAction 配置規則を読み、そちらが指す位置に合わせる。`Text` の色・余白は同スキルのホワイトリストから選ぶ。）

`views/RocketLaunchPadInventory.tsx`:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import RocketLaunchPadSection from "../details/rocket/RocketLaunchPadSection";

export default function RocketLaunchPadInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm" data-testid="rocket-launch-pad-view">
      <RocketLaunchPadSection data={data} />
    </Stack>
  );
}
```

`registeredBlockTypes.ts` に `"RocketLaunchPad",`、`blockComponentRegistry.ts` に `RocketLaunchPad: RocketLaunchPadInventory,`（import も）。

- [ ] **Step 6: テストを書く**

`rocketLaunchPadLogic.test.ts`: (a) 待ち理由 `outputFull` は段階（`readyToLaunch`）より優先、(b) `awaitingBuildCommand` で `"build"`、`awaitingLaunchCommand` で `"launch"`、その他の4段階（`readyToLaunch` を含む）で `null`、(c) 6段階すべてに状態文のキーがある（`switch` の網羅は `tsc` の戻り値の型検査でも落ちる）。

`views/__tests__/RocketLaunchPadInventory.test.tsx`（`TrainPlatformInventory.test.tsx` と同じ mock 構成）: `awaitingBuildCommand` のデータで `rocket-launch-pad-build` があり `rocket-launch-pad-launch` が無い、`building` と `readyToLaunch` でどちらも無い、DTO の `materialSlots: [0]`・`payloadSlots: [1]`・`outputSlots: [2]` どおりの添字のスロットが各群に1つずつ描かれる、`dispatchAction` を mock してボタン押下で `("rocket_launch_pad.command", { command: "build" })` が呼ばれる。

`WireFixtures/block_inventory_rocket_launch_pad.json`:

```json
{
  "open": true,
  "source": "block",
  "blockType": "RocketLaunchPad",
  "identifier": "(8, 0, 4)",
  "blockGuid": "88888888-8888-4888-8888-888888888888",
  "itemSlots": [
    { "itemId": 3, "count": 20 },
    { "itemId": 0, "count": 0 },
    { "itemId": 0, "count": 0 }
  ],
  "fluidSlots": [],
  "rocketLaunchPad": {
    "phase": "awaitingBuildCommand",
    "waitReason": "none",
    "progress": 0.0,
    "materialSlots": [0],
    "payloadSlots": [1],
    "outputSlots": [2]
  }
}
```

`Client.Tests/WebUi/BlockDetail/RocketLaunchPadWireContractTest.cs`: `WireContractPhaseB2Test.TrainPlatformFixtureMatchesDto` と同じ形で同じ値の `BlockInventoryDto` を作り、`WebUiJson.Serialize` の結果が fixture と `JToken.DeepEquals` で一致することを確かめる（`AssertMatchesFixture` は同ファイル内に写す）。`progress` の `0.0` と `0` の差で一致しなければ fixture を C# の出力に合わせる。`wireContract.test.ts` の block detail fixtures 一覧に `"block_inventory_rocket_launch_pad.json"` を足す。

- [ ] **Step 7: コンパイル・テストを実行する**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RocketLaunchPadWireContractTest|WireContractActionNamesTest|ActionErrorCodeWireContractTest"
cd moorestech_web/webui && pnpm vitest run && pnpm tsc --noEmit && pnpm lint
```
Expected: ErrorCount 0 / 全 PASS（`pnpm lint` が無いスクリプト名なら package.json の lint 系を使う）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi Localization/localization.csv moorestech_web/webui/src
git commit -m "feat(webui): ロケット発射台のパネルと建造発射の操作を足す"
```

---

### Task 9: moores-code-review による全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、反映後のコードで Task 1〜8 のテスト一式（各タスクの「コンパイルしテストを実行する」ステップのフィルタすべて）を再実行してから完了とする。この plan に unityプレイ録画テストは無い（統合タスクへ移した。判断記録 D37）ので、レビュー報告に「録画は統合タスク bd `moorestech-xyuaf.2` で撮る」と書く。

---

### Task 10: PR作成

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

- moorestech_master（`feat/tier9-compute-data-loop` → master）の PR も作り、moorestech の PR 本文から相互にリンクする。master の PR の中身は items.json の `"orbitalPayloads": []` と、スタックテーブルが空の7件への Default だけ（D14）。`.moorestech-external-revisions.json` のピンがその PR の head と一致していること。
- PR 本文に入れる節: 概要（この PR はコード・スキーマ・テスト用データまでで、遊べるのは統合タスクの後）／S3向けの口（`Game.Orbit` の API と GUID。`PreviewArrivalOutputs` を足したことと、拠点も成果が出力口に入りきるときだけ打ち上げる方式にすること）／統合タスク（bd `moorestech-xyuaf.2`）へ回したもの／確かめ方（CombinedTest・プロトコルテスト・web UI の vitest と wire 契約テストの結果。録画は統合タスク）／セーブ版 6→7（S1 も 6→7・7→8、S3 は 7→8 を使うので、後からマージする側が番号を繰り上げる）／本PR外のリファクタ提案。
- 「統合タスクへ回したもの」の節: plan 末尾の「統合タスクへ引き継ぐマスタの中身」節の見出しを並べ（アイテム・積荷表の行・発射台の値・ビルドメニュー・レシピ・研究・バランス・録画テスト）、裁定 `.decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md` へリンクする。値（計算データの成果 Lv1=10〜Lv5=160・研究コスト・1便の資材数・建造と打ち上げの秒数・電力）は統合タスクで決め直す仮値であることも書く（判断記録 D12）。
- 「本PR外のリファクタ提案」の節（plan のタスクにはしない。既存側／新設側／同じ役割と判断した根拠の順に書く）:
  1. `Core.Master/Validator/Item/ItemInitialEquipmentValidator.cs` の `InitialLevelMaxStackValidation`（本PRで `ItemMasterUtil.cs` から移すだけで中身は変えない）／新設の `OrbitalPayloadMasterValidator.ValidatePayloadItem`・`RocketLaunchPadParamValidator.FirstLevelStackOrNull` ／どれもアイテムのスタックテーブルの `StackCounts[0]` と個数を比べて読込時に弾く同じ判定（段数0の見送りは D29 で揃えた）。
  2. `Game.Block/Blocks/Machine/VanillaElectricMachineComponent.cs`（同形: `Miner/VanillaElectricMinerComponent.cs`・`Pump/ElectricPumpComponent.cs`・`CleanRoom/Machine/CleanRoomMachineComponent.cs`）／新設の `RocketLaunchPadElectricComponent` ／どれも処理部の要求電力を `RequestEnergy` に出し、要求×`PowerRate` を処理部の供給口へ渡すだけの同じ導出。
  3. `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs`（`CanStoreOutputs`・`InsertOutputSlot`・仮挿入と実挿入で共有する `TrySimulateOutputs`・`InsertConnectInventory`）／新設の `RocketLaunchPadOutputSlots`（`CanStore`・`Store`・`PushToConnected`）／どちらも「出力の並びが出力口へ入りきるかを書き込まずに試し、確定して、接続先へ流す」役割で、`PushToConnected` は `InsertConnectInventory` と同じ処理。
  4. `Game.Block/Blocks/Machine/Inventory/Input/MachineInputSlotBinding.cs`（`ResolveBoundSlots`・`RequiredCountAt`・`CheckPlacement`）と `VanillaMachineInputSimulationUtil.SimulateInsert` ／新設の `RocketLaunchPadSlotLayout.TryGetInputSlot`・`RequirementAt` と `RocketLaunchPadInventoryComponent` の仮挿入 ／どちらも「アイテムを束縛された入れ口へ割り当て、仮挿入する」役割。
  5. `Game.Block/Blocks/Machine/State/Util/MachineStateDetailFactory.cs`（`processingRate` の式）／新設の発射台の進み具合の計算（`Clamp01(1 - 残りtick/総tick)`）／どちらも「残りtickと総tickから0〜1の進み具合を出す」同じ式。
- 全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である。

---

## 判断記録（ADR）

設計セッションの裁定:
- `docs/adr/0088-orbital-arrival-service-shared-by-launch-sites.md`
- `.decisions/2026-10-23-計算データはGPU衛星の打ち上げごとに一括で得る.md`
- `.decisions/2026-10-23-GPU衛星はレベル別アイテムとして製造してから打ち上げる.md`
- `.decisions/2026-10-23-ロケットの手動建造発射はワールドで最初の1回だけ.md`
- `.decisions/2026-10-23-ロケットは打ち上げごとに筐体素材燃料コンピュータペイロードを消費する.md`
- `.decisions/2026-10-23-発射台は届いた積荷で打ち上げ内容を自動で選ぶ.md`
- `.decisions/2026-10-23-ロケットは独立研究にし計算データは半導体アップグレードと軌道エレベーターの両方で使う.md`
- `.decisions/2026-10-23-半導体のアップグレード研究は計算データでモジュールの上位を解放する.md`
- `.decisions/2026-10-23-発射台は電力を使い時間をかけてロケットを建造する.md`
- `.decisions/2026-10-23-発射台は既存Rocketモデルを使いロケット本体だけを上昇させる.md`
- `.decisions/2026-10-23-計算データは打ち上げた設備の出力口に出す.md`
- `.decisions/2026-10-23-発射台は成果が出力口に入りきるときだけ建造と打ち上げを進める.md`
- `.decisions/2026-10-23-発射台の段階は段階ごとの型で持つ.md`
- `.decisions/2026-10-23-発射台の型検査の弱い指摘は4件とも直す.md`
- `.decisions/2026-10-23-計算データの研究ノードはS2が先に作り解放先はS1のモジュールが入ってから足す.md`
- `.decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md`
- 用語: `CONTEXT.md`「ロケットと計算データ」

planning 中の判断:

| # | 判断 | 出所 |
|---|---|---|
| D1 | 積荷表は items.yml のトップレベル配列（新規トップレベルスキーマを作らない） | agent前提（edit-schema スキルの規約） |
| D2 | 成果が出力口に入りきるかを、建造を始める前と打ち上げる直前に確かめ、入りきらなければ待たせて理由（`OutputFull`）を出す。打ち上げ後に成果を預かる保留は作らない。`IOrbitalArrivalService` に成果を先に知る `PreviewArrivalOutputs` を足し、S3 の拠点も同じ方式にする（S3 へ 2026-10-23 連絡済み）。積荷ごとの1便ぶんの成果がレベル1のスタック上限で数えて `outputSlotCount` 以下であることをマスタ検証で強制する（満たさない積荷は空の出力口でも永遠に待つため） | ユーザー裁定 2026-10-23（質問「発射台の出力口が満杯のとき、どこで待たせますか？」→ 選択「入りきる時だけ建造開始」。棄却: 「打ち上げ後に預かって止める」。[[2026-10-23-発射台は成果が出力口に入りきるときだけ建造と打ち上げを進める]]）。マスタ検証は agent前提（帰結） |
| D3 | 軌道到着は打ち上げ開始の時点。昇っている `launchSeconds` の間は次の便を建造しない | agent前提（CONTEXT.md「軌道到着」） |
| D4 | 資材・積荷は建造開始で取り込む。撤去時は建造中・発射の合図待ち・打ち上げ待ちの中身を返し、打ち上げ後は返さない | agent前提（`.decisions/…4つとも毎回消費` の「打ち上げごとに消費」を建造開始＝取り込み時点と解釈） |
| D5 | 積荷1種につき入れ口1スロット。1便の個数はレベル1のスタック上限以下をマスタ検証で強制（S3 のケーブルは専用スタックテーブルで1000本/便） | agent前提（S3 合意 2026-10-23） |
| D6 | 揃った積荷が複数あるときは前回の次から順に選ぶ（積荷表の並び順の巡回） | agent前提（同点選択。どれを選んでも1便の消費・成果は表で同型に決まるため、偏りの防止だけを目的に巡回にする） |
| D7 | 電力は建造中だけ要求し、供給は機械と同じく加算→次の Update で使い切る | agent前提（`VanillaMachineProcessorComponent.SupplyExternalPower` の前例） |
| D8 | 状態は段階・待ち理由が変わったときと、建造中・打ち上げ中は毎tick流す | agent前提（`VanillaMachineProcessorComponent.Update` の前例） |
| D9 | 初打ち上げの通知（`OnFirstLaunch` の購読）は発射の合図待ち→打ち上げ待ちだけを移す（`ReleaseLaunchCommandWait`）。建造の合図待ちは各台の次の Update の `AdvanceWaitingForCargo` が初打ち上げ済みを見て建造を始める（購読が無くても同じtickか次のtickに解けるので、移し替えを2か所に持たない。再検査 注目点1）。打ち上げは各台の次の Update が行う。ロード時に初打ち上げ済みなら発射の合図待ちだけ同じ移し替えをする | ユーザー裁定 2026-10-23（D25）。購読は agent前提（AGENTS.md「状態変化の検知は購読で」）。建造の合図待ちを Update の判定へ任せたのは D39 の前提宣言 |
| D10 | 入れ口のセーブはアイテム単位、出力口は並び。マスタ変更で置き場が無くなった品は警告して捨てる | agent前提（テスターのワールドがマスタ更新を跨ぐため。保留を作らない裁定 D2 の帰結で、無効データは消す既存の流儀 `MissingMasterPruner` と同じ向き） |
| D11 | `inventory.ts`（215行）は列車とベルト統計の schema を別ファイルへ移してから足す。`GameplayServiceRegistration.cs`（202行）には足さず `OrbitServiceRegistration` を新設。`WorldSaveDataRestorer.cs`（194行）は必須欠損の検査を `WorldSaveRequiredFieldGuard` へ移す | agent前提（AGENTS.md 200行規則） |
| D12 | 計算データの成果 Lv1=10・Lv2=20・Lv3=40・Lv4=80・Lv5=160（倍々）。ロケット技術の研究コスト、1便の資材数、建造120秒・打ち上げ15秒も仮値。どれも統合タスクでマスタへ入れるときに決め直す（この plan ではテスト用 mod の値だけを使う） | agent前提（GPUのレベルが計算データへどう効くかは S2 が決める: S1 からの連絡 2026-10-23「GPU の Lv が計算データにどう効くかは S2 で決めてください」。倍々は S1 裁定「大部分のレベル1と少数のレベル2チップ程度でクリアできるようにしたい」の下で Lv1 だけでも研究が進み、上位ほど報われる形にするため。研究コストは S1 の Tier9 研究コストの裁定が出るまでの仮値）。入れる時期はユーザー裁定 D36 |
| D13 | GPU・GPU衛星のレシピと「半導体のアップグレード研究」は、この plan では入れず統合タスクで入れる。GPU衛星は通常レシピ5本（GPU LvN＋資材→GPU衛星 LvN）、研究ノードは計算データを要求し、解放先は S1 の上位モジュールが入ってから足す | ユーザー裁定 2026-10-23（D36。研究ノードの形は質問「計算データで上位モジュールを解放する「半導体のアップグレード研究」はどう入れますか？」→ 選択「研究ノードだけ先に作る」 [[2026-10-23-計算データの研究ノードはS2が先に作り解放先はS1のモジュールが入ってから足す]]）。旧案「S1 の仕組みが origin/master にあるときだけ入れる」はこれで置き換え |
| D14 | moorestech_master の作業ブランチの土台 `b2149a0`（tier9基板）で `stackLevelTableGuid` が空になっている7件へ Default を入れる。今のピン `0d129c8` には空の行が無く、ピンを作業ブランチへ進めるとスタックテーブルの参照検証（Task 1 で `ItemStackLevelTableValidator` へ移す）で読込が止まるため、D36 の「読み込みに要る最小限」としてこの PR に入れる。S3 は同じ7件を重ねて直さない（Task 2 Step 8 の bd note） | agent前提（実測 2026-10-23: `git show 0d129c8:…/items.json` は空0件、`b2149a0` は7件。検証の実装は `ItemMasterUtil.cs` の `StackLevelTableValidation`） |
| D15 | Web UI の失敗コードは既存集合（`invalid_payload`・`block_not_open`・`invalid_state`）から選ぶ | agent前提（`ActionErrorCodeWireContractTest` の集合を広げない） |
| D16 | この plan には unityプレイ録画テストも EditModeInPlayingTest も入れない。ランタイム挙動・UI・3D表示に触れるが、実ゲームで発射台を置くにはマスタの中身が要り、それは統合タスクで入る（D37） | ユーザー裁定 2026-10-23（D36・D37） |
| D17 | Game.Orbit 一式と積荷表のスキーマ（repo 内の全JSON と master の空配列）を Task 1〜2 に置き、Task 2 の最後で両repoを push する。以後 force-push しない | agent前提（S3 からの依頼 2026-10-23「Game.Orbit 一式と orbitalPayloads のスキーマ／マスタ（GPU衛星行込み）を最初の実装タスクにし、できた時点で両 repo へ push」「その後もブランチは force-push しない」）。GPU衛星の行は D36 で統合タスクへ移した（S3 へ 2026-10-23 連絡済み） |
| D18 | `RemoveBlockProtocol` の返却を `IGetRefundItemsInfo` 全件の合算に直す。発射台の部品順は電線を進行より前に置き、修正前のコードでは返却が落ちることをテストで確かめる | agent前提（S3 からの指摘 2026-10-23。`RemoveBlockProtocol.cs:123` の `TryGetComponent` が先頭1件だけを読むことをコードで確認。原因: 推測（根拠: コード読解。未再現。Task 5 の撤去返却テストが修正前に落ちることで実測に変わる）） |
| D19 | ビルドメニュー「生産」の下に「宇宙」（`d2000000-0000-4000-8000-000200000005`）を作り、発射台と S3 の宇宙エレベーター拠点を置く。入れるのは統合タスク | agent前提（S3 からの提案 2026-10-23。既存の「半導体」サブカテゴリと同じ書式）。時期はユーザー裁定 D36 |
| D21 | `VanillaSchema/blocks.yml` に blockType `RocketLaunchPad` と case を足す。全キー必須（`optional` を使わず default だけ置く）で、`IInventoryConnectors`・`IElectricWireConnectParam` を実装させる | agent前提（edit-schema スキル・AGENTS.md「optional: true で吸収するのは設計の敗北」、前例 `CleanRoomMachine` の case） |
| D22 | 積荷表の検証は `Core.Master/Validator/OrbitalPayload/OrbitalPayloadMasterValidator.cs`（新設ディレクトリ）に置き、`ItemMasterUtil.Validate` の `errorLogs +=` 連鎖に1行足す（`ItemMaster.Validate` は委譲1行のまま。検証クラスは `internal` のまま、テストは `ItemMasterUtil.Validate` を呼ぶ）。`ItemMasterUtil.cs`（244行）は1行足す前に、S1 の領域ではないスタックテーブルと初期装備の検証を `Core.Master/Validator/Item/`（`ItemStackLevelTableValidator`・`ItemInitialEquipmentValidator`、internal static、本体は文字どおり写す）へ移して約176行にする。モジュールとレベルファミリーの検証は S1 の領域なので動かさない。発射台パラメータの検証は `Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs` に置き、`BlockMasterUtil.Validate` に1行足す。スタック上限の判定は既存の `InitialLevelMaxStackValidation` と同じく段数0のテーブルを見送る | agent前提（配置検査 #5: 全 Master の `Validate` は Util への委譲1行で、連鎖は Util 側。前例 `BlockMasterUtil.Validate`・`BeltSpeedPerSecondMasterTest.cs:57`。分割は再検査 N1: AGENTS.md「1ファイル200行以下」と moores-code-review の決定論チェック `file-too-long`。切り出しの前例は `Validator/Block/`）。段数0の見送りはユーザー裁定 D29 |
| D23 | 発射台の組み立ては `Game.Block/Factory/BlockTemplate/Rocket/VanillaRocketLaunchPadTemplate.cs`（New/Load とも `Create` へ集める）に置き、`Game.Block/Factory/VanillaIBlockTemplates.cs` の `BlockTypesDictionary` へ登録する。サービスは `ServerContext.GetService<T>()` で取る | agent前提（前例 `VanillaCleanRoomAirFilterTemplate.cs`） |
| D24 | 建造・発射の合図は1本のプロトコル `Server.Protocol/PacketResponse/Rocket/RocketLaunchPadCommandProtocol.cs`（直下は52ファイルのためサブディレクトリ。名前空間は `Server.Protocol.PacketResponse`）にする。操作の種類 `RocketLaunchPadCommandOperation` と通信の失敗理由 `RocketLaunchPadCommandFailureReason` はプロトコルにネストし、ドメインの結果 `RocketLaunchPadCommandResult` から `ToFailureReason` で写す。応答は `Success`＋`FailureReason`。部品は `IRocketLaunchPadComponent` で引くので asmdef の参照は足さない | agent前提（配置検査 #1・#4。前例 `MachineRecipeSelectionProtocol.cs`・`IMachineRecipeSelectorComponent.cs`・`PacketResponse/TrainStation/SetTrainStationNameProtocol.cs`、creating-server-protocol スキル「1プロトコル=1メソッド」） |
| D20 | 「軌道エレベーターの建設」の研究ノードの変更（発射台・筐体素材・燃料の解放を外し、計算データを消費に足す）は S2 の plan では触らない。中身は S3 が決め、統合タスクで入れる | agent前提（ユーザー裁定 2026-10-23「ロケット独立＋両方で消費」の実施分担。ノードの持ち主は S3）。時期はユーザー裁定 D36 |
| D25 | 打ち上げを始める口は `RocketLaunchPadLaunchSequence.AdvanceReadyToLaunch` の1か所だけにする。発射の合図と初打ち上げの通知は段階を `ReadyToLaunchStage` へ移すだけ。別の台は自分の Update で同じtickか次のtickに打ち上がる（テスト4は「次のtickまでに打ち上がる」） | ユーザー裁定 2026-10-23（質問「型と重複の検査で出た弱い指摘のうち、どれを直しますか？」→ 選択「打ち上げ開始の口を1つに, 進み具合と読込時のガード, 判定と導出の重複を消す, 契約型をInterface層へ」。[[2026-10-23-発射台の型検査の弱い指摘は4件とも直す]]）。配置検査の注目点1（再入と発火順の回避策）もこれで解消 |
| D26 | 発射台の段階は不変の段階型（`IdleStage`・`AwaitingBuildCommandStage`・`BuildingStage`（積荷＋残りtick）・`AwaitingLaunchCommandStage`（積荷）・`ReadyToLaunchStage`（積荷）・`LaunchingStage`（残りtick））で持ち、合わない組み合わせは段階型の ctor が例外で拒む。進行は部品の外殻 `RocketLaunchPadProcessorComponent` と段階の進行 `RocketLaunchPadLaunchSequence` に分け、どちらも200行以内 | ユーザー裁定 2026-10-23（質問「発射台の「段階」と、ある段階でしか意味を持たない残り時間・積んだ荷物を、段階ごとの型に分けますか？」→ 選択「段階ごとの型に分ける」。[[2026-10-23-発射台の段階は段階ごとの型で持つ]]）。外殻と進行の分け方は新規パターン（D26 の裁定による）。機械の State ハンドラ方式（`Game.Block/Blocks/Machine/State/ProcessingMachineProcessState.cs` の `IMachineProcessState` が自分の遷移を決める形）とは遷移の持ち主が違う（再検査 注目点2） |
| D27 | 発射台の契約型（`IRocketLaunchPadComponent`・`RocketLaunchPadCommandResult`・`RocketLaunchPadSlotLayout`・`RocketLaunchSlotRequirement`・`RocketLaunchPadPhase`・`RocketLaunchPadWaitReason`・`RocketLaunchPadStateDetail`）を `Game.Block.Interface/Component/RocketLaunchPad/`・`State/RocketLaunchPad/` に置く。プロトコルは `IRocketLaunchPadComponent` を `TryGetComponent` で引き、クライアントは Game.Block の具象型を参照しない | ユーザー裁定 2026-10-23（D25 と同じ質問の「契約型をInterface層へ」）。置き場は agent前提（前例 `Game.Block.Interface/Component/CleanRoom/`・`IMachineRecipeSelectorComponent.cs`）。配置検査の注目点4（クライアントがサーバー型を生成）もこれで解消 |
| D28 | 進み具合は生成側で0〜1に収める。ロード時、今の積荷表で打ち上げられない積荷（表に無い・個数が今の1便ぶんと違う・欠損マスタの刈り込みで空。積んだロケットは常に1便ぶんなので、個数は一致で比べる。比べ方は D39 (b) で改めた）は警告して捨てて待機へ戻し、不明な段階の値も警告して待機へ戻す。残り0以下は警告して1tickにし、負の「次に探す積荷」は `((x % n) + n) % n` で直す。ロードは止めない | ユーザー裁定 2026-10-23（D25 と同じ質問の「進み具合と読込時のガード」）。配置検査の注目点7（刈り込みで積荷が空になりロードが止まる）もこれで解消 |
| D29 | スロットの並びは C# の `RocketLaunchPadSlotLayout` が計算した群ごとの絶対添字を DTO（`materialSlots`・`payloadSlots`・`outputSlots`）に載せ、TS では計算し直さない。スタック上限の判定は既存と同じく段数0のテーブルを見送る | ユーザー裁定 2026-10-23（D25 と同じ質問の「判定と導出の重複を消す」） |
| D30 | 揃った積荷のうち、成果が出力口に入りきる最初の積荷を選ぶ。出力が満杯でも成果の無い積荷（グラフェンケーブル）は打ち上がる。どれも入りきらなければ `OutputFull`、揃った積荷が無ければ `MissingPayload` | agent前提（D2 と D6 の組み合わせ。巡回の先頭が入りきらないだけで、入りきる他の積荷まで止めないため） |
| D31 | 発射台のインベントリは `ISortExcludedSlots` で全スロットを整理の対象から外す。インベントリのテストに「整理しても中身が動かない」を足す | agent前提（配置検査 #2。前例は役割が同じ `VanillaMachineBlockInventoryComponent.cs` で、機構だけ同じ `CleanRoomAirFilterInventoryComponent` ではない。外さないと `InventorySortService` が ItemId 順に詰め直し、専用スロットの中身が入れ替わって建造が止まる） |
| D32 | 発射台のブロックのセーブは段階を int、残り時間を秒（`remainingSeconds`）で持ち、ロードで tick へ戻す | agent前提（配置検査 #3。前例 `CleanRoomMachineProcessorSaveJsonObject.cs` の `state`・`remainingSeconds`、`VanillaMachineProcessorComponent` の「tickを秒数に変換して保存」。`StringEnumConverter` はサーバーで未使用） |
| D33 | 新しいファイルは10ファイルを超えたディレクトリの直下に置かない: プロトコルは `PacketResponse/Rocket/`、テスト共有ヘルパは `Tests/Util/Rocket/`（直下19件）。webui の公開型 `RocketLaunchPadData` は `payloadTypes.ts` だけで導出し、`schemas/rocketLaunchPad/index.ts` には schema だけを置く | agent前提（配置検査 #4・#6・注目点8。前例 `Tests/Util/EnergySystem/`、`payloadTypes.ts` L66「公開DTO型は実行時スキーマから導出し、wire shapeの定義元を一つに保つ」）。録画シナリオの置き場（#4 のもう1件）は末尾の「統合タスクへ引き継ぐマスタの中身」節で `scenarios/rocket/rocket-launch-pad.cs` にした |
| D34 | 型・重複の検査の強1（`rocketLaunchPad` を `.optional()`＋superRefine で持つ）・強3（DTO の段階と待ち理由を文字列で載せる）・強4（`t()` の戻り値をそのまま表示する）は前例どおり変えない | シミュレーター予測（確信高・前提宣言で提示、拒否権の行使なし 2026-10-23）。前例: `BlockInventoryOpenSchema` の machine・generator・miner の `.optional()`、既存 DTO の `CurrentState`・`Mode`・`StopReason` の文字列、既存セクションの `t()`（キーは型付きの `L.*` で、欠けると型エラー）。事実の注記（再検査）: 型エラーにするため、plan の `rocketStatusKey` の戻り値と `group` の `labelKey` を `string` から `TranslationKey` に直した（前例 `details/detailLogic.ts:37` の `stopReasonTranslationKey`） |
| D35 | ADR 0088 の出所の「GPU衛星のレベル別の計算データ量」を「S1 から S2 へ委任」に直し、plan D12 と揃える（強5） | シミュレーター予測（確信高・前提宣言で提示、拒否権の行使なし 2026-10-23）。根拠: S1 からの連絡 2026-10-23「GPU の Lv が計算データにどう効くかは S2 で決めてください」 |
| D36 | moorestech_master の中身（GPU・GPU衛星・計算データのアイテムと levelFamily・積荷表の行・発射台の blocks 行の値・ビルドメニュー「宇宙」・レシピ・研究・バランス）は、S1〜S3 の実装がそろってから統合タスク（bd `moorestech-xyuaf.2`）で1回にまとめて入れる。この PR の master の変更は、スキーマ変更と土台で読込が止まらない最小限（空配列と D14）だけ。旧 Task 9〜11 の中身は末尾の「統合タスクへ引き継ぐマスタの中身」節に下書きとして移した | ユーザー裁定 2026-10-23（原文「マスタはタスク全体の実装が終わった時に全体でまとめて入れるのが良いんじゃないの？どうせ他にもデッドロック的なタスクがあるんじゃないの？」→ 提案「S2 の PR はコード・スキーマ・テスト用データと、読み込みに必要な最小限の master 変更だけにし、master の中身と録画プレイテストは S1〜S3 がそろった後の統合タスクでまとめて行う」への回答「1 ok」。[[2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる]]） |
| D37 | unityプレイ録画テストは統合タスクへ移す。この PR はサーバーの CombinedTest（テスト用マスタで建造→打ち上げ→成果→セーブ）・プロトコルテスト・web UI の vitest と wire 契約テストで確かめる。録画シナリオの下書きは末尾の節（`scenarios/rocket/rocket-launch-pad.cs`） | ユーザー裁定 2026-10-23（D36 と同じ提案の「master の中身と録画プレイテストは S1〜S3 がそろった後の統合タスクでまとめて行う」→「1 ok」） |
| D38 | 計算データの研究ノードは解放アクション0件（`clearedActions: []`）で置ける | agent前提（実測 2026-10-23: `VanillaSchema/research.yml` の `clearedActions` は `ref: gameAction`（配列）で件数の下限が無く、`ResearchMasterUtil.ValidateGameActions` は空配列で何も弾かない。読み込まれる master の research.json に前例2件「フィルター分岐器」「機械オーバークロック」） |
| D39 | 再検査の弱い指摘と注目点を次のとおり扱う。(a) 成果が出力口に入りきるかの式は `RocketLaunchPadLaunchSequence.CanStoreArrivalOutputs` の1本にし、建造前と打ち上げ直前が呼ぶ。(b) `RocketLaunchPadSaveConverter.IsLaunchableCargo` は積荷表を自分で引かず、並びの `TryGetPayloadRequirement` と個数の一致だけで決める（積荷表の受け付けは `Arrive` が持つ）。(c) 積み込みの判定は `HasMaterials`・`IsPayloadReady` の1本ずつにし、巡回とガードが呼ぶ。(d) 入れ口の仮挿入は `SimulateInsert` の1本にし、`InsertionCheck` と実挿入が通る（前例 `VanillaMachineInputSimulationUtil.SimulateInsert`）。(e) 「積荷を持つ段階」「進行中の段階」は `LoadedRocketStage`・`IProgressingStage` の型の階層から引く。(f) 積荷表を並び（入れ口と表示の順）とサービス（成果の引き当て）が別々に索引する二重は残す。Task 7 の cast は前例どおり残す（到達しない根拠は実測: 読み込まれる mod で `Rocket.prefab` を使うのは発射台だけ。注目点6）。出力口の空き待ちの2経路に `silent-ok` を書く。D34 の事実の誤りを `TranslationKey` の型付けで直す。注目点1は D9、注目点2は D26、注目点7は Task 8 の登録行と using に反映する | 前提宣言（シミュレーター予測・確信高: 同型の弱い指摘の束を全採択した裁定が2回ある。拒否権つきで提示）。前例の裁定: [[2026-10-23-発射台の型検査の弱い指摘は4件とも直す]]。(f) を残す理由は agent前提: 並びは発射台の入れ口と UI の順のための索引、サービスは成果の引き当てのための索引で目的が違い、S3 の拠点はサービスしか使わないので、並びをサービスへ寄せると発射台の UI の都合がサービスに入る |

配置検査の注目点のうち、修正しなかったものの扱い（どれも agent前提）:
- 注目点2: `IRocketLaunchHistory` は共有の `Game.Orbit` に置いたままにする。初打ち上げはワールドの打ち上げの記録で、ワールドセーブの復元（`Game.SaveLoad`）が既に `Game.Orbit` を参照するため。エレベーター拠点はこの状態を読まない（ADR 0088）。
- 注目点3: 初打ち上げ済みはトップレベルの `bool? RocketHasLaunchedOnce` に置き、`WorldSaveRequiredFieldGuard` が `HasValue` で欠損を検出して止める。前例 `currentTick`（`ulong?`＋`HasValue`）と同じ形で、素の bool だと欠損が false に化けて黙って通るため nullable にする。bool を1個包むだけの節（JsonObject）は作らない（節の内側の素の bool に同じ欠損の化けが残るため。再検査 N2）。
- 注目点5: 既存マスタの発射台の行は `blockType` が空で `blockParam` も無い（シミュレーターの斥候が moorestech_master `blocks.json` で実測）ので、テスターのワールドに置かれた台は無く、ブロック状態の移行は要らない。
- 注目点6: `Rocket.prefab`（`Vanilla/Block/Rocket`）を使う他のブロックは moorestech_master `server/mods/moorestechAlphaMod_3`（今のゲームが読まない旧 mod）の「ロケット」だけで、読み込まれる `server_v8/mods/moorestechAlphaMod_8` には無い（実測 2026-10-23。`grep -rn "Vanilla/Block/Rocket"`）。Task 7 Step 3 の冒頭で着手時にもう一度確かめる。
- 注目点9（再検査 N1 で改め）: `ItemMasterUtil.cs` は D22 のとおり本PRで S1 の領域ではない検証を `Validator/Item/` へ移し、200行以下にする。
- 注目点10: 発射台のスロット並びは `MasterHolder` の積荷表を直接読み、軌道到着サービスも別に索引する。この二重は D39 (f) の理由で残す。S3 の拠点が並びと同じ読み方を複製するときは、`Game.Orbit` に入口を置くかを S3 の実装時に決める。
- 注目点11: `OrbitServiceRegistration` は D11 の理由（`GameplayServiceRegistration.cs` が202行）で新設する。分け方の軸（役割別とドメイン別）が混ざることは受け入れる。

---

## 統合タスクへ引き継ぐマスタの中身（この plan では実施しない。bd moorestech-xyuaf.2 が使う）

この節は統合タスク（bd `moorestech-xyuaf.2`）向けの下書きで、この plan の手順ではない。この plan を実行する SDD の実行者はこの節を実行しない（そのためチェックボックスを置かない）。入れる時期と範囲はユーザー裁定 D36（[[2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる]]）に従う。数値はどれも agent前提の仮値（D12）で、統合タスクのバランス確認で動かしてよい。ファイルはどれも moorestech_master `server_v8/mods/moorestechAlphaMod_8/master/` の下にある。GUID は Global Constraints の値を使う。

### A. アイテム・levelFamily・積荷表（items.json）

- `stackLevelTableGuid` が空だった7件への Default は、この plan の Task 1 で済ませてある。重ねて直さない。
- GPU Lv1〜Lv5 の行と GPU の levelFamily は、S1 の GUID（Global Constraints「S1 の GPU」）で入れる。名前（「GPU」「GPU Lv2」〜「GPU Lv5」。S1 からの連絡 2026-10-23）・画像・スタックテーブルは S1 の定義に従う。S1 が先に master へ入れていれば、重ねて足さない。levelFamily の形は `{ "familyGuid": "01a167ed-2700-7371-9f95-168966ff60bc", "name": "GPU", "baseItemGuid": "01a167ed-2703-7014-98e4-11bd15f38571", "levelItemGuids": [Lv1〜Lv5 の GUID] }`。
- 計算データ `01a167cf-af88-74d3-9cd6-7d61b71052bb` と GPU衛星Lv1〜Lv5 の行を足す。`stackLevelTableGuid` は Default `9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa` にする。`sortPriority` はロケット燃料の後ろに置く。`imagePath`・`recipeViewType`・`initialUnlocked` はロケット燃料の行と同じ形にする。名前は「計算データ」「GPU衛星Lv1」〜「GPU衛星Lv5」で、録画シナリオもこの名前で引く。
- GPU衛星の levelFamily:
  ```json
  { "familyGuid": "01a167cf-af9a-78e5-9203-1c18ce94cdc6", "name": "GPU衛星", "baseItemGuid": "01a167cf-af8b-7894-8de6-593ee30289c0", "levelItemGuids": ["01a167cf-af8b-7894-8de6-593ee30289c0", "01a167cf-af8e-7e95-b299-09eb712bee12", "01a167cf-af91-7f62-93d1-e499b06f2398", "01a167cf-af94-7e25-9654-825374c3923b", "01a167cf-af97-7f67-bf53-7833b8d17a15"] }
  ```
- `orbitalPayloads` には GPU衛星 LvN ごとに1行、計5行を足す。どの行も `countPerLaunch: 1` で、`arrivalOutputs` は計算データ1種。数は Lv1 が10、Lv2 が20、Lv3 が40、Lv4 が80、Lv5 が160（D12）。Lv1 の行は次の形で、Lv2〜Lv5 は `payloadItemGuid` と `count` だけを変える:
  ```json
  { "payloadItemGuid": "01a167cf-af8b-7894-8de6-593ee30289c0", "countPerLaunch": 1, "arrivalOutputs": [{ "itemGuid": "01a167cf-af88-74d3-9cd6-7d61b71052bb", "count": 10 }] }
  ```
  1便ぶんの成果が空の出力口（`outputSlotCount`）に入りきるかは、マスタ読込時の検証が見る（ADR 0088）。値を変えたときは読込エラーが出ないことを確かめる。
- S3 のグラフェンケーブルの行（1便N個・成果なし。N は S3 が決める）も同じ配列に入る。

### B. 発射台（blocks.json）

- ロケット発射台 `01a162b0-7769-7419-af24-bb35528b93d2` の行に次の値を入れる。`name`・`imagePath`・`initialUnlocked: false`・`placementsPerCost: 1` は今の値のまま残す。電力と建設費は仮値（D12）で、既存の最大要求電力が石油蒸留機の150なので、その上の200に置いた:
```json
"blockType": "RocketLaunchPad",
"category": "生産",
"subCategory": "宇宙",
"sortPriority": 1400,
"blockSize": [10, 21, 10],
"blockPrefabAddressablesPath": "Vanilla/Block/Rocket",
"openable": true,
"requiredItems": [
  { "itemGuid": "019e3a98-cfe5-73eb-bf6a-a564400bdb4f", "count": 200 },
  { "itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 50 },
  { "itemGuid": "019f0ca5-a575-7625-a43e-f43e8471287e", "count": 20 }
],
"blockParam": {
  "requiredPower": 200,
  "buildSeconds": 120,
  "launchSeconds": 15,
  "launchMaterials": [
    { "itemGuid": "01a162a4-9334-7286-a8ba-7daf9c0f407a", "count": 20 },
    { "itemGuid": "01a162a8-e435-73d0-bddb-08533819b80e", "count": 20 },
    { "itemGuid": "019f0ca4-a544-723e-8ff2-feb15f99c84e", "count": 5 }
  ],
  "outputSlotCount": 4,
  "inventoryConnectors": {
    "inputConnects": [
      { "offset": [0, 0, 5], "directions": [[-1, 0, 0]], "connectorGuid": "aacb7e4e-f4b2-4b7e-9f04-6ac871daf753", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" },
      { "offset": [5, 0, 0], "directions": [[0, 0, -1]], "connectorGuid": "ebe860a8-0aff-4c83-93b7-e82a60c1c104", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" }
    ],
    "outputConnects": [
      { "offset": [9, 0, 5], "directions": [[1, 0, 0]], "connectorGuid": "95386744-9e2e-476d-ac75-ab3e3cc0fef3", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" },
      { "offset": [5, 0, 9], "directions": [[0, 0, 1]], "connectorGuid": "a98a689d-08af-4263-973e-6907670d6d64", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98" }
    ]
  },
  "maxWireConnectionCount": 2,
  "connectionRange": 30,
  "connectionHeightRange": 20
}
```
- 配列の並びは、`requiredItems` が鋼鉄板・コンピュータ・制御モータ、`launchMaterials` がロケット筐体素材・ロケット燃料・コンピュータ。
- `launchMaterials` の数は F のバランス確認の結果で下げてよい。
- 入力は西辺と南辺の中央に、出力は東辺と北辺の中央に置いた。書式は化学プラントと EUV露光式半導体製造装置の `inventoryConnectors` に合わせてある。
- `Vanilla/Block/Rocket` を使うほかのブロックは、読み込まれない旧 mod の「ロケット」だけ（注目点6）。入れる直前に `grep -rn "Block/Rocket\"" server_v8/mods` でもう一度確かめる。

### C. ビルドメニュー（buildMenu.json）

- 「生産」（`categoryGuid: d1000000-0000-4000-8000-000000000002`）の下、「半導体」の後ろに `{ "name": "宇宙", "entrySource": "blocks", "subCategoryGuid": "d2000000-0000-4000-8000-000200000005" }` を足す（D19）。発射台と S3 の宇宙エレベーター拠点がここに並ぶ。

### D. レシピ（machineRecipes.json）

- 書式は、既存の電気汎用工作装置と化学プラントのレシピに合わせる。流体を入れるレシピは、化学プラントの既存レシピの書式に合わせる。
- `01a167cf-afa0-7add-8407-eec71456f4f3`: 電気汎用工作装置で、鋼鉄板×4＋プラスチック×2 → ロケット筐体素材×1、10秒。
- `01a167cf-afa3-7c4f-82b7-c0d4f6d513c6`: 化学プラントで、ガソリン100＋固形燃料×2 → ロケット燃料×1、8秒。
- GPU衛星は通常のレシピを5本作る（D13）。各本は GPU LvN（S1 の GUID）＋鋼鉄板×10＋コンピュータ×5 → GPU衛星 LvN、電気汎用工作装置、30秒。GUID は統合タスクで5本ぶん採番する。

### E. 研究（research.json）

- 「ロケット技術」`01a167cf-af9d-7f31-817c-3069b4f6e499`:
  - 前提ノード: 仮に「半導体製造」`019e3ad9-46e2-73fd-9555-8c1d0245d26d` を置く。GPU を解放する研究は S1 が決めるので、前提は S1 の結果に合わせて付け替える（[[2026-10-23-ロケットは独立研究にし計算データは半導体アップグレードと軌道エレベーターの両方で使う]] の補足）。
  - `consumeItems`: 鋼鉄板×1000、コンピュータ×300、ICチップLv1（`f6dd7835-0845-4877-87d0-b719d037ae7d`）×100。どれも仮値で、S1 の Tier9 研究コストの裁定に合わせて直す。
  - `clearedActions`:
    - `unlockBlock`: 発射台
    - `unlockMachineRecipe`: D の筐体素材・燃料の2本と、GPU衛星の5本
    - `unlockItemRecipeView`: ロケット筐体素材・ロケット燃料・計算データ・GPU衛星Lv1〜Lv5
  - 置き場: `graphViewSettings.UIPosition` は前提ノード（半導体製造なら (4080, -200)）の右隣にする。`IconItem` はロケット燃料にする。
- 「半導体のアップグレード研究」（[[2026-10-23-計算データの研究ノードはS2が先に作り解放先はS1のモジュールが入ってから足す]]）:
  - `consumeItems` に計算データを入れる。数は F で決める。
  - 前提ノードは「ロケット技術」にする。
  - `clearedActions` は S1 の上位モジュールのレシピを `unlockMachineRecipe` で解放する。統合時に S1 の上位モジュールの行がまだ無ければ `clearedActions: []` で置き（D38）、行が入った時点で足す。
- 「軌道エレベーターの建設」の変更は S3 が中身を決める（D20）。発射台・筐体素材・燃料の解放を外し、計算データを消費に足す。

### F. バランス

- recipe-chain-machine-count スキルで量を決める。
- 目標（agent前提）は次のとおり:
  - 計算データのループ（打ち上げ→研究）に、Tier9 の15〜25時間のうち4〜6時間をかける。
  - 発射台1台の1便は、建造120秒＋打ち上げ15秒。
  - 1便の資材は、ロケット筐体素材20・ロケット燃料20・コンピュータ5。
- 同スキルで「1便/135秒を1台で回すのに要る電気汎用工作装置・化学プラントの台数」を出す。上流（鋼鉄板・プラスチック・ガソリン・固形燃料）が既存の工場規模から外れないことも確かめる（作者メモ「それ以外の要素はそこまで変な工場建設は要求されないように」）。外れる場合は資材の数を下げる。
- 結果の表は統合タスクの PR 本文に貼る。

### G. push・ピン・読込の確認

- master worktree で `server_v8/mods/moorestechAlphaMod_8/master` をコミットし、push してから `rev-parse HEAD` を取る。
- 本repoの `.moorestech-external-revisions.json` をそのハッシュへ向ける。
- Editor でマスタを読み直し（RepositorySync がピンへ checkout する）、`uloop get-logs --project-path ./moorestech_client --log-type Error` を見る。`[ItemMaster]`・`[BlockMaster]`・`MooresmasterLoaderException` が出ていないことを確かめる。

### H. unityプレイ録画テスト（D37）

- シナリオは `.agents/skills/unity-playmode-recorded-playtest/scenarios/rocket/rocket-launch-pad.cs` に置く。`misc/` は直下が10ファイルを超えるため使わない（D33）。
- 書式は `cleanroom-v2.cs`・`ui-place-then-open-machine.cs` に合わせる。
- 段階の enum は `Game.Block.Interface.State`、外殻の `RocketLaunchPadProcessorComponent` は `Game.Block.Blocks.RocketLaunchPad` にある。
- 発射の合図（`RequestLaunch`）は、段階を打ち上げ待ち（`ReadyToLaunch`）へ移すだけ。打ち上げるのは次の tick の進行である（D25）。
```csharp
using System;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.EnergySystem;
using Game.Fluid;
using Game.Orbit;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("rocket-launch-pad", options, async p =>
{
    await p.SetupFlatGround();

    // 発射台と発電機を置き、電線で給電する
    // Place a launch pad and a generator, then wire power
    var padPos = new Vector3Int(0, 32, 0);
    var pad = p.PlaceBlockDirect("ロケット発射台", padPos, BlockDirection.North);
    var generator = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
    var gasolineId = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
    generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(4000, gasolineId), FluidContainer.Empty);
    var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var genConn = generator.GetComponent<IElectricWireConnector>();
    var padConn = pad.GetComponent<IElectricWireConnector>();
    p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, padConn, freeCost), "wire gen->pad");
    p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(genConn, padConn);

    // 1便ぶんの資材とGPU衛星Lv1を入れる
    // Insert one launch worth of materials and a GPU satellite Lv1
    Core.Inventory.IOpenableInventory inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット筐体素材"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット燃料"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("コンピュータ"), 5);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("GPU衛星Lv1"), 1);
    var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.AwaitingBuildCommand, 5f, "awaiting build command");
    await p.Screenshot("01-awaiting-build");

    // 発射台を開いて建造ボタンを押す
    // Open the pad and press the build button
    p.WarpPlayer(new Vector3(-6f, 33f, -6f));
    await p.WaitSeconds(1f);
    await p.AimAt(pad.BlockPositionInfo.OriginalPos + new Vector3(5f, 1f, 5f));
    await p.PressInteract();
    await p.UntilWebUiElement("rocket-launch-pad-build", 10f);
    await p.Screenshot("02-panel-build-button");
    await p.ClickWebUi("rocket-launch-pad-build");
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Building, 5f, "building");
    await p.Screenshot("03-building");

    // 建造完了で本体が出て、発射ボタンで打ち上げる
    // On build completion the body appears; launch with the launch button
    await p.UntilWebUiElement("rocket-launch-pad-launch", 180f);
    await p.Screenshot("04-awaiting-launch");
    await p.ClickWebUi("rocket-launch-pad-launch");

    // 発射の合図は打ち上げ待ちへ移すだけで、次のtickの進行が打ち上げる
    // The launch command only moves to ready-to-launch; the next tick's advance performs the launch
    await p.Until(() => processor.Phase is RocketLaunchPadPhase.ReadyToLaunch or RocketLaunchPadPhase.Launching, 5f, "ready to launch");
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Launching, 5f, "launching");
    await p.WaitSeconds(5f);
    await p.Screenshot("05-rising");

    // 計算データが出力口に入り、初打ち上げ済みになる
    // Compute data lands in the output slots and the world is marked as launched
    var computeDataId = PlaytestItemOps.ResolveItemId("計算データ");
    await p.Until(() => CountInOutputs() == 10, 5f, "compute data delivered");
    p.Assert(p.ServerService<IRocketLaunchHistory>().HasLaunchedOnce, "first launch recorded");
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Idle, 20f, "launch finished");
    await p.Screenshot("06-output");

    // 2便目は合図なしで建造が始まり、建造後は打ち上げ待ちを通って自動で打ち上がる
    // The second launch builds without commands and launches automatically via ready-to-launch
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット筐体素材"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("ロケット燃料"), 20);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("コンピュータ"), 5);
    inventory.InsertItem(PlaytestItemOps.ResolveItemId("GPU衛星Lv1"), 1);
    await p.Until(() => processor.Phase == RocketLaunchPadPhase.Building, 5f, "auto building");
    await p.Screenshot("07-auto-building");

    int CountInOutputs()
    {
        var total = 0;
        foreach (var stack in inventory.InventoryItems)
            if (stack.Id == computeDataId) total += stack.Count;
        return total;
    }
});
```
- 名前を合わせる: ブロック名とアイテム名は、マスタの `name` と一致させる。
- ワープ位置: 照準と `PressInteract` の距離条件は `ui-place-then-open-machine.cs` で確かめ、10×10 の台にインタラクトが届く位置へワープする。
- 建造120秒の待ち: 長すぎても、台の `buildSeconds` は変えない。DSL に時間を進める手段が無いことを確かめたうえで、そのまま待つ。
- 実行: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/rocket/rocket-launch-pad.cs "$(cd ../../moorestech_master/<統合タスクの master worktree> && pwd)"`
- 期待する結果: result.json が success で、全 Assert が通る。録画1本とスクショ7枚が出る。スクショは次の3点を画像で確かめる:
  - `01`〜`03` では本体が見えない。
  - `04` では本体が見える。
  - `05-rising` ではロケット本体が台より上にある。
- 共有: `share-put <録画のディレクトリ> -n rocket-launch-pad-playtest` で出た URL を、統合タスクの PR 本文に貼る。
