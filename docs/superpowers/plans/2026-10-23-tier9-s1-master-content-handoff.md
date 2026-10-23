# Tier9 S1 マスタの中身（統合タスク bd moorestech-xyuaf.2 への引き継ぎ）

> この文書は S1 の実装計画（`docs/superpowers/plans/2026-10-23-tier9-cleanroom-semiconductor.md`）から切り出した Part D（マスタとバランス）の本文である。ユーザー裁定（`.decisions/2026-10-23-S1のマスタの中身は統合タスクへ移しクリーンルーム系ブロックの直しはS1に残す.md` と、S2 の `2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md`）により、S1 の PR では実行しない。S1〜S3 の実装がそろった後の統合タスクが、この本文を材料にマスタの中身・バランス・出荷マスタでの録画テストを入れる。
>
> 前提: S1 の PR（Part A〜C と専用機5種の簡易モデル prefab。旧 D10）がマージ済みであること。タスク番号・「本 plan」の語は切り出し前の S1 plan のもの。D10 は S1 に残したので「済み」として読む。S2 の裁定で GPU衛星は GPU LvN→GPU衛星 LvN の通常レシピ5本（継承レシピではない）。セーブ版は S1〜S3 で「後からマージする側が繰り上げる」。S1 plan の設計検査（2026-10-23）で Part D に当たった弱い指摘は末尾「設計検査の持ち越し」にある。

## Requirements（Part D）


- RD1 上流は3段: 電気粉砕機（砕いた石材1→砂1・2秒）・電気炉（砂2＋石炭1→金属シリコン1・8秒）・化学プラント（金属シリコン1→高純度シリコン1・4秒）で、電気炉の段だけ約2倍。受け入れ: D8 Step 1 の `verify_master.py` が `PASS upstream ratio`（粉砕2:炉4:化学2）、sim §0 の「上流は電気炉だけ約2倍」が PASS
- RD2 IPA は化学プラントで ナフサ5＋水15→IPA20（2秒）。化学プラントは液体入力2・出力1・アイテム入口1。超純水はマスタから消える。受け入れ: `grep -r 019eeaa7-45b6 $MW/server_v8` が0件、D3 Step 3 が `2 1 2 1 [[3, 0, 0]]`、D9 chip-line の `IPA produced` が PASS
- RD3 ウェハは工程×グレード（G1〜G5）、ダイ・チップは Lv1〜Lv5 の別アイテムで、levelFamilies 6件（トランジスタ設置済み・1層・2層・完成パターンウェハ・シリコンダイ・ICチップ）に束ねる。受け入れ: D4 Step 3 の6行、D9 の起動でマスタロードが成功
- RD4 専用機5種（ウェハ切断機・成膜装置・EUV露光式半導体製造装置・ダイシング装置・パッケージング装置）は CleanRoomMachine・IPA入力1・モジュール3スロットで、工程レシピを各1本（EUV は層ごとに3本）持つ。受け入れ: D9 chip-line で室内ラインから ICチップLv1 と ICチップLv2 が出る
- RD5 抽選の値は cleanRoom.json の `thresholds[]`（品質倍率 A1.0/B0.75/C0.5/D0.25・層失敗 A.10/B.15/C.20/D.25・ダイ損失 A.15/B.22/C.30/D.38）と役割表（`waferGradeDraws` 1行・昇格上限0.6、`layerExposures` 3行・低減上限0.6、`dicings` 1行・300ダイ・低減上限0.6・ダイ表 G1[1] G2[.7,.3] G3[.55,.3,.15] G4[.45,.3,.17,.08] G5[.4,.28,.18,.09,.05]）に入る。受け入れ: D5 Step 3 と D8 Step 1 の `PASS cleanRoom = params`、sim §0 の「3層成功≒50%（クラスC・品質なし 51.2%）」と単調性が PASS
- RD6 部屋は大部屋集約が有利: 汚染係数 aVolume0.02/aSurface0.04/aConnector3.0/aMachine2.0/kHatch4.0、清浄機 q=5・電力150・フィルター容量20000。ドアは接続口に数えない。受け入れ: sim §0 の「大部屋集約が有利」A/B/C が PASS（下書き値でクラスA 大部屋3台 vs 工程別5部屋7台・フィルター14.4 vs 40.0個/時）、D8 Step 1 の `PASS power / filter = params`
- RD7 失敗品は室外の電気粉砕機で 失敗したパターンウェハ1→高純度シリコン5、失敗したシリコンダイ60→高純度シリコン1。受け入れ: `PASS recipes = params`、sim §2 にリサイクルあり・なしの行が並ぶ
- RD8 鋼鉄フレーム（鋼鉄板3＋プラスチック2→1・6秒）を新設し、専用機・清浄機・ハッチ・ドアの建設コストに使う。壁とパネルは 鋼鉄板1＋プラスチック1。仮コスト（砕いた石材5＋レンガ2）を全部置き換える。受け入れ: D5 Step 3 の4行目が `[]`、`PASS structure`
- RD9 モジュール4種×Lv1〜5（品質・速度・省エネ・生産性）と GPU Lv1〜5（S2 との取り決めの固定 GUID）。製品は `InheritUpToUnlocked` の継承レシピ1本ずつで、Lv1 は S1 の研究後に作れ、Lv2 以上は S2 の研究まで作れない。受け入れ: D6 Step 3 の `20 [3, 6, 10]` と `PASS modules = params`、D9 modules-gpu の5製品と `IC chip Lv2 is refused` が PASS
- RD10 研究: 半導体製造にブロック11（クリーンルーム系6・専用機5）・機械レシピ16・レシピ表示48の解放を足す。品質モジュール・機械オーバークロック（速度）・省エネモジュール・GPU設計・モジュールスタック拡張I/II・生産性モジュールの7ノードが ICチップLv1 を消費する（スタック拡張は `unlockModuleSlotStackLevel {level: 1 / 2}`）。受け入れ: D7 Step 4 が `7 <timeline.RESEARCH の chips 合計>`・`11 True 48`・`PASS research = params`
- RD11 S1 時間配分: 半導体製造の解放から最後の S1 研究まで 6〜8 時間（クラスA/B/C とも）、S1 中のチップは Lv1 中心で Lv2 は 0% より多く 15% 以下。室内機械の生産性モジュールと研究での Lv2 代用を含めて測る。受け入れ: D1 Step 2〜4 と D8 Step 1 で `python3 sim/run_all.py` が exit 0（§0 の12行が全部 PASS）
- RD12 シミュレーションは `moorestech_master/design/tier9-semiconductor-balance/` に置き、README・目標判定（targets.py）・出力・マスタ照合を含める。値を直す順と spec への写し先は D1 Step 3 の表に従う。受け入れ: `python3 sim/run_all.py` が exit 0、`python3 verify_master.py` が8項目 PASS
- RD13 新規・改名したアイテム・ブロック・研究のローカライズ行（Source・english・japanese）を mod の localization.csv に持つ。受け入れ: `PASS structure`（Guid 導出キーの行がある）
- RD14 出荷マスタ全体をロードして起動し、チップライン・モジュールと GPU・専用機の見た目と抽選欄を unityプレイ録画テスト3本で確かめる。受け入れ: D9 の3本の result.json が Success・全 Assert PASS・動画が 0 byte でない
- RD16 成膜・EUV・ダイシングの機械UIに、出荷マスタの値で抽選欄（上限グレード・昇格確率／層失敗率／ダイ損失率）が出る。受け入れ: D9 yield-ui の `clean-room-yield-section` と各行の `UntilWebUiElement` が PASS、`02-deposition-yield` `03-euv-yield` `04-dicing-yield` で数値が読める
- RD17 品質モジュールは成膜・EUV・ダイシングにだけ入り、ウェハ切断機とパッケージング装置には入らない。受け入れ: `PASS quality module permission`
- RD18 ICチップ1個にシリコンダイ10個を使う（パッケージング: シリコンダイLv1×10＋ICチップ基板1→ICチップLv1、`Inherit`）。受け入れ: D8 Step 1 の `PASS recipes = params`、sim の `DIES_PER_CHIP == 10`。


## やらないこと（Part D）


