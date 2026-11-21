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
        candidates=list(container.rglob('qa-state.json'))
        for p in candidates:
            try:
                s=json.loads(p.read_text())
                assert not s['errors'],s['errors']
                if s["frame"] >= 30: return s
            except (json.JSONDecodeError,FileNotFoundError): pass
        if candidates:
            # Recorder uses non-atomic File.WriteAllText. A partial JSON read
            # is not a relocated container: avoid slow simctl IPC while running.
            time.sleep(.01)
            continue
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
    # Transition polling below verifies delivery; do not add blind latency.

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
        time.sleep(.05)
    assert predicate(),'Observed native transition timed out'
def start_or_resume():
    press(215,539)
    try:
        wait_for(playing)
    except AssertionError:
        # A native touch can be missed under simulator load. Retry once ONLY
        # when fresh frames prove the unchanged, live paused menu, never loss.
        before=state(); values={v['key']:v['value'] for v in before['values']}
        assert values['MetroDashGame.started']=='True' and values['MetroDashGame.gameOver']=='False'
        time.sleep(.2)
        after=state()
        assert after['frame']>before['frame'] and not playing() and not flag('gameOver')
        with (out/'resume-input-retries.jsonl').open('a') as f:
            f.write(json.dumps({'before':before,'after':after})+'\n')
        press(215,539);wait_for(playing)

def playing():return val('MetroDashGame.playing')=='True'
def move_to(target):
    for _ in range(2):
        lane=int(number('lane'))
        if lane==target:return
        began=time.time();before=state()
        press(59 if target<lane else 371,881)
        wait_for(lambda:int(number('lane'))!=lane or not playing(),3)
        after=state()
        with (out/'lane-input-latency.jsonl').open('a') as f:
            f.write(json.dumps({'target':target,'from':lane,'seconds':time.time()-began,'before':before,'after':after})+'\n')
        if not playing():return
def swipe(direction):
    # Same running XCUITest driver; genuine native screen gestures, not model writes.
    bridge.ensure_native()
    starts={'left':('65%, 60%','30%, 60%'),'right':('30%, 60%','65%, 60%'),
            'up':('50%, 72%','50%, 43%'),'down':('50%, 43%','50%, 72%')}
    start,end=starts[direction];sx,sy=bridge.xy(start);ex,ey=bridge.xy(end)
    bridge.native_request(bridge.native_base,'swipe',{'appId':bundle,'startX':sx,'startY':sy,'endX':ex,'endY':ey,'duration':.2})
def flag(key):return val('MetroDashGame.'+key)=='True'
def hazards(s):return [i for i in s['items'] if i['kind']=='barrier' and i['y']>-5]

def capture_paused(label):
    # simctl screenshot can block for seconds. Use the real pause button so
    # observation does not leave an unattended runner driving into the next row.
    assert playing(), 'Cannot capture a cleared obstacle after loss'
    press(362,54);wait_for(lambda:not playing())
    assert not flag('gameOver'), 'Lost before capture pause'
    frozen=number('Score')
    capture(label+'-paused')
    assert number('Score')==frozen, 'Capture pause advanced world'
    start_or_resume()

