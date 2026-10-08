# Forest dragon animation generation prompts

Generated 2026-10-02 with imagegen. Original RGBA outputs are preserved without pixel rewriting. Sprite rectangles/pivots and a rendering-safe UI material are authored in Unity.

Reference: `forest_dragon_reference.png`

Rows: idle, walk, windup, attack, hurt, defeat. Six frames per row, chronological left to right.

## Dragon 0

Source: `dragon_0_sheet.png`

Generated file: `C:/Users/Junbeom_Choi/.codex/generated_images/01a0fa35-ac2f-7093-84bf-145b0dc5c22d/exec-0ba00473-dc20-471d-acb7-9b5de633ea5e.png`

```text
Use case: stylized-concept. Asset type: production pixel-art animation SPRITE SHEET for a Unity side-view game.
Input Image 1 is a character-design and pixel-art STYLE REFERENCE only, NOT a poster to edit. Generate a NEW square transparent animation sheet of exactly ONE species. Ignore all reference text, UI, other species, and human figures.
OUTPUT CONTRACT: square 1536x1536 RGBA canvas, exactly SIX COLUMNS and SIX ROWS of equal 256x256 cells, exactly36 full-body sprites. Columns are sequential animation frames0 through5; rows are6 different actions described below. No grid lines, borders, letters, labels, shadows, scene or backgrounds. True transparent exterior including all space between sprites, NOT a checkerboard picture. Each silhouette fits inside its own cell with at least20 clear pixels from each edge; horns, claws, long tail never cropped or overlap another cell. Consistent scale, character proportions, palette, side-view camera, and floor line across every frame. Every character faces LEFT, towards an opponent outside the sheet. Designed visible crisp pixel clusters, restrained detailed16-bit game pixel art, no watercolor/smooth painted gradients. Dark contours use charcoal RGB18 or brighter, NO pure opaque RGB0 black because game uses black color-key transparency.
Rows from TOP to BOTTOM:
1 idle: six gently different breathing/eyes/head/tail frames, planted feet, seamless loop.
2 walk: six clearly different stepping frames, alternating front/back feet, left-facing walking cycle.
3 windup: six progressive anticipation poses of this species's attack, increasingly committed by frame5.
4 attack: six chronological poses, frame0 initial release, frame1 accelerates, frame2 nearing contact, frame3 strongest full attack/contact, frame4 recovery starts, frame5 back to stable ready posture. Show actual articulated body movement, not six identical drawings.
5 hurt: six recoil poses, frame0 slight flinch, frame1/2 increasing, frame3 maximum reaction, frame4 recovers, frame5 stable. No wounds, no blood.
6 defeat: six progressive knockout poses, frame0 loses balance, frame1 legs buckle, frame2 lowered, frame3 belly approaches floor, frame4 collapses lying on floor, frame5 peacefully knocked-out fully lying on floor with eyes shut, no gore. Do NOT just rotate an upright sprite.
One complete connected dragon per cell. Do not add motion lines or detached effects, no humans.
SPECIES: only the FIRST/LEFTMOST reference species "forest baby dragon". Small friendly yet scrappy round light-green quadruped, cream muzzle and belly, large expressive dark green eyes, tiny tan forehead horn, a few warm ochre triangular dorsal spikes, short tapering upturned tail. No wings. Keep its rounded baby proportions exactly like the first reference panel, NOT the adult green rhino or red giant. Its windup crouches down with mouth shut, then attack is a small forward hopping nip / front-paw pounce, mouth opens at contact frame3; tail counterbalances. Hurt pulls the head back; defeat curls into a low flattened resting body. Same baby identity in all36 cells. Keep the sprites approximately160x130 within each256 cell, centered horizontally, feet resting at cell y220 from top in idle/walk.
```

## Dragon 1

Source: `dragon_1_sheet.png`

Generated file: `C:/Users/Junbeom_Choi/.codex/generated_images/01a0fa35-ac2f-7093-84bf-145b0dc5c22d/exec-a31ca4ed-d520-4d54-8f0b-4c0ab9155110.png`

