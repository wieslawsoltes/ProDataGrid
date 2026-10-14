#!/usr/bin/env python3
"""Exercise the published .NET/Avalonia app, not an HTML replacement.
Usage: python validate.py --root path/to/publish/wwwroot --output artifacts/browser-tests
Requires playwright and Pillow. CHROMIUM_PATH overrides the installed Playwright browser.
The JS diagnostics are read-only except for explicit gallery selection in the all-kinds smoke test.
"""
from __future__ import annotations
import argparse, csv, functools, io, json, math, os, threading, time, traceback
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Callable
from xml.etree import ElementTree
from PIL import Image
from playwright.sync_api import sync_playwright

class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_: Any) -> None: pass


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    if not (root / 'index.html').is_file():
        raise SystemExit(f'No published index.html in {root}')
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    (output / 'screenshots').mkdir(exist_ok=True)
    (output / 'downloads').mkdir(exist_ok=True)
    server = ThreadingHTTPServer(('127.0.0.1', 0), functools.partial(QuietHandler, directory=str(root)))
    thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
    report: dict[str, Any] = {'checks': [], 'consoleErrors': [], 'pageErrors': [], 'httpErrors': [], 'wasmRequests': [], 'renderers': []}
    start = time.monotonic()
    with sync_playwright() as playwright:
        executable = os.getenv('CHROMIUM_PATH')
        browser = playwright.chromium.launch(executable_path=executable, headless=True,
            args=['--no-sandbox', '--disable-dev-shm-usage', '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--enable-webgl'])
        report['browserVersion'] = browser.version
        context = browser.new_context(viewport={'width': 1440, 'height': 1000}, device_scale_factor=1, accept_downloads=True)
        page = context.new_page()
        page.on('pageerror', lambda e: report['pageErrors'].append(str(e)))
        page.on('console', lambda m: report['consoleErrors'].append(m.text) if m.type == 'error' else None)
        def response(r: Any) -> None:
            if r.status >= 400: report['httpErrors'].append({'status': r.status, 'url': r.url})
            if '.wasm' in r.url: report['wasmRequests'].append({'status': r.status, 'url': r.url})
        page.on('response', response)
        def state() -> dict[str, Any]:
            return page.evaluate('JSON.parse(globalThis.proCharts.GetState())')
        def wait(predicate: Callable[[dict[str, Any]], bool], message: str, timeout: float = 8) -> dict[str, Any]:
            limit = time.monotonic() + timeout
            last: dict[str, Any] = {}
            while time.monotonic() < limit:
                last = state()
                if predicate(last): return last
                page.wait_for_timeout(50)
            raise AssertionError(f'{message}; state={json.dumps(last)}')
        def element(id: str) -> dict[str, Any]:
            items = [e for e in state()['elements'] if e['id'] == id]
            if not items: raise AssertionError(f'Native Avalonia element missing: {id}')
            return items[0]
        def click(id: str, disabled: bool = False) -> None:
            e = element(id)
            if not disabled:
                wait(lambda s: any(x['id']==id and x['enabled'] for x in s['elements']), f'{id} remains disabled')
                e = element(id)
            assert 0 <= e['x'] < page.viewport_size['width'] and 0 <= e['y'] < page.viewport_size['height'], (id, e)
            page.mouse.click(e['x'] + e['width']/2, e['y'] + e['height']/2)
            page.wait_for_timeout(100)
        def check(name: str, condition: bool = True, **details: Any) -> None:
            assert condition, f'{name}: {details}'
            report['checks'].append({'name': name, 'passed': True, **details})
            print(f'PASS {name}', flush=True)
        def screenshot(name: str) -> None:
            page.mouse.move(1, 1); page.wait_for_timeout(100)
            page.screenshot(path=str(output / 'screenshots' / f'{name}.png'))
        def fit() -> None:
            click('chart-reset'); wait(lambda s: s['count'] == s['total'], 'fit all')
        def color_pixels(blob: bytes) -> int:
            image = Image.open(io.BytesIO(blob)).convert('RGB')
            return sum(1 for r,g,b in image.getdata() if max(r,g,b)-min(r,g,b)>45 and max(r,g,b)>100)
        try:
            page.goto(f'http://127.0.0.1:{server.server_port}/', wait_until='domcontentloaded')
            page.wait_for_function('globalThis.chartBootError || (globalThis.proCharts && JSON.parse(proCharts.GetState()).ready)', timeout=180000)
            error = page.evaluate('globalThis.chartBootError || null')
            assert error is None, error
            wait(lambda s: s['hasData'] and any(e['id']=='chart-plot' for e in s['elements']), 'initial chart')
            initial = state(); report['initialState'] = initial
            check('Actual WebAssembly application booted', bool(report['wasmRequests']) and page.locator('canvas').count()>0,
                  wasmModules=len(report['wasmRequests']))
            check('Overview contains three real series', initial['series']==3 and initial['total']==48)
            screenshot('overview-dark')
            click('chart-zoom-in'); zoomed = wait(lambda s: s['count']<48, 'toolbar zoom')
            check('Native toolbar zoom', count=zoomed['count'])
            click('chart-undo'); wait(lambda s: s['count']==48, 'undo')
            click('chart-redo'); wait(lambda s: s['count']==zoomed['count'], 'redo')
            check('Native viewport undo and redo')
            fit(); check('Fit resets viewport')
            plot = element('chart-plot')
            x,y = plot['x']+plot['width']*.5, plot['y']+plot['height']*.5
            page.mouse.move(x,y); page.mouse.wheel(0,-200)
            wait(lambda s: s['count']<48, 'wheel zoom')
            check('Mouse wheel zoom')
            before = state()['start']
            page.mouse.move(x,y); page.mouse.down(); page.mouse.move(x-plot['width']*.16,y,steps=8); page.mouse.up()
            wait(lambda s: s['start']!=before, 'pointer drag pan')
            check('Mouse drag pan')
            page.mouse.click(x,y); page.keyboard.press('Home')
            wait(lambda s: s['count']==48, 'keyboard Home')
            page.keyboard.press('+'); wait(lambda s: s['count']<48, 'keyboard plus')
            check('Keyboard Home and plus')
            fit()
            for i in range(3): click(f'legend-{i}')
            wait(lambda s: s['series']==0 and s['empty'], 'all series hidden')
            check('Native legend hides all while retaining entries', len(state()['legend'])==3)
            for i in range(3): click(f'legend-{i}')
            wait(lambda s: s['series']==3 and not s['empty'], 'legend restore')
            check('Native legend restores original series')
            plot = element('chart-plot'); hovered=False
            for fx in [.2,.35,.5,.65,.8]:
                for fy in [.2,.35,.5,.65,.8]:
                    page.mouse.move(plot['x']+plot['width']*fx,plot['y']+plot['height']*fy)
                    page.wait_for_timeout(40)
                    if state()['hover']:
                        hovered=True; break
                if hovered: break
            check('Pointer hover resolves typed tooltip', hovered, hover=state()['hover'])
            page.screenshot(path=str(output/'screenshots'/'tooltip.png'))
            page.mouse.move(1,1)
            for fmt in ['png','svg','csv']:
                with page.expect_download(timeout=15000) as pending: click(f'chart-{fmt}')
                download = pending.value
                path = output/'downloads'/f'chart.{fmt}'; download.save_as(path)
                assert download.failure() is None
                blob = path.read_bytes()
                assert not state()['exportError'], state()['exportError']
                if fmt=='png':
                    assert blob[:8]==b'\x89PNG\r\n\x1a\n'; assert color_pixels(blob)>200
                elif fmt=='svg':
                    tree = ElementTree.fromstring(blob); assert tree.tag.endswith('svg') and len(list(tree.iter()))>20
                else:
                    rows=list(csv.reader(io.StringIO(blob.decode()))); assert len(rows)==145 and len(rows[0])==9
                check(f'Actual browser {fmt.upper()} download', bytes=len(blob))
            click('sample-theme'); wait(lambda s:s['theme']=='Light','light theme'); screenshot('overview-light')
            click('sample-theme'); wait(lambda s:s['theme']=='Dark','dark theme')
            check('Live light/dark theme switching')
            click('chart-zoom-in'); saved=(state()['start'],state()['count'])
            click('sample-template'); wait(lambda s:s['compact'],'compact template')
            check('Replacement ControlTemplate preserves viewport', (state()['start'],state()['count'])==saved)
            screenshot('compact-template'); click('chart-zoom-out')
            click('sample-template'); wait(lambda s:not s['compact'],'restore template')
            check('Restored default ControlTemplate and native navigator', any(e['id']=='chart-navigator' for e in state()['elements']))
            fit()
            click('sample-legend'); check('Legend visibility option removes native entries', not any(e['id']=='legend-0' for e in state()['elements']))
            click('sample-legend')
            click('sample-loading'); wait(lambda s:s['loading'],'loading state'); check('Loading disables commands', not element('chart-zoom-in')['enabled'] and not element('chart-plot')['enabled'])
            screenshot('loading-state'); click('sample-loading')
            click('sample-error'); wait(lambda s:s['error'],'error state'); check('Error disables export', not element('chart-csv')['enabled'])
            screenshot('error-state'); click('sample-error')
            click('sample-empty'); wait(lambda s:s['empty'] and not s['hasData'],'empty state'); screenshot('empty-state')
            click('sample-reload'); wait(lambda s:s['hasData'] and s['series']==3,'reload')
            check('Empty/loading/error and recovery')
            click('sample-append'); s=wait(lambda s:s['rowsReceived']==100032,'bounded stream')
            check('100,000-row bounded source, 300,000 input values', s['total']==100000 and s['points']<=3600,
                  retainedRows=s['total'], displayedPoints=s['points'])
            click('chart-latest'); wait(lambda s:s['count']==256,'latest')
            check('Latest window follows 256 rows')
            click('sample-stream'); first=state()['rowsReceived']
            wait(lambda s:s['rowsReceived']>=first+64,'live timer stream',15)
            click('sample-stream'); stopped=state()['rowsReceived']; page.wait_for_timeout(400)
            check('Live append, bounded retention and pause', state()['rowsReceived']==stopped and state()['total']==100000, rowsReceived=stopped)
            screenshot('streaming-100k')
            nav=element('chart-navigator')
            page.mouse.click(nav['x']+nav['width']*.4,nav['y']+nav['height']/2); page.wait_for_timeout(100)
            check('Native range navigator changes window', state()['start']<state()['total']-state()['count'])
            # Explicit gallery API is used only for this exhaustive renderer smoke pass.
            page.evaluate("proCharts.SelectExample('overview')"); page.wait_for_timeout(100)
            kinds=page.evaluate('proCharts.GetKinds()').split(',')
            for kind in kinds:
                page.evaluate('(kind)=>proCharts.SelectKind(kind)',kind)
                wait(lambda s:s['kind']==kind and s['hasData'], f'{kind} source')
                page.mouse.move(1,1); page.wait_for_timeout(130)
                plot=element('chart-plot')
                clip={k:plot[k] for k in ['x','y','width','height']}
                blob=page.screenshot(clip=clip)
                colored=color_pixels(blob)
                (output/'screenshots'/f'kind-{kind}.png').write_bytes(blob)
                assert colored>200, f'{kind}: only {colored} colored plot pixels'
                report['renderers'].append({'kind':kind,'coloredPixels':colored,'series':state()['series'],'points':state()['points']})
            check('All chart kinds render in actual browser canvas', len(report['renderers'])==len(kinds), kinds=len(kinds))
            page.evaluate("proCharts.SelectExample('financial')"); page.wait_for_timeout(150); screenshot('financial')
            page.evaluate("proCharts.SelectExample('overview')"); page.wait_for_timeout(150)
            for width,height in [(800,1000),(420,950)]:
                page.set_viewport_size({'width':width,'height':height}); page.wait_for_timeout(250)
                plot=element('chart-plot'); screenshot(f'responsive-{width}')
                check(f'Responsive {width}px layout', plot['width']>200 and plot['height']>=120 and plot['x']+plot['width']<=width+1,
                      plot=plot)
                check(f'Sidebar hidden at {width}px',not any(e['id']=='sample-examples' for e in state()['elements']))
            page.set_viewport_size({'width':1440,'height':1000}); page.wait_for_timeout(200)
            for _ in range(4): click('sample-template'); click('sample-template')
            check('Repeated template replacement remains interactive', element('chart-zoom-in')['enabled'])
            check('No uncaught browser exceptions', not report['pageErrors'], errors=report['pageErrors'])
            check('No failed HTTP resources', not report['httpErrors'], errors=report['httpErrors'])
            check('No browser console errors', not report['consoleErrors'], errors=report['consoleErrors'])
            report['passed']=True
        except Exception as error:
            report['passed']=False; report['failure']=str(error); report['traceback']=traceback.format_exc()
            try: page.screenshot(path=str(output/'failure.png')); report['failureState']=state()
            except Exception: pass
            print(report['traceback'],flush=True)
        finally:
            report['elapsedSeconds']=round(time.monotonic()-start,2)
            (output/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
            browser.close(); server.shutdown()
    if not report.get('passed'): raise SystemExit(1)
    print(json.dumps({'passed':True,'checks':len(report['checks']),'renderers':len(report['renderers']),'elapsedSeconds':report['elapsedSeconds']}))

if __name__=='__main__': main()
