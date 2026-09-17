# プレイテスト運用スクリプト（Mac mini）

受け口 Worker は `tools/playtest-receiver/`。ここには Mac mini 側から叩く運用スクリプトを置く。

## 設定

`/Users/sakastudio/hermes-agent/data/services/playtest/env.sh`（git 管理外・実シークレット。既定は本体 clone の位置から `<repo>/../../services/playtest/env.sh` と導出する）:
```
export PLAYTEST_RECEIVER_BASE=https://playtest.tar-atari.com
export PLAYTEST_ADMIN_KEY=<wrangler secret put ADMIN_KEY で入れたのと同じ値>
```
別の場所に置く場合は `PLAYTEST_ENV_FILE` で指す。worktree から叩くと兄弟パスがずれるので本体 clone のスクリプトを使う。受け口 admin API の呼び出しは `lib/receiver-api.sh` に一本化している。

## 許可リスト

```bash
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/allowlist.sh list
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/allowlist.sh add 76561198000000001
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/allowlist.sh remove 76561198000000001
```

許可リストは全置換 PUT で更新する。`add`/`remove` は内部で GET → 編集 → PUT を行うため、
2人が同時に実行すると後勝ちで片方の変更が消える。人手運用なので排他は設けていない。

不許可にした瞬間から新しいセッショントークンは出なくなるが、発行済みトークンは最大1時間有効で、
その間はアップロードだけ通る。起動時照合（クライアント）は次回起動から効く。

## 配布工程

配布ビルドを焼き、Steam の `playtest` ブランチへ上げ、検証機で通し検証するまでの運用。
用語は CONTEXT.md「プレイテスト」節、裁定は docs/adr/0061 を正とする。

### 1回だけ行う準備

#### Steamworks 側（Web の手動作業。自動化しない）

1. アプリ 1958160 の Steamworks 管理画面 → SteamPipe → Builds でベータブランチ `playtest` を作成する。
2. `playtest` ブランチにパスワードを設定する（テスターへキーと一緒に配る）。
3. Depot のIDを控える（Steamworks → SteamPipe → Depots）。`MOORESTECH_STEAM_DEPOT_ID` に設定する。
4. Steam Web API の publisher key を発行する（受け口 plan D の `STEAM_WEB_API_KEY` に使う）。
5. テスター配布用のキーを発行する（Steamworks → Packages → キー生成）。

#### Mac mini 側

1. steamcmd を入れる: `brew install --cask steamcmd`（`steamcmd` が PATH に載る）。
2. 初回だけ対話で Steam Guard を通す: `steamcmd +login <user> +quit`（以降は保存された資格で無人ログインできる）。
3. `~/hermes-agent/data/services/playtest/env.sh` に次を追記して export する（このファイルは封じ込め env の外に置かず、値をログへ出さない）:
   - `MOORESTECH_STEAM_USER`
   - `MOORESTECH_STEAM_DEPOT_ID`
   - `MOORESTECH_BUILD_BRANCH`（任意。build-info.json の `branch` に焼く配布元 ref。既定 `master`）
   - 検証機向けの変数（「検証機」節の「Mac mini 側の env」を参照）

### 使い方

```bash
. ~/hermes-agent/data/services/playtest/env.sh
cd ~/hermes-agent/data/repos/moorestech   # メインクローンから実行する
scripts/playtest/release-playtest.sh <SHA または origin/master>
```

- **メインクローン（`moores-wt` が worktree を作る clone）から実行する。** fetch とコミット解決はスクリプトを置いた clone で行い、
  worktree はメインクローンで作るため、別 clone（pr-review の baseline clone 等）から実行すると解決した object が無いことがある。
