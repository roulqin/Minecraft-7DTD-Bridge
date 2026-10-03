"""Phase 1.5 real-process test launcher; no game patches or authentication tokens."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'docs' / 'runtime-evidence'
EVIDENCE.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser()
parser.add_argument('component', choices=['bridge', 'minecraft', '7dtd'])
args = parser.parse_args()
env = os.environ.copy()
env['MC7DTD_ROOT'] = str(ROOT)
env['TEMP'] = env['TMP'] = str(ROOT / 'work')
cwd = ROOT

if args.component == 'bridge':
    command = ['dotnet', str(ROOT / 'bridge-server/bin/Release/net10.0/BridgeServer.dll'), str(ROOT / 'config/network.json')]
elif args.component == 'minecraft':
    source = Path(r'D:\wenjian\minecraft\.minecraft')
    version = source / 'versions/1.21.11-Voxy'
    metadata = json.loads((version / '1.21.11-Voxy.json').read_text(encoding='utf-8-sig'))
    cwd = ROOT / 'runtime/minecraft'
    natives = cwd / 'natives'
    natives.mkdir(parents=True, exist_ok=True)
    classpath = []
    for library in metadata['libraries']:
        allowed = 'rules' not in library
        for rule in library.get('rules', []):
            os_rule = rule.get('os', {})
            matches = os_rule.get('name', 'windows') == 'windows' and os_rule.get('arch', 'x86_64') in ('x86_64', 'amd64')
            if 'version' in os_rule:
                matches = matches and bool(re.search(os_rule['version'], '10.0.26200'))
            if matches: allowed = rule['action'] == 'allow'
        if not allowed: continue
        artifact = library.get('downloads', {}).get('artifact', {})
        relative = artifact.get('path')
        if not relative:
            group, name, version_number = library['name'].split(':')[:3]
            relative = f'{group.replace(".", "/")}/{name}/{version_number}/{name}-{version_number}.jar'
        path = source / 'libraries' / relative
        if not path.is_file(): raise FileNotFoundError(path)
        classpath.append(str(path))
    # Read the existing client and dependencies without modifying that instance.
    classpath.append(str(version / '1.21.11-Voxy.jar'))
    command = [r'C:\Program Files\Java\jdk-21.0.12\bin\java.exe', '-Xms256m', '-Xmx2G',
               '-Dfile.encoding=UTF-8', f'-Djava.io.tmpdir={ROOT / "work"}',
               f'-Djava.library.path={natives}', f'-Djna.tmpdir={natives}',
               f'-Dorg.lwjgl.system.SharedLibraryExtractPath={natives}', f'-Dio.netty.native.workdir={natives}',
               '-Dminecraft.launcher.brand=MC7DTD-runtime-test', '-Dminecraft.launcher.version=1.5',
               '-cp', os.pathsep.join(classpath), metadata['mainClass'],
               '--username', 'MC7DTD-Test', '--version', '1.21.11', '--gameDir', str(cwd),
               '--assetsDir', str(source / 'assets'), '--assetIndex', metadata['assetIndex']['id'],
               '--uuid', '00000000000000000000000000000001', '--accessToken', '0',
               '--versionType', 'release', '--demo', '--width', '960', '--height', '600']
    # Java argfile avoids the Windows CreateProcess command-line length limit.
    argfile = cwd / 'runtime-arguments.txt'
    argfile.write_text('\n'.join('"' + x.replace('\\', '\\\\').replace('"', '\\"') + '"' for x in command[1:]), encoding='utf-8')
    command = [command[0], '@' + str(argfile)]
else:
    cwd = ROOT / 'runtime/7dtd'
    cwd.mkdir(parents=True, exist_ok=True)
    command = [r'D:\Steam\steamapps\common\7 Days To Die\7DaysToDie.exe',
               '-UserDataFolder=' + str(cwd), '-logfile', str(EVIDENCE / '7dtd-game.log'),
               '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '600']

with (EVIDENCE / (args.component + '-stdout.log')).open('wb') as stdout, (EVIDENCE / (args.component + '-stderr.log')).open('wb') as stderr:
    process = subprocess.Popen(command, cwd=cwd, env=env, stdout=stdout, stderr=stderr,
                               creationflags=subprocess.CREATE_NO_WINDOW if args.component in ('bridge', 'minecraft') else 0)
record = {'component': args.component, 'pid': process.pid, 'startedUtc': datetime.now(timezone.utc).isoformat(), 'cwd': str(cwd)}
(EVIDENCE / (args.component + '-process.json')).write_text(json.dumps(record, indent=2), encoding='utf-8')
print(json.dumps(record))
