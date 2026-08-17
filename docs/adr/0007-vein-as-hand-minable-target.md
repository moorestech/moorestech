# 鉱脈そのものを手掘りの第一級対象にする

鉱脈（vein）はこれまで掘削機・ポンプ専用の供給源で、手掘りは mapObject 専用だった。序盤の鉱石入手はテンプレートマップに手置きされた巨大HP（10000）の「〜鉱脈」mapObject が代役を務めており、無限資源の表現としても、鉱脈との整合（鉄鉱脈が石を落とすコピペバグが実在）としても破綻していた。**鉱脈自身に手掘り設定（採掘可否・ツール・ドロップ数）を持たせ、手掘り対象を mapObject と鉱脈の2種に広げる**。クライアントの採掘システムは採掘対象を interface に抽象化して両対応する。

- 位置づけは「掘削機を作る前に最初の鉱石を少量入手する序盤経路」。掘削機が上位互換であり続けるよう、序盤資源（石・小石・原木・粘土・銅・青銅・鉄・石炭）のみ手掘り可、タングステンは不可。
  出所: ユーザー裁定 2026-08-04「序盤の入手経路」「序盤資源のみ」
- 鉱脈は無限資源であり HP・ダメージの概念を持たない。ツール照合に通った1振りごとに minCount〜maxCount 個をドロップし、速度は attackSpeed（クールダウン）だけで制御する。ドロップ物は veinParam.itemGuid が唯一の正。
  出所: ユーザー裁定 2026-08-04「1振り1ドロップ」
- 狙い先は露頭ビジュアルのみ。露頭はクライアントが vein AABB 1件につき1個、AABB中心XZの地表に実行時生成する純ビジュアル（サーバー非管理）で、手掘り可能な鉱脈ではコライダを持つ唯一のターゲットになる。サーバーは送られた座標を GetOverVeins で権威判定する（ADR-0004 のサーバ権威は維持）。
  出所: ユーザー裁定 2026-08-04「露頭ビジュアルのみ」「AABBごとに1個」
- プロトコルは手採採掘1ドメイン1本。既存 `va:mapObjectInfoAcquisition` を拡張・改名し、TargetType enum（mapObject=instanceId / vein=座標）で分岐する。ツール照合・1振りクールダウンのサービスは両ターゲットで共有する。
  出所: ユーザー裁定 2026-08-04「採掘1本にMode統合」・プロトコル規約（1ドメイン1本・Mode分岐）

## Considered Options（棄却）

- **露頭を mapObject 化して既存機構を全面流用する案**: 一度採択したが棄却。ドロップ定義・整合バリデーションが vein と mapObject に重複し、設計として真にあるべき姿ではない。
  出所: ユーザー裁定 2026-08-04「mapObjectを露頭として採用する案は棄却する」
- **mapObject への無限採掘（durabilityType）スキーマ拡張**: 無限採掘は鉱脈の専属概念になったため取りやめ。巨大HPの鉱脈 mapObject 4種はマスタ・テンプレートから削除する。
  出所: ユーザー裁定 2026-08-04「取りやめる」
- **ダメージ蓄積式（damage + earnDamageInterval）**: mapObject と対称になるが、HPを持たない無限資源にダメージ概念を持ち込む必然性がなく棄却。
  出所: ユーザー裁定 2026-08-04「1振り1ドロップ」

## 判断記録（ADR）

実装計画（docs/superpowers/plans/2026-08-04-vein-hand-mining.md）のレンズ該当ファイルに対する改修判断の台帳:

