# 配布ビルドは Windows で焼けるものは Windows で焼き、Mac でしか焼けないものだけ Mac で焼く

- 決定: プレイテストの配布ビルドは、Windows 版を Windows 機（RTX2060・winbuild）で焼き、Mac 版だけを Mac mini で焼く。今後の運用も「基本 Windows でビルドできるものは Windows で行い、できないものだけ Mac で行う」
- 棄却案: `release-playtest.sh` の既定どおり Mac mini で Windows→Mac の順に両方を焼く（Mac mini の Editor 1 本の制約と直列で、待ちが長い）
- Mac 版を Windows で焼けるかの調べ（2026-11-17）: 今は焼けない。(1) Windows の Unity 6000.3.8f1 には Mac Build Support が入っていない（PlaybackEngines は windowsstandalonesupport だけ）(2) 配布用の Mac は arm64 へ固定する `MacPlayerArchitecture.PinAppleSilicon` が `UNITY_EDITOR_OSX` の外では UnsupportedHost を返し、ビルドが MacArchitecturePinFailed で止まる (3) 同梱の後の ad-hoc 再署名 `MacAppAdHocSigner`（/usr/bin/codesign）と配布前の検査 `release_require_mac_artifact`（codesign --verify・lipo）が Mac の道具に頼る。Mac 版を Windows へ移すには、Mac Build Support の導入・ホストの縛りの外し・署名と検査を Mac（か rcodesign）へ分けることが要る
- 理由・出所: ユーザー「windows版はwindowsでビルドして。macだけmacでビルドして」「今後の運用して、基本windowsでビルドできるものはwindowsで行い、できないものだけmacで行う.macビルドってwindowsdでできたかしらべてほしい」（2026-11-17、UITK X-switch-pr の配布ビルド中）
