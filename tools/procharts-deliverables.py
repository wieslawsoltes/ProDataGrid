#!/usr/bin/env python3
"""Create chart-only source and audited deliverables from successful same-commit build artifacts."""
from __future__ import annotations
import argparse, hashlib, json, os, shutil, subprocess, tempfile, zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FONTS = {'.ttf','.otf','.woff','.woff2','.ttc'}
PREFIXES = ('src/ProCharts/', 'src/ProCharts.Skia/', 'src/ProCharts.Avalonia/', 'src/ProCharts.Controls/',
    'samples/ProCharts.Sample/', 'samples/ProCharts.Sample.Desktop/', 'samples/ProCharts.Sample.Browser/',
    'tests/ProCharts.Controls.Tests/', 'tests/ProCharts.Browser/')
FILES = {'global.json','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','NuGet.config',
    'ProCharts.Controls.slnx','README.ProCharts.md','readme.md','licence.md','licence-avalonia.md','AGENTS.md','.editorconfig','.gitignore',
    'build/SharedVersion.props','build/avalonia.snk','build/Assets/Icon.png','docs/procharts-controls.md',
    'tools/procharts-package-consumer.py','tools/procharts-deliverables.py','.github/workflows/procharts-controls.yml',
    'docs/procharts-release-notes.md'}

def git(*args: str) -> str:
    return subprocess.check_output(['git',*args],cwd=ROOT,text=True).strip()

def identity() -> dict:
    return {'repository':'wieslawsoltes/ProDataGrid','commit':git('rev-parse','HEAD'),
            'tree':git('rev-parse','HEAD^{tree}'),'run':os.getenv('GITHUB_RUN_ID')}

def digest(path: Path) -> str: return hashlib.sha256(path.read_bytes()).hexdigest()

def create_source(output: Path) -> None:
    output.mkdir(parents=True,exist_ok=True)
    files = subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0')
    selected = sorted(p for p in files if p and (p in FILES or p.startswith(PREFIXES)))
    records = {}
    with zipfile.ZipFile(output/'source.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for name in selected:
            path = ROOT/name
            assert path.is_file() and path.suffix.lower() not in FONTS, name
            data = path.read_bytes()
            blob = hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
            assert blob == git('rev-parse','HEAD:'+name), 'Uncommitted source: '+name
            records[name] = {'sha256':hashlib.sha256(data).hexdigest(),'gitBlob':blob,'bytes':len(data)}
            archive.writestr(name,data)
        archive.writestr('BUILD-IDENTITY.json',json.dumps(identity(),indent=2)+'\n')
        archive.writestr('source-manifest.json',json.dumps(records,indent=2)+'\n')
        archive.writestr('START-HERE.md',(ROOT/'README.ProCharts.md').read_text())
    with zipfile.ZipFile(output/'source.zip') as archive:
        assert archive.testzip() is None
        for name, record in records.items(): assert hashlib.sha256(archive.read(name)).hexdigest()==record['sha256']
    (output/'commit.txt').write_text(identity()['commit']+'\n')
    (output/'tree.txt').write_text(identity()['tree']+'\n')
    (output/'SHA256SUMS.txt').write_text(digest(output/'source.zip')+'  source.zip\n')
    print(json.dumps({'sourceFiles':len(selected),'sourceZipSha256':digest(output/'source.zip'),'identity':identity()}),flush=True)

def verify_source(source: Path, output: Path) -> None:
    output.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='procharts-source-roundtrip-') as directory:
        with zipfile.ZipFile(source) as archive: archive.extractall(directory)
        result = subprocess.run(['dotnet','test','tests/ProCharts.Controls.Tests/ProCharts.Controls.Tests.csproj','-c','Release',
            '--logger','trx','--results-directory',str(output/'roundtrip-tests')],cwd=directory,
            stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=300)
        (output/'source-roundtrip.log').write_text(result.stdout)
        if result.returncode: print(result.stdout[-16000:],flush=True)
        assert result.returncode==0,'Freshly extracted source failed its managed tests'
    print('PASS fresh chart-only source ZIP compiled and ran all control/data tests',flush=True)

def counters(folder: Path) -> list[dict]:
    results=[]
    for path in sorted(folder.rglob('*.trx')):
        counter=ET.parse(path).find('.//{*}Counters')
        assert counter is not None, path
        values={k:int(v) for k,v in counter.attrib.items()}
        assert values.get('total',0)>0 and values.get('failed',0)==0 and values.get('error',0)==0,values
        results.append({'file':path.name,**values})
    assert results,'No executed test results: '+str(folder)
    return results

