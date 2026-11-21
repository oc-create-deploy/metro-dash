# Metro Dash: independent 3D courier runner

## Scope / identity

Keep `Com.play.subsurf`, Metro Dash, 1.0, portrait iPhone, iOS >=15.
Replacement build must be **4 or higher**, checked fresh against ASC before upload.
Original procedural low-poly courier, helmet/backpack, teal metro carriages and
city architecture; no Subway Surfers assets, characters, UI or extracted code.
Reference is the genre: three lanes, chase camera, swipe left/right/up/down,
train/hurdle/overhead hazards, tokens, increasing speed and district progression.

## Acceptance (not satisfied by compilation)

- Actual perspective 3D scene visible on native iPhone simulator. Inspect menu,
  running, trains, jump, slide, pause, night/canal, game-over screenshots.
- Continuous animated character; smooth lateral lane changes. Native swipes
  left/right change lanes, up jumps, down slides. On-screen controls also work.
- Real token collection, train collision loss, hurdle jump and gate slide.
- No three-lane impossible obstacle rows. Each row reserves one unobstructed lane.
- Distance/speed progression; districts unlock at 600/1200 m; unlocked starting
  district selectable after cold launch, with actual route palette/hazard change.
- Pause freezes world/distance/jump timers; resume restores it. Background pauses.
- Retry resets the run, retaining wallet/best/district unlocks. Cold launch agrees.
- No runtime exceptions, pink/stripped materials, HUD overlap or safe-area clipping.
- Merge/push only after acceptance; device export, signing/upload >=4, then fresh
  ASC VALID plus actual intended tester/group/build and eligible beta state.

## Host limitations

Current Linux ARM64 host has no Unity Editor or iOS runtime. Licensed Unity export
and actual simulator gameplay checks run in existing CI; no new paid services.
Old 2D native assertions are invalid for this redesign and must be replaced.
Physical-device verification remains unavailable, not claimed.