- `<commit>` は SHA か `origin/<branch>` を渡す。ローカルブランチ名（`master` 等）は fetch で進まないので古いコミットを焼く。
- 要求コミットが `origin/$MOORESTECH_BUILD_BRANCH`（既定 `origin/master`）に含まれなければビルド前に止まる（焼く `branch` を嘘にしない）。
- ビルド前に、ビルドが同梱する master data（worktree + ピンの `relativePath`。`GameDataBundler.MasterDataRepositoryRoot` と同じ解決）の
  HEAD をコミット済みのピンと突き合わせる。ずれていれば「どこをどのコミットへ合わせるか」を出して止まるので、そのディレクトリを
  ピンのコミットへ合わせてから再実行する（自動では動かさない。並行 worktree のマスタを動かさないため）。

成果物・ログ・告知テキストは `~/hermes-agent/data/services/playtest/runs/<label>/` に残る。告知の「コミット」は解決後の40桁 SHA。

## 検証機（自宅 Windows PC）

配布ビルドの通し検証を回す1台。常時起動ではないので Wake-on-LAN で起こす。

### 初回セットアップ（手作業）

1. Tailscale に参加させ、Mac mini から名前で引けることを確認する（`tailscale status`）。
2. OpenSSH Server を有効にする: 設定 → アプリ → オプション機能 → 「OpenSSH サーバー」を追加し、
   `Set-Service -Name sshd -StartupType Automatic; Start-Service sshd`。
3. Mac mini の公開鍵を `C:\Users\<user>\.ssh\authorized_keys` へ置く（管理者ユーザーの場合は
   `C:\ProgramData\ssh\administrators_authorized_keys` が正しい置き場）。`ssh <user>@<host> echo ok` が
   パスワード無しで通ることを確認する。
4. 既定シェルを PowerShell にしておくと `verify-on-windows.sh` の引用が素直になる:
   `New-ItemProperty -Path "HKLM:\SOFTWARE\OpenSSH" -Name DefaultShell -Value "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -PropertyType String -Force`
5. Wake-on-LAN を有効にする: BIOS/UEFI の Wake on LAN を ON、Windows のデバイスマネージャー →
   ネットワークアダプター → 詳細設定で「ウェイク・オン・マジックパケット」を有効、電源管理タブで
   「このデバイスで、コンピューターのスタンバイ状態を解除できるようにする」を ON。高速スタートアップは OFF にする。
   NIC の MAC アドレスを控え `MOORESTECH_VERIFY_MAC` に設定する。
6. Steam クライアントを入れてテスター用アカウントでログインし、moorestech（app 1958160）をライブラリへ追加。
   プロパティ → ベータ で `playtest` ブランチのパスワードを1度入力して選択しておく。プロパティ → 更新 は「常にこのゲームを最新の状態に保つ」にする
   （`run-smoke.ps1` はゲームを起動せず Steam の自動更新でラベルが切り替わるのを待つ）。Steam を既定以外の場所に入れた場合は
   Windows のシステム環境変数 `MOORESTECH_STEAM_EXE` に steam.exe のフルパスを設定する（未設定ならレジストリ→`C:\Program Files (x86)\Steam` の順に探す）。
   ゲームは Steam ルート直下の `steamapps\common\moorestech` にある前提（別ライブラリフォルダは未対応）。
7. 初回だけ手でゲームを起動し、同意告知（consent notice）を承諾しておく（`PlaytestConsentFlag`。未承諾のまま
   自動運転すると起動前提の確認で理由付きに失敗する）。あわせて Steam のオーバーレイ初期化と受け口の起動時照合が
   通ることを確認する。
8. 検証機に **出展モード（EventMode）の起動引数を設定しないこと**を確認する（下記「出展モードとの併用禁止」参照）。

### 出展モード（EventMode）との併用禁止

出展モードと `--playtestSmoke` はどちらもメインメニューの `StartLocalGame` を起点に開始を試みる。
さらに出展モードは `EventModeStartGate.WaitForLanguageSelectionAsync` で言語選択の人手入力を
無期限に待つため、併用するとその場で無期限停止し `result.json` が出ないまま検証機を占有し続ける
（D-10）。検証機では出展モード関連の環境変数・起動引数を一切設定しないこと。

### Mac mini 側の env（`~/hermes-agent/data/services/playtest/env.sh`）

