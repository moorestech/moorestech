# 最終bug-pass後の画質録画検証

b6242b05c4の製品コードとD1〜D4案Aを維持して検証。追加変更は録画fixtureと復元処理のみ（f14c3bb394 / c0cc2f922f / 15abe42105）。

- Unity絞込107/107、compile Error0（Warning0は増分）。Web tsc/lint成功、設定/bridge Vitest262/262、PauseMenu Playwright8/8。
- 最終6録画85/85、Record=true、各ErrorLogs=[]、全mp4のffprobe/全体decode成功、実画面を目視。
- 中60秒・録画あり平均25.260fps、実時間照合25.253fps、1528frame/60.507秒、p50=33.959ms/p95=65.392ms。全フレーム前面、通常Cinemachine、歩行274.999m、本番UI木のチェスト設置完了。最低20fps達成、目標30fps未達。13852ブロック/10215歯車、1280x720、vSync1。全60秒を密集帯へ固定した計測ではない。
- 実UI低→高→影OFF→閉じる後も同一パイプを維持し、水位0.200→0.500→0.900を観測。
- 既存/新設/新規列車VFX停止と復帰。D2は合成初期非active子を低設置完了で停止、高で5粒再生して目視。実Prefabのparticle/lightは0件、実生成/設置完了通知は入口NUnitで検証。録画はSwitch入口なので区別する。
- D3は起動前破損値から実ホストtopicの復旧通知表示、同値の影ON選択でbool/通知解除を録画。Playwrightは2解像度画像を保存。
- SSAO差はon/on=0、on/off=0.333562。実Animatorの距離停止/復帰・保持解除13865→13864も成功。最終実受信RPM0であり、非ゼロの運動比較は未確認。

機械比較は免責なし。bug-pass前baselineはEditor不在で未取得のため、bug-pass修正の新旧比較は未確認。録画fixture変更の比較は90シナリオ/103診断、新規5・照合済み75・照合不能23。新規5はD3シナリオを検査側の非async関数で包むCS4032。関数宣言だけasync化した非実行の補助compile6/6と実録画は成功したが、機械結果と旧23未確認は残す。検出器は変更していない。

起動State is null9件と一部停止時の追加3〜5件（破棄済みServerStarter・socket等）は新旧/因果未確定。最終compile後console Error0/Warning1（Compatibility Mode）を過去ログの消滅とは扱わない。無関係な製品修正はしていない。

最初のD3はスクロール不足、最初の寿命録画は未受信状態の診断参照で失敗。fixtureを修正して撮り直し、不採用動画も保全。未受信RPMを既定値で補完していない。

Prefsキー有無/生値・復旧staticをfinallyで復元。最終Recovered=false/CacheOverride=null/Playing=false/元URP/LOD1、原本world/map/saveのSHA256一致、通常saves不使用。所有Editor PID4022のみ終了しliveEditors0。自動pin/_CompileRequesterはpatch退避後HEAD restore、差分0byte。保存形式不変のため既済260件は再実行しなかった。

全報告・絶対result/video/PNG一覧・XML・診断・復元ログ・全9回の媒体:
https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-21-final-runtime-bundle/

## 最新master取込後の最終録画

録画検証HEAD `f1cb1b9a99`（origin/master 6e5e9302a0取込済み）。製品コード・シナリオは変更せず、既存final-runtime/run.py＋wrapperでFPS/UIの2本のみRecord=true再録し、54/54（25＋29）成功、各ErrorLogs=[]。今回の報告commitは証拠文書のみである。

- 最終FPSは平均25.126fps（最低平均20達成、目標30未達）。1523frame/dt60.614秒、実時間60.624秒・照合25.122fps、p50=33.648ms/p95=66.177ms。中・録画あり・1280x720・通常Cinemachine・全1523フレーム前面、歩行265.995mと本番UI木のチェスト設置アニメ完了。13852ブロック/10215歯車/Terrain1、target60/vSync1。動画尺70.800秒はFPS分母にしない。全60秒を密集帯に固定した測定ではなく、20fpsは平均基準である。
- UIは低→高→影OFFカスタム→閉じる、同一パイプの水位0.200→0.500→0.900と可視を確認。動画30.533333秒。新2本はffprobe/全体decode成功、PNGとFPS動画抽出でアバター・床・HUD・本番UI・チェストを目視。
- D2/D3/SSAO/寿命の既存4成功動画31/31を維持し、提出6本は85/85。4本はmerge後未再録。7364800ce7..f1cb1b9a99の画質実装・camera・歯車・パイプ・録画fixture差分0、各旧動画SHA256一致。合成D2・寿命RPM0・機械診断未確認等の従来の制限を引継ぐ。
- merge後の別担当検証（235＋7 Unity、compile0/0、Vitest1136、Playwright8/8、Web build/lint成功）を引継ぎ、全テストは重複実行していない。2回のrunner preflightはcompile clean/master load成功。
- Prefsのキー有無/生値・復旧static・環境・override・URP/LODはfinally復元し開始/終了一致。元world/map/saveのSHA256一致。通常saves63ファイルのサイズ/mtime_nsはboot ready前採取から終了まで不変（内容hash未比較）、固定world以外は起動先にしない。所有PID77194だけstart token/projectPath照合後に終了。自動pin/_CompileRequesterは退避後HEAD restore、両patch0byte。
- 起動時State is null9件/bootを継続観測。FPS停止を含む採取14件（終了書き出し・socket等）、UI9件をstack保全。録画本体のErrorLogs0と区別し、新旧/因果は未確定のまま範囲外修正なし。

PR用manifestは6動画＋26PNG（実ホスト復旧・Playwright復旧2解像度を含む）。共有先絶対パスと元パスの対応・SHA256、今回の生ログ・復元記録・報告を保全:
https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-21-merge-final-runtime-bundle/
