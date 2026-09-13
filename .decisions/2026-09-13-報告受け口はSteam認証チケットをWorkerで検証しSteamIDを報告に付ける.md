# 報告受け口はSteam認証チケットをWorkerで検証しSteamIDを報告に付ける

日付: 2026-09-13
出所: ユーザー裁定 質問「R2受け口の認証と、報告へのテスター識別はどうしますか？」→ 選択「Steam認証チケットをWorkerがSteam Web APIで検証し、SteamIDを報告に付ける」

## 決定
- ゲームは Steam の Web API 用認証チケットを取り、Worker が Steam Web API（publisher key は Worker secret）で検証する。app所有アカウントだけが送れる
- 報告・テレメトリに SteamID を付け、同一人物の推移を追えるようにする。テスターへは「Steamアカウントと紐づいて送られる」と明示
- Steam未起動の自作ビルドからは送れないので、開発者は従来のTailscale rsync経路を使う

## 棄却案
- ビルド焼き込みの共有トークン＋インストールごとのランダムID: 抜かれると誰でも送れる、PCを変えると別人になる
- Steam検証はするが報告にはSteamIDのハッシュだけ: 誰かを知って連絡できない
