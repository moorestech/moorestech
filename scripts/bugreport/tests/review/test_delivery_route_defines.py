import os
from pathlib import Path
import subprocess

import pytest

REPO = Path(__file__).resolve().parents[4]


@pytest.mark.parametrize('define', ['UNITY_EDITOR', 'DEVELOPMENT_BUILD', ''])
def test_actual_resolver_compiles_and_runs_for_each_build(tmp_path, define):
    # Unity同梱のコンパイラを使用し、未導入環境では未確認を明示する
    # Use Unity's compiler and explicitly report unverified environments without it
    configured = os.environ.get('UNITY_SCRIPTING_ROOT')
    roots = [Path(configured)] if configured else list(Path('/Applications/Unity/Hub/Editor').glob('*/Unity.app/Contents/Resources/Scripting'))
    roots = [p for p in roots if (p / 'MonoBleedingEdge/bin/mono').is_file()]
    if not roots:
        pytest.skip('Unityのmono/cscが無いため三形態コンパイル・実行は未確認')
    scripting = sorted(roots)[-1]
    mono = scripting / 'MonoBleedingEdge/bin/mono'
    csc = scripting / 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
    source = REPO / 'moorestech_client/Assets/Scripts/Client.PlaytestReceiver'
    output = tmp_path / 'probe.exe'
    sources = [source / 'Delivery/PlaytestDeliveryRouteResolver.cs', source / 'Delivery/PlaytestDeliveryRoute.cs',
               source / 'Credentials/PlaytestDeveloperTokenRead.cs', Path(__file__).with_name('delivery_route_probe.cs')]
    result = subprocess.run([str(mono), str(csc), '-nologo', '-langversion:9', '-out:' + str(output),
                             *(['-define:' + define] if define else []), *(str(p) for p in sources)],
                            capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    executed = subprocess.run([str(mono), str(output)], capture_output=True, text=True)
    assert executed.returncode == 0, executed.stdout + executed.stderr