- `MOORESTECH_VERIFY_HOST` … Tailscale 上のホスト名
- `MOORESTECH_VERIFY_USER` … ssh ユーザー
- `MOORESTECH_VERIFY_MAC` … WoL 用 MAC アドレス
- 受け口の `PLAYTEST_RECEIVER_BASE`（既定値あり）・`PLAYTEST_ADMIN_KEY` は冒頭「設定」節のものをそのまま使う
- `wakeonlan` が要る: `brew install wakeonlan`

### 単体で回す

```bash
. ~/hermes-agent/data/services/playtest/env.sh
scripts/playtest/verify-on-windows.sh <steamBuildLabel>
```

回収した `result.json` と `inbox.json` は
`~/hermes-agent/data/services/playtest/runs/<label>/verify/` に残る。

### 起動経路と合否判定（Steam 経由・result.json）

`run-smoke.ps1` は各フェーズを `steam.exe -applaunch 1958160 --playtestSmoke --smokePhase <phase> --smokeResultDirectory <dir>` で
起動する（テスターと同じ Steam 経由の起動経路・DRM を通すため。exe の直接起動は `SteamAPI.Init` が失敗する）。Steam 経由では
ゲームのプロセスハンドルも終了コードも得られないので、各フェーズの合否は `result.json` の存在とトップレベルの `success` だけで判定する。

各フェーズは「`result.json` が出て、かつ `moorestech` プロセスが自分で終了する」までを期限（既定600秒）付きでポーリングする。
期限を超えたら（クラッシュ確認画面・起動引数の確認ダイアログ・Steam の起動拒否など）ゲームを強制終了し、理由付きで exit 6 にする。
起動前には残っている `moorestech` プロセスを畳む（前フェーズや手動起動が残ると Steam が起動を拒否・更新を保留するため）。

終了コード: 2=ラベル更新待ちの期限切れ（最後に読めたラベルと読み取り失敗の理由を出す）／3=ゲーム未インストール／
4=result.json 無し／5=検証失敗／6=フェーズ期限切れ／7=steam.exe が見つからない。`verify-on-windows.sh` はこの非0をそのまま返す。

`run-smoke.ps1` は UTF-8 BOM 付きで保存しておくこと（Windows PowerShell 5.1 は BOM 無し UTF-8 を ANSI として読み、日本語の文字列で構文が壊れる。テストで固定済み）。

### 注意: 取り込み（plan H）との競合

受け口の `GET /v1/inbox` は未ACKの新着だけを返す。plan H の取り込み（supervisor periodic 300s）が
smoke の報告を先にACKすると、届いているのに「届いていない」と判定される。検証を回す間は
`services.json` から `playtest-ingest` を外す（または取り込みを止める）こと。

### CEF raw input 確認（初回合格ビルドのみ・手動）

配布ビルドでは入力注入が使えないため、Windows の CEF raw input 奪取は人が確認する。
1. Remote Desktop（または実機）で検証機に入り、Steam から `playtest` ブランチのゲームを起動して新規ワールドに入る
2. 右ドラッグで視点を回しながら Tab でインベントリ（WebUI）を開閉し、開いた状態でホイールスクロールが効くか、閉じた状態で右ドラッグ視点回転が効くかを見る
3. 結果（両方効く／ホイール死亡／視点回転死亡）を `bd note <配布タスクid> "CEF raw input: ..."` に書く。奪取が再現したら既知バグ一覧へ載せ、根治は別タスク（[[.decisions/2026-09-13-CEF raw input応急処置は入れず検証機の通し検証で実害を確かめてから決める.md]]）

## テスト

```bash
bash scripts/playtest/tests/test-allowlist.sh          # OK と出れば合格
bash scripts/playtest/tests/test-release-playtest.sh    # PASS: release-playtest contract と出れば合格
bash scripts/playtest/tests/test-release-playtest-origin.sh  # PASS: release-playtest origin contract と出れば合格
bash scripts/playtest/tests/test-verify-on-windows.sh   # PASS: verify-on-windows contract と出れば合格
```