- `VanillaSchema/map.yml`: mapVeinsへ outcropAddressablePath / soundEffectType / handMiningType / handMiningParam（handMiningToolsキー名は生成クラス衝突回避）をトップレベルフラット追加 — 出所: ユーザー裁定（AskUserQuestion 2026-08-04「1振り1ドロップ」）+ agent前提（switchネスト前例ゼロの技術制約）+ シミュレーター予測→ユーザー承認待ち（soundEffectTypeのマスタ駆動化）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs`（旧MapObjectAcquisitionProtocol.cs改名）: 採掘1ドメイン1本・TargetType enum分岐へ統合、タグ va:mining — 出所: ユーザー裁定（AskUserQuestion 2026-08-04「採掘1本にMode統合」）
- `moorestech_server/Assets/Scripts/Game.Map/MiningCooldownService.cs`（新設）/ `MapObjectMiningService.cs`: プレイヤー1振りクールダウンを共有サービスへ抽出（機構は既存dictionary移設・変更なし） — 出所: agent前提（ADR-0007「サービス共有」の実装形）
- `moorestech_server/Assets/Scripts/Game.Map/VeinHandMiningService.cs`（新設）: 座標→GetOverVeins権威判定・1振り1ドロップ — 出所: ユーザー裁定（AskUserQuestion 2026-08-04）
- `moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IItemMapVein.cs` / `Game.Map/ItemMapVein.cs` / `ItemMapVeinDatastore.cs`: VeinGuid追加（マスタ逆引き用） — 出所: agent前提（手掘り設定解決に必須）
- `moorestech_server/Assets/Scripts/Core.Master/Validator/MapVeinMasterUtil.cs`: fluid×minable禁止等のhandMining整合検証 — 出所: ユーザー裁定（AskUserQuestion 2026-08-04「全veinがGuid参照+None型」の後継としてfluid非採掘をバリデーションで担保）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/`（FSM群・IMiningTargetObject新設）/ `Map/MapObject/MapObjectGameObject.cs` / `Map/Outcrop/`（新設3ファイル）: 採掘対象のinterface抽象化と露頭実行時生成 — 出所: ユーザー裁定（発言引用 2026-08-04「採掘する対象をinterfaceにして抽象化すればいい」「露頭ビジュアルのみ」「AABBごとに1個」）
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`: MineVein(座標)送信追加・AttackMapObjectの新MessagePack追従 — 出所: ユーザー裁定（プロトコル統合の帰結）
- `../moorestech_master/server_v8`（map.json master/world・challenges.json）: 序盤資源8種minable・タングステン/fluid none・鉱脈mapObject4種削除 — 出所: ユーザー裁定（AskUserQuestion 2026-08-04「序盤資源のみ」「取りやめる」）
- `VanillaSchema/challenges.yml` / `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/VeinPin.cs`（新設）: チャレンジ誘導ピンのvein対応（tutorialType veinPin新設・最寄り露頭を指す） — 出所: シミュレーター予測→ユーザー承認 2026-08-04
- `moorestech_server/Assets/Scripts/Game.Map/MapObjectDatastore.cs`: LoadMapObjectの欠損instanceIdをthrowから警告スキップへ変更（マップから消えたmapObjectのセーブ状態は無効データとして捨てる） — 出所: シミュレーター予測→ユーザー承認 2026-08-04
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`: dummyText変更のみ（スキーマ変更時のSourceGeneratorトリガという定型手順。edit-schemaスキル準拠） — 出所: agent前提（機械的定型・判断なし）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`: 旧タグva:mapObjectInfoAcquisitionの登録行をva:mining（MiningProtocol）へ差し替え。旧タグは残さない（後方互換考慮不要方針） — 出所: ユーザー裁定（AskUserQuestion 2026-08-04「採掘1本にMode統合」の帰結）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs`（新設）: 露頭の実行時生成はMapObjectGameObjectDatastoreの前例（Mono+シーン配置+[Inject] Construct+Addressablesキャッシュ+フレーム分散）に完全一致させる。IInitialEventApplyWaitTargetは実装しない（露頭はイベント購読を持たない） — 出所: agent前提（前例一致原則）

## Consequences

- mapVeins スキーマに handMiningType enum（none/minable）+ handMiningParam switch（minable: miningTools[toolItemGuid, attackSpeed] + minCount/maxCount）をトップレベルフラットで追加する。veinParam（item/fluid switch）内へのネストは SourceGenerator に switch 入れ子の前例が無いため避ける。fluid×minable は C# バリデーションで禁止する。
  出所: agent前提（既存スキーマ全走査で switch ネスト前例ゼロという技術制約）
- 休眠データだった outcropAddressablePath をスキーマに正式追加し、露頭の見た目解決に使う（map-autogen 計画 P2 の踏襲）。fluid 鉱脈の露頭は叩けない純ビジュアルのまま生成し、発見手段として機能させる。
  出所: agent前提（map-autogen-world-design §5-4 の先行計画に一致）
- テンプレートの鉱脈 mapObject インスタンス（石鉱脈77・鉄鉱脈11・石炭鉱脈7・原木鉱脈5）は削除する。旧セーブは mapObject の instanceId 参照が解決できず互換が壊れるが、後方互換は考慮しない方針に従う。
  出所: agent前提（AGENTS.md「後方互換性は考慮不要」）
