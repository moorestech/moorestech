#!/usr/bin/env bash
L=/Users/juha/Documents/GitHub/moorestech/moorestech_client/PlaytestResults/scratch-local
P='G:\GitHub\moorestech\moorestech_client'
for i in $(seq 1 60); do r=$(ssh windows-desktop "uloop compile --project-path $P" 2>&1); echo "$r" | grep -q '"Success": true' && break; sleep 10; done
echo "$(date +%H:%M:%S) unity ready"
# session-bisect の前半（読み込み〜重い場所へ移す）だけを使い、ブロックは切らずに移す
sed -e 's#^echo "\$(ts) exp: \$(run exp-noblocks.cs)"#echo "$(ts) exp: none"#' -e '/^echo "\$(ts) order:/,$d' $L/session-bisect.sh > $L/debug-session.sh
echo 'sleep 40; echo "$(ts) after wait"' >> $L/debug-session.sh
bash $L/debug-session.sh
ssh windows-desktop '$f="$env:LOCALAPPDATA\Unity\Editor\Editor.log"; Select-String -Path $f -Pattern "D3D12 ERROR|D3D12 CORRUPTION|EXECUTION ERROR|DEVICE_REMOVAL|DRED|Device failed|PageFault|page fault|Breadcrumb|D3D12 WARNING" | Select-Object -First 25 | ForEach-Object { "$($_.LineNumber): " + $_.Line.Substring(0,[Math]::Min(300,$_.Line.Length)) }; Select-String -Path $f -Pattern "force-d3d12-debug|Debug layer|debug layer" | Select-Object -First 3 | ForEach-Object { "$($_.LineNumber): " + $_.Line.Substring(0,[Math]::Min(200,$_.Line.Length)) }'
