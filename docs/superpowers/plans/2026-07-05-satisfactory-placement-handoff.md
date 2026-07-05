# Satisfactory式設置システム 申し送り（プラン1完了時点）

日付: 2026-07-05
状態: プラン1（サーバー基盤）完了。全10タスク＋Fable最終レビュー＋/all-code-review（26系統）通過済み。
ブランチ: feature/replace-place-system（`5cb50b755..d11bf476c`、19コミット）
スペック: `docs/superpowers/specs/2026-07-03-satisfactory-style-placement-design.md`
プラン1: `docs/superpowers/plans/2026-07-03-satisfactory-placement-plan1-server-foundation.md`

## ロードマップと順序制約

1. ~~プラン1: サーバー基盤~~（完了）
2. プラン2: 本番マスタ追加移行 — **順序制約: プラン3・4完了後に実施**（下記「増殖経路」参照）
3. プラン3: クライアント通常ブロック（ビルドメニューUI・PlacementSelection・新プロトコル切替）← 次
4. プラン4: 特殊システム縦切り（TrainCar設置×2・橋脚付きレール接続・電柱延長・チェーンポール延長・接続ツールのメニュー統合。サーバー＋クライアント同時）
5. プラン5: 破壊的クリーンアップ（旧PlaceBlockFromHotBarProtocol削除・ブロック/車両アイテムとレシピ削除・itemGuid/usePlaceItems削除・返却フォールバック削除）

**増殖経路（プラン2を後ろに置く理由）**: 破壊返却はrequiredItems全額に変更済みだが、設置の旧経路（ホットバー・電柱延長等）はアイテム1個消費のまま。requiredItemsが定義されたブロックを旧経路で設置→破壊すると素材が無から湧く。本番マスタへのrequiredItems投入はプロトコル移行完了後が必須。

## プラン1で確立した基盤（後続プランが使うAPI）

- **新設置プロトコル**: `PlaceBlockProtocol`（`va:placeBlock`）。`SendPlaceBlockProtocolMessagePack(int playerId, BlockId blockId, List<PlaceInfo> placeInfos)`。未解放は全セル拒否、セルごとに足りる分だけ設置、失敗セルは非消費
- **建設コスト**: `ConstructionCostService.HasRequiredItems(ConstructionRequiredItemElement[], IReadOnlyList<IItemStack>)` / `ConsumeRequiredItems(..., IOpenableInventory)` / `CreateRefundItems(...)`。**契約: Consume前に必ずHasで検証**（Consumeは不足時に黙って取れる分だけ取る）
- **電線予約**: `ElectricWireAutoConnectService.EvaluateAutoConnect(..., IReadOnlyList<(ItemId itemId, int count)> reservedItems, ...)`。建設コストで消費予定の素材を電線所持数判定から除外する。**プラン4の特殊プロトコル移行時も必ずrequiredItems由来の予約リストを渡すこと**（渡し忘れるとポール=電線同一アイテムのケースで二重計上漏れが再発）。Codex提案: requiredItems→reservedItems変換helperをConstructionCostServiceへ寄せて各プロトコルで共用する
- **アンロック**: `IGameUnlockStateDataController.BlockUnlockStateInfos/TrainCarUnlockStateInfos`（Guidキー）、`UnlockBlock/UnlockTrainCar`、`OnUnlockBlock/OnUnlockTrainCar`。gameAction `unlockBlock`/`unlockTrainCar`実行可。同期: `GetGameUnlockStateProtocol`のKey(10)-(13)、イベント`UnlockEventType.Block/TrainCar`（Key5-6）
- **共有DTO**: `PlaceInfoMessagePack`/`BlockCreateParamMessagePack`/`PlaceInfo`は`PlacePacketDto.cs`（namespace `Server.Protocol.PacketResponse`直下）

## プラン3（クライアント）の必須対応リスト

- **`ClientGameUnlockStateDatastore.OnUpdateUnlock`**: 現在`case Block/TrainCar: break;`で握りつぶし中。本対応（辞書充填+通知）に差し替える
- **`VanillaApiWithResponse.cs:220-227付近`**: `UnlockStateResponse`変換がBlock/TrainCarの4リスト（`UnlockedBlockGuids`等）を捨てている。取り込みを追加
- **`PlaceSystemUpdateContext`のHoldingItemId/CurrentSelectHotbarSlotIndex排除** → `PlacementSelection`へ。`HoldingItemId`参照は9箇所（PlaceSystemSelector/CommonBlockPlaceSystem/TrainRail/TrainCar/TrainRailConnect/GearChainPoleConnect/ElectricWireExtendMode/PlaceSystemUtil等）— プラン3では通常ブロック経路のみ差し替え、特殊システムはプラン4
- **`MarkInsufficientItemPreviewsAsNotPlaceable`**: 選択スロット1枠基準→インベントリ横断のrequiredItems充足判定に差し替え
- **PlaceBlockProtocolはack無し**（旧経路と同型）。クライアントはワールドイベントで結果を知る。セル成功数のフィードバックが欲しければ応答追加の設計余地あり
- **sortPriority型不整合**: blocksはinteger、itemsはnumber。ビルドメニューで混在ソートする場合は要注意