```text
Use case: stylized-concept. Asset type: production pixel-art animation SPRITE SHEET for a Unity side-view game.
Input Image 1 is a character-design and pixel-art STYLE REFERENCE only, NOT a poster to edit. Generate a NEW square transparent animation sheet of exactly ONE species. Ignore all reference text, UI, other species, and human figures.
OUTPUT CONTRACT: square 1536x1536 RGBA canvas, exactly SIX COLUMNS and SIX ROWS of equal 256x256 cells, exactly36 full-body sprites. Columns are sequential animation frames0 through5; rows are6 different actions described below. No grid lines, borders, letters, labels, shadows, scene or backgrounds. True transparent exterior including all space between sprites, NOT a checkerboard picture. Each silhouette fits inside its own cell with at least20 clear pixels from each edge; horns, claws, long tail never cropped or overlap another cell. Consistent scale, character proportions, palette, side-view camera, and floor line across every frame. Every character faces LEFT, towards an opponent outside the sheet. Designed visible crisp pixel clusters, restrained detailed16-bit game pixel art, no watercolor/smooth painted gradients. Dark contours use charcoal RGB18 or brighter, NO pure opaque RGB0 black because game uses black color-key transparency.
Rows from TOP to BOTTOM:
1 idle: six gently different breathing/eyes/head/tail frames, planted feet, seamless loop.
2 walk: six clearly different stepping frames, alternating front/back feet, left-facing walking cycle.
3 windup: six progressive anticipation poses of this species's attack, increasingly committed by frame5.
4 attack: six chronological poses, frame0 initial release, frame1 accelerates, frame2 nearing contact, frame3 strongest full attack/contact, frame4 recovery starts, frame5 back to stable ready posture. Show actual articulated body movement, not six identical drawings.
5 hurt: six recoil poses, frame0 slight flinch, frame1/2 increasing, frame3 maximum reaction, frame4 recovers, frame5 stable. No wounds, no blood.
6 defeat: six progressive knockout poses, frame0 loses balance, frame1 legs buckle, frame2 lowered, frame3 belly approaches floor, frame4 collapses lying on floor, frame5 peacefully knocked-out fully lying on floor with eyes shut, no gore. Do NOT just rotate an upright sprite.
One complete connected dragon per cell. Do not add motion lines or detached effects, no humans.
EDGE QUALITY: The catalog reference has colored masking artifacts; DO NOT copy them. No red/green/yellow halos, no neon outline, no drop shadows. Clean charcoal pixel outlines and truly clear alpha around every sprite. Prefer at least30 pixels of empty space between silhouettes. Use Image2 only as consistency reference for the six-by-six animation layout and pixel detail; NOT its baby body shape.
SPECIES: ONLY the SECOND catalog species, "Headbutt dragon". A stocky low emerald/olive-green quadruped with rugged mossy plate scales, cream/beige belly, ONE prominent thick ivory forehead rhinoceros horn curving slightly upward and forward, smaller ochre spikes along back, short thick tapering tail. Squat bulldog/rhino build, serious small red-brown eye. No wings, no arms shaped like a person. Preserve the horn and armor pattern in all36 cells. Windup progressively plants feet and lowers the heavy forehead horn facing LEFT, paws brace. Attack is a LEFTWARD headbutt: recoil, release, head/neck drive rapidly forward, frame3 strongest level horn contact, then horns lift back into guard. Hurt shakes/recoils the head while legs brace. Defeat folds the legs and ends prone on the belly, horn and snout down. Consistent body roughly160x135 within each256 cell.
```

## Dragon 2

Source: `dragon_2_sheet.png`

Generated file: `C:/Users/Junbeom_Choi/.codex/generated_images/01a0fa35-ac2f-7093-84bf-145b0dc5c22d/exec-c69beab3-41cc-4b84-8e23-69cd77db66b6.png`

