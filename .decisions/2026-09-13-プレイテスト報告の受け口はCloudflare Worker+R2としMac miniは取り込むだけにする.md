# プレイテスト報告の受け口はCloudflare Worker+R2としMac miniは取り込むだけにする

日付: 2026-09-13
出所: ユーザー裁定 原文「http受付口を作るとして、もっと安定した口はない？CFとか」→ 質問「報告バンドルの受け口をどこに置きますか？」→ 選択「Cloudflare Worker + R2」

## 決定
- 配布版（Steamテスター）からの報告はゲーム本体が Cloudflare Worker で認証を受け、R2 へ直接アップロードする
- Mac mini の inbox 監視は R2 の定期ポーリング取り込みに変わる。inbox 以降（自動修正ラン）は ADR 0057 のまま
- ADR 0057「送り手は開発者本人のみ／HTTP受け口は作らない」は本裁定で改訂対象（配布版が送り手に加わる）。開発者本人の Tailscale rsync 経路は残す

## 棄却案
- Cloudflare Worker + R2 に加え Mac mini へ Webhook で即時通知: 経路が2本になり Mac mini 側に受け口が残る
- Mac mini 直受け（cloudflared 経由）: Mac mini・自宅回線・cloudflared のどれかが落ちている間の報告が失われる

## 理由
受け口の可用性を自宅サーバーから切り離す。Mac mini が落ちていても報告は受かる。
