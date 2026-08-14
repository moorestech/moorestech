# 裁定サイトはCloudflare-Accessで保護する

決定: review.tar-atari.com（裁定サイト）は Cloudflare Access（メールOTP）で保護する。完了ボタンがPR更新の引き金になるため認証必須

棄却案: 秘密トークン付きURL／backend自前のパスワード認証

理由: tar-atari.com は既にCF管理下でポリシー追加だけで済み、backend側に認証コードが不要になる。秘密URLはDiscordへURLを流す運用と相性が悪い

リンク: [[2026-08-14-独立レビュー無人化はsupervisor素pollerを起点にする]]