```text
Use case: stylized-concept. Asset type: production pixel-art animation SPRITE SHEET for a Unity side-view game.
Input Image 1 is a character-design and pixel-art STYLE REFERENCE only, NOT a poster to edit. Generate a NEW square transparent animation sheet of exactly ONE species. Ignore all reference text, UI, other species, and human figures.
OUTPUT CONTRACT: square 1536x1536 RGBA canvas, exactly SIX COLUMNS and SIX ROWS of equal 256x256 cells, exactly36 full-body sprites. Columns are sequential animation frames0 through5; rows are6 different actions described below. No grid lines, borders, letters, labels, shadows, scene or backgrounds. True transparent exterior including all space between sprites, NOT a checkerboard picture. Each silhouette fits inside its own cell with at least20 clear pixels from each edge; horns, claws, long tail never cropped or overlap another cell. Consistent scale, character proportions, palette, side-view camera, and floor line across every frame. Every character faces LEFT, towards an opponent outside the sheet. Designed visible crisp pixel clusters, restrained detailed16-bit game pixel art, no watercolor/smooth painted gradients. Dark contours use charcoal RGB18 or brighter, NO pure opaque RGB0 black because game uses black color-key transparency.
Rows from TOP to BOTTOM:
1 idle: six gently different breathing/eyes/head/tail frames, planted feet, seamless loop.
2 walk: six clearly different stepping frames, alternating front/back feet, left-facing walking cycle.
3 windup: six progressive anticipation poses of this species's attack, increasingly committed by frame5.
4 attack: six chronological poses, frame0 initial release, frame1 accelerates, frame2 nearing contact, frame3 strongest full attack/contact, frame4 recovery starts, frame5 back to stable ready posture. Show actual articulated body movement, not six identical drawings.
5 hurt: six recoil poses, frame0 slight flinch, frame1/2 increasing, frame3 maximum reaction, frame4 recovers, frame5 stable. No wounds, no blood.
6 defeat: six progressive knockout poses, frame0 loses balance, frame1 legs buckle, frame2 lowered, frame3 belly approaches floor, frame4 collapses lying on floor, frame5 peacefully knocked-out fully lying on floor with eyes shut, no gore. Do NOT just rotate an upright sprite.
One complete connected dragon per cell. Do not add motion lines or detached effects, no humans.
EDGE QUALITY: The catalog reference has colored masking artifacts; DO NOT copy them. No red/green/yellow halos, no neon outline, no drop shadows. Clean charcoal pixel outlines and truly clear alpha around every sprite. Prefer at least30 pixels of empty space between silhouettes. Use Image2 only as consistency reference for the six-by-six animation layout and pixel detail; NOT its baby body shape.
SPECIES: ONLY the THIRD catalog species, "Logtail dragon". Long low olive/moss-green quadruped lizard/dragon, tan cream lower jaw/belly, tan rocky horns and dorsal spines, short sturdy legs, and a VERY LONG THICK HEAVY CLUB-LIKE TAIL with rough woody bark-like brown-green ridges, curled upward at the tip. Small alert reddish eye. No wings. Its longer torso and distinctive log-tail must stay visible, not a short baby or rhinoceros build. Preserve all horn/tail/spine traits in every cell. Windup torso crouches, hips turn, tail coils behind shoulder progressively. Attack is NOT headbutting: a strong tail sweep, hips/torso rotate and the heavy tail sweeps forward toward the LEFT opponent; frame3 shows maximum forward tail swing, frame4 tail slows, frame5 returns to guard. Head generally still faces LEFT; tail may curve overhead/along flank, never cut off or cross a cell border. Hurt recoils with tail weight following; defeat ends long body flat with heavy tail resting behind on the floor. Consistent body+tail roughly190x140 within each256 cell, use less zoom if required.
```

## Dragon 3

Source: `dragon_3_sheet.png`

