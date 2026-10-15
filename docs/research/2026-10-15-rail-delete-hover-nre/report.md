# 破壊モードのレールホバーNRE 調査

## 結論

(A) ホバーNREの発生とnull値は未確認。地形ファイル欠落でゲーム初期化が止まり、ホバーまで到達していない。NREの原因本体は未確定。
(B) レール/橋脚の撤去可否と拒否理由は未確認。起動失敗のため破壊操作へ到達していない。
(C) 橋脚原点4本は静的抽出できた（下表）。実地形が未ロードのため「本来置けない」の拒否理由は未測定。

## 入力資料と互換性

- テスターセーブ: https://drive.google.com/file/d/1eIeRXyIQ2_yr_DbmONivrGJOUYFedWGG/view 。zip内は `5/save.json` (2026-10-15 01:06、6,402,224 bytes)。NRE発生時ではなく、再現しなくなった後の保存。
- 地形: https://drive.google.com/file/d/10Jv0kh9l7mp6VY_FK8w8XZl9wFFDefd5/view 。`map_world_json_20261015_0048/`にmap.json/world.jsonのみ。
- 報告画像: https://i.gyazo.com/0bc802345cfd3158188a5c33165b88e6.jpg 。画面左に橋脚付き短いレールが映る。
- 配信記録 `playtest-20261014-2253/validated-steam-build.json` と `betas-after-promotion.json`: build ID 25936926 がplaytestへ反映。`promotion.md` のソースはb48d6ac18f4a9b447c841f43cc716532970831f3。
- 配信ソースのmaster pinはa159a4efbc0067ece717d51b54b9e95295adf0b4。本作業branchのpinは47f79caa83c55b1c945dc3d86b1a91c5a0e5c3b3。両者の `server_v8/mods` 差分は回転発電機openable false→trueの1行のみ。ブロック/アイテムGUID削除なし。
- 互換pinのmasterをEditorで `MasterHolder.Load` した戻り値は `master-load-ok` (Success=true)。
- world.json: seed=196、generatorVersion=4.0.0、mapMode=generated。保存プレイヤー位置=(1075.32239,10.1662407,455.552765)。

## 保存された橋脚・レール

橋脚GUID `fbefeea3-0a66-4064-b1ff-a22ebf92126d`。下表はsave.json/masterからの静的抽出であり、ゲーム上の撤去・設置可否の測定ではない。

| 橋脚原点 | レール接続点 | 対向橋脚 |
|---|---|---|
| (1065,10,451) | (1066.5,11,452.5) | (1077,10,451) |
| (1077,10,451) | (1078.5,11,452.5) | (1065,10,451) |
| (1138,10,450) | (1139.5,11,451.5) | (1148,10,450) |
| (1148,10,450) | (1149.5,11,451.5) | (1138,10,450) |

保存位置から前半2本を第一候補とした。画像の「左」との座標対応は未確定。

## 検証環境

指定worktree `fix/rail-delete-hover-freeze`、Unity 6000.3.8f1。既存world_generatedをworld_generated.bak-20261015へ退避し、取得3ファイルを配置。原本zip/saveはgitへ追加しない。

## 観測

起動プリフライトは4項目すべてPASS（疎通、compile、master実在、MasterHolder.Load）。固定world入口でPlayModeへ入ったがready.markerは生成されず、起動ログに以下を観測した。

```text
FileNotFoundException: Could not find file '.../Saves/world_generated/terrain/height_0_0.r16'.
TerrainTransferMetaReader.cs:52 FileInfo.Length
TerrainTransferMetaReader.cs:51 CalculateChunkTotal
TerrainTransferMetaReader.cs:43 ReadGenerated
TerrainTransferMetaReader.cs:24 Read
WorldProvisioner.cs:36 EnsureWorld
ServerInstanceStartup.cs:47 Start
```

zip一覧にもterrainディレクトリは存在しない。これはホバーNREの原因を特定する証拠ではなく、今回の再現試行を妨げた入力不足の証拠。
同じ起動で `[WebUiHost] Node binary not found at .../moorestech_web/node/mac-arm64/bin/node. Run moorestech_web/setup.sh ... first.` (`ViteProcess.cs:93`) も出た。地形欠落の解消後にはWeb UIセットアップも必要。

ランナーはPlayMode突入時の応答再試行で `ERROR: already playing` を返した。本体はcontrol-play-modeのIsPlaying=trueとget-logsで実際の起動状態を確認した。ゲーム初期化前の画面を `Rendering_20261015_133653_076.png` に保存。プレイ映像・撤去成功の証拠ではない。操作シナリオ未実行なので録画mp4は生成されていない。

Task 2 Step 2の「ロードに失敗したらログ行を書いてTask 2を止める」に従って実機観測を停止。Task 3は証拠付き原因未確定のため見送り。候補箇所へのnullガードは追加しない。

## 詰み解消の検証

- Unity compile: ErrorCount=0、WarningCount=48（UnitGenerator型競合、既存obsolete API等）。
- `Client.Tests.UIState|Client.Tests.BuildUndo|Client.Tests.RailGraph`: TestCount=164、PassedCount=164、FailedCount=0、SkippedCount=0、CompletedAt=2026-10-15T04:35:47.0970490Z。
- Reset例外後のCurrent=null・選択空・DeleteCount=1、複数対象の全Delete/Undo、破棄済み警告、入力なしの同対象再Hoverを挟むUI遷移をテストした。
- 独立レビューで、単にCurrentを空にするだけでは再Hoverで詰みが再発する静的故障シーケンスを捕捉。単体ホバー/選択の共通Reset回復モデルへ修正して再レビュー承認済み。
- 実プレイでNRE後にGで離脱する確認は未実施。起動失敗を「再現せず」とは扱わない。

## 残る確認

テスターの `terrain/` を含む完全なworldが必要。現資料だけではホバーNRE・新版の撤去不可・設置拒否理由の原因は判定できない。Task 1の例外回復は164件のユニットテストで確認した範囲に限る。


## 最終レビュー後の状態（draft・承認待ち）

承認待ち・未完了: 複数Reset失敗時の再帰と元例外詳細の喪失が残る。原因本体は未確定・詰み防止の変更。復元1か所のcatchを明示例外とするか、非同期回復へ変更するかをユーザーへ質問済みで、回答前の安全確定・修正完了とは扱わない。

- コードhead 7025e66853のcompile前置成功。フィルタ `Client.Tests.UIState|Client.Tests.BuildUndo|Client.Tests.RailGraph` は177/177 PASS、Failed0、Skipped0、CompletedAt=2026-10-15T05:24:17.5275030Z。以前の181件は接頭辞を限定しない広いフィルタであり、最終検証と件数が異なる。
- 本レビューは発火30/失敗0、23/23系統とCodex3/3回収。w7 correctness/structureを確認。bug-passは発火10/失敗0、6/6とCodex1/1回収。
- 未観測Warningは部分送信時Undo欠落、橋脚経由の共有レールReset迂回、再帰StackOverflow。必要状態・限界件数は未測定。先行例外は型名Warningのみとなり、元例外詳細が最後の例外へ置換される懸念も残る。
- 実セーブでの起動失敗と未確認範囲は上記のとおり。完全worldによる再観測はmoorestech-cdddu.1、元タスクはmoorestech-cdddu。