def survive_until(target_level, seconds=150):
    """Read-only obstacle observation guides real control taps; no hidden game writes."""
    end=time.time()+seconds
    jumped=slid=False
    canal_captured=False
    next_action_at=0
    while playing() and time.time()<end:
        s=state(); observed={v['key']:v['value'] for v in s['values']}
        lane=int(float(observed['MetroDashGame.lane']));speed=float(observed['MetroDashGame.Speed'])
        ahead=[i for i in hazards(s) if i['y']>-5]
        # Train length is 8m: its front reaches the runner at z=4.3, not at its centre.
        nearby=sorted([i for i in ahead if 0<i['y']<32],key=lambda i:i['y'])
        wanted=next((i for i in nearby if (i['id']==2 and not jumped) or (i['id']==3 and not slid)),None)
        # Never steer toward a later target through a nearer obstacle in its
        # destination OR intermediate lanes, including a train whose centre
        # passed z0 but whose trailing 4.3m still collides. See 44899641504.
        with (out/'navigation.jsonl').open('a') as trace:
            trace.write(json.dumps({'time':time.time(),'frame':s['frame'],'score':float(observed['MetroDashGame.Score']),'lane':lane,'nearby':nearby})+'\n')
        if wanted and wanted['group']!=lane and wanted['y']>speed*1.7 and not any(
                i['group']!=lane and min(lane,wanted['group'])<=i['group']<=max(lane,wanted['group'])
                and -5<i['y']<wanted['y']-1 for i in ahead):
            move_to(wanted['group'])
            # Native lane input consumed ~0.7s in 44884402240; never
            # schedule jump/slide from the pre-steering hazard snapshot.
            s=state();observed={v['key']:v['value'] for v in s['values']}
            lane=int(float(observed['MetroDashGame.lane']));speed=float(observed['MetroDashGame.Speed'])
            ahead=hazards(s)
            nearby=sorted([i for i in ahead if 0<i['y']<32],key=lambda i:i['y'])
        danger=next((i for i in nearby if i['group']==lane),None)
        if danger and (danger['id'] in (1,4) or (danger['id']==2 and jumped) or (danger['id']==3 and slid)):
            # Escape the current row; a later row 26m away must not
            # disqualify its otherwise clear escape lane.
            horizon=max(speed*.9, danger['y']+3)
            safe=[l for l in range(3) if not any(i['group']==l and -5<i['y']<horizon for i in ahead)]
            # Do not cross a blocked intermediate lane to reach a clear outer
            # lane. Reserve one native-tap interval plus collision extent for
            # crossing the intermediate lane, not both taps plus a train
            # extent for every hazard. The old 1.5s blanket rejected an
            # early clear path at z28, then trapped lane2 behind a train
            # during the canal capture in 44896401431.
            safe=[l for l in safe if not any(
                min(l,lane)<i['group']<max(l,lane) and -5<i['y']<speed*.85+(4.3 if i['id']==1 else 1.0)
                for i in ahead)]
            if safe:
                move_to(min(safe,key=lambda l:abs(l-lane)))
                continue
            # If a proven hurdle/gate is the only traversable route, perform
            # the real action again rather than crossing an imminent train.
        # Jump airtime is shorter than slide duration. At z10.93/s12.01
        # the 44895474024 jump landed before the trailing collision edge
        # (height1.0316 at z-.502), despite successful native activation.
        action_lead=.82 if danger and danger['id']==2 else .95
        if danger and danger['id'] in (2,3) and danger['y']<speed*action_lead and time.time()>next_action_at:
            (out/('hazard-before-'+str(seq)+'.json')).write_text(json.dumps(s,indent=2))
            # Snapshot cadence (~0.35s) plus native delivery can skip a
            # 0.75s threshold into a <0.45s approach. Use kind-specific lead;
            # still require actual activation AND observed obstacle clearance.
            action_started=time.time()
            key='IsJumping' if danger['id']==2 else 'IsSliding'
            press(160 if danger['id']==2 else 270,881)
            wait_for(lambda:flag(key) or not playing(),2)
            with (out/'input-latency.jsonl').open('a') as trace:
                trace.write(json.dumps({'kind':danger['id'],'beforeZ':danger['y'],'speed':speed,'seconds':time.time()-action_started,'score':number('Score'),'active':flag(key),'playing':playing()})+'\n')
            if not (playing() and flag(key)):
                capture('hazard-input-failed')
                raise AssertionError(('Required real jump/slide did not activate',key,danger,number('Score'),playing()))
            # Crossing the obstacle alive proves geometry, not just animation.
            kind=danger['id']
            # Wait only to the observed row's trailing collision edge, not a
            # fixed 1.2s after input (which approached the NEXT train blindly).
            deadline=action_started+2
            cleared=False
            while playing() and time.time()<deadline:
                row=[i for i in hazards(state()) if i['id']==kind and i['group']==danger['group'] and i['y']<danger['y']+2]
                if not row or min(i['y'] for i in row)<-1:
                    cleared=True;break
                time.sleep(.05)
            assert playing() and cleared,('Failed obstacle clearance',kind)

            if kind==2 and not jumped:capture_paused('04-hurdle-cleared');jumped=True
            if kind==3 and not slid:capture_paused('05-gate-cleared');slid=True
            next_action_at=time.time()+.6
        elif danger:
            pass # Stay aligned until this observed hurdle/gate reaches the action window.
        else:
            tokens=sorted([i for i in s['items'] if i['kind']=='roadCoin' and 5<i['y']<32],key=lambda i:i['y'])
            for token in tokens:
                if not any(i['group']==token['group'] and -5<i['y']<34 for i in ahead):
                    move_to(token['group']);break
        # Pause/resume delivery advances several metres. Defer the canal
        # observation until the CURRENT lane is clear beyond that latency
        # budget, rather than pausing in a mandatory two-lane escape (626m
        # failure in 44901405762). Still require the actual canal capture.
        if int(observed['MetroDashGame.Level'])==1 and not canal_captured:
            fresh=state(); fv={v['key']:v['value'] for v in fresh['values']}
            current=int(float(fv['MetroDashGame.lane']))
            if not any(i['group']==current and -5<i['y']<float(fv['MetroDashGame.Speed'])*2.5 for i in hazards(fresh)):
                capture_paused('06-canal');canal_captured=True
        if int(observed['MetroDashGame.Level'])>=target_level and jumped and slid and canal_captured:
            capture_paused('06-district-'+str(target_level));return
        time.sleep(.02 if danger and danger['y']<speed*1.4 else .1)
    raise AssertionError(('Survival/obstacle/district gate incomplete',number('Score'),jumped,slid,playing()))