## 技術的な罠（プラン1で実際に踏んだもの）

- **スキーマの`default:`はエディタ専用でローダー非反映**。省略可能にするには`optional: true`が必須（生成プロパティはnullable化: `RequiredItems`=null許容 / `SortPriority`=int? / `InitialUnlocked`=bool?）。count等の必須値はJSONに必ず明記
- **テストマスタの休眠キー**: スキーマにフィールドを追加すると、既存JSONにコピペ混入していた同名キーが突然「発動」する。追加時はgrepで既存キーを棚卸しすること（本番マスタにも木の歯車等にinitialUnlocked/sortPriorityが既存。プラン2で意図確認して整理）
- **server配下の新規.cs.metaは2行形式が正規**（ローカルパッケージ参照経由のUnity出力。MonoImporter無しでも手動作成ではない）
- **新規サーバー.csはUnity再起動が必要な場合がある** / テスト一括実行はCLI 180秒タイムアウトに注意（クラス単位分割）
- **テストコードでの`Core.Inventory`参照は`global::`修飾が必要**（`Tests.UnitTest.Core.Inventory`等と衝突）
- **垂直オーバーライド**: 解放判定は基底ブロック、コスト消費・返却は実体（オーバーライド後）ブロック参照。基底とオーバーライド先のrequiredItems一致はロード時バリデーションで強制される（プラン2の投入スクリプトは複製必須）

## 未処理のMinor（最終棚卸し済み・対応先）

- Unlockガード（LogError経路）のテスト / 未解放拒否の複数セルテスト → プラン3の統合テストと合流
- `RemoveBlockProtocol`のGetBlockMaster二重呼び＋itemIdフォールバック → プラン5のフォールバック削除時に整理
- 新規マスタバリデータ4種のmutationテスト（不正マスタを食わせるテスト基盤が無い）→ 任意
- unlockTrainCarGuidsのエディタ表示がGUID生値（車両にnameフィールドが無い）→ 車両name追加は別件

## プラン2完了に伴う追記（2026-07-05）

プラン2（本番マスタ正式移行）完了。moorestech_master ブランチ `plan2-master-migration`（b191737）、移行スクリプトは `tools/plan2_migration/migrate.py`（非冪等・適用済み。追補時の参考実装）。

### プラン4への申し送り
- **requiredItems未投入の除外9ブロック+車両3種**: blockType TrainRail(レール橋脚)/TrainStation/TrainItemPlatform/TrainFluidPlatform/ElectricPole×3/GearChainPole×2 と train.json trainCars。5プロトコル改修と同時に requiredItems を投入し、対応する素材レシピ（存続10件のうち9件）を削除する追補スクリプトを書くこと（migrate.py は適用済みデータ上で再実行不可）
- 除外ブロックは現状「research解放後は無償設置＋アイテムレシピも並行存在」の暫定状態
- unlockTrainCar は投入済み（鉄道の時代×2・ディーゼル機関車研究×1）。プラン4のクライアント車両メニューはこの解放状態を参照できる

### プラン5への申し送り
- ブロックアイテム削除時: (a) 木のシャフト素材レシピ98b86740の扱い（機械レシピ「原始的な加工機→鉄のロッド」が木のシャフトを材料参照。アイテム削除なら機械レシピ側の材料置換が必要）、(b) ビルドメニューのアイコンは itemGuid→ItemImageContainer 経由のまま。item imagePath は全件空文字のため、blocks側 imagePath への画像パス整備 or 別のアイコン解決が必要、(c) category も未投入（クライアント未使用のため）
- 木のシャフトのみ「craftコスト==建設コスト」の往復中立でアイテム経路が残存（増殖なし・ただしバランス調整でコスト乖離させると増殖経路化するので注意）

### 既知の残事象
- 既存セーブ: research完了済みでも clearedActions は再実行されないため、旧セーブではブロック未解放の可能性。新規セーブでの確認を正とする（逆に暫定解放期間中のセーブは解放済みのまま残る可能性もあり、どちらも実害は開発段階では許容）
- moorestech_master の local master(cd5fc11) が e5e144b から先行分岐しており、plan2-master-migration（e5e144b→8beb0f2→...→b191737）とのマージ/リベース整理が未実施
- MapObject guid e76e6b65 がマップに存在するがマスタ未定義のInfoログ多数（プラン2以前からの既存事象・変更範囲外）
- 燃料式風車の unlockBlock が4研究ノードに重複出現（旧giveItem×3の置換由来。解放は冪等なので実害なし、データ整理は任意）
