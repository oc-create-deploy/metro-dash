"""Drive normal native touch input; QA snapshots are read-only observations."""
import json, os, pathlib, subprocess, time
out = pathlib.Path('screenshots'); out.mkdir(exist_ok=True)
udid = os.environ['UDID']; bundle = os.environ['APP_BUNDLE_ID']
container = pathlib.Path(subprocess.check_output(['xcrun','simctl','get_app_container',udid,bundle,'data'],text=True).strip())
seq = 0
import importlib.util
spec=importlib.util.spec_from_file_location('touchbridge',pathlib.Path(__file__).with_name('native-touch-bridge.py'))
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
bridge=module.TouchBridge(out,udid,bundle)

def state():
    global container
    deadline = time.time()+180
    while time.time()<deadline:
        for p in container.rglob('qa-state.json'):
            try:
                s=json.loads(p.read_text())
                assert not s['errors'],s['errors']
                if s["frame"] >= 30: return s
            except (json.JSONDecodeError,FileNotFoundError): pass
        # Simulator can relocate the data container during a slow first launch.
        try:
            container = pathlib.Path(subprocess.check_output(['xcrun','simctl','get_app_container',udid,bundle,'data'],text=True).strip())
        except subprocess.CalledProcessError: pass
        time.sleep(1)
    (out/'container-probe.txt').write_text(str(container)+'\n'+'\n'.join(str(p) for p in container.rglob('*') if p.is_file()))
    probe_errors=list(container.rglob('qa-recorder-error.txt'))
    if probe_errors:
        detail=probe_errors[0].read_text()
        (out/'qa-recorder-error.txt').write_text(detail)
        raise AssertionError('Native diagnostic recorder failed: '+detail)
    raise AssertionError('Read-only native snapshot missing')

def capture(label):
    (out/(label+'.json')).write_text(json.dumps(state(),indent=2))
    subprocess.run(['xcrun','simctl','io',udid,'screenshot',str(out/(label+'.png'))],check=True)

def point(item):
    assert 0<item['x']<1 and 0<item['y']<1,item
    return f"{round(item['x']*100)}%, {round(item['y']*100)}%"

def swipe_point(item):
    return f"{item['x']*100:.4f}%, {item['y']*100:.4f}%"

def action(body):
    global seq
    seq+=1
    p=out/f'input-{seq:03}.yaml'
    p.write_text(f'appId: {bundle}\n---\n'+body+'\n')
    if bridge.proc is not None and body.startswith("- tapOn:"):
        bridge.tap_direct(body.split('"')[1])
    else: bridge.action(body)
    time.sleep(.1)

def tap(text):
    choices=[i for i in state()['items'] if i['kind']=='button' and i['interactable'] and text.casefold() in (i.get('text','').strip().casefold(),i['name'].strip().casefold())]
    assert choices,('Button missing',text)
    action('- tapOn:\n    point: "'+point(choices[0])+'"')

def val(key):return next(v['value'] for v in state()['values'] if v['key']==key)

def number(key): return float(val('MetroDashGame.'+key))
def press(x,y):
    s=state(); factor=min(s['safeW']/430,s['safeH']/932)
    left=s['safeX']+(s['safeW']-430*factor)/2
    top=s['height']-s['safeY']-s['safeH']+(s['safeH']-932*factor)/2
    action('- tapOn:\n    point: "'+f"{100*(left+x*factor)/s['width']:.4f}%, {100*(top+y*factor)/s['height']:.4f}%"+'"')
def restart():
    subprocess.run(['xcrun','simctl','terminate',udid,bundle],check=True)
    for p in container.rglob('qa-state.json'):p.unlink()
    subprocess.run(['xcrun','simctl','launch',udid,bundle],check=True)
    time.sleep(4)
def wait_for(predicate,seconds=8):
    end=time.time()+seconds
    while time.time()<end:
        if predicate():return
        time.sleep(.2)
    assert predicate(),'Observed native transition timed out'
def playing():return val('MetroDashGame.playing')=='True'
def move_to(target):
    for _ in range(2):
        lane=int(number('lane'))
        if lane==target:return
        press(74 if target<lane else 356,881)
        wait_for(lambda:int(number('lane'))!=lane or not playing(),3)
state();action('- tapOn:\n    point: "95%, 95%"');capture('01-menu')
press(215,520);wait_for(playing)
move_to(0);assert int(number('lane'))==0
move_to(2);assert int(number('lane'))==2
press(215,881);wait_for(lambda:not playing())
capture('02-paused');score=number('Score');time.sleep(2);assert number('Score')==score,'Paused distance changed'
press(215,520);wait_for(playing)
# Drive normal UI toward observed coins; collisions/retries remain real.
initial=number('Coins');end=time.time()+120;loss=False
while time.time()<end and (number('Coins')<=initial or not loss):
    if not playing():
        assert val('MetroDashGame.gameOver')=='True','Unexpected paused state'
        loss=True;capture('03-loss');press(215,520);wait_for(playing)
    s=state();near=sorted([i for i in s['items'] if i['kind'] in ['roadCoin','barrier'] and 100<i['y']<700],key=lambda i:-i['y'])
    if near:
        obj=near[0];target=obj['group'] if obj['kind']=='roadCoin' else (obj['group']+1)%3
        move_to(target)
    time.sleep(.15)
assert number('Coins')>initial,'No actual coin collected'
capture('04-collected')
# Deliberately enter a real barrier lane to exercise game-over and retry.
end=time.time()+60
while playing() and time.time()<end:
    barriers=sorted([i for i in state()['items'] if i['kind']=='barrier' and 0<i['y']<700],key=lambda i:-i['y'])
    if barriers:move_to(barriers[0]['group'])
    time.sleep(.2)
assert not playing() and val('MetroDashGame.gameOver')=='True'
capture('05-gameover');coins=number('Coins');best=number('best');assert best>0
press(215,520);wait_for(playing);assert number('Score')<best
press(215,881);wait_for(lambda:not playing())
restart();assert number('Coins')==coins and number('best')==best,'Coins/best coldlaunch failed'
capture('06-restored')
(out/'result.json').write_text(json.dumps({'nativeLeftRight':True,'pauseStableResume':True,'actualCoinCollection':True,'barrierLossRetry':True,'coinsBestColdLaunch':True}))
