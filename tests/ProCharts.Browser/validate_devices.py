#!/usr/bin/env python3
"""Validate actual Avalonia input and exports at fractional/high DPI and emulated tablet touch.
Native Reveal() scrolls a target into view; it does not invoke commands or synthesize app input.
"""
from __future__ import annotations
import argparse, functools, json, threading, time, traceback
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from PIL import Image
from playwright.sync_api import sync_playwright

class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *args): pass

def main() -> None:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True); parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args(); root=args.root.resolve(); output=args.output.resolve(); output.mkdir(parents=True,exist_ok=True)
    assert (root/'_framework/dotnet.js').is_file(),'No actual published .NET browser runtime'
    server=ThreadingHTTPServer(('127.0.0.1',0),functools.partial(QuietHandler,directory=str(root)))
    threading.Thread(target=server.serve_forever,daemon=True).start()
    report={'passed':False,'configurations':[]}
    with sync_playwright() as p:
        browser=p.chromium.launch(headless=True,args=['--no-sandbox','--disable-dev-shm-usage','--enable-unsafe-swiftshader','--use-angle=swiftshader','--enable-webgl'])
        report['browserVersion']=browser.version
        try:
            for scale,touch,width,height in [(1.25,False,1280,950),(2,False,1280,950),(2,True,800,1100)]:
                name=('touch' if touch else 'mouse')+'-dpr-'+str(scale)
                run={'name':name,'scale':scale,'touchEmulated':touch,'checks':[],'errors':[]}
                report['configurations'].append(run)
                context=browser.new_context(viewport={'width':width,'height':height},device_scale_factor=scale,has_touch=touch,accept_downloads=True)
                page=context.new_page()
                page.on('pageerror',lambda error:run['errors'].append(str(error)))
                page.on('console',lambda message:run['errors'].append(message.text) if message.type=='error' else None)
                page.on('response',lambda response:run['errors'].append(str(response.status)+' '+response.url) if response.status>=400 else None)
                def state(): return page.evaluate('JSON.parse(proCharts.GetState())')
                def wait(predicate):
                    until=time.monotonic()+12
                    while time.monotonic()<until:
                        s=state()
                        if predicate(s): return s
                        page.wait_for_timeout(50)
                    raise AssertionError('Device condition timed out: '+json.dumps(state()))
                def element(id):
                    page.evaluate('(id)=>proCharts.Reveal(id)',id)
                    page.wait_for_timeout(100)
                    return next(e for e in state()['elements'] if e['id']==id)
                def activate(id):
                    wait(lambda s:any(e['id']==id and e['enabled'] for e in s['elements']))
                    e=element(id); x=e['x']+e['width']/2; y=e['y']+e['height']/2
                    assert 0 <= x < width and 0 <= y < height,(id,e)
                    if touch: page.touchscreen.tap(x,y)
                    else: page.mouse.click(x,y)
                    page.wait_for_timeout(100)
                def check(name,condition=True,**details):
                    assert condition,(name,details)
                    run['checks'].append({'name':name,'passed':True,**details})
                    print('PASS '+run['name']+' '+name,flush=True)
                def chrome_does_not_overlap():
                    items=state()['elements']; plot=next(e for e in items if e['id']=='chart-plot')
                    collisions=[]
                    for e in items:
                        if not e['id'].startswith('chart-') or e['id'] in ('chart-plot','chart-navigator'): continue
                        overlap=e['x'] < plot['x']+plot['width'] and e['x']+e['width'] > plot['x'] and e['y'] < plot['y']+plot['height'] and e['y']+e['height'] > plot['y']
                        if overlap: collisions.append(e['id'])
                    return collisions
                page.goto(f'http://127.0.0.1:{server.server_port}/',wait_until='domcontentloaded')
                page.wait_for_function('globalThis.chartBootError || (globalThis.proCharts && JSON.parse(proCharts.GetState()).ready)',timeout=180000)
                assert page.evaluate('globalThis.chartBootError || null') is None
                wait(lambda s:s['hasData'] and s['total']==48)
                check('Avalonia canvas boot',page.locator('canvas').count()>0)
                check('Wrapped toolbar does not overlap plot',not chrome_does_not_overlap(),collisions=chrome_does_not_overlap())
                activate('chart-zoom-in'); wait(lambda s:s['count']<48)
                check('Native zoom input')
                initial=state()['start']; plot=element('chart-plot')
                x=plot['x']+plot['width']*.55; y=plot['y']+plot['height']*.5
                if touch:
                    cdp=context.new_cdp_session(page)
                    cdp.send('Input.dispatchTouchEvent',{'type':'touchStart','touchPoints':[{'x':x,'y':y}]})
                    for i in range(1,9):
                        cdp.send('Input.dispatchTouchEvent',{'type':'touchMove','touchPoints':[{'x':x-plot['width']*.2*i/8,'y':y}]})
                    cdp.send('Input.dispatchTouchEvent',{'type':'touchEnd','touchPoints':[]})
                else:
                    page.mouse.move(x,y); page.mouse.down(); page.mouse.move(x-plot['width']*.2,y,steps=8); page.mouse.up()
                wait(lambda s:s['start']!=initial)
                check('Plot drag changes viewport')
                activate('chart-reset'); wait(lambda s:s['count']==48)
                activate('legend-0'); wait(lambda s:s['series']==2)
                activate('legend-0'); wait(lambda s:s['series']==3)
                check('Native legend toggle and restore')
                plot=element('chart-plot')
                with page.expect_download() as pending: activate('chart-png')
                downloaded=pending.value; path=output/(name+'.png'); downloaded.save_as(path)
                assert downloaded.failure() is None
                with Image.open(path) as image:
                    expected=(round(plot['width']*scale),round(plot['height']*scale))
                    check('PNG uses actual render scaling',abs(image.width-expected[0])<=2 and abs(image.height-expected[1])<=2,
                        pngPixels=list(image.size),expectedPixels=list(expected))
                activate('sample-template'); wait(lambda s:s['compact'])
                check('Compact toolbar does not overlap plot',not chrome_does_not_overlap(),collisions=chrome_does_not_overlap())
                activate('sample-template'); wait(lambda s:not s['compact'])
                check('Retemplating retains usable plot',element('chart-plot')['width']>200)
                page.screenshot(path=str(output/(name+'-screen.png')))
                check('No browser errors',not run['errors'],errors=run['errors'])
                run['passed']=True
                context.close()
            report['passed']=True
        except Exception as error:
            report['failure']=str(error); report['traceback']=traceback.format_exc()
            try:
                report['failureState']=state()
                report['hostDownloads']=page.evaluate('globalThis.chartDownloads')
                page.screenshot(path=str(output/'failure.png'))
            except Exception: pass
            print(report['traceback'],flush=True)
        finally:
            browser.close(); server.shutdown()
            (output/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    if not report['passed']: raise SystemExit(1)
    print('DEVICE_SUMMARY='+json.dumps(report,separators=(',',':')),flush=True)

if __name__=='__main__': main()
