# Metro Dash — logo-led quality revision, 2026-11-22

## Actual reference inspected
Assets/Brand/MetroDash-AppIcon.png: rear-view dark hooded runner, denim,
cyan-soled trainers, cyan and orange trains with gold-lit windows; palms,
sunlit skyline, stone viaduct arches, gold tokens, red/white barricade.
Genre benchmark: official Subway Surfers listing
https://apps.apple.com/us/app/subway-surfers/id512939461
Use readable chase composition, action rhythm, polish and vivid world as goals;
no copied character, assets, branding or code.

## Implemented source
- Replace boxy helmet/backpack courier with rounded hood/hoodie, rounded limbs,
  denim, hands, cyan-soled shoes and tread. Preserve independent limb animation.
- Cyan/orange carriages, rounded nose, warm lit windows, inset doors/gold belt.
- Red/white diagonal barricades and warning lamps; existing hazard volumes retained.
- Recycled palm-lined skyline and stone viaducts, with open central arches,
  blue sky and warm directional light. Decoration remains off playable tracks.
- Eight authored beats instead of random rows: center/left hold/center/right
  hold/center/recovery. Two breathing rows per phrase. Adjacent escape lane
  changes, safe token trails, separate train/jump/slide teaching in sunrise;
  canal/night mix hazards and add electric-track jumps. Existing 600/1200m
  unlocks and persistence retained. Physical input/collision code unchanged.

## Acceptance REQUIRED — source changes are not product approval
Linux host lacks Unity/iOS runtime. Diff checks only; Unity compile, materials,
rendering/performance, motion, native route playability NOT proven here.
After current sequential queue (Rally, Merge, Prism), export THIS branch with
simulator=true and run same-branch native QA against that NEW export.
Do not reuse earlier Metro build4/export evidence as acceptance of this revision.

Inspect original unobscured running captures and actual video: hoodie silhouette,
train/nose/window seams, cyan/orange contrast, arches/palms, coin and hazard
readability, district transitions, no scenery/HUD occlusion. Check 60fps budget
and draw-call impact of added primitives; optimize if needed before acceptance.
Native real swipes/buttons: tokens, jump hurdle, slide overhead, train death,
retry, all three earned districts, cold unlock/selection/wallet persistence,
pause/resume and genuine background/return. Play multiple authored phrases at
initial AND capped speed: safe lane never trapped by adjacent row train extents,
recovery has no surprise overlapping hazards. Fix source/harness honestly.
Only after genuine acceptance merge/push, device export/sign an unused build>=5
(fresh ASC check), independently verify VALID and intended tester/group/build
eligible beta. No physical device claim. Preserve ongoing CI; do not overlap.