try:
    state();action('- tapOn:\n    point: "95%, 95%"');capture('01-menu-3d')
    start_or_resume()
    # Running image already verified in 44875831756. Synchronous simctl
    # screenshots took seconds while hazards advanced; do not block first swipes.
    swipe('left');wait_for(lambda:int(number('lane'))==0)
    swipe('right');wait_for(lambda:int(number('lane'))==1)
    swipe('up');wait_for(lambda:flag('IsJumping'))
    press(362,54);wait_for(lambda:not playing());capture('02-jump-paused')
    score=number('Score');h=number('PlayerHeight');time.sleep(1)
    assert number('Score')==score and number('PlayerHeight')==h,'Paused world advanced'
    start_or_resume()
    wait_for(lambda:not flag('IsJumping'))
    swipe('down');wait_for(lambda:flag('IsSliding'))
    press(362,54);wait_for(lambda:not playing());capture('03-slide-paused')
    start_or_resume()
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
    start_or_resume();assert number('Score')<20,'Retry did not reset run'
    press(362,54);wait_for(lambda:not playing())
    restart();assert number('Coins')==coins and number('best')==best and int(number('UnlockedLevel'))==2,'Cold-launch persistence mismatch'
    capture('08-restored')
    press(331,663);start_or_resume()
    assert number('Score')>=1200 and int(number('Level'))==2,'Unlocked district selection did not start correct route'
    press(362,54);capture('09-night-start')
    start_or_resume()
    # Switch to a real system app, then foreground Metro; never mutate its model.
    before_background=state()
    subprocess.run(['xcrun','simctl','launch',udid,'com.apple.mobilesafari'],check=True)
    # Unity's recorder is suspended with the app: its on-disk pre-background
    # snapshot can still say playing=True. Inspect the real system app, then
    # require a fresh foreground frame to prove the actual pause callback.
    subprocess.run(['xcrun','simctl','io',udid,'screenshot',str(out/'10-system-background.png')],check=True)
    time.sleep(1)
    subprocess.run(['xcrun','simctl','launch',udid,bundle,'--playabilityQA'],check=True)
    wait_for(lambda:state()['frame']>before_background['frame'] and not playing(),20)
    assert not flag('gameOver'),'Background return lost the run'
    background_score=number('Score');time.sleep(1)
    assert number('Score')==background_score,'Foreground unexpectedly resumed world'
    capture('10-background-return-paused')
    start_or_resume()
    wait_for(lambda:number('Score')>background_score)
    press(362,54);wait_for(lambda:not playing())
    (out/'result.json').write_text(json.dumps({'nativeSwipeLeftRight':True,'nativeSwipeJumpSlide':True,'pauseStableResume':True,'actualCoinCollection':True,'hurdleJumpAndGateSlide':True,'trainLossRetry':True,'allDistrictsUnlocked':True,'coinsBestDistrictColdLaunch':True,'districtSelection':True,'backgroundPauseReturnResume':True,'visualAcceptance':'REQUIRES HUMAN IMAGE INSPECTION'}))
except Exception:
    capture('failure-state')
    raise