Generated file: `C:/Users/Junbeom_Choi/.codex/generated_images/01a0fa35-ac2f-7093-84bf-145b0dc5c22d/exec-a22b004b-148a-479b-9e38-814433492bde.png`

```text
Use case: stylized-concept. Asset type: production pixel-art animation SPRITE SHEET for a Unity side-view game.
Input Image 1 is a character-design and pixel-art STYLE REFERENCE only, NOT a poster to edit. Generate a NEW square transparent animation sheet of exactly ONE species. Ignore all reference text, UI, other species, and human figures.
OUTPUT CONTRACT: square 1536x1536 RGBA canvas, exactly SIX COLUMNS and SIX ROWS of equal 256x256 cells, exactly36 full-body sprites. Columns are sequential animation frames0 through5; rows are6 different actions described below. No grid lines, borders, letters, labels, shadows, scene or backgrounds. True transparent exterior including all space between sprites, NOT a checkerboard picture. Each silhouette fits inside its own cell with at least20 clear pixels from each edge; horns, claws, long tail never cropped or overlap another cell. Consistent scale, character proportions, palette, side-view camera, and floor line across every frame. Every character faces LEFT, towards an opponent outside the sheet. Designed visible crisp pixel clusters, restrained detailed16-bit game pixel art, no watercolor/smooth painted gradients. Dark contours use charcoal RGB18 or brighter, NO pure opaque RGB0 black because game uses black color-key transparency.
Rows from TOP to BOTTOM:
1 idle: six gently different breathing/eyes/head/tail frames, planted feet, seamless loop.
2 walk: six clearly different stepping frames, alternating front/back feet, left-facing walking cycle.
3 windup: six progressive anticipation poses of this species's attack, increasingly committed by frame5.
4 attack: six chronological poses, frame0 initial release, frame1 accelerates, frame2 nearing contact, frame3 strongest full attack/contact, frame4 recovery starts, frame5 back to stable ready posture. Show actual articulated body movement, not six identical drawings.
5 hurt: six recoil poses, frame0 slight flinch, frame1/2 increasing, frame3 maximum reaction, frame4 recovers, frame5 stable. No wounds, no blood.
6 defeat: six progressive knockout poses, frame0 loses balance, frame1 legs buckle, frame2 lowered, frame3 belly approaches floor, frame4 collapses lying on floor, frame5 peacefully knocked-out fully lying on floor with eyes shut, no gore. Do NOT just rotate an upright sprite.
One complete connected dragon per cell. Do not add motion lines or detached effects, no humans.
EDGE QUALITY: The catalog reference has colored masking artifacts; DO NOT copy them. No red/green/yellow halos, no neon outline, no drop shadows. Clean charcoal pixel outlines and truly clear alpha around every sprite. Prefer at least30 pixels of empty space between silhouettes. Use Image2 only as consistency reference for the six-by-six animation layout and pixel detail; NOT its baby body shape.
SPECIES: ONLY the FOURTH catalog species, "Stonehorn dragon". Heavy broad orange/ochre-brown quadruped covered in chunky layered rock armor, huge ivory forward horn plus smaller head horn, cream gray lower jaw and belly, thick dark stone feet, rugged orange rocky dorsal spines and a stubby thick armored tail. No wings. Body is stout, adult, heavy like a stone rhinoceros, NOT green and NOT the red giant. Preserve the reference armor, two head horn sizes, thick build in all36 cells. Windup digs front feet in, lowers shoulders and aims big horn toward LEFT. Attack is a heavy shoulder/horn shove: slow weight commitment then quick body thrust, frame3 shoulders fully committed, then knees and shoulders recoil and reset. Hurt briefly rocks/tilts the body without removing armor. Defeat legs buckle under weight and body settles belly-down on floor, head/ivory horn resting low. Consistent body roughly180x150 within each256 cell.
```

## Dragon 4

Source: `dragon_4_sheet.png`

Generated file: `C:/Users/Junbeom_Choi/.codex/generated_images/01a0fa35-ac2f-7093-84bf-145b0dc5c22d/exec-53d8bbf5-c0ee-4eab-b164-2f19419a0c9b.png`

