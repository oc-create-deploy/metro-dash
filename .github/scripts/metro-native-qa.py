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
    deadline = time.time()+300
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
    subprocess.run(['xcrun','simctl','launch',udid,bundle,'--playabilityQA'],check=True)
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
        press(59 if target<lane else 371,881)
        wait_for(lambda:int(number('lane'))!=lane or not playing(),3)
def swipe(direction):
    # Same running XCUITest driver; genuine native screen gestures, not model writes.
    bridge.ensure_native()
    starts={'left':('65%, 60%','30%, 60%'),'right':('30%, 60%','65%, 60%'),
            'up':('50%, 72%','50%, 43%'),'down':('50%, 43%','50%, 72%')}
    start,end=starts[direction];sx,sy=bridge.xy(start);ex,ey=bridge.xy(end)
    bridge.native_request(bridge.native_base,'swipe',{'appId':bundle,'startX':sx,'startY':sy,'endX':ex,'endY':ey,'duration':.2})
def flag(key):return val('MetroDashGame.'+key)=='True'
def hazards(s):return [i for i in s['items'] if i['kind']=='barrier' and i['y']>-5]

def survive_until(target_level, seconds=150):
    """Read-only obstacle observation guides real control taps; no hidden game writes."""
    end=time.time()+seconds
    jumped=slid=False
    next_action_at=0
    while playing() and time.time()<end:
        s=state(); lane=int(number('lane')); speed=number('Speed')
        ahead=[i for i in hazards(s) if i['y']>-5]
        # Train length is 8m: its front reaches the runner at z=4.3, not at its centre.
        nearby=sorted([i for i in ahead if 0<i['y']<32],key=lambda i:i['y'])
        danger=next((i for i in nearby if i['group']==lane),None)
        if danger and danger['id'] in (1,4):
            safe=[l for l in range(3) if not any(i['group']==l and -5<i['y']<34 for i in ahead)]
            if safe:move_to(min(safe,key=lambda l:abs(l-lane)))
        elif danger and danger['id'] in (2,3) and danger['y']<speed*.42 and time.time()>next_action_at:
            key='IsJumping' if danger['id']==2 else 'IsSliding'
            press(160 if danger['id']==2 else 270,881)
            wait_for(lambda:flag(key) or not playing(),2)
            assert playing() and flag(key),'Required real jump/slide did not activate'
            # Crossing the obstacle alive proves geometry, not just animation.
            kind=danger['id'];deadline=time.time()+1.2
            while playing() and time.time()<deadline:time.sleep(.08)
            assert playing(),('Failed obstacle clearance',kind)
            if kind==2 and not jumped:capture('04-hurdle-cleared');jumped=True
            if kind==3 and not slid:capture('05-gate-cleared');slid=True
            next_action_at=time.time()+.6
        else:
            tokens=sorted([i for i in s['items'] if i['kind']=='roadCoin' and 5<i['y']<32],key=lambda i:i['y'])
            for token in tokens:
                if not any(i['group']==token['group'] and -5<i['y']<34 for i in ahead):
                    move_to(token['group']);break
        if int(number('Level'))>=target_level and jumped and slid:
            capture('06-district-'+str(target_level));return
        time.sleep(.1)
    raise AssertionError(('Survival/obstacle/district gate incomplete',number('Score'),jumped,slid,playing()))

state();action('- tapOn:\n    point: "95%, 95%"');capture('01-menu-3d')
press(215,539);wait_for(playing)
swipe('left');wait_for(lambda:int(number('lane'))==0)
swipe('right');wait_for(lambda:int(number('lane'))==1)
swipe('up');wait_for(lambda:flag('IsJumping'))
press(362,54);wait_for(lambda:not playing());capture('02-jump-paused')
score=number('Score');h=number('PlayerHeight');time.sleep(1)
assert number('Score')==score and number('PlayerHeight')==h,'Paused world advanced'
press(215,539);wait_for(playing)
wait_for(lambda:not flag('IsJumping'))
swipe('down');wait_for(lambda:flag('IsSliding'))
press(362,54);wait_for(lambda:not playing());capture('03-slide-paused')
press(215,539);wait_for(playing)
initial=number('Coins')
survive_until(2,150)
assert number('Coins')>initial,'No actual token collected'
assert int(number('UnlockedLevel'))==2,'Districts did not unlock'
# Deliberately collide with a train, then exercise retry.
end=time.time()+50
while playing() and time.time()<end:
    trains=sorted([i for i in hazards(state()) if i['id']==1 and 7<i['y']<35],key=lambda i:i['y'])
    if trains:move_to(trains[0]['group'])
    time.sleep(.12)
assert not playing() and flag('gameOver'),'Real train collision did not end run'
capture('07-train-collision');coins=number('Coins');best=number('best');assert best>=1200
press(215,539);wait_for(playing);assert number('Score')<20,'Retry did not reset run'
press(362,54);wait_for(lambda:not playing())
restart();assert number('Coins')==coins and number('best')==best and int(number('UnlockedLevel'))==2,'Cold-launch persistence mismatch'
capture('08-restored')
press(331,663);press(215,539);wait_for(playing)
assert number('Score')>=1200 and int(number('Level'))==2,'Unlocked district selection did not start correct route'
press(362,54);capture('09-night-start')
(out/'result.json').write_text(json.dumps({'nativeSwipeLeftRight':True,'nativeSwipeJumpSlide':True,'pauseStableResume':True,'actualCoinCollection':True,'hurdleJumpAndGateSlide':True,'trainLossRetry':True,'allDistrictsUnlocked':True,'coinsBestDistrictColdLaunch':True,'districtSelection':True,'visualAcceptance':'REQUIRES HUMAN IMAGE INSPECTION'}))
