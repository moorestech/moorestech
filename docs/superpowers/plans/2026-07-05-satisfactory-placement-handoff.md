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

## ベルト長尺バリアント移行完了に伴う追記（2026-07-06, Task 9）

全10タスク完了・E2E検証実施済み（実施内容は`.superpowers/sdd/task-9-report.md`参照）。ブランチ`feature/belt-conveyor-place-system`。

### プラン4への申し送り（Step 3記載事項）
- **ベルト4種は長尺バリアント方式に移行済み**。プラン4で「除外9ブロック+車両3種」へrequiredItems投入する際、旧レシピ産出数>1のブロックがないか要チェック（今回投入時に該当したのはベルト4種のみだった。長尺バリアント12種は`requiredItems`が1連と同一＝1セットで統一済みのため、この観点では追加確認不要）
- `PlaceBlockProtocol`の未解放判定は`BeltConveyorPlaceFamilyUtil.TryGetFamily`経由でファミリー代表（length==1の直線ブロック）のunlock状態を参照する。**プラン4の特殊プロトコル移行時、同種の「バリアント→代表」解決パターンが必要になった場合はこのユーティリティを参考にできる**
- `PlaceBlockFromHotBarProtocol`は垂直オーバーライド呼び出しが除去済み。ベルト旧アイテム＋今回追加の隠しアイテム12個は、itemGuidフィールド削除時（プラン5）に一括削除対象

### プラン5への申し送り
- 垂直オーバーライド機構は完全削除済み（スキーマ・コード・データ）。プラン5のitemGuid/usePlaceItems削除時に参照する旧経路は残っていない
- ベルト長尺バリアント12種の隠しアイテム（itemGuid）もプラン5の一括削除対象に含まれる（上記プラン4申し送りと同一事項）

### E2E検証で判明した事実
- **列車車両unlockテストの既存失敗**: `BlockUnlockStateTest.列車車両の解放が保存とロードで維持される`はマージ地点38a78d2c8で既に失敗する**親ブランチ由来の既存問題**（`f226731c3`がテストマスタ先頭車両に`initialUnlocked:True`を設定したが、テストコード側は初期ロック前提のまま）。ベルト作業とは無関係、対応不要
- **基本土台をBeltConveyor対象外にした経緯**: Task 8で`placeSystem.json`のBeltConveyorエントリ（priority=180）に基本土台ブロック（blockSize[5,1,5]）が誤って`straightBlocks[0]`として登録されていることが判明。`CommonBlockPlacePointCalculator`のバリデータが大型ブロック（isLargeBlock）のコンベア設置を許容しないため、この状態ではPlay Mode起動自体が例外で失敗していた。設計ミスと判断しエントリを削除（moorestech_master `584a14e`）。旧仕様でも大型ブロックはコンベア設置無効だったため退行なし。基本土台は通常の`CommonBlockPlaceSystem`（blockSize刻み設置）のみで設置可能
- **`PlaceBlockProtocol`の未解放判定は「ファミリー代表」基準**: `BeltConveyorPlaceFamilyUtil.TryGetFamily`で解決される代表（length==1の直線ブロック）のunlock状態のみが参照され、上り・下り・2連〜5連の個別blockGuidを直接unlockしても設置可否には反映されない。E2E検証時にこれを踏み、代表guidの再確認が必要だった（実装は意図通り、テスト手順上の注意点）
- **moorestech-worktrees/moorestech_masterはRepositorySync管理の実クローンになった**（旧仕様のsymlinkから移行済み、旧symlinkは`_bk`へ退避）。ピンと一致していれば自己管理されるため新常態として受容してよいが、ローカル限定コミット（下記）はcanonicalリポジトリへの手動fetch+resetでのみ同期される
- **moorestech_master側の未pushコミット群**: `f67eee8`（chore: placeSystem既存エントリにplaceParam追加）、`8919c5c`（feat: ベルト長尺バリアント12種とBeltConveyor placeSystemエントリを追加）、`584a14e`（fix: 基本土台のBeltConveyorエントリを削除）の3件は`origin/master`に存在しない（`git branch -r --contains`で確認済み）。moorestech_masterリポジトリの正式push作業が別途必要
- ClientContext.VanillaApi.Response.BlockRemove等の実プロトコル経由の削除は、client側の可視化オブジェクトも正しく同期して破棄される（`ServerContext.WorldBlockDatastore.RemoveBlock`をテストコードから直接呼ぶ場合はネットワークブロードキャストを経由しないため、client側の可視化は同期されない点に注意。データ層の検証のみなら直接呼び出しで十分だが、見た目の同期まで検証したい場合は実プロトコル経由が必須）
