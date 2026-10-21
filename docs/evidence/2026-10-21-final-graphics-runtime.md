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