- スキーマ（VanillaSchema/*.yml）とサーバー・クライアント・webui のコード変更（Part A/B/C の担当）。本体側で Part D が足すのは、D9 のシナリオ3本・D10 の prefab と材質と Addressables グループ・D8 のピンだけ
- GPU の効き方・GPU衛星・計算データ・計算データ研究、モジュールと GPU の上位レベルの製作上限を上げる研究（S2。Part C の `unlockLevelInheritanceCraftLevel` に乗る）
- 研究ノード「フィルター分岐器」の削除と「金属超精密加工」の前提張り替え（S3）
- アイテム画像（imagePath は空のまま）
- 専用機の作り込んだモデル（D10 は既存 prefab を複製して色・上面の形・ラベルを変える簡易モデルまで）
- 化学プラントの prefab に、足した液体口・アイテム口の見た目を足すこと
- 電気粉砕機への 石→砕いた石材 のレシピ追加
- 上位チップの確定合成（裁定で入れない）

## 事実の訂正（Part D）


- 化学プラント（`019ee880…`、6×16×11）の `inventoryConnectors` はアイテム出口 (3,0,10)+z だけで、アイテム入口が無い。金属シリコン→高純度シリコンのレシピを動かすには入口が要る（D3 で (3,0,0) −z を足す）
- v8 マスタの items.json には `levelFamilies`・`modules` がまだ無い。キーは既存スキーマ items.yml にある（`optional: true`）。D4・D6 が初めて入れる
- 金属シリコンのアイテムは v8 マスタに無い（D3 で足す）。石・砕いた石材・砂は既存
- 砕いた石材を作るレシピは 原始的な粉砕機（石1→砕いた石材1・3秒）だけ。電気粉砕機のレシピは鉄鉱石・銅鉱石の粉だけ
- blocks.json 最上位の `cleanRoomThresholds`・`semiconductorChips` はスキーマに無い死にキー（D5 で消す）
- 既存のクリーンルーム系ブロックは prefab `Vanilla/Block/TmpBlock`（パイプコネクタは `PipeStraightWindow`、清浄機は `Vacuum_pump`）。EUV露光式半導体製造装置だけが `Vanilla/Block/Semiconductor_Manufacturing_Equipment`（3×2×3）で、`inputSlotCount 2`・`moduleSlotCount 0`・`requiredPower 100`
- v8 マスタの cleanRoom.json のクラスA `requiredAirChangeRate` は 0.017（本 plan は 0.0167 に揃える）。清浄機は `requiredPower 100`・`filterCapacity 5000`
- `ResearchDataStore.CompleteResearch` は前提研究と素材が揃わないと拒む（`ResearchDataStore.cs:78`）。前提と素材を飛ばして完了させるには `IResearchDataStore.LoadResearchData(ResearchSaveJsonObject)` を使う（完了 GUID を読み込み、各研究の `ClearedActions` を再実行する。`ResearchDataStore.cs:204-223`）
- CleanRoomMachine の処理部 `CleanRoomMachineProcessorComponent` は `IMachineRecipeSelectorComponent` を実装している（レシピを明示で選べる）
- 上流の既存機械の電力は 電気粉砕機40・電気炉30・化学プラント75・電気汎用工作装置20 で、sim の `POWER` と一致する
- mod の localization.csv の列は `key,Source,english,japanese,german,korean`
- Part A のドアは 1×2×1（prefab `Vanilla/Block/CleanRoomDoor`）、パネル `クリーンルームパネル` はアイテムを持たないブロック（items.json に行が無い）
- Part B の抽選は生産性の追加セットを「同じ抽選結果をもう1組並べる」形で出す。sim はこれに合わせ、追加セットを確率×同じ期待値で数える

## 配置と前例（Part D）


| 決めたこと | 前例（同じ役割） | 従う／逸脱 |
|---|---|---|
| バランスシミュレーションの置き場 | `moorestech_master/design/gear-connector-rules/`（設計検討用の Python と README） | 従う（`design/tier9-semiconductor-balance/`） |
| master-refine の並び順と研究座標 | master-refine 操作 A・B | 従う（`apply_spec.py` は再適用で打ち消さない） |


## タスク一覧（Part D）


- D1 バランスシミュレーション（生産性・研究の Lv2 代用込み）と目標判定を master repo に置き、全目標 PASS まで値を締めて「シミュレーション結果」節を書き換える
- D2 マスタ適用スクリプト・照合スクリプト（8項目）と Guid 台帳
- D3 上流（砂・金属シリコン・高純度シリコン）・IPA・鋼鉄フレーム・素材とリサイクルのレシピ、化学プラントの口、超純水の削除
- D4 ウェハ・ダイ・チップのグレード別アイテムと levelFamilies 6件・スタック表「ダイ」
- D5 専用機5種のブロック・工程レシピ7本・cleanRoom.json のクラス値と役割表・汚染係数・クリーンルーム系の建設コスト
- D6 モジュール4種×Lv1〜5 と GPU Lv1〜5・継承レシピ5本
- D7 研究（半導体製造の解放内容・S1 の7ノード）と sortPriority・研究座標の再計算
- D8 目標判定とマスタ照合を通し、出荷検査・push・本体のピン更新
- D9 unityプレイ録画テスト3本（チップライン・モジュールと GPU・専用機の見た目と抽選欄）


## Part D: マスタとバランス

### シミュレーション結果（下書き時（生産性なし）の値。D1 Step 4 で生産性込みの値に書き換える）

シミュレーションの置き場（前例）: `moorestech_master/design/gear-connector-rules/`（設計検討用の Python シミュレータと README を master repo に置いている前例）に倣い、`moorestech_master/design/tier9-semiconductor-balance/` に置く。D1 で置くコード（params.py・lottery.py・chain.py・room.py・timeline.py・targets.py・run_all.py）を回した出力全文が `sim/output.md`。

この節の表は、室内機械の生産性モジュールと「研究で Lv2 チップを Lv1 の代わりに使う」代用を入れる前の下書きの値。D1 の実装はこの2つを含めて回し直し、目標の判定（`sim/output.md` §0）が全行 PASS になるまで値を直してから、この節の表を書き換える（D1 Step 2〜4）。D1 のコードで下書きの値のまま回すと、§1 の抽選表・単調性は下の表と一致し、§4 は クラスA 5.83h（S1 6〜8時間を FAIL）/ B 6.27h / C 6.87h、Lv2 割合 3.4% / 2.6% / 1.7% になる（本 plan 作成時に実測）。部屋の表はドアを接続口に数えない（Part A の A1）ため、クラスA の大部屋が 汚染80/秒・フィルター14.4個/時、工程別5部屋が 222/秒・40.0個/時 に下がる。

#### 抽選（トランジスタ設置済みウェハ1枚あたり。成膜・EUV・ダイシングに同じ品質構成）

| クラス | 品質モジュール | 昇格u | 層失敗 | 3層成功 | 良品ダイ/完成ウェハ | チップ/投入ウェハ | Lv1 / Lv2 / Lv3 / Lv4 / Lv5 |
|---|---|---|---|---|---|---|---|
| C | なし | 0 | 20.0% | 51.2% | 210 | 10.8 | 100% |
| C | Lv1×3 | 0.06 | 17.6% | 55.9% | 221 | 12.4 | 98.2 / 1.8 |
| C | Lv1×9 | 0.18 | 12.8% | 66.3% | 242 | 16.1 | 94.6 / 5.4 |
| B | なし | 0 | 15.0% | 61.4% | 234 | 14.4 | 100% |
| B | Lv1×3 | 0.09 | 13.2% | 65.4% | 242 | 15.8 | 97.3 / 2.7 |
| B | Lv1×9 | 0.27 | 9.6% | 73.9% | 258 | 19.0 | 91.9 / 8.1 |
| B | Lv2×9（S2後） | 0.41 | 6.9% | 80.7% | 270 | 21.8 | 85.4 / 12.2 / 2.5 |
| A | Lv1×30（スタックII後） | 0.60 | 4.0% | 88.5% | 282 | 24.9 | 82.0 / 18.0 |
| A | Lv4×30（やり込み） | 0.60 | 4.0% | 88.5% | 282 | 24.9 | 73.8 / 17.7 / 6.0 / 1.9 / 0.6 |
| D | なし | 0 | 25.0% | 42.2% | 186 | 7.8 | 100% |

- 単調性: 全6構成で D→C→B→A の良品ダイ合計と Lv2 以上ダイがともに増加（例 Lv1×9: 良品 135→161→190→222、Lv2以上 3.6→8.7→15.4→24.0）
- モンテカルロ（クラスB・Lv1×9・投入1000枚）: 3層成功 746枚（期待739）、良品ダイ/完成ウェハ 平均257.9・標準偏差5.9（二項の期待6.0）、Lv2割合 8.0%（期待8.1%）
- ダイ表: G1[1.0] / G2[0.70,0.30] / G3[0.55,0.30,0.15] / G4[0.45,0.30,0.17,0.08] / G5[0.40,0.28,0.18,0.09,0.05]（Part B の初期案のまま採用）
- 作者メモ「Lv1 80%/Lv2 18%/Lv3 2%」は Lv1×30（A）や Lv2×9（A: 79.4/16.2/4.4）が近い＝S1 終盤〜S2 後の手応え

#### 基準ライン1本（高純度シリコン 0.5/秒＝ウェハ3枚/分）の機械台数

| 段 | 機械 | 台数（クラスB・品質なし） | 稼働率 |
|---|---|---|---|
| 砂 | 電気粉砕機 | 2 | 1.00 |
| 金属シリコン | 電気炉 | **4（2倍の段）** | 1.00 |
| 高純度シリコン | 化学プラント | 2 | 1.00 |
| IPA | 化学プラント | 1 | 0.30（2.98 IPA/秒） |
| ICチップ基板 | 電気汎用工作装置 | 3 | 0.72 |
| リサイクル | 電気粉砕機 | 1 | 0.21 |
| ウェハ切断 | ウェハ切断機 | 1 | 0.40 |
| 成膜 | 成膜装置 | 1 | 0.50 |
| 積層（3層） | EUV露光式半導体製造装置 | 2 | 0.97 |
| ダイシング | ダイシング装置 | 1 | 0.55 |
| パッケージング | パッケージング装置 | 3 | 0.72 |

- 室内8台・室外12台＋既存Tier8（ナフサ・銅板・プラスチック・石炭）。電力は室内1030＋清浄機3台450＋上流465＝約1950/本（ガソリン発電機2台）
- 室内のダイ流量 7.2/秒（パッケージング1回でダイ10個を使うため、ダイをハッチ越しに運ぶと汚染が跳ねる）

#### 1本あたりのチップ/時（Lv1 / Lv2）

| クラス | 品質なし | 品質Lv1×3 | 品質Lv1×9 | 品質Lv1×3・リサイクルあり |
|---|---|---|---|---|
| A | 3346 / 0 | 3427 / 128 | 3571 / 432 | 4133 / 154 |
| B | 2587 / 0 | 2771 / 77 | 3150 / 278 | 3628 / 101 |
| C | 1935 / 0 | 2184 / 40 | 2737 / 156 | 3093 / 57 |

#### 部屋の作り方（基準ライン1本・クラスB運転）

| 目標クラス | 大部屋1つ（高純度シリコン・基板・IPAだけ搬入） | 工程別5部屋 |
|---|---|---|
| A | 清浄機3台・汚染83/秒・フィルター14.9個/時（内寸18×11×4, V≈606） | 7台・汚染237/秒・フィルター42.7個/時 |
| B | 2台・14.9個/時 | 5台・42.8個/時 |
| C | 1台・14.9個/時 | 5台・42.8個/時 |

- 仕様 v2 §8 の不変条件も保つ: 5×5×3 の小部屋（V75, S110, 接続点2, 機械1）は清浄機1台で A（C_eq=13.9/5=2.8, ACH 0.067）、10×10×5（V500, S400）は1台では ACH 0.010 < 0.0209 で A に届かず増設が要る

#### S1 の研究コストと所要時間（初回ライン完成3.0h・2本目4.0h・3本目5.5hの建設仮定、リサイクルなし）

| 研究 | 前提 | ICチップLv1 | 鋼鉄フレーム | コンピュータ | 制御モータ | 完了（A / B / C） |
|---|---|---|---|---|---|---|
| 品質モジュール | 半導体製造 | 1000 | 150 | 60 | 0 | 3.28 / 3.38 / 3.52 h |
| 機械オーバークロック（速度） | 品質モジュール | 1500 | 150 | 0 | 120 | 3.75 / 3.95 / 4.12 h |
| 省エネモジュール | 品質モジュール | 1500 | 150 | 60 | 0 | 4.12 / 4.28 / 4.52 h |
| GPU設計 | 品質モジュール | 2000 | 200 | 120 | 0 | 4.43 / 4.67 / 5.00 h |
| モジュールスタック拡張I（3→6） | 機械オーバークロック・省エネモジュール | 3000 | 200 | 0 | 150 | 4.88 / 5.22 / 5.62 h |
| 生産性モジュール | モジュールスタック拡張I | 4000 | 300 | 150 | 0 | 5.50 / 5.82 / 6.28 h |
| モジュールスタック拡張II（6→10） | 生産性モジュール | 7000 | 400 | 200 | 200 | **6.22 / 6.70 / 7.40 h** |
| 合計 | | 20000 | 1550 | 590 | 470 | |

- 研究のほかに Lv1 モジュール製作で ICチップLv1 1365 個（品質18・速度45・省エネ30・スタック後60・生産性30・スタック後60 個）
- Lv2 以上のモジュール・GPU は S2 の計算データ研究の後に作れる前提（S2 との取り決め（判断記録））。S1 中に出た Lv2 チップは在庫に積まれる（S1 終了時 A755 / B557 / C369 個。研究での Lv2 代用を入れる前の値で、D1 の再実行では研究に回る）
- S1 終了時の生産力（3本・クラスB・品質Lv1×3）: ICチップLv1 約8300/時・Lv2 約230/時。S2/S3 はこの値を前提にチップ要求量を決められる
- 鋼鉄需要（S1 中）: 研究の鋼鉄フレーム1550＋建設（1本あたり 専用機フレーム370・清浄機45・壁とパネル≈630枚の鋼鉄板）→ 鋼鉄板 約10,000枚＝電気炉で鋼鉄板 約43枚/分（作者メモ「鋼鉄を大量に要求」）

#### モジュール効果（3スロットに同種を積んだ合計）

| 軸 | Lv | 上限3（9個） | 上限6（18個） | 上限10（30個） |
|---|---|---|---|---|
| 速度 | Lv1 | 速度+90%・電力×2.8 | +180%・×4.6 | +300%・×7.0 |
| 省エネ | Lv1 | 電力×0.43 | ×0.27 | ×0.18 |
| 生産性 | Lv1 | 追加27%・時間×1.72 | 54%・×2.44 | 90%・×3.40 |
| 品質 | Lv1 | 品質合計0.36・時間×1.45 | 0.72・×1.90 | 1.20・×2.50 |

（Lv3/Lv5 は output.md §5。効果式は `MachineModuleEffect.Aggregate` と同じ）

---


共通: `$B = $MW/design/tier9-semiconductor-balance`、`$MOD = $MW/server_v8/mods/moorestechAlphaMod_8`。Python はすべて `python3`（3.15・標準ライブラリのみ）。master 側のコミットは `git -C $MW` で行い、push は D8 でまとめて行う。

### Task D1: バランスシミュレーションを master repo に置く

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/README.md`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/params.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/lottery.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/chain.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/room.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/timeline.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/targets.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/sim/run_all.py`
- Create（生成物）: `moorestech_master/design/tier9-semiconductor-balance/sim/output.md`
- Modify: 本 plan ファイルの「シミュレーション結果」節（Step 4 で生産性込みの値へ書き換える）

**Interfaces:**
- Consumes: Part B の抽選の式（成膜の昇格確率 u＝min(上限, 品質合計×クラス倍率)・グレード上限＝最高tier+1、EUV の層失敗率＝クラス値×(1−min(品質合計, 上限))、ダイシングの損失率＝クラス値×(1−min(品質合計, 上限))、生産性の追加セットは同じ抽選結果を並べる）。Part C の研究消費の代用（ICチップLv1 の要求に Lv2 以上も下位から充てる）と製作上限（モジュール・GPU は Lv1 まで）。Part A の A1（ドアは接続口に数えない）
- Produces: `sim/params.py` の定数（後段の verify と spec が値の正本として読む）: `LINE_SI_PER_S=0.5`、`CLASS_PARAMS[class]["qualityMultiplier"|"layerFailRate"|"dieLossRate"]`、`DIES_PER_WAFER=300`、`PROMOTION_CAP=0.6`、`QUALITY_REDUCTION_CAP=0.6`、`DIE_TABLES{grade: [Lv1..]}`、`MODULES[axis][tier-1]=(effectValue, tradeoffValue)`、`STACK_LIMITS=[3,6,10]`、`RECIPES[name]{machine,time,in,ipa?,out}`、`POWER[machine]`、`POLLUTION`、`FILTER_Q=5.0`、`FILTER_CAPACITY=20000`、`THRESHOLDS`。`sim/timeline.py` の `RESEARCH`（name/chips/frames/computers/motors/pre のリスト）。`sim/targets.py` の `evaluate()`（目標ごとの合否）。値は Step 3 の調整で変わりうる（変えたら同じ値を D3〜D7 の spec に書く）

- [ ] **Step 1: README とシミュレーション7ファイルを書く**

`$B/README.md`:

````markdown
# tier9-semiconductor-balance

Tier9 S1（半導体・クリーンルーム）の値を決めるシミュレーションと、その値を v8 mod のマスタへ入れる道具。
値の正本は `sim/params.py`。マスタの値を変えるときは params.py を直してシミュレーションを回し、目標に収まることを確かめてから spec とマスタを直す。

## 目標（裁定）
- 半導体製造の解放から S1 最後の研究まで 6〜8 時間（`sim/output.md` §4）。室内機械の生産性モジュールも含めて数える
- クリアに要るチップは Lv1 中心・Lv2 少数。研究は ICチップLv1 を要求し、Lv2 以上も代わりに消費される（下位から）
- クラスC・品質なしで3層成功 ≒ 50%
- 上流（電気粉砕機→電気炉→化学プラント）は電気炉だけ約2倍の台数
- 部屋は工程別に分けるより大部屋1つが清浄機・フィルターとも安い

## 使い方
```bash
cd design/tier9-semiconductor-balance
python3 sim/run_all.py                    # sim/output.md を書き直す
python3 apply/apply_spec.py --mod-dir ../../server_v8/mods/moorestechAlphaMod_8 --spec-dir spec --ledger guids.json
python3 verify_master.py --mod-dir ../../server_v8/mods/moorestechAlphaMod_8 --ledger guids.json
```

- `apply_spec.py` は何度流しても同じ結果になる。新しい要素の Guid は初回に UUIDv7 を発行して `guids.json`（台帳）へ書き、2回目以降は台帳の値を使う。台帳はコミットする
- 他セッションと共有する Guid（研究・モジュール・GPU）は `guids.json` に最初から入っている。spec の `guid` と台帳が食い違うと例外で止まる
- spec の値は `@item:名前` `@block:名前` `@fluid:名前` `@research:名前` `@recipe:レシピキー` `@new:任意キー` で参照する。解決できない名前は例外で止まる
- `sim/run_all.py` は目標を1つでも外すと `sim/output.md` を書いたうえで終了コード1（判定は output.md §0）
- `verify_master.py` は1件でも食い違いがあれば終了コード1

## ファイル
- `sim/params.py` 値の正本 / `sim/lottery.py` 成膜・EUV・ダイシングの抽選（生産性の追加セットは同じ抽選結果を2倍にする） / `sim/chain.py` 基準ライン1本の台数 / `sim/room.py` 汚染と清浄機 / `sim/timeline.py` S1 研究の時間配分 / `sim/targets.py` 目標の判定 / `sim/run_all.py` 表の出力
- `spec/01〜05_*.json` マスタへ入れる要素の宣言 / `apply/` 適用 / `verify_master.py`・`verify_checks.py` 照合
````

`$B/sim/params.py`:

```python
"""Tier9 S1 バランスシミュレーションの提案値（マスタへ入れる値の正本）。
Proposed master values for the Tier9 S1 balance simulation (source of truth for the JSON values)."""

LEVELS = [1, 2, 3, 4, 5]
# 基準ライン1本の高純度シリコン投入（毎秒）
# High-purity silicon fed to one reference line (per second)
LINE_SI_PER_S = 0.5
CLASSES = ["A", "B", "C", "D"]

# 部屋クラス別の抽選パラメータ（Part B の cleanRoom.yml に入る値）
# Per-class draw parameters (values that go into Part B's cleanRoom.yml)
CLASS_PARAMS = {
    "A": {"qualityMultiplier": 1.00, "layerFailRate": 0.10, "dieLossRate": 0.15},
    "B": {"qualityMultiplier": 0.75, "layerFailRate": 0.15, "dieLossRate": 0.22},
    "C": {"qualityMultiplier": 0.50, "layerFailRate": 0.20, "dieLossRate": 0.30},
    "D": {"qualityMultiplier": 0.25, "layerFailRate": 0.25, "dieLossRate": 0.38},
}
DIES_PER_WAFER = 300
# 昇格確率 u の上限・失敗率/損失率の品質低減の上限
# Cap of the promotion probability u and of the quality reduction for fail/loss rates
PROMOTION_CAP = 0.60
QUALITY_REDUCTION_CAP = 0.60
# グレード別ダイ表（Lv1..Lv5 の割合）
# Die table per wafer grade (share of Lv1..Lv5)
DIE_TABLES = {
    1: [1.0],
    2: [0.70, 0.30],
    3: [0.55, 0.30, 0.15],
    4: [0.45, 0.30, 0.17, 0.08],
    5: [0.40, 0.28, 0.18, 0.09, 0.05],
}

# モジュール（effectValue, tradeoffValue）Lv1..Lv5
# Modules (effectValue, tradeoffValue) for Lv1..Lv5
MODULES = {
    "Quality":      [(0.04, 0.05), (0.06, 0.05), (0.08, 0.05), (0.10, 0.05), (0.12, 0.05)],
    "Speed":        [(0.10, 0.20), (0.15, 0.20), (0.20, 0.20), (0.25, 0.20), (0.30, 0.20)],
    "Efficiency":   [(0.15, 0.00), (0.20, 0.00), (0.25, 0.00), (0.30, 0.00), (0.35, 0.00)],
    "Productivity": [(0.03, 0.08), (0.045, 0.08), (0.06, 0.08), (0.075, 0.08), (0.09, 0.08)],
}
MODULE_SLOTS_CLEANROOM = 3
STACK_LIMITS = [3, 6, 10]

# レシピ（機械, 秒, 入力, 出力）。液体は IPA 量
# Recipes (machine, seconds, inputs, outputs); fluid is the IPA amount
RECIPES = {
    "砂":             {"machine": "電気粉砕機", "time": 2.0, "in": {"砕いた石材": 1}, "out": 1},
    "金属シリコン":    {"machine": "電気炉", "time": 8.0, "in": {"砂": 2, "石炭": 1}, "out": 1},
    "高純度シリコン":  {"machine": "化学プラント", "time": 4.0, "in": {"金属シリコン": 1}, "out": 1},
    "IPA":            {"machine": "化学プラント", "time": 2.0, "in": {"ナフサ": 5, "水": 15}, "out": 20},
    "シリコンウェハ":  {"machine": "ウェハ切断機", "time": 8.0, "in": {"高純度シリコン": 10}, "ipa": 10, "out": 1},
    "トランジスタ設置済みウェハ": {"machine": "成膜装置", "time": 10.0, "in": {"シリコンウェハ": 1}, "ipa": 10, "out": 1},
    "EUV層":          {"machine": "EUV露光装置", "time": 15.0, "ipa": 5, "out": 1},
    "ダイ":           {"machine": "ダイシング装置", "time": 18.0, "ipa": 20, "out": DIES_PER_WAFER},
    "ICチップ":        {"machine": "パッケージング装置", "time": 3.0, "in": {"シリコンダイ": 10, "ICチップ基板": 1}, "ipa": 1, "out": 1},
    "ICチップ基板":    {"machine": "電気汎用工作装置", "time": 3.0, "in": {"プラスチック": 1, "銅板": 1}, "out": 1},
    "鋼鉄フレーム":    {"machine": "電気汎用工作装置", "time": 6.0, "in": {"鋼鉄板": 3, "プラスチック": 2}, "out": 1},
    "クリーンルームフィルター": {"machine": "電気汎用工作装置", "time": 4.0, "in": {"プラスチック": 4, "鉄のワイヤー": 10}, "out": 1},
    "失敗ウェハ再生":  {"machine": "電気粉砕機", "time": 4.0, "in": {"失敗したパターンウェハ": 1}, "out": 5},
    "失敗ダイ再生":    {"machine": "電気粉砕機", "time": 4.0, "in": {"失敗したシリコンダイ": 60}, "out": 1},
}
DIES_PER_CHIP = RECIPES["ICチップ"]["in"]["シリコンダイ"]

# 機械の電力（requiredPower）
# Machine power (requiredPower)
POWER = {"ウェハ切断機": 80, "成膜装置": 150, "EUV露光装置": 250, "ダイシング装置": 120, "パッケージング装置": 60,
         "電気粉砕機": 40, "電気炉": 30, "化学プラント": 75, "電気汎用工作装置": 20, "空気清浄機": 150}

# 汚染と清浄機（cleanRoom.json pollution / 空気清浄機 blockParam）
# Pollution and air filter (cleanRoom.json pollution / air filter blockParam)
POLLUTION = {"aVolume": 0.02, "aSurface": 0.04, "aConnector": 3.0, "aMachine": 2.0, "kHatch": 4.0}
FILTER_Q = 5.0
FILTER_CAPACITY = 20000
THRESHOLDS = {"A": (10, 0.0167), "B": (50, 0.0083), "C": (200, 0.0042), "D": (1000, 0.0014)}
```

`$B/sim/lottery.py`:

```python
"""チップ抽選（Part B 方式）の期待値とモンテカルロ。生産性の追加セットは同じ抽選結果を2倍にする（Part B の書き換え器と同じ）。
Expected values and Monte Carlo of the chip draw (Part B scheme); a productivity extra set doubles the same draw (as Part B's rewriters do)."""
import random

from params import (CLASS_PARAMS, DIE_TABLES, DIES_PER_WAFER, DIES_PER_CHIP, MODULES,
                    PROMOTION_CAP, QUALITY_REDUCTION_CAP)

# 生産性なし（工程ごとの追加セット確率がすべて0）
# No productivity (every stage's extra-set chance is 0)
NO_EXTRA = {"depo": 0.0, "euv": 0.0, "dice": 0.0, "pack": 0.0}


def quality_sum(modules):
    # modules: [(tier, count)] の品質モジュール効果合計
    # Sum of quality effect over [(tier, count)]
    return sum(MODULES["Quality"][tier - 1][0] * count for tier, count in modules)


def grade_distribution(class_name, depo_modules):
    # 成膜のグレード分布 P(G=1..5)。上限は最高Lv+1、各段同じ u で昇格
    # Deposition grade distribution; cap is max tier + 1, each step promotes with the same u
    max_grade = 1 + max([tier for tier, count in depo_modules if count > 0], default=0)
    max_grade = min(max_grade, 5)
    u = min(PROMOTION_CAP, quality_sum(depo_modules) * CLASS_PARAMS[class_name]["qualityMultiplier"])
    dist = [0.0] * 5
    for g in range(1, max_grade):
        dist[g - 1] = (u ** (g - 1)) * (1 - u)
    dist[max_grade - 1] = u ** (max_grade - 1)
    return dist, u


def layer_fail(class_name, euv_modules):
    reduction = min(QUALITY_REDUCTION_CAP, quality_sum(euv_modules))
    return CLASS_PARAMS[class_name]["layerFailRate"] * (1 - reduction)


def die_loss(class_name, dice_modules):
    reduction = min(QUALITY_REDUCTION_CAP, quality_sum(dice_modules))
    return CLASS_PARAMS[class_name]["dieLossRate"] * (1 - reduction)


def expected_per_wafer(class_name, depo, euv, dice, extra):
    # シリコンウェハ1枚を成膜に入れたときの期待値。extra は工程ごとの生産性の追加セット確率
    # Expectations per silicon wafer entering deposition; extra holds each stage's productivity extra-set chance
    grades, u = grade_distribution(class_name, depo)
    fail = layer_fail(class_name, euv)
    good = DIES_PER_WAFER * (1 - die_loss(class_name, dice))
    transistor = 1 + extra["depo"]
    # 層ごとに成功も失敗も追加セットで同じ結果が増える
    # On every layer the extra set repeats the same result, success or failure
    survive = (1 - fail) * (1 + extra["euv"])
    layer_ops = transistor * (1 + survive + survive ** 2)
    finished = transistor * survive ** 3
    dice_sets = finished * (1 + extra["dice"])
    dies = [0.0] * 5
    for g, pg in enumerate(grades, start=1):
        for lv, share in enumerate(DIE_TABLES[g], start=1):
            dies[lv - 1] += dice_sets * good * pg * share
    pack_ops = sum(dies) / DIES_PER_CHIP
    chips = [d / DIES_PER_CHIP * (1 + extra["pack"]) for d in dies]
    return {"u": u, "grades": grades, "fail": fail, "success3": (1 - fail) ** 3, "goodPerWafer": good,
            "dies": dies, "chips": chips, "packOps": pack_ops, "transistorWafers": transistor,
            "layerOps": layer_ops, "finishedWafers": finished,
            "failedWafers": layer_ops * fail * (1 + extra["euv"]), "failedDies": dice_sets * (DIES_PER_WAFER - good)}


def monte_carlo(class_name, depo, euv, dice, wafers, seed):
    # 二項分布で取れ数を引き、ダイ表で多項に振り分ける（生産性なし）
    # Draw the yield binomially and split by the die table multinomially (no productivity)
    rng = random.Random(seed)
    grades, _ = grade_distribution(class_name, depo)
    fail = layer_fail(class_name, euv)
    loss = die_loss(class_name, dice)
    dies = [0] * 5
    goods = []
    for _ in range(wafers):
        g = rng.choices(range(1, 6), weights=grades)[0]
        if any(rng.random() < fail for _layer in range(3)):
            continue
        good = sum(1 for _ in range(DIES_PER_WAFER) if rng.random() >= loss)
        goods.append(good)
        for lv in rng.choices(range(1, len(DIE_TABLES[g]) + 1), weights=DIE_TABLES[g], k=good):
            dies[lv - 1] += 1
    return dies, goods


def check_monotonic(depo, euv, dice):
    # クラスを上げると良品合計と Lv2 以上が減らないことを確かめる
    # Verify total good dies and Lv2+ dies never drop when the class improves
    rows = [expected_per_wafer(c, depo, euv, dice, NO_EXTRA) for c in ["D", "C", "B", "A"]]
    totals = [sum(r["dies"]) for r in rows]
    upper = [sum(r["dies"][1:]) for r in rows]
    ok = all(totals[i] <= totals[i + 1] + 1e-9 and upper[i] <= upper[i + 1] + 1e-9 for i in range(3))
    return ok, totals, upper
```

`$B/sim/chain.py`:

```python
"""基準ライン（上流→クリーンルーム→チップ）の流量と機械台数。室内機械の品質と生産性を数える。
Flows and machine counts of the reference line (upstream -> clean room -> chips), counting quality and productivity in the room."""
import math

from lottery import expected_per_wafer
from params import MODULES, RECIPES

ROOM_MACHINES = ["ウェハ切断機", "成膜装置", "EUV露光装置", "ダイシング装置", "パッケージング装置"]
RECIPE_OF = {"ウェハ切断機": "シリコンウェハ", "成膜装置": "トランジスタ設置済みウェハ", "EUV露光装置": "EUV層",
             "ダイシング装置": "ダイ", "パッケージング装置": "ICチップ"}


def time_multiplier(modules):
    # MachineModuleEffect.Aggregate と同じ処理時間倍率（速度で短縮・品質/生産性で延長）
    # Same processing-time multiplier as MachineModuleEffect.Aggregate
    speed = sum(MODULES["Speed"][t - 1][0] * c for axis, t, c in modules if axis == "Speed")
    trade = sum(MODULES[axis][t - 1][1] * c for axis, t, c in modules if axis in ("Quality", "Productivity"))
    return (1 + trade) / max(0.01, 1 + speed)


def extra_chance(modules):
    # 生産性の追加セット確率（MachineModuleEffect と同じく効果合計を1で切る）
    # Productivity extra-set chance (effect sum clamped to 1, as in MachineModuleEffect)
    return min(1.0, sum(MODULES["Productivity"][t - 1][0] * c for axis, t, c in modules if axis == "Productivity"))


def reference_line(si_per_s, class_name, cfg, use_recycle):
    # cfg: 機械名 -> [(軸, Lv, 個数)]。品質は抽選へ、生産性は追加セットへ渡す
    # cfg: machine name -> [(axis, tier, count)]; quality feeds the draw, productivity the extra set
    def quality_of(machine):
        return [(t, c) for axis, t, c in cfg.get(machine, []) if axis == "Quality"]

    extra = {"depo": extra_chance(cfg.get("成膜装置", [])), "euv": extra_chance(cfg.get("EUV露光装置", [])),
             "dice": extra_chance(cfg.get("ダイシング装置", [])), "pack": extra_chance(cfg.get("パッケージング装置", []))}
    lot = expected_per_wafer(class_name, quality_of("成膜装置"), quality_of("EUV露光装置"), quality_of("ダイシング装置"), extra)
    wafers_per_cut = 1 + extra_chance(cfg.get("ウェハ切断機", []))
    si_per_cut = RECIPES["シリコンウェハ"]["in"]["高純度シリコン"]
    recycled_per_wafer = 0.0
    if use_recycle:
        recycled_per_wafer = (lot["failedWafers"] * RECIPES["失敗ウェハ再生"]["out"]
                              + lot["failedDies"] / RECIPES["失敗ダイ再生"]["in"]["失敗したシリコンダイ"])
    # 切断1回の高純度シリコンから、リサイクルで戻る分を引いた正味で切断回数を決める
    # Size the cut count by the net high-purity silicon per cut after recycling returns
    cuts = si_per_s / max(0.1, si_per_cut - wafers_per_cut * recycled_per_wafer)
    wafers = cuts * wafers_per_cut
    ops = {"ウェハ切断機": cuts, "成膜装置": wafers, "EUV露光装置": wafers * lot["layerOps"],
           "ダイシング装置": wafers * lot["finishedWafers"], "パッケージング装置": wafers * lot["packOps"]}
    machines = {}
    ipa = 0.0
    for machine in ROOM_MACHINES:
        recipe = RECIPES[RECIPE_OF[machine]]
        machines[machine] = ops[machine] * recipe["time"] * time_multiplier(cfg.get(machine, []))
        ipa += ops[machine] * recipe["ipa"]
    chips_per_s = [wafers * c for c in lot["chips"]]
    substrate_per_s = ops["パッケージング装置"]
    upstream = upstream_counts(si_per_s, ipa, substrate_per_s, wafers, lot)
    hatch = {"in_高純度シリコン": cuts * si_per_cut, "in_ICチップ基板": substrate_per_s, "out_ICチップ": sum(chips_per_s),
             "out_失敗したパターンウェハ": wafers * lot["failedWafers"], "out_失敗したシリコンダイ": wafers * lot["failedDies"]}
    return {"lot": lot, "wafers": wafers, "machines": machines, "chips": chips_per_s, "ipa": ipa,
            "upstream": upstream, "hatch": hatch, "dieFlow": wafers * sum(lot["dies"])}


def upstream_counts(si_per_s, ipa_per_s, substrate_per_s, wafers, lot):
    # 室外の機械台数（上流3段・IPA・基板・リサイクル）
    # Outdoor machine counts (3 upstream stages, IPA, substrate, recycling)
    r = RECIPES
    furnace = si_per_s * r["金属シリコン"]["time"]
    sand_per_s = si_per_s * r["金属シリコン"]["in"]["砂"]
    crusher = sand_per_s * r["砂"]["time"]
    chem_si = si_per_s * r["高純度シリコン"]["time"]
    chem_ipa = ipa_per_s / r["IPA"]["out"] * r["IPA"]["time"]
    substrate = substrate_per_s * r["ICチップ基板"]["time"]
    recycle = (wafers * lot["failedWafers"] * r["失敗ウェハ再生"]["time"]
               + wafers * lot["failedDies"] / r["失敗ダイ再生"]["in"]["失敗したシリコンダイ"] * r["失敗ダイ再生"]["time"])
    # 基板のプラスチック1個 = ナフサ2.5（ナフサ5→プラスチック2）
    # One substrate plastic costs 2.5 naphtha (naphtha 5 -> plastic 2)
    naphtha = ipa_per_s / r["IPA"]["out"] * r["IPA"]["in"]["ナフサ"] + substrate_per_s * 2.5
    return {"電気粉砕機(砂)": crusher, "電気炉(金属シリコン)": furnace, "化学プラント(高純度シリコン)": chem_si,
            "化学プラント(IPA)": chem_ipa, "電気汎用工作装置(ICチップ基板)": substrate, "電気粉砕機(リサイクル)": recycle,
            "砕いた石材/s": sand_per_s, "石炭/s": si_per_s, "IPA/s": ipa_per_s, "ナフサ/s(IPA+基板用プラ)": naphtha}


def ceil_counts(machines):
    return {k: math.ceil(v - 1e-6) for k, v in machines.items()}
```

`$B/sim/room.py`:

```python
"""部屋の汚染平衡（仕様 v2 §3 の式）で大部屋集約と工程別小部屋を比べる。
Compare one big room against per-process small rooms with the spec v2 §3 equilibrium."""
import math

from params import FILTER_CAPACITY, FILTER_Q, POLLUTION, POWER, THRESHOLDS

MACHINE_FOOTPRINT = (3, 2, 3)
ROOM_HEIGHT = 4
FILTER_CELLS = 2
PROMOTE_CONC = 0.8
PROMOTE_ACH = 1.25
# 接続口は IPA のパイプコネクタ1つとアイテムハッチ。ドアは接続口に数えない（Part A の A1）
# Connectors are one IPA pipe hatch plus the item hatches; doors are not connectors (Part A's A1)
PIPE_HATCHES = 1
BIG_ROOM_ITEM_HATCHES = 4


def room_geometry(machine_count, filter_count):
    # 機械を中央通路の両側に並べた直方体の内寸から V と S を出す
    # Derive V and S from a box with machines lined on both sides of a central aisle
    per_row = math.ceil(machine_count / 2)
    length = per_row * 4 + 2
    width = 3 + 3 + 3 + 2
    machine_cells = machine_count * MACHINE_FOOTPRINT[0] * MACHINE_FOOTPRINT[1] * MACHINE_FOOTPRINT[2]
    belt_cells = length * 2
    occupied = machine_cells + belt_cells + filter_count * FILTER_CELLS
    volume = length * width * ROOM_HEIGHT - occupied
    footprint = machine_count * 9 + belt_cells + filter_count
    surface = 2 * (length * width + length * ROOM_HEIGHT + width * ROOM_HEIGHT) - footprint
    return volume, surface, (length, width, ROOM_HEIGHT)


def pollution(machine_count, hatch_rate, connectors, filter_count):
    volume, surface, dims = room_geometry(machine_count, filter_count)
    p = POLLUTION
    total = (p["aMachine"] * machine_count + p["kHatch"] * hatch_rate
             + p["aVolume"] * volume + p["aSurface"] * surface + p["aConnector"] * connectors)
    return total, volume, surface, dims


def best_class(total, volume, filter_count):
    # 昇格ヒステリシス込みで到達できる最良クラス
    # Best class reachable including promotion hysteresis
    removal = filter_count * FILTER_Q
    if removal <= 0:
        return "Out"
    conc = total / removal
    ach = removal / volume
    for name in ["A", "B", "C", "D"]:
        max_conc, req_ach = THRESHOLDS[name]
        if conc <= max_conc * PROMOTE_CONC and ach >= req_ach * PROMOTE_ACH:
            return name
    return "Out"


def filters_for(machine_count, hatch_rate, connectors, target):
    # 目標クラスに必要な清浄機台数とフィルター消費/時
    # Air filters needed for the target class and filters consumed per hour
    order = ["A", "B", "C", "D"]
    for n in range(1, 200):
        total, volume, surface, dims = pollution(machine_count, hatch_rate, connectors, n)
        cls = best_class(total, volume, n)
        if cls != "Out" and order.index(cls) <= order.index(target):
            return {"filters": n, "pollution": total, "V": volume, "S": surface, "dims": dims,
                    "filtersPerHour": total * 3600 / FILTER_CAPACITY, "filterPower": n * POWER["空気清浄機"],
                    "class": cls}
    return None


def compare_layouts(line, target):
    # 大部屋（上流と基板だけ搬入）と工程別5部屋の比較
    # One big room (only upstream and substrate in) versus five per-process rooms
    m = {k: math.ceil(v - 1e-6) for k, v in line["machines"].items()}
    hatch = line["hatch"]
    big_rate = sum(hatch.values())
    big = filters_for(sum(m.values()), big_rate, connectors=PIPE_HATCHES + BIG_ROOM_ITEM_HATCHES, target=target)
    w = line["wafers"]
    lot = line["lot"]
    flows = {
        "ウェハ切断機": hatch["in_高純度シリコン"] + w,
        "成膜装置": w + w * lot["transistorWafers"],
        "EUV露光装置": w * lot["transistorWafers"] + w * lot["finishedWafers"] + hatch["out_失敗したパターンウェハ"],
        "ダイシング装置": w * lot["finishedWafers"] + line["dieFlow"] + hatch["out_失敗したシリコンダイ"],
        "パッケージング装置": line["dieFlow"] + hatch["in_ICチップ基板"] + hatch["out_ICチップ"],
    }
    hatches = {"ウェハ切断機": 2, "成膜装置": 2, "EUV露光装置": 3, "ダイシング装置": 3, "パッケージング装置": 3}
    split = {}
    for machine, rate in flows.items():
        split[machine] = filters_for(m[machine], rate, connectors=PIPE_HATCHES + hatches[machine], target=target)
    return {"big": big, "bigHatchRate": big_rate, "split": split, "splitRates": flows}
```

`$B/sim/timeline.py`:

```python
"""S1（半導体製造の解放〜モジュール）の時間配分を分刻みで進める。研究は ICチップLv1 を要求し、足りない分を Lv2 で払う。
Step the S1 time budget (semiconductor unlock -> modules) minute by minute; research asks for IC chip Lv1 and pays any shortfall with Lv2."""
from chain import ROOM_MACHINES, reference_line
from params import LINE_SI_PER_S

# 研究ノード（ICチップLv1, 鋼鉄フレーム, コンピュータ, 制御モータ）と前提
# Research nodes (IC chip Lv1, steel frame, computer, control motor) and prerequisites
RESEARCH = [
    {"name": "品質モジュール", "chips": 1000, "frames": 150, "computers": 60, "motors": 0, "pre": []},
    {"name": "機械オーバークロック", "chips": 1500, "frames": 150, "computers": 0, "motors": 120, "pre": ["品質モジュール"]},
    {"name": "省エネモジュール", "chips": 1500, "frames": 150, "computers": 60, "motors": 0, "pre": ["品質モジュール"]},
    {"name": "GPU設計", "chips": 2000, "frames": 200, "computers": 120, "motors": 0, "pre": ["品質モジュール"]},
    {"name": "モジュールスタック拡張I", "chips": 3000, "frames": 200, "computers": 0, "motors": 150, "pre": ["機械オーバークロック", "省エネモジュール"]},
    {"name": "生産性モジュール", "chips": 4000, "frames": 300, "computers": 150, "motors": 0, "pre": ["モジュールスタック拡張I"]},
    {"name": "モジュールスタック拡張II", "chips": 7000, "frames": 400, "computers": 200, "motors": 200, "pre": ["生産性モジュール"]},
]
# 研究完了後にプレイヤーが作るLv1モジュール（個数, 1個のチップ数）。製作上限 Lv1 の継承レシピなので Lv1 チップだけで払う
# Lv1 modules crafted after each research (count, chips per module); the Lv1-capped inheritance recipe takes Lv1 chips only
CRAFT_AFTER = {
    "品質モジュール": (18, 5), "機械オーバークロック": (45, 5), "省エネモジュール": (30, 5),
    "モジュールスタック拡張I": (60, 5), "生産性モジュール": (45, 10), "モジュールスタック拡張II": (60, 5),
}
# 建設時間の仮定（時間）と、ラインを増やす時刻
# Build-time assumptions (hours) and when the player adds a line
FIRST_LINE_READY_H = 3.0
EXTRA_LINES_AT_H = [3.5, 5.0]
EXTRA_LINE_BUILD_H = 0.5


def line_config(done):
    # 研究済みのモジュールを室内機械へ入れる（品質Lv1×3は成膜・EUV・ダイシング、生産性Lv1×3は室内5台。3本×5台×3個=45個）
    # Fit researched modules into the room (quality Lv1 x3 on depo/EUV/dicing, productivity Lv1 x3 on all five; 3 lines x 5 x 3 = 45)
    cfg = {machine: [] for machine in ROOM_MACHINES}
    if "品質モジュール" in done:
        for machine in ("成膜装置", "EUV露光装置", "ダイシング装置"):
            cfg[machine].append(("Quality", 1, 3))
    if "生産性モジュール" in done:
        for machine in ROOM_MACHINES:
            cfg[machine].append(("Productivity", 1, 3))
    return cfg


def line_rate(class_name, done):
    # 基準ライン1本の Lv1/Lv2 チップ毎分
    # Lv1/Lv2 chips per minute of one reference line
    line = reference_line(LINE_SI_PER_S, class_name, line_config(done), use_recycle=False)
    return line["chips"][0] * 60, line["chips"][1] * 60


def run(class_name):
    done = {}
    stock = [0.0, 0.0]
    produced = [0.0, 0.0]
    lv2_to_research = 0.0
    pending_craft = 0.0
    order = list(RESEARCH)
    minute = 0
    while order and minute < 20 * 60:
        minute += 1
        hours = minute / 60
        lines = 0
        if hours >= FIRST_LINE_READY_H:
            lines = 1 + sum(1 for t in EXTRA_LINES_AT_H if hours >= t + EXTRA_LINE_BUILD_H)
        for lv, rate in enumerate(line_rate(class_name, done)):
            stock[lv] += rate * lines
            produced[lv] += rate * lines
        # 先にモジュール製作分のチップを Lv1 で払う
        # Pay chips for module crafting first, with Lv1 only
        pay = min(stock[0], pending_craft)
        stock[0] -= pay
        pending_craft -= pay
        node = next((n for n in order if all(p in done for p in n["pre"])), None)
        if node and pending_craft <= 0 and stock[0] + stock[1] >= node["chips"]:
            # 研究は下位（Lv1）から消費し、足りない分を Lv2 で払う
            # Research consumes the lower level (Lv1) first and pays the rest with Lv2
            from_lv1 = min(stock[0], node["chips"])
            stock[0] -= from_lv1
            stock[1] -= node["chips"] - from_lv1
            lv2_to_research += node["chips"] - from_lv1
            done[node["name"]] = hours
            order.remove(node)
            count, chips = CRAFT_AFTER.get(node["name"], (0, 0))
            pending_craft += count * chips
    return {"done": done, "stockLv2": stock[1], "producedLv1": produced[0], "producedLv2": produced[1],
            "lv2ToResearch": lv2_to_research}


def totals():
    keys = ["chips", "frames", "computers", "motors"]
    research = {k: sum(n[k] for n in RESEARCH) for k in keys}
    crafting_chips = sum(c * k for c, k in CRAFT_AFTER.values())
    return research, crafting_chips
```

`$B/sim/targets.py`:

```python
"""裁定の目標（S1 6〜8時間・3層成功≒50%・Lv1中心＋少数Lv2・上流は電気炉だけ約2倍・大部屋集約が有利）を判定する。
Judge the ruled targets (S1 6-8 h, ~50% 3-layer success, mostly Lv1 with few Lv2, only the furnace doubled upstream, one big room wins)."""
import timeline
from chain import reference_line
from lottery import NO_EXTRA, check_monotonic, expected_per_wafer
from params import LINE_SI_PER_S
from room import compare_layouts

S1_HOURS = (6.0, 8.0)
SUCCESS3_CLASS_C = (0.45, 0.55)
# S1 中に出たチップに占める Lv2 の割合（0 より大きく 15% 以下＝少数）
# Share of Lv2 among chips made during S1 (above 0 and at most 15% = a few)
LV2_SHARE = (0.0, 0.15)
QUALITY_CONFIGS = [[], [(1, 3)], [(1, 9)], [(1, 30)], [(2, 9)], [(4, 30)]]


def evaluate():
    # (目標, 合否, 実測) の一覧を返す
    # Return a list of (target, passed, measured)
    results = []
    for cls in "ABC":
        run = timeline.run(cls)
        end = run["done"].get("モジュールスタック拡張II")
        results.append((f"S1 6〜8時間（クラス{cls}）", end is not None and S1_HOURS[0] <= end <= S1_HOURS[1], f"{end}h"))
        share = run["producedLv2"] / max(1.0, run["producedLv1"] + run["producedLv2"])
        results.append((f"Lv1 中心＋少数 Lv2（クラス{cls}）", LV2_SHARE[0] < share <= LV2_SHARE[1], f"Lv2 {share * 100:.1f}%"))
    success = expected_per_wafer("C", [], [], [], NO_EXTRA)["success3"]
    results.append(("3層成功≒50%（クラスC・品質なし）", SUCCESS3_CLASS_C[0] <= success <= SUCCESS3_CLASS_C[1], f"{success * 100:.1f}%"))
    line = reference_line(LINE_SI_PER_S, "B", {}, False)
    up = line["upstream"]
    crush, furnace, chem = up["電気粉砕機(砂)"], up["電気炉(金属シリコン)"], up["化学プラント(高純度シリコン)"]
    ratio_ok = abs(crush - chem) < 1e-6 and abs(furnace - 2 * crush) < 1e-6
    results.append(("上流は電気炉だけ約2倍", ratio_ok, f"粉砕:炉:化学 = {crush:.2f}:{furnace:.2f}:{chem:.2f}"))
    for target in "ABC":
        r = compare_layouts(line, target)
        split_filters = sum(x["filters"] for x in r["split"].values())
        split_fph = sum(x["filtersPerHour"] for x in r["split"].values())
        ok = r["big"]["filters"] < split_filters and r["big"]["filtersPerHour"] < split_fph
        results.append((f"大部屋集約が有利（目標クラス{target}）", ok, f"清浄機 {r['big']['filters']} vs {split_filters}台"))
    mono = all(check_monotonic(q, q, q)[0] for q in QUALITY_CONFIGS)
    results.append(("クラスを上げて良品合計と Lv2 以上が減らない", mono, f"{len(QUALITY_CONFIGS)}構成"))
    return results
```

`$B/sim/run_all.py`:

```python
"""全シミュレーションを回して Markdown の表を output.md に書く。目標を1つでも外せば終了コード1。
Run every simulation and write Markdown tables to output.md; exit 1 if any target fails."""
import math
import statistics
import sys
from pathlib import Path

import targets
import timeline
from chain import reference_line, time_multiplier
from lottery import NO_EXTRA, check_monotonic, expected_per_wafer, monte_carlo
from params import LINE_SI_PER_S, MODULES, POWER, STACK_LIMITS
from room import compare_layouts

CONFIGS = [("なし", []), ("品質Lv1×3", [(1, 3)]), ("品質Lv1×9", [(1, 9)]), ("品質Lv1×30", [(1, 30)]),
           ("品質Lv2×9", [(2, 9)]), ("品質Lv4×30", [(4, 30)])]
# §2 のライン構成（timeline.line_config に渡す研究済みノード）
# Line setups for section 2 (researched nodes passed to timeline.line_config)
LINE_SETUPS = [("B", "品質なし", []), ("B", "品質Lv1×3", ["品質モジュール"]), ("B", "品質Lv1×3＋生産性Lv1×3", ["品質モジュール", "生産性モジュール"]),
               ("C", "品質なし", []), ("A", "品質Lv1×3", ["品質モジュール"]), ("A", "品質Lv1×3＋生産性Lv1×3", ["品質モジュール", "生産性モジュール"])]
out = []


def table(header, rows):
    out.append("| " + " | ".join(header) + " |")
    out.append("|" + "---|" * len(header))
    for r in rows:
        out.append("| " + " | ".join(str(x) for x in r) + " |")
    out.append("")


def pct(x):
    return f"{x * 100:.1f}%"


results = targets.evaluate()
out.append("## 0. 目標の判定（生産性込み）\n")
for name, ok, measured in results:
    out.append(f"- {'PASS' if ok else 'FAIL'} {name}（{measured}）")
out.append("")

out.append("## 1. 抽選（成膜・EUV・ダイシングに同じ品質構成を入れた場合、シリコンウェハ1枚あたり・生産性なし）\n")
rows = []
for cls in "ABCD":
    for name, q in CONFIGS:
        r = expected_per_wafer(cls, q, q, q, NO_EXTRA)
        total = sum(r["dies"])
        share = " / ".join(pct(d / total) for d in r["dies"] if d > 0)
        rows.append([cls, name, f"{r['u']:.2f}", pct(r["fail"]), pct(r["success3"]), f"{r['goodPerWafer']:.0f}",
                     f"{total:.0f}", f"{sum(r['chips']):.1f}", share])
table(["クラス", "品質", "昇格u", "層失敗", "3層成功", "良品ダイ/完成ウェハ", "良品ダイ/投入ウェハ", "チップ/投入ウェハ", "Lv1/Lv2/…割合"], rows)

out.append("### 単調性（D→C→B→A で良品合計と Lv2 以上が減らない）\n")
rows = []
for name, q in CONFIGS:
    ok, totals, upper = check_monotonic(q, q, q)
    rows.append([name, "OK" if ok else "NG", " → ".join(f"{t:.0f}" for t in totals), " → ".join(f"{u:.1f}" for u in upper)])
table(["品質", "判定", "良品ダイ D→A", "Lv2以上 D→A"], rows)

out.append("### モンテカルロ（クラスB・品質Lv1×9・投入1000枚、二項＋多項）\n")
dies, goods = monte_carlo("B", [(1, 9)], [(1, 9)], [(1, 9)], 1000, 7)
exp = expected_per_wafer("B", [(1, 9)], [(1, 9)], [(1, 9)], NO_EXTRA)
table(["項目", "期待値", "実測"], [
    ["3層成功枚数", f"{exp['success3'] * 1000:.0f}", len(goods)],
    ["良品ダイ/完成ウェハ 平均", f"{exp['goodPerWafer']:.1f}", f"{statistics.mean(goods):.1f}"],
    ["良品ダイ/完成ウェハ 標準偏差", f"{math.sqrt(300 * (exp['goodPerWafer'] / 300) * (1 - exp['goodPerWafer'] / 300)):.1f}", f"{statistics.pstdev(goods):.1f}"],
    ["Lv2ダイ割合", pct(exp["dies"][1] / sum(exp["dies"])), pct(dies[1] / sum(dies))]])

out.append(f"## 2. 基準ライン1本（高純度シリコン {LINE_SI_PER_S}/秒。品質は成膜・EUV・ダイシング、生産性は室内5台）\n")
for cls, label, nodes in LINE_SETUPS:
    cfg = timeline.line_config(nodes)
    for recycle in (False, True):
        line = reference_line(LINE_SI_PER_S, cls, cfg, recycle)
        machines = " / ".join(f"{k} {v:.2f}→{math.ceil(v - 1e-6)}" for k, v in line["machines"].items())
        up = line["upstream"]
        upstr = " : ".join(f"{up[k]:.2f}" for k in ["電気粉砕機(砂)", "電気炉(金属シリコン)", "化学プラント(高純度シリコン)"])
        chips = " / ".join(f"{c * 60:.1f}" for c in line["chips"][:2])
        out.append(f"- クラス{cls}・{label}・リサイクル{'あり' if recycle else 'なし'}: チップ/分 Lv1/Lv2 = {chips}、"
                   f"ウェハ {line['wafers'] * 60:.2f}/分、室内 {machines}、上流 粉砕:炉:化学 = {upstr}、"
                   f"IPA {line['ipa']:.2f}/秒（化学プラント {up['化学プラント(IPA)']:.2f}台）、基板 汎用工作 {up['電気汎用工作装置(ICチップ基板)']:.2f}台、"
                   f"リサイクル粉砕 {up['電気粉砕機(リサイクル)']:.2f}台、ダイ流量 {line['dieFlow']:.2f}/秒")
out.append("")

out.append("## 3. 部屋の作り方（基準ライン1本・クラスB運転、清浄機 q と汚染係数は params.py）\n")
line = reference_line(LINE_SI_PER_S, "B", {}, False)
rows = []
for target in "ABC":
    r = compare_layouts(line, target)
    b = r["big"]
    split_filters = sum(x["filters"] for x in r["split"].values())
    split_fph = sum(x["filtersPerHour"] for x in r["split"].values())
    split_pol = sum(x["pollution"] for x in r["split"].values())
    rows.append([target, f"{b['filters']}台 / 汚染{b['pollution']:.0f} / フィルター{b['filtersPerHour']:.1f}/時 / V{b['V']} 内寸{b['dims']}",
                 f"{split_filters}台 / 汚染{split_pol:.0f} / フィルター{split_fph:.1f}/時", f"{r['bigHatchRate']:.1f}/秒"])
table(["目標クラス", "大部屋1つ（上流と基板だけ搬入）", "工程別5部屋", "大部屋のハッチ搬送"], rows)

out.append("## 4. S1 の時間配分（初回ライン完成 3.0時間・2本目 4.0時間・3本目 5.5時間の仮定、生産性モジュールの研究後は室内5台に生産性Lv1×3）\n")
research, craft = timeline.totals()
out.append(f"- 研究合計: ICチップLv1 {research['chips']}・鋼鉄フレーム {research['frames']}・コンピュータ {research['computers']}・制御モータ {research['motors']}。Lv1モジュール製作に ICチップLv1 {craft}")
for cls in "ABC":
    run = timeline.run(cls)
    lv1_q, lv2_q = timeline.line_rate(cls, ["品質モジュール"])
    lv1_p, lv2_p = timeline.line_rate(cls, ["品質モジュール", "生産性モジュール"])
    out.append(f"- クラス{cls}（1本 Lv1/Lv2 毎分: 品質のみ {lv1_q:.1f}/{lv2_q:.1f}・生産性込み {lv1_p:.1f}/{lv2_p:.1f}）: "
               + "、".join(f"{k} {v:.2f}h" for k, v in run["done"].items())
               + f"。研究に回った Lv2 {run['lv2ToResearch']:.0f}・S1終了時の Lv2 在庫 {run['stockLv2']:.0f}")
out.append("")

out.append("## 5. モジュール効果（1スロットに同じモジュールを積んだ合計。3スロット全部同種）\n")
rows = []
for axis in ["Speed", "Efficiency", "Productivity", "Quality"]:
    for lv in (1, 3, 5):
        eff, trade = MODULES[axis][lv - 1]
        cells = []
        for stack in STACK_LIMITS:
            n = stack * 3
            if axis == "Speed":
                cells.append(f"速度+{eff * n * 100:.0f}% 電力×{1 + trade * n:.2f}")
            elif axis == "Efficiency":
                cells.append(f"電力×{max(0.1, 1 / (1 + eff * n)):.2f}")
            elif axis == "Productivity":
                cells.append(f"追加{min(1.0, eff * n) * 100:.0f}% 時間×{time_multiplier([(axis, lv, n)]):.2f}")
            else:
                cells.append(f"品質合計{eff * n:.2f} 時間×{time_multiplier([(axis, lv, n)]):.2f}")
        rows.append([axis, f"Lv{lv}"] + cells)
table(["軸", "Lv", "上限3（9個）", "上限6（18個）", "上限10（30個）"], rows)

out.append("## 6. 電力（基準ライン1本・クラスB・満稼働）\n")
counts = {k: math.ceil(v - 1e-6) for k, v in line["machines"].items()}
room_power = sum(POWER[k] * v for k, v in counts.items())
up = line["upstream"]
up_power = (math.ceil(up["電気粉砕機(砂)"] - 1e-6) * POWER["電気粉砕機"] + math.ceil(up["電気炉(金属シリコン)"] - 1e-6) * POWER["電気炉"]
            + (math.ceil(up["化学プラント(高純度シリコン)"] - 1e-6) + 1) * POWER["化学プラント"] + 2 * POWER["電気汎用工作装置"])
out.append(f"- 室内機械 {room_power}・清浄機3台 {3 * POWER['空気清浄機']}・上流 {up_power} → 合計 {room_power + 3 * POWER['空気清浄機'] + up_power}（ガソリン発電機1000/台）\n")

Path(__file__).with_name("output.md").write_text("\n".join(out), encoding="utf-8")
print("\n".join(out))
sys.exit(0 if all(ok for _, ok, _ in results) else 1)
```

- [ ] **Step 2: 生産性込みでシミュレーションを回し、目標の判定を見る**

Run: `cd $B && python3 sim/run_all.py > /dev/null; echo "exit=$?"; grep -E "^- (PASS|FAIL) " sim/output.md`
Expected: 12行の判定が出る。全行 PASS なら `exit=0` で Step 4 へ進む。1行でも FAIL なら `exit=1` で Step 3 へ進む。
- 生産性を含めない部分（§1 の抽選表・単調性・モンテカルロ）は下書き時の値と一致する: `grep "^| C | なし" sim/output.md` が `| C | なし | 0.00 | 20.0% | 51.2% | 210 | 108 | 10.8 | 100.0% |`
- 本 plan 作成時にこのコードと下書きの値で回した結果: クラスA の S1 終了が 5.83h で `FAIL S1 6〜8時間（クラスA）`（B 6.27h・C 6.87h は PASS、Lv2 割合は 3.4% / 2.6% / 1.7%、他の目標は PASS）。生産性モジュールの研究後に室内5台へ生産性Lv1×3 を入れると、ライン1本の Lv1 チップが 1.8 倍前後になり（クラスB 46.2→84.4 /分）、最後の研究が早く終わるため

- [ ] **Step 3: FAIL した目標ごとに、決まった順で値を直して回し直す**

`sim/params.py`（研究コストは `sim/timeline.py` の `RESEARCH`）を直し、Step 2 を全行 PASS まで繰り返す。1回に動かす値は1つで、前の値が範囲の端に達してから次へ進む。

| FAIL した目標 | 動かす順 | 動かす向きと範囲 |
|---|---|---|
| S1 6〜8時間（6時間未満） | 1. `RESEARCH` の後半3ノード（モジュールスタック拡張I・生産性モジュール・モジュールスタック拡張II）の `chips` → 2. `MODULES["Productivity"]` の `effectValue`（Lv1〜5 を同じ比率で） → 3. 同 `tradeoffValue` | 1 は 1000 刻みで増やす（本 plan 作成時の感度: 拡張II を +1000 でクラスA が約 +0.05h。前半ノードは触らない＝品質モジュールが 3.3h 前後で取れる手触りを保つ）。2 は 0.005 刻みで下げ、Lv1 の下限 0.02。3 は 0.02 刻みで上げ、上限 0.16 |
| S1 6〜8時間（8時間超） | 上の 1 → 2 → 3 を逆向きに | 1 は 1000 刻みで減らし、下書き値（1000/1500/1500/2000/3000/4000/7000）を下回らない。2 は 0.005 刻みで上げ、Lv1 の上限 0.04 |
| Lv1 中心＋少数 Lv2（15% 超） | 1. `MODULES["Quality"]` の `effectValue` → 2. `PROMOTION_CAP` | 1 は 0.005 刻みで下げる（Lv1 の下限 0.02）。2 は 0.05 刻みで下げる（下限 0.4） |
| Lv1 中心＋少数 Lv2（0%） | `MODULES["Quality"]` の `effectValue` | 0.005 刻みで上げる |
| 3層成功≒50%（クラスC・品質なし） | `CLASS_PARAMS["C"]["layerFailRate"]` | 0.01 刻み。A < B < C < D の順を崩さない（崩れると単調性が FAIL し、Part B の起動時検証 `CleanRoomYieldTableRules.ValidateClassRows` でも止まる） |
| 上流は電気炉だけ約2倍 | `RECIPES` の `砂`・`金属シリコン`・`高純度シリコン` の `time` | 粉砕:炉:化学 = x:2x:x に戻す |
| 大部屋集約が有利 | 1. `POLLUTION["kHatch"]` → 2. `POLLUTION["aConnector"]` | 0.5 刻みで上げる（工程別の部屋ほどハッチと接続口が多いので差が開く） |
| 単調性 | `CLASS_PARAMS` の行の並び | 品質倍率 A ≥ B ≥ C ≥ D、層失敗率・ダイ損失率 A ≤ B ≤ C ≤ D に戻す |

変えた値は、マスタへ入れる spec に同じ値で書く（D8 の `verify_master.py` が食い違いを FAIL にする）。対応:

| params / timeline | spec の場所（本 plan の JSON の値を置き換えて書く） |
|---|---|
| `RESEARCH[n]["chips"]` ほか研究コスト | D7 spec 05 の `research[].consumeItems`（機械オーバークロックは `patchResearch` の `consumeItems`） |
| `MODULES[axis][tier-1]` | D6 spec 04 の `modules[].effectValue` / `tradeoffValue` |
| `CLASS_PARAMS` / `THRESHOLDS` / `POLLUTION` | D5 spec 03 の `setMaster` の `cleanRoom.data.0.thresholds` / `cleanRoom.data.0.pollution` |
| `PROMOTION_CAP` / `QUALITY_REDUCTION_CAP` / `DIES_PER_WAFER` / `DIE_TABLES` | D5 spec 03 の役割表（`waferGradeDraws[].maxPromotionChance`、`layerExposures[].maxFailReduction`、`dicings[].maxLossReduction` / `diesPerWafer` / `gradeDieTables`） |
| `RECIPES` | D3 spec 01・D5 spec 03 の `machineRecipes[]` |
| `POWER` / `FILTER_Q` / `FILTER_CAPACITY` | D5 spec 03 の `blocks[].element.blockParam.requiredPower` と `patchBlocks` |

- [ ] **Step 4: 本 plan の「シミュレーション結果」節を output.md の値に書き換える**

全目標 PASS の `sim/output.md` から、本 plan ファイルの「シミュレーション結果」節の表を書き換える。対応: 「抽選」表 ← §1（クラス・構成の行を同じ並びで転記）、「機械台数」表 ← §2 の「クラスB・品質なし・リサイクルなし」行、「1本あたりのチップ/時」表 ← §2 の各行のチップ/分×60（列に「品質Lv1×3＋生産性Lv1×3」を足す）、「部屋の作り方」表 ← §3、「S1 の研究コストと所要時間」表 ← §4 と `timeline.RESEARCH`、「モジュール効果」表 ← §5。節の冒頭の「下書き時（生産性なし）の値」の注記を「生産性込み・D1 で再実行した値」に直す。Step 3 で値を変えたら、変えた params・研究コストと理由（どの目標が FAIL したか）を節の末尾に1行ずつ書く。

Run: `grep -c "生産性込み・D1 で再実行した値" <本 plan ファイル>`
Expected: `1`

- [ ] **Step 5: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/README.md design/tier9-semiconductor-balance/sim
git -C $MW commit -m "design: Tier9 半導体のバランスシミュレーションを追加する（室内の生産性込み）"
git add <本 plan ファイル>
git commit -m "docs(plan): Tier9 半導体のシミュレーション結果を生産性込みの値に更新する"
```

### Task D2: マスタ適用・照合スクリプトと Guid 台帳

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/apply/master_io.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/apply/resolver.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/apply/element_builders.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/apply/localization_rows.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/apply/apply_spec.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/verify_checks.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/verify_master.py`
- Create: `moorestech_master/design/tier9-semiconductor-balance/guids.json`

**Interfaces:**
- Consumes: D1 の `sim/params.py`・`sim/timeline.py`
- Produces:
  - CLI `python3 apply/apply_spec.py --mod-dir <mod> --spec-dir spec --ledger guids.json`（spec ファイル名順に適用。標準出力 `applied N spec files; issued M new guids`）
  - CLI `python3 verify_master.py --mod-dir <mod> --ledger guids.json`（検査8項目を `PASS|FAIL <名前>` で出し、1件でも FAIL なら終了コード1）
  - spec の書式: 最上位キー `stackTables` `items`（`stackTable` 省略時は Default 表）`renameItems` `patchItems` `levelFamilies` `modules` `removeFluids` `removeMachineRecipes` `machineRecipes` `blocks` `patchBlocks` `research` `patchResearch` `setMaster` `deleteTopLevelKeys`。値中の文字列 `@item:名前` `@block:名前` `@fluid:名前` `@research:名前` `@recipe:レシピキー` `@family:名前` `@module:名前` `@stackTable:名前` は Guid に、`@new:キー` は台帳の新規 Guid に置き換わる
  - 台帳 `guids.json`: `{"<種類>:<名前>": "<guid>"}`。新しい要素は初回適用で UUIDv7 が発行され追記される

Guid の生成手順（このタスクで決める）: 新規要素の Guid は手で書かない。`apply_spec.py` が初回に `uuid.uuid7()` で発行して `guids.json` に記録し、以後の再適用は台帳の値を使う（何度流しても同じ結果）。他セッションと共有済みの Guid（研究6・モジュール4ファミリー・製品レシピ5・モジュールLv1アイテム4・GPU 6）は spec 側の `guid` に書いてあり、台帳と食い違えば `ValueError` で止まる。

- [ ] **Step 1: 適用スクリプト5ファイルを書く**

`$B/apply/master_io.py`:

```python
"""マスタJSON・ローカライズCSV・Guid台帳の読み書き（既存ファイルと同じ書式で書き戻す）。
Read and write master JSON, the localization CSV, and the GUID ledger in the existing formats."""
import csv
import io
import json
import uuid
from pathlib import Path

MASTER_FILES = ["items", "fluids", "blocks", "machineRecipes", "research", "cleanRoom"]


def load_master(mod_dir):
    master = {}
    for name in MASTER_FILES:
        path = Path(mod_dir) / "master" / f"{name}.json"
        master[name] = json.loads(path.read_text(encoding="utf-8"))
    return master


def save_master(mod_dir, master):
    # インデント2・非ASCIIそのまま・末尾改行なし（現行ファイルと往復一致する書式）
    # Indent 2, raw non-ASCII, no trailing newline (round-trips the current files)
    for name in MASTER_FILES:
        path = Path(mod_dir) / "master" / f"{name}.json"
        path.write_text(json.dumps(master[name], indent=2, ensure_ascii=False), encoding="utf-8")


def load_localization(mod_dir):
    path = Path(mod_dir) / "localization" / "localization.csv"
    with path.open(encoding="utf-8", newline="") as source:
        reader = csv.reader(source)
        header = next(reader)
        rows = [row for row in reader]
    return header, rows


def save_localization(mod_dir, header, rows):
    buffer = io.StringIO()
    writer = csv.writer(buffer, lineterminator="\n")
    writer.writerow(header)
    writer.writerows(rows)
    (Path(mod_dir) / "localization" / "localization.csv").write_text(buffer.getvalue(), encoding="utf-8")


class GuidLedger:
    """キー（例 item:金属シリコン）→ Guid の台帳。無いキーは UUIDv7 を発行して記録する。
    Ledger from key (e.g. item:金属シリコン) to GUID; unknown keys get a fresh UUIDv7."""

    def __init__(self, path):
        self._path = Path(path)
        self._table = json.loads(self._path.read_text(encoding="utf-8")) if self._path.exists() else {}
        self.issued = []

    def guid(self, key):
        if key not in self._table:
            self._table[key] = str(uuid.uuid7())
            self.issued.append(key)
        return self._table[key]

    def set_fixed(self, key, guid):
        # 他セッションと共有する固定Guidを台帳へ記録する（既存値と食い違えば例外）
        # Record a fixed GUID shared with other sessions (raise on a mismatch)
        if key in self._table and self._table[key] != guid:
            raise ValueError(f"ledger guid differs from fixed guid: {key}")
        self._table[key] = guid

    def has(self, key):
        return key in self._table

    def save(self):
        self._path.write_text(json.dumps(self._table, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
```

`$B/apply/resolver.py`:

```python
"""spec の「@種類:名前」トークンを Guid に解決する。未解決は即例外（黙って空にしない）。
Resolve "@kind:name" tokens in specs to GUIDs; unresolved tokens raise immediately."""


class NameResolver:
    def __init__(self, master, ledger):
        self._ledger = ledger
        self._maps = {
            "item": {e["name"]: e["itemGuid"] for e in master["items"]["data"]},
            "block": {e["name"]: e["blockGuid"] for e in master["blocks"]["data"]},
            "fluid": {e["name"]: e["fluidGuid"] for e in master["fluids"]["data"]},
            "research": {e["researchNodeName"]: e["researchNodeGuid"] for e in master["research"]["data"]},
            "recipe": {},
            "family": {e["name"]: e["familyGuid"] for e in master["items"].get("levelFamilies", [])},
            "module": {e["name"]: e["moduleGuid"] for e in master["items"].get("modules", [])},
            "stackTable": {e["name"]: e["tableGuid"] for e in master["items"]["itemStackLevelTables"]},
        }

    def declare(self, kind, name, fixed_guid):
        # 新規要素を宣言して Guid を割り当てる（固定Guidがあれば台帳より優先して記録）
        # Declare a new element and assign its GUID (a fixed GUID wins and is recorded)
        key = f"{kind}:{name}"
        if name in self._maps[kind]:
            return self._maps[kind][name]
        if fixed_guid:
            self._ledger.set_fixed(key, fixed_guid)
        guid = self._ledger.guid(key)
        self._maps[kind][name] = guid
        return guid

    def rename(self, kind, guid, new_name):
        old = [n for n, g in self._maps[kind].items() if g == guid]
        if not old:
            raise KeyError(f"rename target not found: {kind} {guid}")
        del self._maps[kind][old[0]]
        self._maps[kind][new_name] = guid

    def resolve(self, value):
        if isinstance(value, dict):
            return {k: self.resolve(v) for k, v in value.items()}
        if isinstance(value, list):
            return [self.resolve(v) for v in value]
        if isinstance(value, str) and value.startswith("@"):
            kind, _, name = value[1:].partition(":")
            if kind == "new":
                return self._ledger.guid(f"new:{name}")
            if name not in self._maps.get(kind, {}):
                raise KeyError(f"unresolved token: {value}")
            return self._maps[kind][name]
        return value
```

`$B/apply/element_builders.py`:

```python
"""spec の簡略記法からマスタ要素を組み立てる（スキーマの必須キーを全部書く）。
Build master elements from the compact spec notation (writing every schema key)."""

DEFAULT_STACK_TABLE = "9c4f9d1e-3a77-4a2b-9b62-6d0f5c8e21aa"


def item_element(guid, spec, stack_table_guid):
    return {"itemGuid": guid, "imagePath": "", "name": spec["name"], "sortPriority": spec["sortPriority"],
            "initialUnlocked": False, "recipeViewType": "IsUnlocked",
            "addressablePaths": {"handGrabModel": "", "entityModel": ""}, "stackLevelTableGuid": stack_table_guid}


def counts(pairs):
    # [["名前", 個数]] → [{"itemGuid", "count"}]（解決済みの Guid を受け取る）
    # [["name", count]] -> [{"itemGuid", "count"}] (receives resolved GUIDs)
    return [{"itemGuid": guid, "count": count} for guid, count in pairs]


def machine_recipe_element(guid, spec):
    element = {"machineRecipeGuid": guid, "blockGuid": spec["block"], "time": spec["time"],
               "inputItems": [{"itemGuid": g, "count": c, "isRemain": False} for g, c in spec["inputItems"]],
               "inputFluids": [{"fluidGuid": g, "amount": a} for g, a in spec["inputFluids"]],
               "outputItems": [{"itemGuid": g, "count": c, "percent": 1} for g, c in spec["outputItems"]],
               "outputFluids": [{"fluidGuid": g, "amount": a} for g, a in spec["outputFluids"]],
               "initialUnlocked": False, "levelInheritance": spec["levelInheritance"]}
    return element


def research_element(guid, spec):
    return {"researchNodeGuid": guid, "researchNodeName": spec["name"], "researchNodeDescription": spec["description"],
            "consumeItems": [{"itemGuid": g, "itemCount": c} for g, c in spec["consumeItems"]],
            "prevResearchNodeGuids": spec["prev"], "clearedActions": spec["clearedActions"],
            "graphViewSettings": {"UIPosition": [0, 0], "UIScale": [1, 1, 1], "IconItem": spec["iconItem"]}}


def set_path(root, dotted, value):
    # "blockParam.inputTankCount" のようなドット区切りパスへ代入（数字は配列添字）
    # Assign to a dotted path such as "blockParam.inputTankCount" (digits index arrays)
    keys = dotted.split(".")
    node = root
    for key in keys[:-1]:
        node = node[int(key)] if key.isdigit() else node[key]
    last = keys[-1]
    if last.isdigit():
        node[int(last)] = value
    else:
        node[last] = value


def get_path(root, dotted):
    node = root
    for key in dotted.split("."):
        node = node[int(key)] if key.isdigit() else node[key]
    return node


def upsert(array, guid_key, element, keep_paths):
    # 既存要素は置き換えるが、master-refine が再計算する値（keep_paths）は既存側を残す
    # Replace an existing element but keep values master-refine recalculates (keep_paths)
    for index, existing in enumerate(array):
        if existing[guid_key] == element[guid_key]:
            for path in keep_paths:
                set_path(element, path, get_path(existing, path))
            array[index] = element
            return
    array.append(element)
```

`$B/apply/localization_rows.py`:

```python
"""mod の localization.csv の行をキー単位で追加・更新・削除する。
Add, update, and remove rows of the mod localization.csv by key."""
from master_io import load_localization, save_localization


class LocalizationRows:
    def __init__(self, mod_dir):
        self._mod_dir = mod_dir
        self._header, self._rows = load_localization(mod_dir)
        self._index = {row[0]: i for i, row in enumerate(self._rows)}

    def put(self, key, source_ja, english):
        # Source と japanese は日本語原文、english は英訳。他言語は空（english へフォールバック）
        # Source and japanese hold the Japanese text, english the translation; others stay empty (fall back to english)
        row = [""] * len(self._header)
        row[0] = key
        row[self._header.index("Source")] = source_ja
        row[self._header.index("english")] = english
        row[self._header.index("japanese")] = source_ja
        if key in self._index:
            old = self._rows[self._index[key]]
            # 原文が同じなら既存の他言語訳を残す
            # Keep existing translations for other languages when the source is unchanged
            if old[self._header.index("Source")] == source_ja:
                row = [new if new else prev for new, prev in zip(row, old)]
            self._rows[self._index[key]] = row
            return
        self._index[key] = len(self._rows)
        self._rows.append(row)

    def remove(self, key):
        self._rows = [row for row in self._rows if row[0] != key]
        self._index = {row[0]: i for i, row in enumerate(self._rows)}

    def save(self):
        save_localization(self._mod_dir, self._header, self._rows)
```

`$B/apply/apply_spec.py`:

```python
"""spec/*.json を名前解決してマスタJSONとローカライズCSVへ upsert する（何度流しても同じ結果）。
Resolve spec/*.json by name and upsert them into master JSON and the localization CSV (idempotent).

使い方 / Usage: python3 apply/apply_spec.py --mod-dir <mod dir> --spec-dir spec --ledger guids.json"""
import argparse
import json
from pathlib import Path

import element_builders as eb
from localization_rows import LocalizationRows
from master_io import GuidLedger, load_master, save_master
from resolver import NameResolver


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mod-dir", required=True)
    parser.add_argument("--spec-dir", required=True)
    parser.add_argument("--ledger", required=True)
    args = parser.parse_args()

    master = load_master(args.mod_dir)
    ledger = GuidLedger(args.ledger)
    resolver = NameResolver(master, ledger)
    loc = LocalizationRows(args.mod_dir)
    specs = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(Path(args.spec_dir).glob("*.json"))]

    # 宣言フェーズ: 全 spec の新規要素に Guid を割り当て、名前→Guid を引けるようにする
    # Declare phase: assign GUIDs to every new element so names resolve across specs
    for spec in specs:
        declare_all(spec, resolver)
    # 適用フェーズ: spec ファイル順に upsert・削除・部分更新を行う
    # Apply phase: upsert, remove, and patch in spec file order
    for spec in specs:
        apply_one(spec, master, resolver, loc)

    save_master(args.mod_dir, master)
    loc.save()
    ledger.save()
    print(f"applied {len(specs)} spec files; issued {len(ledger.issued)} new guids")


def declare_all(spec, r):
    for table in spec.get("stackTables", []):
        r.declare("stackTable", table["name"], table.get("guid"))
    for item in spec.get("items", []):
        r.declare("item", item["name"], item.get("guid"))
    for rename in spec.get("renameItems", []):
        r.rename("item", rename["guid"], rename["name"])
    for family in spec.get("levelFamilies", []):
        r.declare("family", family["name"], family.get("guid"))
    for module in spec.get("modules", []):
        r.declare("module", module["name"], module.get("guid"))
    for recipe in spec.get("machineRecipes", []):
        r.declare("recipe", recipe["key"], recipe.get("guid"))
    for block in spec.get("blocks", []):
        r.declare("block", block["name"], block.get("guid"))
    for node in spec.get("research", []):
        r.declare("research", node["name"], node.get("guid"))


def apply_one(spec, master, r, loc):
    items = master["items"]
    for guid in spec.get("removeFluids", []):
        master["fluids"]["data"] = [f for f in master["fluids"]["data"] if f["fluidGuid"] != guid]
        loc.remove(f"fluid.{guid}.name")
    for guid in spec.get("removeMachineRecipes", []):
        master["machineRecipes"]["data"] = [m for m in master["machineRecipes"]["data"] if m["machineRecipeGuid"] != guid]
    for table in spec.get("stackTables", []):
        element = {"tableGuid": r.resolve(f"@stackTable:{table['name']}"), "name": table["name"], "stackCounts": table["stackCounts"]}
        eb.upsert(items["itemStackLevelTables"], "tableGuid", element, [])
    for item in spec.get("items", []):
        guid = r.resolve(f"@item:{item['name']}")
        # 指定が無いアイテムは既存の Default 表（上限100）を使う
        # Items without a stackTable use the existing Default table (stack 100)
        stack_table = r.resolve(item["stackTable"]) if "stackTable" in item else eb.DEFAULT_STACK_TABLE
        eb.upsert(items["data"], "itemGuid", eb.item_element(guid, item, stack_table), ["sortPriority"])
        loc.put(f"item.{guid}.name", item["name"], item["en"])
    for rename in spec.get("renameItems", []):
        target = next(e for e in items["data"] if e["itemGuid"] == rename["guid"])
        target["name"] = rename["name"]
        loc.put(f"item.{rename['guid']}.name", rename["name"], rename["en"])
    for patch in spec.get("patchItems", []):
        target = next(e for e in items["data"] if e["name"] == patch["name"])
        for path, value in patch["set"].items():
            eb.set_path(target, path, r.resolve(value))
    for family in spec.get("levelFamilies", []):
        levels = [r.resolve(f"@item:{name}") for name in family["levels"]]
        element = {"familyGuid": r.resolve(f"@family:{family['name']}"), "name": family["name"],
                   "baseItemGuid": levels[0], "levelItemGuids": levels}
        eb.upsert(items.setdefault("levelFamilies", []), "familyGuid", element, [])
    for module in spec.get("modules", []):
        element = {"moduleGuid": r.resolve(f"@module:{module['name']}"), "name": module["name"],
                   "itemGuid": r.resolve(f"@item:{module['item']}"), "effectAxis": module["effectAxis"],
                   "tier": module["tier"], "effectValue": module["effectValue"], "tradeoffValue": module["tradeoffValue"]}
        eb.upsert(items.setdefault("modules", []), "moduleGuid", element, [])
    for recipe in spec.get("machineRecipes", []):
        element = eb.machine_recipe_element(r.resolve(f"@recipe:{recipe['key']}"), r.resolve(recipe))
        eb.upsert(master["machineRecipes"]["data"], "machineRecipeGuid", element, [])
    for block in spec.get("blocks", []):
        guid = r.resolve(f"@block:{block['name']}")
        element = {"blockGuid": guid, "name": block["name"]}
        element.update(r.resolve(block["element"]))
        eb.upsert(master["blocks"]["data"], "blockGuid", element, ["sortPriority"])
        loc.put(f"block.{guid}.name", block["name"], block["en"])
    for patch in spec.get("patchBlocks", []):
        target = next(b for b in master["blocks"]["data"] if b["name"] == patch["name"])
        for path, value in patch["set"].items():
            eb.set_path(target, path, r.resolve(value))
    for node in spec.get("research", []):
        guid = r.resolve(f"@research:{node['name']}")
        eb.upsert(master["research"]["data"], "researchNodeGuid", eb.research_element(guid, r.resolve(node)), ["graphViewSettings.UIPosition"])
        loc.put(f"research.{guid}.name", node["name"], node["en"])
        loc.put(f"research.{guid}.description", node["description"], node["descEn"])
    for patch in spec.get("patchResearch", []):
        target = next(n for n in master["research"]["data"] if n["researchNodeName"] == patch["name"])
        for path, value in patch["set"].items():
            eb.set_path(target, path, r.resolve(value))
        if "descEn" in patch:
            loc.put(f"research.{target['researchNodeGuid']}.description", target["researchNodeDescription"], patch["descEn"])
    for path, value in spec.get("setMaster", {}).items():
        eb.set_path(master, path, r.resolve(value))
    for file_name, keys in spec.get("deleteTopLevelKeys", {}).items():
        for key in keys:
            master[file_name].pop(key, None)


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 照合スクリプト2ファイルを書く**

`$B/verify_checks.py`:

```python
"""マスタJSONが sim/params.py と同じ値・同じ構造かを調べる検査群。各関数は失敗理由の文字列リストを返す。
Checks that master JSON matches sim/params.py; each function returns a list of failure messages."""
from params import (CLASS_PARAMS, DIE_TABLES, DIES_PER_WAFER, FILTER_CAPACITY, FILTER_Q, LINE_SI_PER_S, MODULES,
                    POLLUTION, POWER, PROMOTION_CAP, QUALITY_REDUCTION_CAP, RECIPES, STACK_LIMITS)
from timeline import RESEARCH

ULTRA_PURE_WATER = "019eeaa7-45b6-7733-bd3b-a043be2ccc21"
RENAMED_ITEMS = ["019e3c14-b270-716f-84e5-c0b06a97592b", "019e3c25-78f1-70ec-be1b-2c2cdd9297bb",
                 "019e3b02-b9fd-73c7-9538-ef13b0f755f1", "019e3c26-9808-765d-ace7-ea1845a7e072"]
OLD_TEMP_COST = {("60daab46-bce2-4d09-b895-d872bef3f9c3", 5), ("54ccd10a-a6c1-4ee5-99d5-b61fea7e92a9", 2)}
# sim のレシピ名 → spec のレシピキー / sim の機械名 → ブロック名
# Sim recipe name -> spec recipe key / sim machine name -> block name
PARAM_TO_RECIPE = {"シリコンウェハ": ["ウェハ切断"], "トランジスタ設置済みウェハ": ["成膜"], "EUV層": ["EUV1層", "EUV2層", "EUV3層"],
                   "ダイ": ["ダイシング"], "ICチップ": ["パッケージング"]}
MACHINE_TO_BLOCK = {"EUV露光装置": "EUV露光式半導体製造装置", "空気清浄機": "クリーンルーム空気清浄機"}
# クラス行で sim と照合する値（Part B の cleanRoom.json thresholds[]）
# Class-row values compared with the sim (Part B's cleanRoom.json thresholds[])
CLASS_VALUE_KEYS = ("qualityMultiplier", "layerFailRate", "dieLossRate")
# 専用機ごとの品質モジュール可否（Part C の blockParam.allowQualityModule）
# Quality module permission per dedicated machine (Part C's blockParam.allowQualityModule)
QUALITY_ALLOWED = {"成膜装置": True, "EUV露光式半導体製造装置": True, "ダイシング装置": True,
                   "ウェハ切断機": False, "パッケージング装置": False}


def near(a, b):
    return abs(a - b) < 1e-9


def check_upstream_ratio(ix, recipes):
    # 基準ライン1本の 粉砕:炉:化学 台数を実マスタのレシピ時間から出し、1段だけ2倍かを見る
    # Count crusher:furnace:chemical machines for one line from the real recipes; exactly one stage doubled
    metal = ix.recipe(recipes["高純度シリコン"])
    furnace = ix.recipe(recipes["金属シリコン"])
    sand = ix.recipe(recipes["砂"])
    chem = LINE_SI_PER_S * metal["time"] / metal["outputItems"][0]["count"]
    metal_per_s = LINE_SI_PER_S * metal["inputItems"][0]["count"]
    furn = metal_per_s * furnace["time"] / furnace["outputItems"][0]["count"]
    sand_per_s = metal_per_s * ix.input_count(furnace, "砂") / furnace["outputItems"][0]["count"]
    crush = sand_per_s * sand["time"] / sand["outputItems"][0]["count"]
    counts = sorted([crush, furn, chem])
    ok = near(counts[0], counts[1]) and near(counts[2], counts[0] * 2)
    return [] if ok else [f"upstream ratio crusher {crush} : furnace {furn} : chemical {chem} is not x:x:2x"]


def check_recipes_match_params(ix, recipes):
    fails = []
    for name, spec in RECIPES.items():
        for key in PARAM_TO_RECIPE.get(name, [name]):
            r = ix.recipe(recipes[key])
            block = MACHINE_TO_BLOCK.get(spec["machine"], spec["machine"])
            if ix.block_name(r["blockGuid"]) != block or not near(r["time"], spec["time"]):
                fails.append(f"recipe {key}: block/time differ from params")
            inputs = [(ix.item_name(i["itemGuid"]), i["count"]) for i in r["inputItems"]]
            inputs += [(ix.fluid_name(f["fluidGuid"]), f["amount"]) for f in r["inputFluids"]]
            for item, count in spec.get("in", {}).items():
                if not any(n.startswith(item) and c == count for n, c in inputs):
                    fails.append(f"recipe {key}: input {item}x{count} missing")
            ipa = sum(f["amount"] for f in r["inputFluids"] if ix.fluid_name(f["fluidGuid"]) == "イソプロピルアルコール")
            if "ipa" in spec and ipa != spec["ipa"]:
                fails.append(f"recipe {key}: IPA {ipa} != {spec['ipa']}")
            out = r["outputItems"][0]["count"] if r["outputItems"] else r["outputFluids"][0]["amount"]
            if out != spec["out"]:
                fails.append(f"recipe {key}: output {out} != {spec['out']}")
    return fails


def check_power_and_filter(ix):
    fails = []
    for machine, power in POWER.items():
        block = ix.block(MACHINE_TO_BLOCK.get(machine, machine))
        if block["blockParam"]["requiredPower"] != power:
            fails.append(f"power {machine}: {block['blockParam']['requiredPower']} != {power}")
    air = ix.block("クリーンルーム空気清浄機")["blockParam"]
    if air["removalVolumePerSecond"] != FILTER_Q or air["filterCapacity"] != FILTER_CAPACITY:
        fails.append("air filter q/capacity differ from params")
    return fails


def check_cleanroom_values(ix, clean, recipes):
    fails = []
    room = clean["data"][0]
    if any(not near(room["pollution"][k], v) for k, v in POLLUTION.items()):
        fails.append("pollution coefficients differ from params")
    for row in room["thresholds"]:
        expected = CLASS_PARAMS[row["className"]]
        if any(not near(row[k], expected[k]) for k in CLASS_VALUE_KEYS):
            fails.append(f"class {row['className']} draw values differ from params")
    return fails + check_role_rows(ix, room, recipes)


def check_role_rows(ix, room, recipes):
    # 役割表（成膜1・EUV3・ダイシング1）が工程レシピと G1/Lv1 品を指し、上限・ダイ数・ダイ表が params と同じ
    # Role rows (1 deposition, 3 EUV, 1 dicing) point at the stage recipes and G1/Lv1 items with the params values
    fails = []
    draws, exposures, dicings = room["waferGradeDraws"], room["layerExposures"], room["dicings"]
    euv = {recipes[k]: f"{n}G1" for k, n in (("EUV1層", "1層パターンウェハ"), ("EUV2層", "2層パターンウェハ"), ("EUV3層", "完成パターンウェハ"))}
    if [(d["machineRecipeGuid"], ix.item_name(d["gradedWaferItemGuid"])) for d in draws] != [(recipes["成膜"], "トランジスタ設置済みウェハG1")]:
        fails.append("waferGradeDraws is not the single 成膜 row")
    if {e["machineRecipeGuid"]: ix.item_name(e["layeredWaferItemGuid"]) for e in exposures} != euv or len(exposures) != 3 \
            or any(ix.item_name(e["failedWaferItemGuid"]) != "失敗したパターンウェハ" for e in exposures):
        fails.append("layerExposures are not the three EUV rows")
    if [(d["machineRecipeGuid"], ix.item_name(d["dieItemGuid"]), ix.item_name(d["failedDieItemGuid"])) for d in dicings] \
            != [(recipes["ダイシング"], "シリコンダイLv1", "失敗したシリコンダイ")]:
        fails.append("dicings is not the single ダイシング row")
    if any(not near(d["maxPromotionChance"], PROMOTION_CAP) for d in draws) \
            or any(not near(e["maxFailReduction"], QUALITY_REDUCTION_CAP) for e in exposures) \
            or any(not near(d["maxLossReduction"], QUALITY_REDUCTION_CAP) or d["diesPerWafer"] != DIES_PER_WAFER for d in dicings):
        fails.append("role-row caps / dies per wafer differ from params")
    tables = [DIE_TABLES[grade] for grade in sorted(DIE_TABLES)]
    if any([t["levelRates"] for t in d["gradeDieTables"]] != tables for d in dicings):
        fails.append("dicing gradeDieTables differ from params DIE_TABLES")
    return fails


def check_quality_module_permission(ix):
    return [f"{name}: allowQualityModule != {allowed}" for name, allowed in QUALITY_ALLOWED.items()
            if ix.block(name)["blockParam"]["allowQualityModule"] != allowed]


def check_modules(items):
    fails = []
    for m in items["modules"]:
        effect, tradeoff = MODULES[m["effectAxis"]][m["tier"] - 1]
        if not near(m["effectValue"], effect) or not near(m["tradeoffValue"], tradeoff):
            fails.append(f"module {m['name']} values differ from params")
    if len(items["modules"]) != 20:
        fails.append(f"modules count {len(items['modules'])} != 20")
    if [level["stackLimit"] for level in items["moduleSlotStackLimitLevels"]] != STACK_LIMITS:
        fails.append("moduleSlotStackLimitLevels differ from params")
    return fails


def check_research_costs(ix, research):
    fails = []
    for node in RESEARCH:
        target = next((n for n in research["data"] if n["researchNodeName"] == node["name"]), None)
        if target is None:
            fails.append(f"research {node['name']} missing")
            continue
        cost = {ix.item_name(c["itemGuid"]): c["itemCount"] for c in target["consumeItems"]}
        expected = {"ICチップLv1": node["chips"], "鋼鉄フレーム": node["frames"], "コンピュータ": node["computers"],
                    "制御モータ": node["motors"]}
        if cost != {k: v for k, v in expected.items() if v}:
            fails.append(f"research {node['name']} cost {cost} != {expected}")
    return fails
```

`$B/verify_master.py`:

```python
"""マスタが sim/params.py の値どおりで、Part D の構造要件を満たすかを検査する。1件でも落ちたら終了コード1。
Verify the master matches sim/params.py and Part D structure; exit 1 on any failure.

使い方 / Usage: python3 verify_master.py --mod-dir <mod dir> --ledger guids.json"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / "sim"))
sys.path.insert(0, str(Path(__file__).parent / "apply"))
import verify_checks as vc  # noqa: E402
from master_io import load_localization, load_master  # noqa: E402


class MasterIndex:
    def __init__(self, master):
        self._items = {e["itemGuid"]: e["name"] for e in master["items"]["data"]}
        self._fluids = {e["fluidGuid"]: e["name"] for e in master["fluids"]["data"]}
        self._blocks = {e["blockGuid"]: e for e in master["blocks"]["data"]}
        self._recipes = {e["machineRecipeGuid"]: e for e in master["machineRecipes"]["data"]}

    def item_name(self, guid):
        return self._items[guid]

    def fluid_name(self, guid):
        return self._fluids[guid]

    def block_name(self, guid):
        return self._blocks[guid]["name"]

    def block(self, name):
        return next(b for b in self._blocks.values() if b["name"] == name)

    def recipe(self, guid):
        return self._recipes[guid]

    def input_count(self, recipe, item_name):
        return next(i["count"] for i in recipe["inputItems"] if self._items[i["itemGuid"]] == item_name)


def check_structure(mod_dir, master, ledger):
    fails = []
    # 超純水が全マスタから消えている
    # Ultra-pure water is gone from every master file
    for path in (Path(mod_dir) / "master").glob("*.json"):
        if vc.ULTRA_PURE_WATER in path.read_text(encoding="utf-8"):
            fails.append(f"ultra-pure water guid remains in {path.name}")
    # 仮コスト（砕いた石材5＋レンガ2）のブロックが残っていない
    # No block keeps the placeholder cost (crushed stone 5 + brick 2)
    for block in master["blocks"]["data"]:
        if {(c["itemGuid"], c["count"]) for c in block["requiredItems"]} == vc.OLD_TEMP_COST:
            fails.append(f"block {block['name']} still has the placeholder cost")
    # levelFamilies は11ファミリー×5レベル（ウェハ4・ダイ・チップ・モジュール4・GPU）
    # levelFamilies hold 11 families of 5 levels (4 wafers, die, chip, 4 modules, GPU)
    families = master["items"]["levelFamilies"]
    if len(families) < 11 or any(len(f["levelItemGuids"]) != 5 or f["levelItemGuids"][0] != f["baseItemGuid"] for f in families):
        fails.append("levelFamilies are not 11 families of 5 levels with the base first")
    # 旧チップ抽選（chipDraws）が残っていない（Part B の撤去が前提。旧 EUV レシピを参照するため）
    # The old chip draw (chipDraws) is gone (Part B removes it; it references the deleted EUV recipe)
    if "chipDraws" in master["cleanRoom"]["data"][0]:
        fails.append("cleanRoom.json still has chipDraws (Part B removal missing)")
    # ダイと失敗ダイのスタック上限が 1ウェハ分×2（生産性の追加1セット）以上
    # Dies and failed dies stack to at least two wafers' worth (one productivity extra set)
    tables = {t["tableGuid"]: min(t["stackCounts"]) for t in master["items"]["itemStackLevelTables"]}
    for item in master["items"]["data"]:
        if item["name"].startswith("シリコンダイ") or item["name"] == "失敗したシリコンダイ":
            if tables[item["stackLevelTableGuid"]] < vc.DIES_PER_WAFER * 2:
                fails.append(f"{item['name']} stacks below {vc.DIES_PER_WAFER * 2}")
    # ICチップLv1 を研究素材にするノードが7つで、合計が timeline.RESEARCH と同じ
    # Seven research nodes consume IC chip Lv1, totalling the same as timeline.RESEARCH
    chip = "f6dd7835-0845-4877-87d0-b719d037ae7d"
    total = sum(node["chips"] for node in vc.RESEARCH)
    uses = [c["itemCount"] for n in master["research"]["data"] for c in n["consumeItems"] if c["itemGuid"] == chip]
    if len(uses) != 7 or sum(uses) != total:
        fails.append(f"IC chip Lv1 research use {len(uses)} nodes / {sum(uses)} != 7 / {total}")
    return fails + check_localization(mod_dir, ledger)


def check_localization(mod_dir, ledger):
    # Part D が足した・改名した要素に、Guid から導いたキーの行がある（他部の要素は見ない）
    # Elements Part D added or renamed have rows keyed by their GUIDs (other parts are out of scope)
    _, rows = load_localization(mod_dir)
    keys = {row[0] for row in rows}
    wanted = [f"item.{g}.name" for g in vc.RENAMED_ITEMS]
    for key, guid in ledger.items():
        kind = key.split(":", 1)[0]
        if kind in ("item", "block"):
            wanted.append(f"{kind}.{guid}.name")
        if kind == "research":
            wanted += [f"research.{guid}.name", f"research.{guid}.description"]
    return [f"localization row missing: {k}" for k in wanted if k not in keys]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mod-dir", required=True)
    parser.add_argument("--ledger", required=True)
    args = parser.parse_args()
    master = load_master(args.mod_dir)
    ledger = json.loads(Path(args.ledger).read_text(encoding="utf-8"))
    recipes = {k.split(":", 1)[1]: v for k, v in ledger.items() if k.startswith("recipe:")}
    ix = MasterIndex(master)

    results = {
        "structure": check_structure(args.mod_dir, master, ledger),
        "upstream ratio": vc.check_upstream_ratio(ix, recipes),
        "recipes = params": vc.check_recipes_match_params(ix, recipes),
        "power / filter = params": vc.check_power_and_filter(ix),
        "cleanRoom = params": vc.check_cleanroom_values(ix, master["cleanRoom"], recipes),
        "quality module permission": vc.check_quality_module_permission(ix),
        "modules = params": vc.check_modules(master["items"]),
        "research = params": vc.check_research_costs(ix, master["research"]),
    }
    for name, fails in results.items():
        print(f"{'PASS' if not fails else 'FAIL'} {name}")
        for fail in fails:
            print(f"  - {fail}")
    sys.exit(1 if any(results.values()) else 0)


if __name__ == "__main__":
    main()
```

- [ ] **Step 3: 共有済み Guid で台帳を作る**

`$B/guids.json`:

```json
{
  "research:品質モジュール": "01a167f3-bd6b-70c9-be6e-52e41907b21f",
  "research:省エネモジュール": "01a167f3-bd6e-7745-84c0-0e1cf105c2cc",
  "research:生産性モジュール": "01a167f3-bd71-707c-b8e6-615063559cc1",
  "research:GPU設計": "01a167f3-bd74-73f8-8c43-1869db833299",
  "research:モジュールスタック拡張I": "01a167f3-bd77-7013-b982-5f4cdf3c86a9",
  "research:モジュールスタック拡張II": "01a167f3-bd7a-75c4-a266-8b9a4d5d1900",
  "family:品質モジュール": "01a167f3-bd7c-72be-ac56-e45d70759c32",
  "family:速度モジュール": "01a167f3-bd7f-76f2-b2e3-6452c966fd02",
  "family:省エネモジュール": "01a167f3-bd82-764f-809d-f64b92c66667",
  "family:生産性モジュール": "01a167f3-bd85-72fb-a518-c298b8072dae",
  "recipe:品質モジュール": "01a167f3-bd88-747d-b30e-13f23f169ce4",
  "recipe:速度モジュール": "01a167f3-bd8b-7732-badd-8c1a011f7c26",
  "recipe:省エネモジュール": "01a167f3-bd8e-7525-af33-5cce09108374",
  "recipe:生産性モジュール": "01a167f3-bd90-7307-91a6-e45243f99379",
  "recipe:GPU": "01a167f3-bd94-76d2-ada6-617fdbf3d9b3",
  "item:品質モジュールLv1": "01a167f3-bd96-73a2-a0ce-06ba261e109d",
  "item:速度モジュールLv1": "01a167f3-bd9a-72ec-8b7a-cd188d0d5272",
  "item:省エネモジュールLv1": "01a167f3-bd9d-74b4-b7ff-91e6f9e3bdb0",
  "item:生産性モジュールLv1": "01a167f3-bda0-74f7-8002-1407403a36a2"
}
```

- [ ] **Step 4: 空の spec で適用し、マスタとローカライズが1バイトも変わらない（書式の往復一致）ことを確かめる**

Run:
```bash
cd $B && mkdir -p /tmp/empty-spec && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir /tmp/empty-spec --ledger guids.json && git -C $MW status --short server_v8
```
Expected: `applied 0 spec files; issued 0 new guids` の後、`git status` の出力が空

- [ ] **Step 5: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/apply design/tier9-semiconductor-balance/verify_checks.py design/tier9-semiconductor-balance/verify_master.py design/tier9-semiconductor-balance/guids.json
git -C $MW commit -m "design: Tier9 半導体のマスタ適用・照合スクリプトを追加する"
```

### Task D3: 上流・IPA・鋼鉄フレーム・素材レシピ（超純水の削除）

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/spec/01_upstream_materials.json`
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`（金属シリコン・鋼鉄フレームを追加）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/fluids.json`（超純水を削除）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（化学プラントを液体入力2・出力1へ。金属シリコンを入れるアイテム搬入口を足す）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`（9本追加）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `moorestech_master/design/tier9-semiconductor-balance/guids.json`（発行分の追記）

**Interfaces:**
- Consumes: D2 の `apply/apply_spec.py`。Part C の機械レシピ必須キー `levelInheritance`（`None` / `Inherit` / `InheritUpToUnlocked`。上流・素材のレシピはすべて `None`）
- Produces: レシピキー（台帳 `recipe:<キー>`）`砂` `金属シリコン` `高純度シリコン` `IPA` `鋼鉄フレーム` `ICチップ基板` `クリーンルームフィルター` `失敗ウェハ再生` `失敗ダイ再生`。アイテム `金属シリコン` `鋼鉄フレーム`。化学プラントの液体タンク: 入力0=ナフサ（既存コネクタ (3,1,0)）、入力1=水（新コネクタ (2,1,0)）、出力0=IPA（新コネクタ (2,1,10)、+z 向き）。化学プラントのアイテム搬入口（新コネクタ (3,0,0)、−z 向き。現行の化学プラントはアイテム出口 (3,0,10) だけで、金属シリコンを入れる口が無い）

- [ ] **Step 1: spec 01 を書く**

`$B/spec/01_upstream_materials.json`:

```json
{
  "items": [
    {"name": "金属シリコン", "en": "Metallurgical Silicon", "sortPriority": 1366},
    {"name": "鋼鉄フレーム", "en": "Steel Frame", "sortPriority": 1075}
  ],
  "removeFluids": [
    "019eeaa7-45b6-7733-bd3b-a043be2ccc21"
  ],
  "patchBlocks": [
    {"name": "化学プラント", "set": {"blockParam.inventoryConnectors.inputConnects": [{"connectorGuid": "@new:connector:化学プラント:itemIn", "offset": [3, 0, 0], "directions": [[0, 0, -1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}], "blockParam.inputTankCount": 2, "blockParam.outputTankCount": 1, "blockParam.fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "019eedb5-0a5e-7581-b6f1-1baa8dd3f301", "offset": [3, 1, 0], "directions": [[0, 0, -1]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}, {"connectorGuid": "@new:connector:化学プラント:inflow1", "offset": [2, 1, 0], "directions": [[0, 0, -1]], "option": {"flowCapacity": 600, "connectTankIndex": 1}}], "outflowConnects": [{"connectorGuid": "@new:connector:化学プラント:outflow0", "offset": [2, 1, 10], "directions": [[0, 0, 1]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}]}}}
  ],
  "machineRecipes": [
    {"key": "砂", "block": "@block:電気粉砕機", "time": 2, "levelInheritance": "None", "inputItems": [["@item:砕いた石材", 1]], "inputFluids": [], "outputItems": [["@item:砂", 1]], "outputFluids": []},
    {"key": "金属シリコン", "block": "@block:電気炉", "time": 8, "levelInheritance": "None", "inputItems": [["@item:砂", 2], ["@item:石炭", 1]], "inputFluids": [], "outputItems": [["@item:金属シリコン", 1]], "outputFluids": []},
    {"key": "高純度シリコン", "block": "@block:化学プラント", "time": 4, "levelInheritance": "None", "inputItems": [["@item:金属シリコン", 1]], "inputFluids": [], "outputItems": [["@item:高純度シリコン", 1]], "outputFluids": []},
    {"key": "IPA", "block": "@block:化学プラント", "time": 2, "levelInheritance": "None", "inputItems": [], "inputFluids": [["@fluid:ナフサ", 5], ["@fluid:水", 15]], "outputItems": [], "outputFluids": [["@fluid:イソプロピルアルコール", 20]]},
    {"key": "鋼鉄フレーム", "block": "@block:電気汎用工作装置", "time": 6, "levelInheritance": "None", "inputItems": [["@item:鋼鉄板", 3], ["@item:プラスチック", 2]], "inputFluids": [], "outputItems": [["@item:鋼鉄フレーム", 1]], "outputFluids": []},
    {"key": "ICチップ基板", "block": "@block:電気汎用工作装置", "time": 3, "levelInheritance": "None", "inputItems": [["@item:プラスチック", 1], ["@item:銅板", 1]], "inputFluids": [], "outputItems": [["@item:ICチップ基板", 1]], "outputFluids": []},
    {"key": "クリーンルームフィルター", "block": "@block:電気汎用工作装置", "time": 4, "levelInheritance": "None", "inputItems": [["@item:プラスチック", 4], ["@item:鉄のワイヤー", 10]], "inputFluids": [], "outputItems": [["@item:クリーンルームフィルター", 1]], "outputFluids": []},
    {"key": "失敗ウェハ再生", "block": "@block:電気粉砕機", "time": 4, "levelInheritance": "None", "inputItems": [["@item:失敗したパターンウェハ", 1]], "inputFluids": [], "outputItems": [["@item:高純度シリコン", 5]], "outputFluids": []},
    {"key": "失敗ダイ再生", "block": "@block:電気粉砕機", "time": 4, "levelInheritance": "None", "inputItems": [["@item:失敗したシリコンダイ", 60]], "inputFluids": [], "outputItems": [["@item:高純度シリコン", 1]], "outputFluids": []}
  ]
}
```

- [ ] **Step 2: 適用する**

Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`
Expected: `applied 1 spec files; issued 14 new guids`（アイテム2・レシピ9・コネクタ3＝液体2・アイテム1）

- [ ] **Step 3: 結果を確かめる**

Run:
```bash
grep -c "019eeaa7-45b6" $MOD/master/*.json | grep -v ":0$"; echo "---"
cd $B && python3 -c "
import json; b=json.load(open('$MOD/master/blocks.json'))
c=next(e for e in b['data'] if e['name']=='化学プラント')['blockParam']
print(c['inputTankCount'], c['outputTankCount'], len(c['fluidInventoryConnectors']['inflowConnects']), len(c['fluidInventoryConnectors']['outflowConnects']), [i['offset'] for i in c['inventoryConnectors']['inputConnects']])"
```
Expected: `---` の前に何も出ない（超純水の Guid が全マスタから消えた）。続けて `2 1 2 1 [[3, 0, 0]]`

- [ ] **Step 4: 同じ spec をもう一度適用して差分が出ないことを確かめる**

Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json && git -C $MW diff --stat | tail -1`
Expected: `applied 1 spec files; issued 0 new guids`。diff の行数は Step 2 直後と同じ

- [ ] **Step 5: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/spec/01_upstream_materials.json design/tier9-semiconductor-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: Tier9 上流（砂・金属シリコン・高純度シリコン・IPA）と鋼鉄フレームを入れ、超純水を消す"
```

### Task D4: ウェハ・ダイ・チップの変種と levelFamilies

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/spec/02_wafers_dies_chips.json`
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`（22件追加・4件改名・levelFamilies 6件・スタック表「ダイ」[600] を足してダイ5種と失敗したシリコンダイに付ける）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `moorestech_master/design/tier9-semiconductor-balance/guids.json`

**Interfaces:**
- Consumes: D2 の適用スクリプト。items.json 直下の `levelFamilies`（既存スキーマ items.yml のキー `familyGuid` `name` `baseItemGuid` `levelItemGuids`。現行の v8 マスタには無く、本タスクで初めて入れる）。Part B の起動時検証（`CleanRoomYieldTableRules`）が求める「ダイと失敗ダイのスタック上限 ≥ diesPerWafer×2」
- Produces: アイテム名（Part B の抽選とシナリオが使う）: `トランジスタ設置済みウェハG1〜G5` `1層パターンウェハG1〜G5` `2層パターンウェハG1〜G5` `完成パターンウェハG1〜G5` `シリコンダイLv1〜Lv5` `ICチップLv1〜Lv5`。ファミリー名 `トランジスタ設置済みウェハ` `1層パターンウェハ` `2層パターンウェハ` `完成パターンウェハ` `シリコンダイ` `ICチップ`（`levelItemGuids[0] == baseItemGuid` が G1/Lv1）。スタック表 `ダイ`（`stackCounts: [600]`＝1ウェハ300ダイ×生産性の追加1セット。Part B の起動時検証「ダイ・失敗ダイのスタック上限 ≥ diesPerWafer×2」を満たす）。改名4件は Guid 不変: トランジスタ設置済みウェハ→…G1（019e3c14-b270…）、N回積層パターンウェハ→1層パターンウェハG1（019e3c25…）、完成パターンウェハ→…G1（019e3b02…）、シリコンダイ→シリコンダイLv1（019e3c26…）

- [ ] **Step 1: spec 02 を書く**

`$B/spec/02_wafers_dies_chips.json`:

```json
{
  "stackTables": [
    {"name": "ダイ", "stackCounts": [600]}
  ],
  "items": [
    {"name": "トランジスタ設置済みウェハG2", "en": "Transistor Wafer G2", "sortPriority": 1402},
    {"name": "トランジスタ設置済みウェハG3", "en": "Transistor Wafer G3", "sortPriority": 1403},
    {"name": "トランジスタ設置済みウェハG4", "en": "Transistor Wafer G4", "sortPriority": 1404},
    {"name": "トランジスタ設置済みウェハG5", "en": "Transistor Wafer G5", "sortPriority": 1405},
    {"name": "1層パターンウェハG2", "en": "1-Layer Patterned Wafer G2", "sortPriority": 1412},
    {"name": "1層パターンウェハG3", "en": "1-Layer Patterned Wafer G3", "sortPriority": 1413},
    {"name": "1層パターンウェハG4", "en": "1-Layer Patterned Wafer G4", "sortPriority": 1414},
    {"name": "1層パターンウェハG5", "en": "1-Layer Patterned Wafer G5", "sortPriority": 1415},
    {"name": "2層パターンウェハG1", "en": "2-Layer Patterned Wafer G1", "sortPriority": 1416},
    {"name": "2層パターンウェハG2", "en": "2-Layer Patterned Wafer G2", "sortPriority": 1417},
    {"name": "2層パターンウェハG3", "en": "2-Layer Patterned Wafer G3", "sortPriority": 1418},
    {"name": "2層パターンウェハG4", "en": "2-Layer Patterned Wafer G4", "sortPriority": 1419},
    {"name": "2層パターンウェハG5", "en": "2-Layer Patterned Wafer G5", "sortPriority": 1420},
    {"name": "完成パターンウェハG2", "en": "Finished Patterned Wafer G2", "sortPriority": 1422},
    {"name": "完成パターンウェハG3", "en": "Finished Patterned Wafer G3", "sortPriority": 1423},
    {"name": "完成パターンウェハG4", "en": "Finished Patterned Wafer G4", "sortPriority": 1424},
    {"name": "完成パターンウェハG5", "en": "Finished Patterned Wafer G5", "sortPriority": 1425},
    {"name": "シリコンダイLv2", "en": "Silicon Die Lv2", "sortPriority": 1442, "stackTable": "@stackTable:ダイ"},
    {"name": "シリコンダイLv3", "en": "Silicon Die Lv3", "sortPriority": 1443, "stackTable": "@stackTable:ダイ"},
    {"name": "シリコンダイLv4", "en": "Silicon Die Lv4", "sortPriority": 1444, "stackTable": "@stackTable:ダイ"},
    {"name": "シリコンダイLv5", "en": "Silicon Die Lv5", "sortPriority": 1445, "stackTable": "@stackTable:ダイ"},
    {"name": "ICチップLv5", "en": "IC Chip Lv5", "sortPriority": 1465}
  ],
  "renameItems": [
    {"guid": "019e3c14-b270-716f-84e5-c0b06a97592b", "name": "トランジスタ設置済みウェハG1", "en": "Transistor Wafer G1"},
    {"guid": "019e3c25-78f1-70ec-be1b-2c2cdd9297bb", "name": "1層パターンウェハG1", "en": "1-Layer Patterned Wafer G1"},
    {"guid": "019e3b02-b9fd-73c7-9538-ef13b0f755f1", "name": "完成パターンウェハG1", "en": "Finished Patterned Wafer G1"},
    {"guid": "019e3c26-9808-765d-ace7-ea1845a7e072", "name": "シリコンダイLv1", "en": "Silicon Die Lv1"}
  ],
  "levelFamilies": [
    {"name": "トランジスタ設置済みウェハ", "levels": ["トランジスタ設置済みウェハG1", "トランジスタ設置済みウェハG2", "トランジスタ設置済みウェハG3", "トランジスタ設置済みウェハG4", "トランジスタ設置済みウェハG5"]},
    {"name": "1層パターンウェハ", "levels": ["1層パターンウェハG1", "1層パターンウェハG2", "1層パターンウェハG3", "1層パターンウェハG4", "1層パターンウェハG5"]},
    {"name": "2層パターンウェハ", "levels": ["2層パターンウェハG1", "2層パターンウェハG2", "2層パターンウェハG3", "2層パターンウェハG4", "2層パターンウェハG5"]},
    {"name": "完成パターンウェハ", "levels": ["完成パターンウェハG1", "完成パターンウェハG2", "完成パターンウェハG3", "完成パターンウェハG4", "完成パターンウェハG5"]},
    {"name": "シリコンダイ", "levels": ["シリコンダイLv1", "シリコンダイLv2", "シリコンダイLv3", "シリコンダイLv4", "シリコンダイLv5"]},
    {"name": "ICチップ", "levels": ["ICチップLv1", "ICチップLv2", "ICチップLv3", "ICチップLv4", "ICチップLv5"]}
  ],
  "patchItems": [
    {"name": "シリコンダイLv1", "set": {"stackLevelTableGuid": "@stackTable:ダイ"}},
    {"name": "失敗したシリコンダイ", "set": {"stackLevelTableGuid": "@stackTable:ダイ"}}
  ]
}
```

- [ ] **Step 2: 適用する**

Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`
Expected: `applied 2 spec files; issued 29 new guids`（アイテム22・ファミリー6・スタック表1）

- [ ] **Step 3: ファミリーを確かめる**

Run:
```bash
python3 -c "
import json; i=json.load(open('$MOD/master/items.json')); n={e['itemGuid']:e['name'] for e in i['data']}
for f in i['levelFamilies']: print(f['name'], [n[g] for g in f['levelItemGuids']][0], len(f['levelItemGuids']), f['levelItemGuids'][0]==f['baseItemGuid'])"
```
Expected:
```
トランジスタ設置済みウェハ トランジスタ設置済みウェハG1 5 True
1層パターンウェハ 1層パターンウェハG1 5 True
2層パターンウェハ 2層パターンウェハG1 5 True
完成パターンウェハ 完成パターンウェハG1 5 True
シリコンダイ シリコンダイLv1 5 True
ICチップ ICチップLv1 5 True
```

- [ ] **Step 4: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/spec/02_wafers_dies_chips.json design/tier9-semiconductor-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master/items.json server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: Tier9 ウェハ・ダイ・チップをグレード別アイテムにして levelFamilies で束ねる"
```

### Task D5: 専用機5種・工程レシピ・クリーンルームの値と建設コスト

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/spec/03_cleanroom_machines.json`
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`（専用機4種のブロックアイテム）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（専用機4種追加・EUV の値と prefab・クリーンルーム系6ブロック（ブロック・パネル・ドア・アイテムハッチ・パイプコネクタ・空気清浄機）の値とコスト・最上位の旧キー2つ削除）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`（工程7本追加・旧 EUV レシピ 3a13a5a9 削除）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/cleanRoom.json`（汚染係数・クラス別抽選値・役割表3種）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `moorestech_master/design/tier9-semiconductor-balance/guids.json`

**Interfaces:**
- Consumes:
  - D3・D4 のアイテム／レシピ名。D10 の prefab アドレス `Vanilla/Block/WaferSlicer` `Vanilla/Block/DepositionTool` `Vanilla/Block/EuvLithographyTool` `Vanilla/Block/DicingSaw` `Vanilla/Block/PackagingMachine`
  - Part B の cleanRoom.json: `data[0].thresholds[] {className, maxConcentration, requiredAirChangeRate, qualityMultiplier, layerFailRate, dieLossRate}`、役割表 `data[0].waferGradeDraws[] {machineRecipeGuid, gradedWaferItemGuid, maxPromotionChance}`・`data[0].layerExposures[] {machineRecipeGuid, layeredWaferItemGuid, failedWaferItemGuid, maxFailReduction}`・`data[0].dicings[] {machineRecipeGuid, dieItemGuid, failedDieItemGuid, diesPerWafer, maxLossReduction, gradeDieTables[] {levelRates}}`（Part B 完了時点のマスタでは3表とも空配列。旧 `chipDraws`・`maxChipLevel`・`downBinRate` は撤去済み）。Part B のレシピの約束: EUV の出力はちょうど `[次層ウェハ(基準), 失敗したパターンウェハ]` で同数・`Inherit`、ダイシングの出力はちょうど `[ダイLv1(基準)…ダイLvN, 失敗ダイ]` でどれも個数 `diesPerWafer`・`Inherit`・出力スロット N+1 以上、`gradeDieTables` は G1 から順に入力ウェハのグレード数だけ並ぶ
  - CleanRoomMachine の液体キー（Part B が VanillaMachine と同じ形で足した `inputTankCount` `outputTankCount` `innerTankCapacity` `fluidInventoryConnectors`）
  - Part C の `blockParam.allowQualityModule`（C の master 追従で EUV だけ true が入っている）と `levelInheritance`
  - Part A のブロック `クリーンルームパネル`（blockGuid `a069613c-bcb5-4b19-a956-1e264a23f81e`・CleanRoomWall・壁と同じ仮コストで入っている）と、ドア（1×2×1・prefab `Vanilla/Block/CleanRoomDoor`）
- Produces: ブロック名 `ウェハ切断機` `成膜装置` `EUV露光式半導体製造装置` `ダイシング装置` `パッケージング装置`（3×2×3・prefab は D10 のアドレス・アイテム入口 (1,0,0) −z・出口 (1,0,2) +z・IPA 入口 (0,0,1) −x・モジュール3スロット）。レシピキー `ウェハ切断` `成膜` `EUV1層` `EUV2層` `EUV3層` `ダイシング` `パッケージング`

値（spec 03 の中身）:

| ブロック | 電力 | 入力/出力スロット | 品質モジュール | 建設コスト |
|---|---|---|---|---|
| ウェハ切断機 | 80 | 1 / 1 | 不可 | 鋼鉄フレーム30・制御モータ10・コンピュータ5 |
| 成膜装置 | 150 | 1 / 1 | 可 | 鋼鉄フレーム50・制御モータ10・コンピュータ15 |
| EUV露光式半導体製造装置 | 250 | 1 / 2 | 可 | 鋼鉄フレーム70・制御モータ20・コンピュータ25 |
| ダイシング装置 | 120 | 1 / 6 | 可 | 鋼鉄フレーム60・制御モータ15・コンピュータ10 |
| パッケージング装置 | 60 | 2 / 1 | 不可 | 鋼鉄フレーム30・制御モータ10・コンピュータ10 |
| クリーンルームブロック・クリーンルームパネル | — | — | — | 鋼鉄板1・プラスチック1 |
| クリーンルームドア | — | — | — | 鋼鉄フレーム2・プラスチック4 |
| クリーンルームアイテムハッチ | — | — | — | 鋼鉄フレーム4・制御モータ2 |
| クリーンルームパイプコネクタ | — | — | — | 鋼鉄フレーム4・ゴム4 |
| クリーンルーム空気清浄機 | 150（q=5・容量20000） | — | — | 鋼鉄フレーム15・制御モータ5・プラスチック20 |

| レシピ | 機械 | 秒 | 入力 | IPA | 出力 | 継承 |
|---|---|---|---|---|---|---|
| ウェハ切断 | ウェハ切断機 | 8 | 高純度シリコン10 | 10 | シリコンウェハ1 | None |
| 成膜 | 成膜装置 | 10 | シリコンウェハ1 | 10 | トランジスタ設置済みウェハG1（グレードは Part B が抽選） | None |
| EUV1層/2層/3層 | EUV | 15 | 前段ウェハG1 | 5 | 次段ウェハG1・失敗したパターンウェハ（Part B が層の成否でどちらか1つにする） | Inherit（入力グレードを受け継ぐ。品質モジュールは失敗率を下げる） |
| ダイシング | ダイシング装置 | 18 | 完成パターンウェハG1 | 20 | シリコンダイLv1〜Lv5 各300・失敗したシリコンダイ300（並び＝出力スロット。実個数は Part B の抽選で合計300） | Inherit（入力グレードのダイ表で振り分ける。品質モジュールは損失率を下げる） |
| パッケージング | パッケージング装置 | 3 | シリコンダイLv1×10＋ICチップ基板1 | 1 | ICチップLv1 | Inherit（製作上限なし） |

品質モジュールの可否は、抽選に品質が効く成膜・EUV・ダイシングだけ可。ウェハ切断機とパッケージング装置は品質を使う抽選が無いので不可（Part C の効き先制限）。役割表の値は `sim/params.py`（`PROMOTION_CAP` `QUALITY_REDUCTION_CAP` `DIES_PER_WAFER` `DIE_TABLES`）と同じ。D1 Step 3 で params を変えたら、ここも同じ値で書く。

- [ ] **Step 1: spec 03 を書く**

`$B/spec/03_cleanroom_machines.json`:

```json
{
  "items": [
    {"name": "ウェハ切断機", "en": "Wafer Slicer", "sortPriority": 1385},
    {"name": "成膜装置", "en": "Deposition Tool", "sortPriority": 1395},
    {"name": "ダイシング装置", "en": "Dicing Saw", "sortPriority": 1435},
    {"name": "パッケージング装置", "en": "Packaging Machine", "sortPriority": 1455}
  ],
  "blocks": [
    {"name": "ウェハ切断機", "en": "Wafer Slicer", "element": {"blockType": "CleanRoomMachine", "blockSize": [3, 2, 3], "blockPrefabAddressablesPath": "Vanilla/Block/WaferSlicer", "openable": true, "blockParam": {"requiredPower": 80, "idlePowerRate": 0.2, "inputSlotCount": 1, "outputSlotCount": 1, "moduleSlotCount": 3, "allowQualityModule": false, "inventoryConnectors": {"inputConnects": [{"offset": [1, 0, 0], "directions": [[0, 0, -1]], "connectorGuid": "@new:connector:ウェハ切断機:itemIn", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}], "outputConnects": [{"offset": [1, 0, 2], "directions": [[0, 0, 1]], "connectorGuid": "@new:connector:ウェハ切断機:itemOut", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]}, "inputTankCount": 1, "outputTankCount": 0, "innerTankCapacity": 300, "fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "@new:connector:ウェハ切断機:fluidIn", "offset": [0, 0, 1], "directions": [[-1, 0, 0]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []}, "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20}, "initialUnlocked": false, "requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 30}, {"itemGuid": "@item:制御モータ", "count": 10}, {"itemGuid": "@item:コンピュータ", "count": 5}], "placementsPerCost": 1, "sortPriority": 1385, "itemGuid": "@item:ウェハ切断機", "category": "生産", "subCategory": "半導体"}},
    {"name": "成膜装置", "en": "Deposition Tool", "element": {"blockType": "CleanRoomMachine", "blockSize": [3, 2, 3], "blockPrefabAddressablesPath": "Vanilla/Block/DepositionTool", "openable": true, "blockParam": {"requiredPower": 150, "idlePowerRate": 0.2, "inputSlotCount": 1, "outputSlotCount": 1, "moduleSlotCount": 3, "allowQualityModule": true, "inventoryConnectors": {"inputConnects": [{"offset": [1, 0, 0], "directions": [[0, 0, -1]], "connectorGuid": "@new:connector:成膜装置:itemIn", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}], "outputConnects": [{"offset": [1, 0, 2], "directions": [[0, 0, 1]], "connectorGuid": "@new:connector:成膜装置:itemOut", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]}, "inputTankCount": 1, "outputTankCount": 0, "innerTankCapacity": 300, "fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "@new:connector:成膜装置:fluidIn", "offset": [0, 0, 1], "directions": [[-1, 0, 0]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []}, "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20}, "initialUnlocked": false, "requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 50}, {"itemGuid": "@item:制御モータ", "count": 10}, {"itemGuid": "@item:コンピュータ", "count": 15}], "placementsPerCost": 1, "sortPriority": 1395, "itemGuid": "@item:成膜装置", "category": "生産", "subCategory": "半導体"}},
    {"name": "ダイシング装置", "en": "Dicing Saw", "element": {"blockType": "CleanRoomMachine", "blockSize": [3, 2, 3], "blockPrefabAddressablesPath": "Vanilla/Block/DicingSaw", "openable": true, "blockParam": {"requiredPower": 120, "idlePowerRate": 0.2, "inputSlotCount": 1, "outputSlotCount": 6, "moduleSlotCount": 3, "allowQualityModule": true, "inventoryConnectors": {"inputConnects": [{"offset": [1, 0, 0], "directions": [[0, 0, -1]], "connectorGuid": "@new:connector:ダイシング装置:itemIn", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}], "outputConnects": [{"offset": [1, 0, 2], "directions": [[0, 0, 1]], "connectorGuid": "@new:connector:ダイシング装置:itemOut", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]}, "inputTankCount": 1, "outputTankCount": 0, "innerTankCapacity": 300, "fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "@new:connector:ダイシング装置:fluidIn", "offset": [0, 0, 1], "directions": [[-1, 0, 0]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []}, "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20}, "initialUnlocked": false, "requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 60}, {"itemGuid": "@item:制御モータ", "count": 15}, {"itemGuid": "@item:コンピュータ", "count": 10}], "placementsPerCost": 1, "sortPriority": 1435, "itemGuid": "@item:ダイシング装置", "category": "生産", "subCategory": "半導体"}},
    {"name": "パッケージング装置", "en": "Packaging Machine", "element": {"blockType": "CleanRoomMachine", "blockSize": [3, 2, 3], "blockPrefabAddressablesPath": "Vanilla/Block/PackagingMachine", "openable": true, "blockParam": {"requiredPower": 60, "idlePowerRate": 0.2, "inputSlotCount": 2, "outputSlotCount": 1, "moduleSlotCount": 3, "allowQualityModule": false, "inventoryConnectors": {"inputConnects": [{"offset": [1, 0, 0], "directions": [[0, 0, -1]], "connectorGuid": "@new:connector:パッケージング装置:itemIn", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}], "outputConnects": [{"offset": [1, 0, 2], "directions": [[0, 0, 1]], "connectorGuid": "@new:connector:パッケージング装置:itemOut", "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]}, "inputTankCount": 1, "outputTankCount": 0, "innerTankCapacity": 300, "fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "@new:connector:パッケージング装置:fluidIn", "offset": [0, 0, 1], "directions": [[-1, 0, 0]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []}, "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20}, "initialUnlocked": false, "requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 30}, {"itemGuid": "@item:制御モータ", "count": 10}, {"itemGuid": "@item:コンピュータ", "count": 10}], "placementsPerCost": 1, "sortPriority": 1455, "itemGuid": "@item:パッケージング装置", "category": "生産", "subCategory": "半導体"}}
  ],
  "removeMachineRecipes": [
    "3a13a5a9-d254-40f9-be93-e24c9ebfdfaf"
  ],
  "machineRecipes": [
    {"key": "ウェハ切断", "block": "@block:ウェハ切断機", "time": 8, "levelInheritance": "None", "inputItems": [["@item:高純度シリコン", 10]], "inputFluids": [["@fluid:イソプロピルアルコール", 10]], "outputItems": [["@item:シリコンウェハ", 1]], "outputFluids": []},
    {"key": "成膜", "block": "@block:成膜装置", "time": 10, "levelInheritance": "None", "inputItems": [["@item:シリコンウェハ", 1]], "inputFluids": [["@fluid:イソプロピルアルコール", 10]], "outputItems": [["@item:トランジスタ設置済みウェハG1", 1]], "outputFluids": []},
    {"key": "EUV1層", "block": "@block:EUV露光式半導体製造装置", "time": 15, "levelInheritance": "Inherit", "inputItems": [["@item:トランジスタ設置済みウェハG1", 1]], "inputFluids": [["@fluid:イソプロピルアルコール", 5]], "outputItems": [["@item:1層パターンウェハG1", 1], ["@item:失敗したパターンウェハ", 1]], "outputFluids": []},
    {"key": "EUV2層", "block": "@block:EUV露光式半導体製造装置", "time": 15, "levelInheritance": "Inherit", "inputItems": [["@item:1層パターンウェハG1", 1]], "inputFluids": [["@fluid:イソプロピルアルコール", 5]], "outputItems": [["@item:2層パターンウェハG1", 1], ["@item:失敗したパターンウェハ", 1]], "outputFluids": []},
    {"key": "EUV3層", "block": "@block:EUV露光式半導体製造装置", "time": 15, "levelInheritance": "Inherit", "inputItems": [["@item:2層パターンウェハG1", 1]], "inputFluids": [["@fluid:イソプロピルアルコール", 5]], "outputItems": [["@item:完成パターンウェハG1", 1], ["@item:失敗したパターンウェハ", 1]], "outputFluids": []},
    {"key": "ダイシング", "block": "@block:ダイシング装置", "time": 18, "levelInheritance": "Inherit", "inputItems": [["@item:完成パターンウェハG1", 1]], "inputFluids": [["@fluid:イソプロピルアルコール", 20]], "outputItems": [["@item:シリコンダイLv1", 300], ["@item:シリコンダイLv2", 300], ["@item:シリコンダイLv3", 300], ["@item:シリコンダイLv4", 300], ["@item:シリコンダイLv5", 300], ["@item:失敗したシリコンダイ", 300]], "outputFluids": []},
    {"key": "パッケージング", "block": "@block:パッケージング装置", "time": 3, "levelInheritance": "Inherit", "inputItems": [["@item:シリコンダイLv1", 10], ["@item:ICチップ基板", 1]], "inputFluids": [["@fluid:イソプロピルアルコール", 1]], "outputItems": [["@item:ICチップLv1", 1]], "outputFluids": []}
  ],
  "patchBlocks": [
    {"name": "EUV露光式半導体製造装置", "set": {"blockPrefabAddressablesPath": "Vanilla/Block/EuvLithographyTool", "blockParam.requiredPower": 250, "blockParam.inputSlotCount": 1, "blockParam.outputSlotCount": 2, "blockParam.moduleSlotCount": 3, "blockParam.allowQualityModule": true, "blockParam.inputTankCount": 1, "blockParam.innerTankCapacity": 300, "blockParam.fluidInventoryConnectors": {"inflowConnects": [{"connectorGuid": "@new:connector:EUV:fluidIn", "offset": [0, 0, 1], "directions": [[-1, 0, 0]], "option": {"flowCapacity": 600, "connectTankIndex": 0}}], "outflowConnects": []}, "requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 70}, {"itemGuid": "@item:制御モータ", "count": 20}, {"itemGuid": "@item:コンピュータ", "count": 25}]}},
    {"name": "クリーンルームブロック", "set": {"requiredItems": [{"itemGuid": "@item:鋼鉄板", "count": 1}, {"itemGuid": "@item:プラスチック", "count": 1}]}},
    {"name": "クリーンルームパネル", "set": {"requiredItems": [{"itemGuid": "@item:鋼鉄板", "count": 1}, {"itemGuid": "@item:プラスチック", "count": 1}]}},
    {"name": "クリーンルームドア", "set": {"requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 2}, {"itemGuid": "@item:プラスチック", "count": 4}]}},
    {"name": "クリーンルームアイテムハッチ", "set": {"requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 4}, {"itemGuid": "@item:制御モータ", "count": 2}]}},
    {"name": "クリーンルームパイプコネクタ", "set": {"requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 4}, {"itemGuid": "@item:ゴム", "count": 4}]}},
    {"name": "クリーンルーム空気清浄機", "set": {"blockParam.requiredPower": 150, "blockParam.removalVolumePerSecond": 5, "blockParam.filterCapacity": 20000, "requiredItems": [{"itemGuid": "@item:鋼鉄フレーム", "count": 15}, {"itemGuid": "@item:制御モータ", "count": 5}, {"itemGuid": "@item:プラスチック", "count": 20}]}}
  ],
  "deleteTopLevelKeys": {
    "blocks": ["cleanRoomThresholds", "semiconductorChips"]
  },
  "setMaster": {
    "cleanRoom.data.0.pollution": {"aVolume": 0.02, "aSurface": 0.04, "aConnector": 3.0, "aMachine": 2.0, "kHatch": 4.0},
    "cleanRoom.data.0.thresholds": [{"className": "A", "maxConcentration": 10, "requiredAirChangeRate": 0.0167, "qualityMultiplier": 1.0, "layerFailRate": 0.1, "dieLossRate": 0.15}, {"className": "B", "maxConcentration": 50, "requiredAirChangeRate": 0.0083, "qualityMultiplier": 0.75, "layerFailRate": 0.15, "dieLossRate": 0.22}, {"className": "C", "maxConcentration": 200, "requiredAirChangeRate": 0.0042, "qualityMultiplier": 0.5, "layerFailRate": 0.2, "dieLossRate": 0.3}, {"className": "D", "maxConcentration": 1000, "requiredAirChangeRate": 0.0014, "qualityMultiplier": 0.25, "layerFailRate": 0.25, "dieLossRate": 0.38}],
    "cleanRoom.data.0.waferGradeDraws": [{"machineRecipeGuid": "@recipe:成膜", "gradedWaferItemGuid": "@item:トランジスタ設置済みウェハG1", "maxPromotionChance": 0.6}],
    "cleanRoom.data.0.layerExposures": [{"machineRecipeGuid": "@recipe:EUV1層", "layeredWaferItemGuid": "@item:1層パターンウェハG1", "failedWaferItemGuid": "@item:失敗したパターンウェハ", "maxFailReduction": 0.6}, {"machineRecipeGuid": "@recipe:EUV2層", "layeredWaferItemGuid": "@item:2層パターンウェハG1", "failedWaferItemGuid": "@item:失敗したパターンウェハ", "maxFailReduction": 0.6}, {"machineRecipeGuid": "@recipe:EUV3層", "layeredWaferItemGuid": "@item:完成パターンウェハG1", "failedWaferItemGuid": "@item:失敗したパターンウェハ", "maxFailReduction": 0.6}],
    "cleanRoom.data.0.dicings": [{"machineRecipeGuid": "@recipe:ダイシング", "dieItemGuid": "@item:シリコンダイLv1", "failedDieItemGuid": "@item:失敗したシリコンダイ", "diesPerWafer": 300, "maxLossReduction": 0.6, "gradeDieTables": [{"levelRates": [1.0]}, {"levelRates": [0.7, 0.3]}, {"levelRates": [0.55, 0.3, 0.15]}, {"levelRates": [0.45, 0.3, 0.17, 0.08]}, {"levelRates": [0.4, 0.28, 0.18, 0.09, 0.05]}]}]
  }
}
```

- [ ] **Step 2: 適用する**

Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`
Expected: `applied 3 spec files; issued 28 new guids`（アイテム4・ブロック4・レシピ7・コネクタ13）

- [ ] **Step 3: 結果を確かめる**

Run:
```bash
python3 -c "
import json; m='$MOD/master/'; b=json.load(open(m+'blocks.json')); r=json.load(open(m+'machineRecipes.json'))
print(sorted(k for k in b if k!='data'))
print(any(e['machineRecipeGuid'].startswith('3a13a5a9') for e in r['data']))
print(sorted(e['name'] for e in b['data'] if e['blockType']=='CleanRoomMachine'))
print([e['name'] for e in b['data'] if {(c['itemGuid'],c['count']) for c in e['requiredItems']}=={('60daab46-bce2-4d09-b895-d872bef3f9c3',5),('54ccd10a-a6c1-4ee5-99d5-b61fea7e92a9',2)}])
print(sorted(e['blockPrefabAddressablesPath'] for e in b['data'] if e['blockType']=='CleanRoomMachine'))
c=json.load(open(m+'cleanRoom.json'))['data'][0]
print(len(c['waferGradeDraws']), len(c['layerExposures']), len(c['dicings']), c['dicings'][0]['diesPerWafer'], [len(x['levelRates']) for x in c['dicings'][0]['gradeDieTables']])"
cd $B && python3 verify_master.py --mod-dir $MOD --ledger guids.json | grep -E "cleanRoom = params|quality module permission|power / filter"
```
Expected:
```
['beltConveyorFamilies', 'blockDestructionCategories', 'connectorSettings']
False
['EUV露光式半導体製造装置', 'ウェハ切断機', 'ダイシング装置', 'パッケージング装置', '成膜装置']
[]
['Vanilla/Block/DepositionTool', 'Vanilla/Block/DicingSaw', 'Vanilla/Block/EuvLithographyTool', 'Vanilla/Block/PackagingMachine', 'Vanilla/Block/WaferSlicer']
1 3 1 300 [1, 2, 3, 4, 5]
PASS power / filter = params
PASS cleanRoom = params
PASS quality module permission
```
（`verify_master.py` の他の項目は D6・D7 の後に PASS する。ここでは3行だけを見る）

- [ ] **Step 4: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/spec/03_cleanroom_machines.json design/tier9-semiconductor-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: Tier9 専用機5種と工程レシピ・クリーンルームの抽選と汚染の値・建設コストを入れる"
```

### Task D6: モジュール4種×Lv1〜5 と GPU Lv1〜5

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/spec/04_modules_gpu.json`
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`（アイテム25・modules 20・levelFamilies 5。`moduleSlotStackLimitLevels` は Part C が入れた値を同じ値で上書きする）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`（継承レシピ5本）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `moorestech_master/design/tier9-semiconductor-balance/guids.json`

**Interfaces:**
- Consumes: D4 の `ICチップ` ファミリー（基準 ICチップLv1）。Part C の `levelInheritance: InheritUpToUnlocked`（製作上限は出力ファミリーの基準アイテムごと・初期は Lv1。上げる研究アクションは `unlockLevelInheritanceCraftLevel {"targetItemGuids": [基準アイテムGuid], "level": int}` で S2 が使う）、継承レシピの検証（継承元のファミリー基準品は入力・出力にちょうど1つずつ、出力ファミリーの長さ ≥ 入力ファミリー）、items.json 直下の必須キー `moduleSlotStackLimitLevels: [{"stackLimit": int}, …]`（0始まりのレベル→1スロットに積める数。Part C の C6 が `[3, 6, 10]` を入れ済み）。`modules[].effectAxis` は `Quality` / `Speed` / `Efficiency` / `Productivity`。S2 との取り決め（判断記録）の GPU 固定 Guid
- Produces（S2 が参照する固定 Guid。台帳 `guids.json` と spec に同じ値）:
  - ファミリー: 品質 `01a167f3-bd7c-72be-ac56-e45d70759c32`、速度 `01a167f3-bd7f-76f2-b2e3-6452c966fd02`、省エネ `01a167f3-bd82-764f-809d-f64b92c66667`、生産性 `01a167f3-bd85-72fb-a518-c298b8072dae`、GPU `01a167ed-2700-7371-9f95-168966ff60bc`
  - 継承レシピ: 品質 `01a167f3-bd88-747d-b30e-13f23f169ce4`、速度 `01a167f3-bd8b-7732-badd-8c1a011f7c26`、省エネ `01a167f3-bd8e-7525-af33-5cce09108374`、生産性 `01a167f3-bd90-7307-91a6-e45243f99379`、GPU `01a167f3-bd94-76d2-ada6-617fdbf3d9b3`
  - Lv1 アイテム: 品質 `01a167f3-bd96-73a2-a0ce-06ba261e109d`、速度 `01a167f3-bd9a-72ec-8b7a-cd188d0d5272`、省エネ `01a167f3-bd9d-74b4-b7ff-91e6f9e3bdb0`、生産性 `01a167f3-bda0-74f7-8002-1407403a36a2`、GPU Lv1〜5 は S2 との取り決め（判断記録）の値
  - 名前: `品質モジュールLv1〜Lv5` `速度モジュールLv1〜Lv5` `省エネモジュールLv1〜Lv5` `生産性モジュールLv1〜Lv5` `GPU` `GPU Lv2〜GPU Lv5`

値（spec 04 の中身。効果値は `sim/params.py` の `MODULES`）:

| 製品 | 機械 | 秒 | 入力（継承元は ICチップ） | 継承 |
|---|---|---|---|---|
| 品質モジュール | 電気汎用工作装置 | 10 | ICチップ5・発展電子回路5・鋼鉄フレーム1 | InheritUpToUnlocked |
| 速度モジュール | 電気汎用工作装置 | 10 | ICチップ5・制御モータ2・鋼鉄フレーム1 | InheritUpToUnlocked |
| 省エネモジュール | 電気汎用工作装置 | 10 | ICチップ5・被覆銅線10・鋼鉄フレーム1 | InheritUpToUnlocked |
| 生産性モジュール | 電気汎用工作装置 | 15 | ICチップ10・コンピュータ2・鋼鉄フレーム2 | InheritUpToUnlocked |
| GPU | 電気汎用工作装置 | 20 | ICチップ20・コンピュータ2・鋼鉄フレーム2・被覆銅線10 | InheritUpToUnlocked |

- [ ] **Step 1: spec 04 を書く**

`$B/spec/04_modules_gpu.json`:

```json
{
  "items": [
    {"name": "品質モジュールLv1", "en": "Quality Module Lv1", "sortPriority": 1501, "guid": "01a167f3-bd96-73a2-a0ce-06ba261e109d"},
    {"name": "品質モジュールLv2", "en": "Quality Module Lv2", "sortPriority": 1502},
    {"name": "品質モジュールLv3", "en": "Quality Module Lv3", "sortPriority": 1503},
    {"name": "品質モジュールLv4", "en": "Quality Module Lv4", "sortPriority": 1504},
    {"name": "品質モジュールLv5", "en": "Quality Module Lv5", "sortPriority": 1505},
    {"name": "速度モジュールLv1", "en": "Speed Module Lv1", "sortPriority": 1511, "guid": "01a167f3-bd9a-72ec-8b7a-cd188d0d5272"},
    {"name": "速度モジュールLv2", "en": "Speed Module Lv2", "sortPriority": 1512},
    {"name": "速度モジュールLv3", "en": "Speed Module Lv3", "sortPriority": 1513},
    {"name": "速度モジュールLv4", "en": "Speed Module Lv4", "sortPriority": 1514},
    {"name": "速度モジュールLv5", "en": "Speed Module Lv5", "sortPriority": 1515},
    {"name": "省エネモジュールLv1", "en": "Efficiency Module Lv1", "sortPriority": 1521, "guid": "01a167f3-bd9d-74b4-b7ff-91e6f9e3bdb0"},
    {"name": "省エネモジュールLv2", "en": "Efficiency Module Lv2", "sortPriority": 1522},
    {"name": "省エネモジュールLv3", "en": "Efficiency Module Lv3", "sortPriority": 1523},
    {"name": "省エネモジュールLv4", "en": "Efficiency Module Lv4", "sortPriority": 1524},
    {"name": "省エネモジュールLv5", "en": "Efficiency Module Lv5", "sortPriority": 1525},
    {"name": "生産性モジュールLv1", "en": "Productivity Module Lv1", "sortPriority": 1531, "guid": "01a167f3-bda0-74f7-8002-1407403a36a2"},
    {"name": "生産性モジュールLv2", "en": "Productivity Module Lv2", "sortPriority": 1532},
    {"name": "生産性モジュールLv3", "en": "Productivity Module Lv3", "sortPriority": 1533},
    {"name": "生産性モジュールLv4", "en": "Productivity Module Lv4", "sortPriority": 1534},
    {"name": "生産性モジュールLv5", "en": "Productivity Module Lv5", "sortPriority": 1535},
    {"name": "GPU", "guid": "01a167ed-2703-7014-98e4-11bd15f38571", "en": "GPU", "sortPriority": 1541},
    {"name": "GPU Lv2", "guid": "01a167ed-2706-7bfd-8361-a430dd96a569", "en": "GPU Lv2", "sortPriority": 1542},
    {"name": "GPU Lv3", "guid": "01a167ed-2709-74f0-a5c6-25026c7479d9", "en": "GPU Lv3", "sortPriority": 1543},
    {"name": "GPU Lv4", "guid": "01a167ed-270c-7873-a83a-5cf02ead6277", "en": "GPU Lv4", "sortPriority": 1544},
    {"name": "GPU Lv5", "guid": "01a167ed-270f-7276-8700-469ab54a26a2", "en": "GPU Lv5", "sortPriority": 1545}
  ],
  "levelFamilies": [
    {"name": "品質モジュール", "guid": "01a167f3-bd7c-72be-ac56-e45d70759c32", "levels": ["品質モジュールLv1", "品質モジュールLv2", "品質モジュールLv3", "品質モジュールLv4", "品質モジュールLv5"]},
    {"name": "速度モジュール", "guid": "01a167f3-bd7f-76f2-b2e3-6452c966fd02", "levels": ["速度モジュールLv1", "速度モジュールLv2", "速度モジュールLv3", "速度モジュールLv4", "速度モジュールLv5"]},
    {"name": "省エネモジュール", "guid": "01a167f3-bd82-764f-809d-f64b92c66667", "levels": ["省エネモジュールLv1", "省エネモジュールLv2", "省エネモジュールLv3", "省エネモジュールLv4", "省エネモジュールLv5"]},
    {"name": "生産性モジュール", "guid": "01a167f3-bd85-72fb-a518-c298b8072dae", "levels": ["生産性モジュールLv1", "生産性モジュールLv2", "生産性モジュールLv3", "生産性モジュールLv4", "生産性モジュールLv5"]},
    {"name": "GPU", "guid": "01a167ed-2700-7371-9f95-168966ff60bc", "levels": ["GPU", "GPU Lv2", "GPU Lv3", "GPU Lv4", "GPU Lv5"]}
  ],
  "modules": [
    {"name": "品質モジュールLv1", "item": "品質モジュールLv1", "effectAxis": "Quality", "tier": 1, "effectValue": 0.04, "tradeoffValue": 0.05},
    {"name": "品質モジュールLv2", "item": "品質モジュールLv2", "effectAxis": "Quality", "tier": 2, "effectValue": 0.06, "tradeoffValue": 0.05},
    {"name": "品質モジュールLv3", "item": "品質モジュールLv3", "effectAxis": "Quality", "tier": 3, "effectValue": 0.08, "tradeoffValue": 0.05},
    {"name": "品質モジュールLv4", "item": "品質モジュールLv4", "effectAxis": "Quality", "tier": 4, "effectValue": 0.1, "tradeoffValue": 0.05},
    {"name": "品質モジュールLv5", "item": "品質モジュールLv5", "effectAxis": "Quality", "tier": 5, "effectValue": 0.12, "tradeoffValue": 0.05},
    {"name": "速度モジュールLv1", "item": "速度モジュールLv1", "effectAxis": "Speed", "tier": 1, "effectValue": 0.1, "tradeoffValue": 0.2},
    {"name": "速度モジュールLv2", "item": "速度モジュールLv2", "effectAxis": "Speed", "tier": 2, "effectValue": 0.15, "tradeoffValue": 0.2},
    {"name": "速度モジュールLv3", "item": "速度モジュールLv3", "effectAxis": "Speed", "tier": 3, "effectValue": 0.2, "tradeoffValue": 0.2},
    {"name": "速度モジュールLv4", "item": "速度モジュールLv4", "effectAxis": "Speed", "tier": 4, "effectValue": 0.25, "tradeoffValue": 0.2},
    {"name": "速度モジュールLv5", "item": "速度モジュールLv5", "effectAxis": "Speed", "tier": 5, "effectValue": 0.3, "tradeoffValue": 0.2},
    {"name": "省エネモジュールLv1", "item": "省エネモジュールLv1", "effectAxis": "Efficiency", "tier": 1, "effectValue": 0.15, "tradeoffValue": 0},
    {"name": "省エネモジュールLv2", "item": "省エネモジュールLv2", "effectAxis": "Efficiency", "tier": 2, "effectValue": 0.2, "tradeoffValue": 0},
    {"name": "省エネモジュールLv3", "item": "省エネモジュールLv3", "effectAxis": "Efficiency", "tier": 3, "effectValue": 0.25, "tradeoffValue": 0},
    {"name": "省エネモジュールLv4", "item": "省エネモジュールLv4", "effectAxis": "Efficiency", "tier": 4, "effectValue": 0.3, "tradeoffValue": 0},
    {"name": "省エネモジュールLv5", "item": "省エネモジュールLv5", "effectAxis": "Efficiency", "tier": 5, "effectValue": 0.35, "tradeoffValue": 0},
    {"name": "生産性モジュールLv1", "item": "生産性モジュールLv1", "effectAxis": "Productivity", "tier": 1, "effectValue": 0.03, "tradeoffValue": 0.08},
    {"name": "生産性モジュールLv2", "item": "生産性モジュールLv2", "effectAxis": "Productivity", "tier": 2, "effectValue": 0.045, "tradeoffValue": 0.08},
    {"name": "生産性モジュールLv3", "item": "生産性モジュールLv3", "effectAxis": "Productivity", "tier": 3, "effectValue": 0.06, "tradeoffValue": 0.08},
    {"name": "生産性モジュールLv4", "item": "生産性モジュールLv4", "effectAxis": "Productivity", "tier": 4, "effectValue": 0.075, "tradeoffValue": 0.08},
    {"name": "生産性モジュールLv5", "item": "生産性モジュールLv5", "effectAxis": "Productivity", "tier": 5, "effectValue": 0.09, "tradeoffValue": 0.08}
  ],
  "machineRecipes": [
    {"key": "品質モジュール", "guid": "01a167f3-bd88-747d-b30e-13f23f169ce4", "block": "@block:電気汎用工作装置", "time": 10, "levelInheritance": "InheritUpToUnlocked", "inputItems": [["@item:ICチップLv1", 5], ["@item:発展電子回路", 5], ["@item:鋼鉄フレーム", 1]], "inputFluids": [], "outputItems": [["@item:品質モジュールLv1", 1]], "outputFluids": []},
    {"key": "速度モジュール", "guid": "01a167f3-bd8b-7732-badd-8c1a011f7c26", "block": "@block:電気汎用工作装置", "time": 10, "levelInheritance": "InheritUpToUnlocked", "inputItems": [["@item:ICチップLv1", 5], ["@item:制御モータ", 2], ["@item:鋼鉄フレーム", 1]], "inputFluids": [], "outputItems": [["@item:速度モジュールLv1", 1]], "outputFluids": []},
    {"key": "省エネモジュール", "guid": "01a167f3-bd8e-7525-af33-5cce09108374", "block": "@block:電気汎用工作装置", "time": 10, "levelInheritance": "InheritUpToUnlocked", "inputItems": [["@item:ICチップLv1", 5], ["@item:被覆銅線", 10], ["@item:鋼鉄フレーム", 1]], "inputFluids": [], "outputItems": [["@item:省エネモジュールLv1", 1]], "outputFluids": []},
    {"key": "生産性モジュール", "guid": "01a167f3-bd90-7307-91a6-e45243f99379", "block": "@block:電気汎用工作装置", "time": 15, "levelInheritance": "InheritUpToUnlocked", "inputItems": [["@item:ICチップLv1", 10], ["@item:コンピュータ", 2], ["@item:鋼鉄フレーム", 2]], "inputFluids": [], "outputItems": [["@item:生産性モジュールLv1", 1]], "outputFluids": []},
    {"key": "GPU", "guid": "01a167f3-bd94-76d2-ada6-617fdbf3d9b3", "block": "@block:電気汎用工作装置", "time": 20, "levelInheritance": "InheritUpToUnlocked", "inputItems": [["@item:ICチップLv1", 20], ["@item:コンピュータ", 2], ["@item:鋼鉄フレーム", 2], ["@item:被覆銅線", 10]], "inputFluids": [], "outputItems": [["@item:GPU", 1]], "outputFluids": []}
  ],
  "setMaster": {
    "items.moduleSlotStackLimitLevels": [{"stackLimit": 3}, {"stackLimit": 6}, {"stackLimit": 10}]
  }
}
```

- [ ] **Step 2: 適用する**

Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`
Expected: `applied 4 spec files; issued 36 new guids`（モジュールLv2〜5 アイテム16・モジュール定義20。固定 Guid は発行数に入らない）

- [ ] **Step 3: 結果を確かめる**

Run:
```bash
python3 -c "
import json; i=json.load(open('$MOD/master/items.json'))
print(len(i['modules']), [level['stackLimit'] for level in i['moduleSlotStackLimitLevels']])
print([f['name'] for f in i['levelFamilies']][-5:])
print(next(e['name'] for e in i['data'] if e['itemGuid']=='01a167ed-2706-7bfd-8361-a430dd96a569'))"
cd $B && python3 verify_master.py --mod-dir $MOD --ledger guids.json | grep "modules = params"
```
Expected:
```
20 [3, 6, 10]
['品質モジュール', '速度モジュール', '省エネモジュール', '生産性モジュール', 'GPU']
GPU Lv2
PASS modules = params
```

- [ ] **Step 4: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/spec/04_modules_gpu.json design/tier9-semiconductor-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: Tier9 モジュール4種と GPU を Lv1〜5 で入れ、ICチップからの継承レシピを足す"
```

### Task D7: 研究（半導体製造の解放内容・S1 の研究7ノード）と並び順・配置の再計算

**Files:**
- Create: `moorestech_master/design/tier9-semiconductor-balance/spec/05_research.json`
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/research.json`（新規6・半導体製造と機械オーバークロックを更新・UIPosition 再計算）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`・`blocks.json`（sortPriority 再計算と並べ替え）
- Modify: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

**Interfaces:**
- Consumes: D3〜D6 の全レシピキー・アイテム名・ブロック名。Part C の gameActionType `unlockModuleSlotStackLevel {"level": N}`（N は `moduleSlotStackLimitLevels` の0始まり添字。研究画面に解放内容が出る）。Part C の研究消費の代用（`consumeItems` の要求が levelFamily の基準品＝ICチップLv1 のとき、ICチップLv2 以上も受け付けて下位から消費する）。Part A のブロック `クリーンルームパネル`（アイテムを持たないブロック）。研究の固定 Guid（D2 の台帳 `guids.json`・S2 と共有）
- Produces: 研究ノード（S2 が前提に使う）: 品質モジュール `01a167f3-bd6b-70c9-be6e-52e41907b21f`、省エネモジュール `01a167f3-bd6e-7745-84c0-0e1cf105c2cc`、GPU設計 `01a167f3-bd74-73f8-8c43-1869db833299`、モジュールスタック拡張I `01a167f3-bd77-7013-b982-5f4cdf3c86a9`、生産性モジュール `01a167f3-bd71-707c-b8e6-615063559cc1`、モジュールスタック拡張II `01a167f3-bd7a-75c4-a266-8b9a4d5d1900`、機械オーバークロック（既存 `019e3ae2-5005-7439-b242-9b7b3191a2c3`）

値（spec 05 の中身。コストは `sim/timeline.py` の `RESEARCH`）:

| 研究 | 前提 | 消費 | 解放 |
|---|---|---|---|
| 半導体製造（既存・解放だけ差し替え） | 既存のまま | 既存のまま | ブロック11（クリーンルーム系6＝ブロック・パネル・ドア・アイテムハッチ・パイプコネクタ・空気清浄機、専用機5）、機械レシピ16（D3 の9・D5 の7）、レシピ表示48（素材8・ウェハ20・ダイ5・チップ5・ブロックアイテム10。パネルはアイテムを持たないので表示に入れない） |
| 品質モジュール | 半導体製造 | ICチップLv1 1000・鋼鉄フレーム150・コンピュータ60 | 品質モジュールのレシピ・表示（Lv1） |
| 機械オーバークロック（既存） | 品質モジュール | ICチップLv1 1500・鋼鉄フレーム150・制御モータ120 | 速度モジュールのレシピ・表示（Lv1） |
| 省エネモジュール | 品質モジュール | ICチップLv1 1500・鋼鉄フレーム150・コンピュータ60 | 省エネモジュールのレシピ・表示 |
| GPU設計 | 品質モジュール | ICチップLv1 2000・鋼鉄フレーム200・コンピュータ120 | GPU のレシピ・表示 |
| モジュールスタック拡張I | 機械オーバークロック・省エネモジュール | ICチップLv1 3000・鋼鉄フレーム200・制御モータ150 | `unlockModuleSlotStackLevel {level: 1}`（3→6） |
| 生産性モジュール | モジュールスタック拡張I | ICチップLv1 4000・鋼鉄フレーム300・コンピュータ150 | 生産性モジュールのレシピ・表示 |
| モジュールスタック拡張II | 生産性モジュール | ICチップLv1 7000・鋼鉄フレーム400・コンピュータ200・制御モータ200 | `unlockModuleSlotStackLevel {level: 2}`（6→10） |

研究の ICチップLv1 の要求は、Part C の代用で ICチップLv2 以上でも払える（下位から消費）。D1 の timeline はこれを含めて S1 の時間を出している。

フィルター分岐器・金属超精密加工は触らない（S3 の担当）。機械オーバークロックの前提が半導体製造から品質モジュールへ変わるので、金属超精密加工の到達順は品質モジュールの後になる。

- [ ] **Step 1: spec 05 を書く**

`$B/spec/05_research.json`:

```json
{
  "research": [
    {"name": "品質モジュール", "guid": "01a167f3-bd6b-70c9-be6e-52e41907b21f", "en": "Quality Module", "description": "品質モジュールを作れるようにする。成膜・EUV・ダイシングに入れると、ウェハのグレードが上がりやすく、層の失敗とダイの損失が減る", "descEn": "Unlocks the Quality Module. In deposition, EUV and dicing tools it raises wafer grades and cuts layer failures and die losses", "prev": ["@research:半導体製造"], "consumeItems": [["@item:ICチップLv1", 1000], ["@item:鋼鉄フレーム", 150], ["@item:コンピュータ", 60]], "clearedActions": [{"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:品質モジュール"]}}, {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:品質モジュールLv1"]}}], "iconItem": "@item:品質モジュールLv1"},
    {"name": "省エネモジュール", "guid": "01a167f3-bd6e-7745-84c0-0e1cf105c2cc", "en": "Efficiency Module", "description": "省エネモジュールを作れるようにする。機械の消費電力を下げる", "descEn": "Unlocks the Efficiency Module, which lowers machine power use", "prev": ["@research:品質モジュール"], "consumeItems": [["@item:ICチップLv1", 1500], ["@item:鋼鉄フレーム", 150], ["@item:コンピュータ", 60]], "clearedActions": [{"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:省エネモジュール"]}}, {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:省エネモジュールLv1"]}}], "iconItem": "@item:省エネモジュールLv1"},
    {"name": "GPU設計", "guid": "01a167f3-bd74-73f8-8c43-1869db833299", "en": "GPU Design", "description": "ICチップからGPUを作れるようにする", "descEn": "Unlocks crafting GPUs from IC chips", "prev": ["@research:品質モジュール"], "consumeItems": [["@item:ICチップLv1", 2000], ["@item:鋼鉄フレーム", 200], ["@item:コンピュータ", 120]], "clearedActions": [{"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:GPU"]}}, {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:GPU"]}}], "iconItem": "@item:GPU"},
    {"name": "モジュールスタック拡張I", "guid": "01a167f3-bd77-7013-b982-5f4cdf3c86a9", "en": "Module Stacking I", "description": "モジュールスロット1つに積めるモジュールを3個から6個に増やす", "descEn": "Raises modules per module slot from 3 to 6", "prev": ["@research:機械オーバークロック", "@research:省エネモジュール"], "consumeItems": [["@item:ICチップLv1", 3000], ["@item:鋼鉄フレーム", 200], ["@item:制御モータ", 150]], "clearedActions": [{"gameActionType": "unlockModuleSlotStackLevel", "gameActionParam": {"level": 1}}], "iconItem": "@item:速度モジュールLv1"},
    {"name": "生産性モジュール", "guid": "01a167f3-bd71-707c-b8e6-615063559cc1", "en": "Productivity Module", "description": "生産性モジュールを作れるようにする。加工は遅くなるが、追加の生産物が出る", "descEn": "Unlocks the Productivity Module: slower crafting with a chance of extra output", "prev": ["@research:モジュールスタック拡張I"], "consumeItems": [["@item:ICチップLv1", 4000], ["@item:鋼鉄フレーム", 300], ["@item:コンピュータ", 150]], "clearedActions": [{"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:生産性モジュール"]}}, {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:生産性モジュールLv1"]}}], "iconItem": "@item:生産性モジュールLv1"},
    {"name": "モジュールスタック拡張II", "guid": "01a167f3-bd7a-75c4-a266-8b9a4d5d1900", "en": "Module Stacking II", "description": "モジュールスロット1つに積めるモジュールを6個から10個に増やす", "descEn": "Raises modules per module slot from 6 to 10", "prev": ["@research:生産性モジュール"], "consumeItems": [["@item:ICチップLv1", 7000], ["@item:鋼鉄フレーム", 400], ["@item:コンピュータ", 200], ["@item:制御モータ", 200]], "clearedActions": [{"gameActionType": "unlockModuleSlotStackLevel", "gameActionParam": {"level": 2}}], "iconItem": "@item:生産性モジュールLv1"}
  ],
  "patchResearch": [
    {"name": "半導体製造", "set": {"clearedActions": [{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["@block:クリーンルームブロック", "@block:クリーンルームパネル", "@block:クリーンルームドア", "@block:クリーンルームアイテムハッチ", "@block:クリーンルームパイプコネクタ", "@block:クリーンルーム空気清浄機", "@block:ウェハ切断機", "@block:成膜装置", "@block:EUV露光式半導体製造装置", "@block:ダイシング装置", "@block:パッケージング装置"]}}, {"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:砂", "@recipe:金属シリコン", "@recipe:高純度シリコン", "@recipe:IPA", "@recipe:鋼鉄フレーム", "@recipe:ICチップ基板", "@recipe:クリーンルームフィルター", "@recipe:失敗ウェハ再生", "@recipe:失敗ダイ再生", "@recipe:ウェハ切断", "@recipe:成膜", "@recipe:EUV1層", "@recipe:EUV2層", "@recipe:EUV3層", "@recipe:ダイシング", "@recipe:パッケージング"]}}, {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:金属シリコン", "@item:鋼鉄フレーム", "@item:高純度シリコン", "@item:シリコンウェハ", "@item:ICチップ基板", "@item:クリーンルームフィルター", "@item:失敗したパターンウェハ", "@item:失敗したシリコンダイ", "@item:トランジスタ設置済みウェハG1", "@item:トランジスタ設置済みウェハG2", "@item:トランジスタ設置済みウェハG3", "@item:トランジスタ設置済みウェハG4", "@item:トランジスタ設置済みウェハG5", "@item:1層パターンウェハG1", "@item:1層パターンウェハG2", "@item:1層パターンウェハG3", "@item:1層パターンウェハG4", "@item:1層パターンウェハG5", "@item:2層パターンウェハG1", "@item:2層パターンウェハG2", "@item:2層パターンウェハG3", "@item:2層パターンウェハG4", "@item:2層パターンウェハG5", "@item:完成パターンウェハG1", "@item:完成パターンウェハG2", "@item:完成パターンウェハG3", "@item:完成パターンウェハG4", "@item:完成パターンウェハG5", "@item:シリコンダイLv1", "@item:シリコンダイLv2", "@item:シリコンダイLv3", "@item:シリコンダイLv4", "@item:シリコンダイLv5", "@item:ICチップLv1", "@item:ICチップLv2", "@item:ICチップLv3", "@item:ICチップLv4", "@item:ICチップLv5", "@item:クリーンルームブロック", "@item:クリーンルームドア", "@item:クリーンルームアイテムハッチ", "@item:クリーンルームパイプコネクタ", "@item:クリーンルーム空気清浄機", "@item:ウェハ切断機", "@item:成膜装置", "@item:EUV露光式半導体製造装置", "@item:ダイシング装置", "@item:パッケージング装置"]}}]}, "descEn": "Unlocks the clean room and the chip line: wafer slicing, deposition, EUV, dicing and packaging"},
    {"name": "機械オーバークロック", "set": {"prevResearchNodeGuids": ["@research:品質モジュール"], "consumeItems": [{"itemGuid": "@item:ICチップLv1", "itemCount": 1500}, {"itemGuid": "@item:鋼鉄フレーム", "itemCount": 150}, {"itemGuid": "@item:制御モータ", "itemCount": 120}], "researchNodeDescription": "速度モジュールを作れるようにする。機械の処理を速くする代わりに電力を多く使う", "clearedActions": [{"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:速度モジュール"]}}, {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:速度モジュールLv1"]}}], "graphViewSettings.IconItem": "@item:速度モジュールLv1"}, "descEn": "Unlocks the Speed Module: faster machines at a higher power cost"}
  ]
}
```

- [ ] **Step 2: 適用する**

Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`
Expected: `applied 5 spec files; issued 0 new guids`（研究の Guid はすべて固定）

- [ ] **Step 3: sortPriority と研究ノード座標を再計算する（master-refine 操作 A・B）**

Run（`$W` から）:
```bash
python3 .claude/skills/master-refine/scripts/recalc_sort_priority.py --mod-dir "$MOD" --quiet
python3 .claude/skills/master-refine/scripts/recalc_research_positions.py --mod-dir "$MOD" | tail -1
git -C $MW diff --stat -- server_v8 | tail -4
```
Expected: `Updated <件数> items.`・`updated <件数> nodes -> .../research.json`。diff の対象は blocks.json・items.json・research.json・localization.csv の4つだけ

（下書き時の試行では 品質モジュール [4250,0]・機械オーバークロック [4420,0]・省エネモジュール [4590,0]・GPU設計 [4760,0]・スタック拡張I [4930,0]・生産性 [5270,0]・スタック拡張II [5440,0] に並んだ。再適用しても `apply_spec.py` は既存の sortPriority と UIPosition を残すので、再計算を打ち消さない）

- [ ] **Step 4: 研究と解放内容を確かめる**

Run:
```bash
python3 -c "
import json; r=json.load(open('$MOD/master/research.json'))
u=[(n['researchNodeName'],c['itemCount']) for n in r['data'] for c in n['consumeItems'] if c['itemGuid']=='f6dd7835-0845-4877-87d0-b719d037ae7d']
print(len(u), sum(c for _,c in u))
b={e['blockGuid']:e['name'] for e in json.load(open('$MOD/master/blocks.json'))['data']}
s=next(n for n in r['data'] if n['researchNodeName']=='半導体製造')['clearedActions']
print(len(next(a for a in s if a['gameActionType']=='unlockBlock')['gameActionParam']['unlockBlockGuids']), 'クリーンルームパネル' in [b[g] for a in s if a['gameActionType']=='unlockBlock' for g in a['gameActionParam']['unlockBlockGuids']], len(next(a for a in s if a['gameActionType']=='unlockItemRecipeView')['gameActionParam']['unlockItemGuids']))"
cd $B && python3 verify_master.py --mod-dir $MOD --ledger guids.json | grep "research = params"
```
Expected: `7 20000`（合計は `sim/timeline.py` の `RESEARCH` の `chips` の和。D1 Step 3 で研究コストを変えたらその値）、`11 True 48`、`PASS research = params`

- [ ] **Step 5: コミットする**

```bash
git -C $MW add design/tier9-semiconductor-balance/spec/05_research.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: Tier9 S1 の研究（モジュール・GPU設計・スタック拡張）と半導体製造の解放内容を入れる"
```

### Task D8: マスタ全体の照合・push・本体のピン更新

**Files:**
- Modify: `.moorestech-external-revisions.json`（moorestech_master のピン）
- Create（生成物）: `moorestech_master/design/tier9-semiconductor-balance/sim/output.md`（再生成して差分が無いこと）

**Interfaces:**
- Consumes: D1〜D7 の全成果
- Produces: push 済みの `feat/tier9-cleanroom-playable`（moorestech_master）コミット `<MASTER_SHA>`。本体の `.moorestech-external-revisions.json` の `moorestech_master.commitHash == <MASTER_SHA>`

- [ ] **Step 1: 目標の判定と値の照合を通す**

Run: `cd $B && python3 sim/run_all.py > /dev/null; echo "exit=$?"; git -C $MW status --short design; python3 verify_master.py --mod-dir $MOD --ledger guids.json`
Expected: `exit=0`（`sim/output.md` §0 の目標判定が全行 PASS）。`git status` が空（output.md が D1 Step 5 のコミットから変わらない）。続けて
```
PASS structure
PASS upstream ratio
PASS recipes = params
PASS power / filter = params
PASS cleanRoom = params
PASS quality module permission
PASS modules = params
PASS research = params
```
終了コード 0。
- `exit=1`（目標の FAIL）なら、D1 Step 3 の順で `sim/params.py`・`sim/timeline.py` を直して D1 Step 2〜4 をやり直し、変えた値を spec 01〜05 に同じ値で書いて `python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json` で再適用してから、この Step をやり直す
- `verify_master.py` だけが FAIL なら、params.py を正としてマスタ側（spec）を直して再適用する

- [ ] **Step 2: master repo の出荷検査を通す**

Run: `cd $MW && python3 scripts/check_shipped_data_contracts.py`
Expected: 終了コード 0（`::error` 行なし）

- [ ] **Step 3: push してピンを向ける**

```bash
git -C $MW push -u origin feat/tier9-cleanroom-playable
MASTER_SHA=$(git -C $MW rev-parse HEAD)
python3 - "$MASTER_SHA" <<'EOF'
import json, sys
path = ".moorestech-external-revisions.json"
data = json.load(open(path))
next(r for r in data["repositories"] if r["key"] == "moorestech_master")["commitHash"] = sys.argv[1]
open(path, "w").write(json.dumps(data, indent=4, ensure_ascii=False) + "\n")
EOF
git diff --stat .moorestech-external-revisions.json
```
Expected: `1 file changed, 1 insertion(+), 1 deletion(-)`（書式は既存と同じインデント4）

- [ ] **Step 4: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: Tier9 半導体のマスタ投入（moorestech_master feat/tier9-cleanroom-playable）へピンを向ける"
```

### Task D9: unityプレイ録画テスト（出荷マスタ全体で起動し、チップライン・モジュールと GPU・専用機の見た目と抽選欄を確かめる）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-chip-line.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-modules-gpu.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-yield-ui.cs`

**Interfaces:**
- Consumes: D8 で push・ピン留めした moorestech_master（`run-scenario.sh` が `.moorestech-external-revisions.json` のコミットに HEAD が一致する master worktree＝`$MW` を自動で選ぶ）。D10 の prefab 5種。Part A のブロック `クリーンルームパネル`（北向きで水平の板）・ドア（1×2×1）。Part B の CleanRoomMachine の液体入力と抽選、B6 の抽選欄（testid `clean-room-yield-section`、行 `clean-room-yield-max-grade` / `clean-room-yield-promotion-chance`（成膜）・`clean-room-yield-layer-fail-rate`（EUV）・`clean-room-yield-die-loss-rate`（ダイシング）。役割レシピを選び、部屋が稼働できる機械にだけ出る）。Part C の継承レシピと製作上限。既存 `IResearchDataStore.LoadResearchData(ResearchSaveJsonObject)`（完了済みとして読み込み、各研究の解放アクションを再実行する）
- Produces: `moorestech_client/PlaytestResults/<run>/result.json`（Success・全 Assert PASS）、動画、スクショ `01-room-and-line` `02-dies` `03-chips`（chip-line）、`01-workshops` `02-modules-gpu`（modules-gpu）、`01-five-machines` `02-deposition-yield` `03-euv-yield` `04-dicing-yield`（yield-ui）

- [ ] **Step 0: サブエージェント探索（unity-playmode-recorded-playtest の必須ゲート）**

fresh-context の Explore サブエージェントに、次の名前と挙動を `$W` の実コード（Part A/B/C 反映後）で確かめさせ、違うものだけシナリオ側を直す。返答は「名前→実在するか・正しい綴り・根拠ファイル:行」の表で受け取る。
1. `IResearchDataStore.LoadResearchData` をプレイ中に呼んで、解放アクション（unlockBlock・unlockMachineRecipe・Part C の `unlockModuleSlotStackLevel`）がクライアントの解放状態にも届くか。届かない場合も本シナリオはサーバー側の直置き・直投入だけを使うので通るが、届かないことを報告に書く
2. `CleanRoomDatastore.TryGetCleanRoom(IBlock, out room)`・`room.ThresholdIndex`・`MasterHolder.CleanRoomMaster.OutThresholdIndex`（Part A/B の改名がないか）
3. `FluidStack` のフィールド名（`FluidId`・`Amount`）、`MasterHolder.FluidMaster.Fluids.Data[].Name/FluidGuid`、`MasterHolder.ItemMaster.GetItemMaster(ItemId).Name/ItemGuid`、`MachineRecipeMasterElement.BlockGuid/OutputItems[].ItemGuid`
4. `広範囲電柱` の配線距離と本数上限（chip-line は電柱 (-2,32,-3) から最遠で化学プラント (0,32,-26)・清浄機 (12,33,5)、yield-ui は電柱 (11,32,-3) から最遠で (21,33,2)・(18,33,6)。届かなければ電柱を2本にして電柱間を繋ぐ）、`IElectricWireNetworkDatastore.RebuildAround` の引数
5. CleanRoomMachine が入力アイテムから工程レシピを自動で選ぶか（EUV は3本のうち入力の層で決まる）。選ばないなら chip-line でも `SelectRecipe` を EUV・各専用機に呼ぶ（yield-ui は常に明示で選ぶ）
6. `VanillaMachineBlockInventoryComponent.InsertItem` が Part C の製作上限を超えるレベルを受け取らずに残りとして返すか（返さない経路なら、上限の拒否を確かめる正しい API に置き換える）
7. `PlaceBlockDirect` で y=32 の平地に化学プラント（6×16×11）と発電機が置けるか、パネルを北向きで置くと水平の板になるか（Part A の A4・A11 の配置と同じか）
8. 室内から機械を `AimAt`→`PressInteract` で開いたとき、抽選欄が出る条件（レシピ選択・部屋の稼働・給電）を B6 の実装で確かめる

- [ ] **Step 1: シナリオ3本を書く**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-chip-line.cs`:

```csharp
// 出荷マスタ全体で起動し、半導体製造の研究から IPA と Lv1/Lv2 の ICチップが室内ラインで作れることを通しで確かめる
// Boot on the full shipped master and confirm IPA and Lv1/Lv2 IC chips come out of the clean room line after the semiconductor research
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var research = new[]
{
    "019e3ad9-46e2-73fd-9555-8c1d0245d26d", "01a167f3-bd6b-70c9-be6e-52e41907b21f", "019e3ae2-5005-7439-b242-9b7b3191a2c3",
    "01a167f3-bd6e-7745-84c0-0e1cf105c2cc", "01a167f3-bd74-73f8-8c43-1869db833299", "01a167f3-bd77-7013-b982-5f4cdf3c86a9",
    "01a167f3-bd71-707c-b8e6-615063559cc1", "01a167f3-bd7a-75c4-a266-8b9a4d5d1900",
}.Select(Guid.Parse).ToArray();
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-chip-line", options, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    p.WarpPlayer(new Vector3(7f, 40f, -14f));
    var ipaId = MasterHolder.FluidMaster.GetFluidId(MasterHolder.FluidMaster.Fluids.Data.First(f => f.Name == "イソプロピルアルコール").FluidGuid);
    var pipes = new List<IBlock>();
    var consumers = new List<IBlock>();

    // 半導体製造と S1 の研究を前提ごと完了扱いにする（解放アクションも再実行される）
    // Mark the semiconductor and S1 research done with all prerequisites (unlock actions are replayed)
    p.Note("半導体製造と S1 の研究を前提ごと完了させる");
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() });
    await p.Until(() => research.All(researchStore.IsResearchCompleted), 15f, "S1 research completed");

    // 化学プラントで ナフサ＋水 → IPA を回す（液体は隣のパイプへ直接注入する）
    // Run naphtha + water -> IPA in the chemical plant (fluids are injected into adjacent pipes)
    p.Note("化学プラントへナフサと水を入れ、IPA のレシピを選ぶ");
    var chem = Place("化学プラント", new Vector3Int(0, 32, -26));
    var naphthaPipe = PipeAt(new Vector3Int(3, 33, -27));
    var waterPipe = PipeAt(new Vector3Int(2, 33, -27));
    var ipaOutPipe = p.PlaceBlockDirect("鋼鉄のパイプ", new Vector3Int(2, 33, -15), BlockDirection.North);
    SelectRecipe(chem, MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.OutputFluids.Any(f => MasterHolder.FluidMaster.GetFluidId(f.FluidGuid) == ipaId)).MachineRecipeGuid);

    // 部屋（パネルの床・天井、壁ブロックの周回壁、ドア）を建て、室内に専用機5種と清浄機2台を置く
    // Build the room (panel floor and ceiling, wall-block perimeter, door) and place the five machines and two air filters inside
    p.Note("クリーンルームを建て、専用機5種と清浄機を置いて給電する");
    BuildRoom();
    var cutter = PlaceWithPipe("ウェハ切断機", new Vector3Int(2, 33, 1));
    var depo = PlaceWithPipe("成膜装置", new Vector3Int(6, 33, 1));
    var euv = PlaceWithPipe("EUV露光式半導体製造装置", new Vector3Int(10, 33, 1));
    var dicing = PlaceWithPipe("ダイシング装置", new Vector3Int(2, 33, 5));
    var packaging = PlaceWithPipe("パッケージング装置", new Vector3Int(6, 33, 5));
    foreach (var x in new[] { 10, 12 }) Give(Place("クリーンルーム空気清浄機", new Vector3Int(x, 33, 5)), 0, "クリーンルームフィルター", 5);
    PowerAll();
    await p.Until(() => p.ServerService<CleanRoomDatastore>().TryGetCleanRoom(cutter, out var r) && r.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex, 30f, "clean room classified");
    await p.Screenshot("01-room-and-line");

    // IPA が化学プラントから出る
    // IPA comes out of the chemical plant
    await p.Until(() => { Feed(); return ipaOutPipe.GetComponent<IFluidInventory>().GetFluidInventory().Any(f => f.FluidId == ipaId && f.Amount > 0); }, 60f, "IPA produced");

    // 切断 → 成膜 → EUV の各工程が出力を出す
    // Slicing, deposition and EUV each produce output
    p.Note("高純度シリコン→ウェハ→成膜→EUV の順に各工程へ素材を入れる");
    Give(cutter, 0, "高純度シリコン", 20);
    await p.Until(() => { Feed(); return Count(cutter, "シリコンウェハ") > 0; }, 60f, "wafer sliced");
    Give(depo, 0, "シリコンウェハ", 2);
    await p.Until(() => { Feed(); return Count(depo, "トランジスタ設置済みウェハ") > 0; }, 60f, "transistor wafer deposited");
    Give(euv, 0, "トランジスタ設置済みウェハG1", 4);
    await p.Until(() => { Feed(); return Count(euv, "1層パターンウェハ") + Count(euv, "失敗したパターンウェハ") > 0; }, 60f, "EUV layer exposed");

    // ダイシング（G2 完成ウェハ）で Lv1 と Lv2 のダイが出る
    // Dicing a G2 finished wafer yields Lv1 and Lv2 dies
    p.Note("G2 の完成ウェハをダイシングして Lv1/Lv2 ダイを取る");
    Give(dicing, 0, "完成パターンウェハG2", 1);
    await p.Until(() => { Feed(); return Count(dicing, "シリコンダイLv1") >= 10 && Count(dicing, "シリコンダイLv2") >= 10; }, 60f, "Lv1 and Lv2 dies diced");
    await p.Screenshot("02-dies");

    // パッケージングで Lv1 → ICチップLv1、Lv2 → ICチップLv2（継承レシピ）
    // Packaging turns Lv1 dies into IC chip Lv1 and Lv2 dies into IC chip Lv2 (inheritance recipe)
    p.Note("Lv1 ダイと Lv2 ダイを順にパッケージングする");
    Give(packaging, 1, "ICチップ基板", 2);
    MoveDies("シリコンダイLv1");
    await p.Until(() => { Feed(); return Count(packaging, "ICチップLv1") > 0; }, 30f, "IC chip Lv1 packaged");
    MoveDies("シリコンダイLv2");
    await p.Until(() => { Feed(); return Count(packaging, "ICチップLv2") > 0; }, 30f, "IC chip Lv2 packaged");
    await p.Screenshot("03-chips");

    #region Internal

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var done = new HashSet<Guid>();
        var pending = new Stack<Guid>(targets);
        while (pending.Count > 0)
        {
            var guid = pending.Pop();
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        }
        return done;
    }

    IBlock Place(string name, Vector3Int pos)
    {
        var block = p.PlaceBlockDirect(name, pos, BlockDirection.North);
        consumers.Add(block);
        return block;
    }

    IBlock PipeAt(Vector3Int pos) => p.PlaceBlockDirect("鋼鉄のパイプ", pos, BlockDirection.North);

    IBlock PlaceWithPipe(string name, Vector3Int origin)
    {
        // 液体入口 offset(0,0,1)・向き -x の隣にパイプを置く
        // Put a pipe next to the fluid inlet at offset (0,0,1) facing -x
        pipes.Add(PipeAt(origin + new Vector3Int(-1, 0, 1)));
        return Place(name, origin);
    }

    void Feed()
    {
        foreach (var pipe in pipes) pipe.GetComponent<IFluidInventory>().AddLiquid(new FluidStack(50, ipaId), default);
        naphthaPipe.GetComponent<IFluidInventory>().AddLiquid(new FluidStack(50, FluidIdOf("ナフサ")), default);
        waterPipe.GetComponent<IFluidInventory>().AddLiquid(new FluidStack(50, FluidIdOf("水")), default);
    }

    FluidId FluidIdOf(string name) => MasterHolder.FluidMaster.GetFluidId(MasterHolder.FluidMaster.Fluids.Data.First(f => f.Name == name).FluidGuid);

    void Give(IBlock block, int slot, string item, int count) => block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(slot, PlaytestItemOps.ResolveItemId(item), count);

    int Count(IBlock block, string prefix)
    {
        var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
        return Enumerable.Range(0, inventory.GetSlotSize()).Select(inventory.GetItem)
            .Where(s => s.Count > 0 && MasterHolder.ItemMaster.GetItemMaster(s.Id).Name.StartsWith(prefix)).Sum(s => s.Count);
    }

    void MoveDies(string dieName)
    {
        var from = dicing.GetComponent<IOpenableBlockInventoryComponent>();
        var id = PlaytestItemOps.ResolveItemId(dieName);
        var slot = Enumerable.Range(0, from.GetSlotSize()).First(s => from.GetItem(s).Id == id && from.GetItem(s).Count >= 10);
        from.SetItem(slot, id, from.GetItem(slot).Count - 10);
        Give(packaging, 0, dieName, 10);
    }

    void SelectRecipe(IBlock block, Guid recipeGuid)
    {
        var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        p.Assert(selector.SetSelectedRecipe(MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid), overflow) == MachineRecipeSelectionResult.Success, $"select recipe {recipeGuid}");
    }

    void BuildRoom()
    {
        // 内寸 x1..13 / z1..9 / y33..34。床 y32・天井 y35 はパネル（北向き＝水平）、ドア（1×2×1）は南壁
        // Interior x1..13 / z1..9 / y33..34; floor y32 and ceiling y35 are flat panels, the 1x2x1 door is on the south wall
        var door = new Vector3Int(7, 33, 0);
        for (var x = 0; x <= 14; x++)
        for (var z = 0; z <= 10; z++)
        {
            p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 32, z), BlockDirection.North);
            p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 35, z), BlockDirection.North);
            if ((x != 0 && x != 14 && z != 0 && z != 10) || (x == door.x && z == door.z)) continue;
            for (var y = 33; y <= 34; y++) p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        }
        p.PlaceBlockDirect("クリーンルームドア", door, BlockDirection.North);
    }

    void PowerAll()
    {
        // 室外の発電機 → 広範囲電柱 → 全消費ブロックへ配線する
        // Wire the outdoor generator -> wide-range pole -> every consumer block
        var generator = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
        var gasoline = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
        generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, gasoline), FluidContainer.Empty);
        var pole = p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var genConn = generator.GetComponent<IElectricWireConnector>();
        p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, pole, free), "wire generator -> pole");
        foreach (var block in consumers)
            p.Assert(ElectricWireSystemUtil.TryConnectBothSides(pole, block.GetComponent<IElectricWireConnector>(), free), $"wire pole -> {block.BlockMasterElement.Name}");
        p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(genConn, pole);
    }

    #endregion
});
```

`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-modules-gpu.cs`:

```csharp
// 出荷マスタ全体で起動し、S1 の研究後に 4種のモジュールLv1 と GPU が作れ、Lv2 チップは製作上限で拒まれることを確かめる
// Boot on the full shipped master and confirm the four Lv1 modules and the GPU are craftable after S1 research, and Lv2 chips are refused by the craft cap
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var research = new[]
{
    "019e3ad9-46e2-73fd-9555-8c1d0245d26d", "01a167f3-bd6b-70c9-be6e-52e41907b21f", "019e3ae2-5005-7439-b242-9b7b3191a2c3",
    "01a167f3-bd6e-7745-84c0-0e1cf105c2cc", "01a167f3-bd74-73f8-8c43-1869db833299", "01a167f3-bd77-7013-b982-5f4cdf3c86a9",
    "01a167f3-bd71-707c-b8e6-615063559cc1", "01a167f3-bd7a-75c4-a266-8b9a4d5d1900",
}.Select(Guid.Parse).ToArray();
var productRecipes = new (string recipe, string product)[]
{
    ("01a167f3-bd88-747d-b30e-13f23f169ce4", "品質モジュールLv1"), ("01a167f3-bd8b-7732-badd-8c1a011f7c26", "速度モジュールLv1"),
    ("01a167f3-bd8e-7525-af33-5cce09108374", "省エネモジュールLv1"), ("01a167f3-bd90-7307-91a6-e45243f99379", "生産性モジュールLv1"),
    ("01a167f3-bd94-76d2-ada6-617fdbf3d9b3", "GPU"),
};
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-modules-gpu", options, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    p.WarpPlayer(new Vector3(8f, 40f, -4f));

    // 半導体製造と S1 の研究を前提ごと完了扱いにする（解放アクションも再実行される）
    // Mark the semiconductor and S1 research done with all prerequisites (unlock actions are replayed)
    p.Note("半導体製造と S1 の研究を前提ごと完了させる");
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() });
    await p.Until(() => research.All(researchStore.IsResearchCompleted), 15f, "S1 research completed");

    // 電気汎用工作装置を5台並べて給電し、各レシピを選んで材料を入れる
    // Place five powered electric workshops, select each recipe and insert its inputs
    p.Note("電気汎用工作装置5台でモジュール4種と GPU のレシピを選び材料を入れる");
    var crafters = productRecipes.Select((_, i) => p.PlaceBlockDirect("電気汎用工作装置", new Vector3Int(i * 4, 32, 0), BlockDirection.North)).ToArray();
    PowerAll();
    for (var i = 0; i < productRecipes.Length; i++)
    {
        var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(productRecipes[i].recipe));
        SelectRecipe(crafters[i], recipe.MachineRecipeGuid);
        foreach (var input in recipe.InputItems) Insert(crafters[i], input.ItemGuid, input.Count);
    }
    await p.Screenshot("01-workshops");

    // 5製品の Lv1 がそれぞれ出る
    // Each of the five products comes out at Lv1
    for (var i = 0; i < productRecipes.Length; i++)
    {
        var index = i;
        await p.Until(() => Count(crafters[index], productRecipes[index].product) > 0, 60f, $"{productRecipes[index].product} crafted");
    }
    await p.Screenshot("02-modules-gpu");

    // 製作上限 Lv1 のため ICチップLv2 は品質モジュールの工作装置へ入らない（S2 の研究まで）
    // With the craft cap at Lv1, IC chip Lv2 does not enter the quality-module workshop (until S2 research)
    p.Note("製作上限 Lv1 の工作装置へ ICチップLv2 を入れて拒まれることを見る");
    var lv2 = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId("ICチップLv2")).ItemGuid;
    var rest = Insert(crafters[0], lv2, 5);
    p.Assert(rest.Count == 5, "IC chip Lv2 is refused by the Lv1-capped module recipe");

    #region Internal

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var done = new HashSet<Guid>();
        var pending = new Stack<Guid>(targets);
        while (pending.Count > 0)
        {
            var guid = pending.Pop();
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        }
        return done;
    }

    IItemStack Insert(IBlock block, Guid itemGuid, int count)
    {
        return block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(ServerContext.ItemStackFactory.Create(itemGuid, count));
    }

    int Count(IBlock block, string itemName)
    {
        var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
        var id = PlaytestItemOps.ResolveItemId(itemName);
        return Enumerable.Range(0, inventory.GetSlotSize()).Select(inventory.GetItem).Where(s => s.Id == id).Sum(s => s.Count);
    }

    void SelectRecipe(IBlock block, Guid recipeGuid)
    {
        var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        p.Assert(selector.SetSelectedRecipe(MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid), overflow) == MachineRecipeSelectionResult.Success, $"select recipe {recipeGuid}");
    }

    void PowerAll()
    {
        // 発電機 → 広範囲電柱 → 工作装置5台へ配線する
        // Wire generator -> wide-range pole -> the five workshops
        var generator = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
        var gasoline = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
        generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, gasoline), FluidContainer.Empty);
        var pole = p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var genConn = generator.GetComponent<IElectricWireConnector>();
        p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, pole, free), "wire generator -> pole");
        foreach (var crafter in crafters)
            p.Assert(ElectricWireSystemUtil.TryConnectBothSides(pole, crafter.GetComponent<IElectricWireConnector>(), free), "wire pole -> workshop");
        p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(genConn, pole);
    }

    #endregion
});
```

`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-yield-ui.cs`:

```csharp
// 出荷マスタ全体で起動し、専用機5種の見た目が互いに違い、成膜・EUV・ダイシングの機械UIに抽選の中身が出ることを確かめる
// Boot on the full shipped master and confirm the five machines look distinct and the deposition, EUV and dicing UIs show the draw values
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var machineNames = new[] { "ウェハ切断機", "成膜装置", "EUV露光式半導体製造装置", "ダイシング装置", "パッケージング装置" };
var semiconductorResearch = Guid.Parse("019e3ad9-46e2-73fd-9555-8c1d0245d26d");
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-yield-ui", options, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    await p.SetupFlatGround();
    var consumers = new List<IBlock>();

    // 半導体製造の研究を前提ごと完了扱いにする
    // Mark the semiconductor research done with all prerequisites
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(semiconductorResearch).Select(g => g.ToString()).ToList() });
    p.Assert(researchStore.IsResearchCompleted(semiconductorResearch), "semiconductor research completed");

    // 床のパネルを敷き、専用機5種を1列に置いて、壁を建てる前に見た目を撮る
    // Lay the panel floor, line up the five machines and shoot their looks before the walls go up
    p.Note("専用機5種を1列に置き、見た目の違いを撮る");
    for (var x = 0; x <= 22; x++)
    for (var z = 0; z <= 10; z++)
        p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 32, z), BlockDirection.North);
    var machines = machineNames.Select((name, i) => Place(name, new Vector3Int(2 + i * 4, 33, 6))).ToArray();
    foreach (var machine in machines) await p.WaitBlockGameObject(machine.BlockPositionInfo.OriginalPos);
    p.WarpPlayer(new Vector3(11.5f, 32.5f, -8f));
    await p.Screenshot("01-five-machines");

    // 壁・ドア・天井で閉じ、清浄機2台にフィルターを入れて給電し、クラスが付くまで待つ
    // Close the room with walls, door and ceiling, load two air filters, power everything and wait for a class
    p.Note("部屋を閉じて清浄機を動かし、クラスが付くのを待つ");
    BuildWallsAndCeiling();
    foreach (var x in new[] { 1, 21 }) Give(Place("クリーンルーム空気清浄機", new Vector3Int(x, 33, 2)), 0, "クリーンルームフィルター", 5);
    PowerAll();
    await p.Until(() => p.ServerService<CleanRoomDatastore>().TryGetCleanRoom(machines[1], out var r) && r.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex, 30f, "clean room classified");

    // 抽選欄は役割レシピを選んだ機械にだけ出るので、成膜・EUV1層・ダイシングを選ぶ
    // The draw section only shows for a selected role recipe, so select deposition, EUV layer 1 and dicing
    SelectRecipeByFirstOutput(machines[1], "トランジスタ設置済みウェハG1");
    SelectRecipeByFirstOutput(machines[2], "1層パターンウェハG1");
    SelectRecipeByFirstOutput(machines[3], "シリコンダイLv1");

    // 室内に入り、3台の機械UIで抽選欄の行を確かめて撮る
    // Step inside and check and shoot the draw rows in the three machine UIs
    p.Note("成膜・EUV・ダイシングの機械UIを開き、抽選欄を確かめる");
    p.WarpPlayer(new Vector3(11.5f, 33.5f, 3.5f));
    await CheckYieldUi(machines[1], "02-deposition-yield", new[] { "clean-room-yield-max-grade", "clean-room-yield-promotion-chance" });
    await CheckYieldUi(machines[2], "03-euv-yield", new[] { "clean-room-yield-layer-fail-rate" });
    await CheckYieldUi(machines[3], "04-dicing-yield", new[] { "clean-room-yield-die-loss-rate" });

    #region Internal

    IEnumerable<Guid> WithPrerequisites(Guid target)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var done = new HashSet<Guid>();
        var pending = new Stack<Guid>(new[] { target });
        while (pending.Count > 0)
        {
            var guid = pending.Pop();
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        }
        return done;
    }

    IBlock Place(string name, Vector3Int pos)
    {
        var block = p.PlaceBlockDirect(name, pos, BlockDirection.North);
        consumers.Add(block);
        return block;
    }

    void Give(IBlock block, int slot, string item, int count) => block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(slot, PlaytestItemOps.ResolveItemId(item), count);

    void BuildWallsAndCeiling()
    {
        // 内寸 x1..21 / z1..9 / y33..34。天井 y35 はパネル、ドア（1×2×1）は南壁の中央
        // Interior x1..21 / z1..9 / y33..34; the y35 ceiling is panels, the 1x2x1 door sits mid south wall
        var door = new Vector3Int(11, 33, 0);
        for (var x = 0; x <= 22; x++)
        for (var z = 0; z <= 10; z++)
        {
            p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 35, z), BlockDirection.North);
            if ((x != 0 && x != 22 && z != 0 && z != 10) || (x == door.x && z == door.z)) continue;
            for (var y = 33; y <= 34; y++) p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        }
        p.PlaceBlockDirect("クリーンルームドア", door, BlockDirection.North);
    }

    void SelectRecipeByFirstOutput(IBlock block, string firstOutputName)
    {
        // その機械の工程レシピのうち、最初の出力が指定アイテムのものを選ぶ
        // Select the machine's stage recipe whose first output is the given item
        var output = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId(firstOutputName)).ItemGuid;
        var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == block.BlockMasterElement.BlockGuid && r.OutputItems.Any() && r.OutputItems.First().ItemGuid == output);
        var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        p.Assert(selector.SetSelectedRecipe(recipe, overflow) == MachineRecipeSelectionResult.Success, $"select recipe -> {firstOutputName}");
    }

    async UniTask CheckYieldUi(IBlock machine, string screenshotName, string[] rowTestIds)
    {
        // 3×2×3 の中心を狙って開き、抽選欄と行を待って撮り、Tab で閉じる
        // Aim at the 3x2x3 center, open it, wait for the section and rows, shoot, then close with Tab
        var origin = machine.BlockPositionInfo.OriginalPos;
        await p.AimAt(new Vector3(origin.x + 1.5f, 34f, origin.z + 1.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.UntilWebUiElement("clean-room-yield-section", 10f);
        foreach (var rowTestId in rowTestIds) await p.UntilWebUiElement(rowTestId, 10f);
        await p.Screenshot(screenshotName);
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    void PowerAll()
    {
        // 室外の発電機 → 広範囲電柱 → 室内の機械と清浄機へ配線する
        // Wire the outdoor generator -> wide-range pole -> the machines and air filters inside
        var generator = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-6, 32, -14), BlockDirection.North);
        var gasoline = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
        generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, gasoline), FluidContainer.Empty);
        var pole = p.PlaceBlockDirect("広範囲電柱", new Vector3Int(11, 32, -3), BlockDirection.North).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var genConn = generator.GetComponent<IElectricWireConnector>();
        p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, pole, free), "wire generator -> pole");
        foreach (var block in consumers)
            p.Assert(ElectricWireSystemUtil.TryConnectBothSides(pole, block.GetComponent<IElectricWireConnector>(), free), $"wire pole -> {block.BlockMasterElement.Name}");
        p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(genConn, pole);
    }

    #endregion
});
```

- [ ] **Step 2: 調査実行（Record=false のコピー）で通す**

Run（`$W` から。3本とも同じ手順。下は chip-line の例で、`tier9-modules-gpu.cs`・`tier9-yield-ui.cs` もファイル名だけ替えて流す）:
```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"; mkdir -p "$SCRATCH"
sed 's/Record = true/Record = false/' "$SKILL/scenarios/misc/tier9-chip-line.cs" > "$SCRATCH/tier9-chip-line.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/tier9-chip-line.cs"
```
Expected: result.json が `"Success": true`、Asserts と Until がすべて PASS。マスタのロード失敗（MooresmasterLoaderException）なら preflight か起動で止まるので、そのログの要素名を D3〜D7 の spec へ戻って直す

- [ ] **Step 3: 提出用の録画実行（Record=true）**

Run: `"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/tier9-chip-line.cs"`、同じく `tier9-modules-gpu.cs`・`tier9-yield-ui.cs`
Expected: 3本とも Success・全 Assert PASS・動画が 0 byte でない。スクショを Read で開いて次を見る（実プレイ視点: アバター・地面・HUD が写る）:
- chip-line `03-chips`: パッケージング装置の出力に ICチップLv1 と ICチップLv2
- modules-gpu `02-modules-gpu`: 5台の出力に各製品
- yield-ui `01-five-machines`: 5台の色と形が互いに違い、灰色の箱や TmpBlock の見た目が無い（D10 の prefab が解決している）
- yield-ui `02-deposition-yield`・`03-euv-yield`・`04-dicing-yield`: 機械UIに抽選欄が出て、成膜は「上限グレード」と「昇格確率」、EUV は「層失敗率」、ダイシングは「ダイ損失率」の行が数値つきで読める

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-chip-line.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-modules-gpu.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/tier9-yield-ui.cs
git commit -m "test: Tier9 半導体のチップライン・モジュールと GPU・専用機の見た目と抽選欄の録画プレイテストを追加する"
```

---

---

## 判断記録（Part D）


- 値の正本を `sim/params.py`（研究コストは `sim/timeline.py`）に置き、マスタへは名前参照の spec を適用スクリプトで入れ、`verify_master.py` が「マスタ＝params」を検査する。出所: agent前提（転記ミスをシミュレーションとの食い違いとして機械で捕まえるため。置き場は前例 `/Users/sakastudio/hermes-agent/data/repos/moorestech_master/design/gear-connector-rules/`）
- シミュレーションの目標を `sim/targets.py` で機械判定し、FAIL なら D1 Step 3 の順（研究の後半3ノードの chips → 生産性の効果 → 品質の効果・昇格上限 → クラスC の層失敗率 → 上流のレシピ時間 → kHatch・aConnector）で1つずつ動かす。`DIES_PER_CHIP=10`・`DIES_PER_WAFER=300` は動かさない。出所: agent前提（裁定の目標を数値で閉じるため。ダイ数はユーザー裁定 .decisions/2026-10-23-ダイ数と機械の見た目と部材と部屋の状態の取得.md）
- 下書き値のまま生産性込みで回すとクラスA が 5.83h で S1 の下限を割る（実測）。スタック拡張II を 7000→10000 にすると A6.0/B6.45/C7.1h で全 PASS になる（参考の感度。どの値で閉じるかは D1 Step 3 で実行者が決めて「シミュレーション結果」節に記録する）。出所: agent前提（本 plan 作成時の sim 実行）
- 室内5台の生産性モジュールは「生産性モジュール」研究の後に各3個（Lv1）入れ、追加セットは同じ抽選結果をもう1組並べる。出所: シミュレーター予測→ユーザー承認（.decisions/2026-10-23-上位チップは下位の代わりに使え抽選の中身は機械UIと研究に見せる.md）
- 研究の ICチップLv1 の要求は ICチップLv2 以上でも払え、sim は Lv1 を先に使い、足りない分を Lv2 で払う（下位から消費）。モジュール・GPU の製作は製作上限 Lv1 なので Lv1 だけで払う。出所: シミュレーター予測→ユーザー承認（.decisions/2026-10-23-上位チップは下位の代わりに使え抽選の中身は機械UIと研究に見せる.md）
- 上流で約2倍にする段は電気炉（金属シリコン8秒、粉砕2秒・化学4秒）。基準ライン1本＝高純度シリコン 0.5/秒で 2:4:2。出所: agent前提（炉は既存の鋼鉄板生産とも取り合い「増やす段」として分かりやすい。上流3段はユーザー裁定 .decisions/2026-10-23-超純水は廃止しIPAを室内各工程で使い上流は砂から高純度シリコンの3段にする.md）
- ICチップ1個にダイ10個・1ウェハ300ダイ。出所: ユーザー裁定（.decisions/2026-10-23-ダイ数と機械の見た目と部材と部屋の状態の取得.md）
- リサイクルは 失敗ウェハ1→高純度シリコン5・失敗ダイ60→1 で、1本あたりのチップの増分を 生産性なしで +30% 前後・生産性込みで +55% 前後に抑える。出所: agent前提（sim §2 のリサイクルあり・なしの行。本 plan 作成時の実行でクラスB・品質Lv1×3＋生産性Lv1×3 が 84.4→129.7 /分。室外で砕くのはユーザー裁定 .decisions/2026-10-23-失敗ウェハと失敗ダイは室外で砕いて高純度シリコンへ一部戻す.md）
- 清浄機 q=5・電力150・フィルター容量20000、汚染係数 aVolume0.02/aSurface0.04/aConnector3.0/aMachine2.0/kHatch4.0。ハッチは kHatch の汚染に加えて搬送量の上限（S1 の A14。本番の暫定値は毎秒4個）で工程分割を高くつかせる（裁定 `.decisions/2026-10-23-アイテムハッチに搬送量の上限を足して大部屋集約を有利にする.md`）。出所: agent前提（sim §3。大部屋集約を有利にするのはユーザー裁定 .decisions/2026-10-23-クリーンルームの抽選と建て方の細部.md。上限を入れるにはスキーマとコードが要り Part A/B の範囲を広げるため）
- ドアは接続口に数えない（sim の部屋の接続口は パイプハッチ1＋アイテムハッチ4）。出所: agent前提（Part A の A1。本 plan の Task A1）
- 機械オーバークロックの前提を 半導体製造 → 品質モジュール に付け替え、S1 研究を1本の鎖＋2分岐に並べる。金属超精密加工（S3）の到達はその分後ろへずれる。出所: agent前提（研究を直列に並べないと 6〜8時間の配分にならない。sim §4。/Users/sakastudio/hermes-agent/data/repos/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/research.json）
- 初回ライン完成3.0h・2本目4.0h・3本目5.5h の建設時間の仮定で S1 時間を測る。出所: agent前提（実測できない値。実プレイの計測が出たら `timeline.py` の `BUILD` を直して D1 Step 3 で締め直す）
- 品質モジュールは成膜・EUV・ダイシングだけ `allowQualityModule: true`（EUV・ダイシングは `Inherit` で入力グレードを受け継いだうえで品質が失敗率・損失率を下げる）。ウェハ切断機とパッケージング装置は品質を使う抽選が無いので `false`。出所: ユーザー裁定の統合（共通ブリーフの解決1。/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-cleanroom-playable/.decisions/2026-10-23-モジュールは品質速度省エネ生産性の4種で歩留まりは品質に統合する.md）
- EUV は層ごとの機械レシピ3本、成膜は `None`、EUV・ダイシング・パッケージングは `Inherit`、モジュール・GPU は `InheritUpToUnlocked`。出所: agent前提（Part B のレシピの約束と Part C の製作上限。本 plan の Part B・Part C）
- `apply_spec.py` は既存要素を置き換えるとき sortPriority と研究の UIPosition を残す。出所: agent前提（master-refine 操作 A・B の再計算を再適用で打ち消さないため）
- 研究で解放するモジュール・GPU のレシピ表示は Lv1 だけ。Lv2 以上は S2 の研究が解放する。出所: agent前提（S2 との取り決め（判断記録）。製作上限と表示を揃える）
- 専用機5種は既存 `Semiconductor_Manufacturing_Equipment` を複製し、本体の色・上面のアクセントの形・ラベルを変えた簡易モデルにする（置き場 `Assets/AddressableResources/Block/CleanRoomMachine/`・材質 `Assets/AddressableResources/Material/CleanRoomMachine/`・グループ `Vanilla Asset Group`）。複製なので原点・大きさ・当たり判定・入口/出口/IPA 入口の位置は5種とも同じ。出所: ユーザー裁定（.decisions/2026-10-23-ダイ数と機械の見た目と部材と部屋の状態の取得.md）。置き場は agent前提（前例 `moorestech_client/Assets/AddressableResources/Block/Util/` と `Material/`）
- 化学プラントの液体タンクは 入力0=ナフサ（既存口 (3,1,0)）・入力1=水（新口 (2,1,0)）・出力0=IPA（新口 (2,1,10)）、アイテム入口は新口 (3,0,0)。出所: agent前提（レシピの inputFluids の順がタンクに束縛される現行仕様。既存のナフサ配管の互換を保つ）
- 壁とパネルのコストは 鋼鉄板1＋プラスチック1、専用機・清浄機・ハッチ・ドアは鋼鉄フレーム中心。出所: agent前提（作者メモ「鋼鉄を大量に要求」。部屋1つで壁とパネル≈630枚使うので1枚は軽くし、機械側で鋼鉄フレームを要求する）
- ダイシング装置の出力スロットは6（Lv1〜Lv5＋失敗ダイの専用スロット）。ダイと失敗ダイはスタック表「ダイ」[600]。成膜・EUV の出力スロットは増やさない。出所: agent前提（Part B の B5「出力スロット N+1 以上・スタック ≥ diesPerWafer×2」）とユーザー裁定（.decisions/2026-10-23-上位チップは下位の代わりに使え抽選の中身は機械UIと研究に見せる.md の (3)）
- 電気粉砕機に 石→砕いた石材 のレシピは足さない。出所: agent前提（砕いた石材は前の Tier の素材で供給経路が既にあり、1本あたり 1/秒は原始的な粉砕機3台で賄える。電気版を足すと前の Tier の手触りを変える）
- 化学プラントの prefab に口の見た目は足さない。出所: agent前提（化学プラントの見た目は Tier8 の既存資産で、口の位置はブロックのコネクタ表示で分かる）
- 新規アイテムの sortPriority は仮値を入れ、D7 で master-refine 操作 A が再計算する。出所: agent前提（master-refine の規約）
- D9 の研究は `LoadResearchData` に前提の閉包を渡して完了扱いにする。出所: agent前提（`CompleteResearch` は前提・素材が揃わないと拒む。/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-cleanroom-playable/moorestech_server/Assets/Scripts/Game.Research/ResearchDataStore.cs）

## 設計検査の持ち越し（S1 plan の検査 2026-10-23 で Part D に当たった弱い指摘。ユーザー承認「全部拾う」）

- sim の Python が B1 `ChipYieldFormula`・`MachineModuleEffect`・C11 の研究消費と同じ導出を書き直しており、照合が無い → B1 の期待値テストと同じ入力（クラスA・品質合計0.6・最高tier2 で Lv1/Lv2/Lv3 = 191.114/44.909/13.473）で sim を突き合わせる fixture を D1 に足す
- `verify_master.py` が B5 のスタック検証・B3 の役割検証・クラス行の単調性を Python でもう1本持つ → D8 の検証は、出荷マスタでサーバーを起動して起動時のマスタ検証を通す1本に寄せ、Python 側は値の照合（params と JSON の一致）だけにする
- 「電気炉だけ2倍」の判定が targets.py と verify の `check_upstream_ratio` に2か所あり基準が違う（verify は炉以外が2倍でも PASS）→ params 側の1関数にまとめ両方から呼ぶ
- `math.ceil(v - 1e-6)` が5か所にあり定義済みの `ceil_counts` を使っていない → 全箇所をその関数に置き換える
- 品目の種類を名前の前方一致（`startswith("シリコンダイ")` 等）で決めている → `levelFamilies` と Guid 台帳で判別する
- 既存の `moorestech_master/tools/research_sync/sync_machine_recipe_unlocks.py` は D7 の解放の割り当てと役割が重なり、D5 で消す旧 EUV レシピを `KNOWN_ORPHANS` に登録したままで `--check` が exit 2 になる見込み（推測・未実行）→ D7 で使うか、D5 で `KNOWN_ORPHANS` から外す
- S1 の裁定で追加: アイテムハッチの搬送量上限（S1 の Task A14 が必須キーを足し、本番値は暫定）の本番値をバランスで決め、sim の部屋の比較（大部屋集約が有利）に搬送量の上限を入れる（`.decisions/2026-10-23-アイテムハッチに搬送量の上限を足して大部屋集約を有利にする.md`）
- S1 の裁定で追加: 製作上限を超えるチップは製品に入れず取っておく（`.decisions/2026-10-23-製作上限を超えるチップは製品に入れず解放まで取っておく.md`）。sim の S1 期間の Lv2 チップは研究の代用か在庫として数える
