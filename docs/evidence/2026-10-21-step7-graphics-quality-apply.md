# Step7 D1〜D4案Aの適用検証

実装commit: `6a6f202b9c`。ユーザー原文「AでPR作成して」はD1〜D4全てA承認として適用。裁定4本、planの通信契約/D7/D10/R5b、CONTEXTを追従。

- D1: 単項目item/value union、最新ホスト値への適用。全9項目の連続操作とpreset直後操作、拒否理由・対象と保存不変を検証。
- D2: 設置完了時だけparticleの実再生を既存保存フラグへOR。非active→低で生成→activeで再生→設置完了停止→高で再開をPlayMode入口試験で確認。通常同値OFFで手動再開を保存しない条件も検証。
- D3: Sprint同形の復旧boolをLoadから明示選択まで保持。複数回読取、同値preset/項目の解除eventを実購読接続で確認。1280×720/2432×786で復旧表示のrectとスクリーンショットを確認。
- D4: vSync 0/Chr001部分非表示の除外は今回の承認として新裁定へ記録。旧原文へ過去の承認を遡及していない。

## 結果

| 対象 | 結果 |
|---|---|
| Unity GraphicsQuality/EffectsQuality | 88/88、失敗0、skip0 |
| 検出器が指定した保存往復 | 260/260、失敗0、skip0 |
| Web settings/bridge/PauseMenuのVitest | 28ファイル262/262 |
| PauseMenu/設定画面のPlaywright | 8/8 |
| TypeScript / ESLint | 成功 |
| 最終Unity compile | Error0/Warning0（増分） |
| 画質5シナリオ補助compile | 5/5、診断0、本文は未実行 |

Unity対象XML開始`2026-10-21 00:59:20Z`、保存往復XML開始`01:02:58Z`。対象実行のCLIはPlayMode遷移で切断したが二重実行せず今回XMLを回収した。途中compileには46警告、Webには環境・辞書警告があり、生ログを保存。全警告の新旧差分は未測定。

89シナリオの正規比較は**新規診断0、既存照合75、未確認23**、status `unverified`。source行不明等の23件を既存扱いにしない。画質5件には非async wrapper由来と推測されるCS4032があり、スキルを変更せず検証用コピーのwrapperだけasync Task<object>とした補助compileで診断0を確認した。補助結果は全体23件を免責しない。

snapshot: w7-s0=`b5aa742c47614dadc895f0a36a521276324c8ed6`、w7-s1=`c81b9ff1a38b80e68ecf9b774c2eadf457580866`。指定RUNDIRの`refix/w7-round1.diff`と前後scenario JSONを保存。検出器の「線・チェーンを含むセーブからクライアント起動を通す自動試験がない」未確認も保持。

## 復元と範囲

PlayerPrefsの存在・生値、購読、復旧boolをscope/finallyで復元。最終GraphicsQualityキー無/空・復旧false、Environmentキー無/-1、PlayMode false、LOD1、UniversalRP-HighQuality、BootstrapDisabled false。最終Console Error0。

所有Editor PID69780の専用projectPathを確認して`kill 69780`のみ実行、終了後liveEditors0。自動_CompileRequesterハッシュ差分は退避して復元、pin差分0byte、両者を成果commitに含めていない。master互換pin923367a2/server_v8を維持。通常saves・元セーブ・Library・skill・reaperは変更なし。

今回の担当は実装・対象試験・診断まで。実prefab全数の発症確認、録画再実行、最終bug-pass、PRは親の後続工程。

生証拠・詳細コマンド・2画面PNG・全XML/JSON/ログ:
https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-21-step7-apply/