def bundle(incoming: Path, output: Path) -> None:
    managed=incoming/'procharts-managed'; browser=incoming/'procharts-browser'
    ident=identity()
    for folder in (managed/'identity',browser/'source'):
        assert (folder/'commit.txt').read_text().strip()==ident['commit'],folder
        assert (folder/'tree.txt').read_text().strip()==ident['tree'],folder
    report=json.loads((browser/'browser-tests/report.json').read_text())
    devices=json.loads((browser/'device-tests/report.json').read_text())
    consumer=json.loads((managed/'consumer/consumer.json').read_text())
    assert report['passed'] and devices['passed'] and consumer['passed']
    assert not report['pageErrors'] and not report['httpErrors'] and not report['consoleErrors']
    summary={'identity':ident,'controls':counters(managed/'tests'),'regressions':counters(managed/'regressions'),
        'sourceRoundtrip':counters(managed/'source-check/roundtrip-tests'),
        'browser':{'checks':len(report['checks']),'renderers':len(report['renderers']),'version':report['browserVersion'],
            'errors':len(report['pageErrors'])+len(report['httpErrors'])+len(report['consoleErrors']),
            'streaming':[c for c in report['checks'] if '100,000' in c['name']]},
        'devices':devices,'consumer':consumer,'passed':True}
    output.mkdir(parents=True,exist_ok=True)
    for name in ('source','browser','desktop','packages','validation'): (output/name).mkdir(exist_ok=True)
    with zipfile.ZipFile(browser/'source/source.zip') as archive:
        assert archive.testzip() is None
        archive.extractall(output/'source')
    manifest=json.loads((output/'source/source-manifest.json').read_text())
    for name, item in manifest.items(): assert digest(output/'source'/name)==item['sha256'],name
    shutil.copytree(browser/'browser/wwwroot',output/'browser',dirs_exist_ok=True)
    shutil.copytree(managed/'desktop',output/'desktop',dirs_exist_ok=True)
    shutil.copytree(managed/'packages',output/'packages',dirs_exist_ok=True)
    evidence=output/'validation'
    for name in ('browser-tests','device-tests'):
        shutil.copytree(browser/name,evidence/name,dirs_exist_ok=True)
    for name in ('tests','regressions','consumer','source-check'):
        shutil.copytree(managed/name,evidence/name,dirs_exist_ok=True)
    for origin in (managed,browser):
        for path in origin.glob('*.log'): shutil.copy2(path,evidence/path.name)
    (output/'browser/README.txt').write_text('Actual Avalonia WebAssembly build. Serve this folder over HTTP:\npython -m http.server 8000\nOpen http://localhost:8000/\nDo not open index.html using file://.\n')
    (output/'desktop/README.txt').write_text('Framework-dependent Avalonia desktop sample. Requires .NET 10 runtime and native platform prerequisites.\nRun: dotnet ProCharts.Sample.Desktop.dll\nBuilt on Linux; this bundle is not an OS installer.\n')
    shutil.copy2(ROOT/'docs/procharts-controls.md',evidence/'integration-guide.md')
    shutil.copy2(ROOT/'docs/procharts-release-notes.md',evidence/'release-notes.md')
    for name in ('source','browser','desktop','packages','validation'):
        folder=output/name
        (folder/'BUILD-IDENTITY.json').write_text(json.dumps(ident,indent=2)+'\n')
        for path in folder.rglob('*'):
            if path.is_file(): assert path.suffix.lower() not in FONTS,path
        hashes=[digest(p)+'  '+p.relative_to(folder).as_posix() for p in sorted(folder.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.txt']
        (folder/'SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n')
    (evidence/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    # Include summary in the evidence manifest after writing it.
    hashes=[digest(p)+'  '+p.relative_to(evidence).as_posix() for p in sorted(evidence.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.txt']
    (evidence/'SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n')
    print('VERIFIED_DELIVERY_SUMMARY='+json.dumps(summary,separators=(',',':')),flush=True)

def main() -> None:
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('mode',choices=['source','verify-source','bundle'])
    parser.add_argument('--output',type=Path,required=True); parser.add_argument('--input',type=Path)
    args=parser.parse_args(); output=args.output.resolve()
    if args.mode=='source': create_source(output)
    elif args.mode=='verify-source': verify_source(args.input.resolve(),output)
    else: bundle(args.input.resolve(),output)

if __name__=='__main__': main()
