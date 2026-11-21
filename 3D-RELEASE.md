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

## Native acceptance — 2026-11-21

- Native QA run 44933295393 passed all real swipe/button, hurdle/gate clearance,
  token collection, train loss/retry, 600/1200m unlock, exact cold persistence,
  district selection and genuine running Safari/background/return/resume gates.
- Export 44917501743; tested head 8d7bf34. Gameplay/rendering code remains the
  lighting-corrected f4d9e47; later Unity changes are read-only QA publication.
- Inspected all 13 current native captures (menu, jump/slide pause, explicitly
  deferred clearance followups, canal/night, train loss, restored menu, actual
  Safari and fresh paused return) and original unobscured running image from
  run 44875831756, same rendering code. Original low-poly courier/trains/route,
  legible HUD/controls, no pink materials or safe-area overlaps observed.
- Current captures intentionally show pause overlays, not fabricated live poses.
  Raw clearance observations are separate from deferred followup screenshots.
- Functional and visual simulator acceptance complete. Physical device not tested.
- Replacement release/distribution still pending; build 4 unused at fresh ASC check.

## Replacement TestFlight delivery — 2026-11-21 18:49 UTC

Main 2da28ff; device export44935505879; signing/upload44936580335 succeeded.
ASC app6812287453 / Com.play.subsurf replacement build4
267e86e6-f469-4dd2-98ad-f9c95445ed2d is VALID and IN_BETA_TESTING.
Separately re-read Internal Testing group5da9902a-c591-428f-b83a-77e298b7dae4:
contains build4 and intended app-specific testerab5055ff-1094-4d5d-bac3-eef947466890.
Tester membership/state exists; installation of this replacement was not observed.
Simulator product acceptance and intended internal distribution complete; no claim
of external beta/App Store approval or physical-device testing.
