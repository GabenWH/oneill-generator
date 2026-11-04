"""Compile using this project's Unity-generated references, without changing Library.

This is a C# compile check, not a Unity asset import or player build.
Run once Unity has generated Library/Bee/artifacts/*/Assembly-CSharp.rsp.
UNITY_REFERENCE_PROJECT can reuse another matching Unity project's references.
"""
from pathlib import Path
import os
import re
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
os.chdir(root)
editor = Path(os.environ.get('UNITY_EDITOR_DIR', '/home/Grace/Unity/Hub/Editor/2022.3.62f3/Editor'))
reference_root = Path(os.environ.get('UNITY_REFERENCE_PROJECT', str(root))).resolve()
responses = list((reference_root / 'Library/Bee/artifacts').glob('*/Assembly-CSharp.rsp'))
if not responses:
    raise SystemExit('Open this project in Unity once to generate its compiler response files.')
response = max(responses, key=lambda p: p.stat().st_mtime)
with tempfile.TemporaryDirectory(prefix='vwater-compile-') as tmp:
    for name in ['Assembly-CSharp', 'Assembly-CSharp-Editor']:
        original = response.with_name(name + '.rsp')
        text = original.read_text()
        # Keep Unity/package references and defines; enumerate the current Assets
        # sources so newly created files cannot silently miss this check.
        text = '\n'.join(line for line in text.splitlines()
                         if not re.fullmatch(r'".*\.cs"', line.strip()))
        text = text.replace('"Library/', '"' + str(reference_root) + '/Library/')
        text = re.sub(r'^-out:.*$', f'-out:"{tmp}/{name}.dll"', text, flags=re.M)
        text = re.sub(r'^-refout:.*$', f'-refout:"{tmp}/{name}.ref.dll"', text, flags=re.M)
        if name.endswith('Editor'):
            runtime_ref = str(response.parent / 'Assembly-CSharp.ref.dll')
            text = text.replace(runtime_ref, tmp + '/Assembly-CSharp.ref.dll')
        sources = sorted(p.relative_to(root) for p in (root / 'Assets').rglob('*.cs')
                         if ('Editor' in p.relative_to(root).parts) == name.endswith('Editor'))
        for source in sources:
            text += '\n"' + str(source) + '"\n'
        path = Path(tmp) / (name + '.rsp')
        path.write_text(text)
        subprocess.run(['dotnet', str(editor / 'Data/DotNetSdkRoslyn/csc.dll'), '@' + str(path)], check=True)
        print('PASS:', name, 'compiled', len(sources), 'current Assets sources with project Unity references')
