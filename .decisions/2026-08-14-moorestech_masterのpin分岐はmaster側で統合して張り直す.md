決定: .moorestech-external-revisions.json の moorestech_master pin が PR側 094d242 と client-master側 c610e13 で分岐し互いを含まない問題は、moorestech_master リポジトリ側で統合コミットを作り、その結果を client の pin に据える
棄却案: ①c610e13 を採り「巨大HP鉱脈mapObject4種を削除しvein手掘りへ一本化」を作り直す ②pin を保留して実装を先に進める
理由: 094d242 だけが巨大HP鉱脈mapObject4種の削除を持ち、これが無いと旧mapObjectが残って手掘り露頭と二重になる。一方 c610e13 は map 再生成と原油鉱脈追加を持つ。両方の内容が要るので①は既存コミットを捨てる無駄になり、②は検証段階のテストプレイが正しいマスタを引けない
リンク: moorestech_client_private の pin は両者とも 40cdf1ad で既に一致しており B7 の「private revision バンプをPR本文へ明記」は取り込み後に差分として消える / [[2026-08-14-PR1127はB7先行でmasterをmerge取り込みする]] / [[2026-08-04-露頭mapObject化を棄却しvein自体を採掘対象にする]]