```text
Use case: stylized-concept. Asset type: production pixel-art animation SPRITE SHEET for a Unity side-view game.
Input Image 1 is a character-design and pixel-art STYLE REFERENCE only, NOT a poster to edit. Generate a NEW square transparent animation sheet of exactly ONE species. Ignore all reference text, UI, other species, and human figures.
OUTPUT CONTRACT: square 1536x1536 RGBA canvas, exactly SIX COLUMNS and SIX ROWS of equal 256x256 cells, exactly36 full-body sprites. Columns are sequential animation frames0 through5; rows are6 different actions described below. No grid lines, borders, letters, labels, shadows, scene or backgrounds. True transparent exterior including all space between sprites, NOT a checkerboard picture. Each silhouette fits inside its own cell with at least20 clear pixels from each edge; horns, claws, long tail never cropped or overlap another cell. Consistent scale, character proportions, palette, side-view camera, and floor line across every frame. Every character faces LEFT, towards an opponent outside the sheet. Designed visible crisp pixel clusters, restrained detailed16-bit game pixel art, no watercolor/smooth painted gradients. Dark contours use charcoal RGB18 or brighter, NO pure opaque RGB0 black because game uses black color-key transparency.
Rows from TOP to BOTTOM:
1 idle: six gently different breathing/eyes/head/tail frames, planted feet, seamless loop.
2 walk: six clearly different stepping frames, alternating front/back feet, left-facing walking cycle.
3 windup: six progressive anticipation poses of this species's attack, increasingly committed by frame5.
4 attack: six chronological poses, frame0 initial release, frame1 accelerates, frame2 nearing contact, frame3 strongest full attack/contact, frame4 recovery starts, frame5 back to stable ready posture. Show actual articulated body movement, not six identical drawings.
5 hurt: six recoil poses, frame0 slight flinch, frame1/2 increasing, frame3 maximum reaction, frame4 recovers, frame5 stable. No wounds, no blood.
6 defeat: six progressive knockout poses, frame0 loses balance, frame1 legs buckle, frame2 lowered, frame3 belly approaches floor, frame4 collapses lying on floor, frame5 peacefully knocked-out fully lying on floor with eyes shut, no gore. Do NOT just rotate an upright sprite.
One complete connected dragon per cell. Do not add motion lines or detached effects, no humans.
EDGE QUALITY: The catalog reference has colored masking artifacts; DO NOT copy them. No red/green/yellow halos, no neon outline, no drop shadows. Clean charcoal pixel outlines and truly clear alpha around every sprite. Prefer at least30 pixels of empty space between silhouettes. Use Image2 only as consistency reference for the six-by-six animation layout and pixel detail; NOT its baby body shape.
SPECIES: ONLY the FIFTH/RIGHTMOST catalog species, "Giant forest dragon". Powerful mature dark brick-red/rust-brown quadruped with cream gray throat and belly, elongated fierce dragon snout, long pointed ivory horns swept back/up, several jagged rust dorsal spikes, very broad shoulders, massive clawed forelegs, heavy hindquarters and long thick muscular tail. Small glowing amber eye but no glow outside body. Its head and shoulders must be more imposing than the four other species. No wings, no flames, no humanoid boxer, no separate opponent. Preserve long horns and red armored identity in all36 cells. Windup slowly crouches down, opens mouth slightly and raises one heavy front paw before committing forward. Attack is a fierce front-claw/head forward strike toward LEFT: foreleg winds, torso surges, frame3 strongest extended front paw and open jaw, then weight rocks back and paw lands. Hurt shoulders/head recoil substantially while heavy tail counterbalances. Defeat knees fold, head drops, torso collapses and final2 frames lie fully belly-down, eyes closed, long horns and whole tail visible. Consistent full body roughly195x165 within each256 cell; reduce zoom uniformly if needed to fit horns/tail.
```

