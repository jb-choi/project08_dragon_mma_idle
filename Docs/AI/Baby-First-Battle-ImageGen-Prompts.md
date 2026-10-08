# First-battle continuity assets

Built-in ImageGen, transparent_background=true, two separate calls; no CLI/API fallback. Original outputs preserved; project copies are under Assets/DragonMMA/ArtSource/BasicCombat/. Existing six approved runtime atlases were not regenerated.

## Fighter first guard

Reference: Assets/Resources/DragonMMA/Art/fighter_fight_idle_sheet.png.
Source: Assets/DragonMMA/ArtSource/BasicCombat/fighter-first-guard.png.
Runtime: Assets/Resources/DragonMMA/Art/fighter_first_guard_sheet.png.

Exact prompt:

Use case: identity-preserve. Asset type: production pixel-art sprite sheet for the SAME fighter in the reference, isolated individual actor, transparent background. Reference image is a character/style/size/stance anchor, NOT a target to overwrite. Generate exactly EIGHT sequential full-body frames in a clear 4-column by 2-row grid. Facing RIGHT. A short careful boxing high-guard reaction: frame0 EXACT same neutral guard as reference frame0; frame1 gloves begin rising; frame2 gloves cover temples; frame3 high guard, elbows tight to ribs and chin tucked a little; frame4 hold high guard without being hit; frame5 gloves start lowering; frame6 almost neutral; frame7 same neutral reference pose. Keep planted bare feet, identical stance width and locations, rear foot on the left. No stepping, no bobbing or squashing body. Preserve curly black hair, beard, muscular tan skin, black/gold MMA shorts and gloves, outline/pixel density, character proportions. Every frame same character scale and ground baseline with generous transparent margins; frames do not touch. No punches, no enemy, no text, no numbers, no labels, no grid lines, no ground shadow, no background glow.

## Baby surrender

Reference: Assets/DragonMMA/ArtSource/BasicCombat/dragon-idle-calm.png.
Source: Assets/DragonMMA/ArtSource/BasicCombat/dragon-first-yield.png.
Runtime: Assets/Resources/DragonMMA/DragonSheets/dragon_0_yield.png.

Exact prompt:

Use case: identity-preserve. Asset type: production pixel-art sprite sheet, SAME green forest BABY dragon as reference. Reference is identity/palette/proportion/style anchor, not an image to overwrite. Exactly EIGHT sequential FULL-BODY frames in a separated 4-column by 2-row grid. All face LEFT; tail to the right; no fighter. Gentle losing/surrender motion after beginner jabs, NOT violent knockout. Frame0 EXACT neutral standing pose of reference first dragon; frame1 blink and head lowers a little; frame2 front knees bend; frame3 chest lowers; frame4 forelegs tuck, belly gently rests near ground; frame5 head droops, eyes close; frame6 settled on belly with folded forelegs and calm closed eyes; frame7 same settled belly pose. Crucial preserve same green body, cream muzzle and belly, single ivory forehead horn, leaf-shaped golden green mane and same curled tail. No orange/red dragon, no new wings, no blood, no stars or effects, no back flipping. Same body length, proportion and scale in all frames; rear hindfoot and tail anchor stay fixed. Lower silhouette comes from bending joints, not rescaling. All silhouettes isolated, generous genuine transparent padding, identical floor baseline. Hard-edged pixel art with dark charcoal outlines, readable at small game size. No text, no numbers, labels, grid, shadows or backdrop.

## Unity import registration

Each source retains eight disconnected silhouettes. The existing artist-frame importer applies a uniform scale from frame0, fixed foot registration, point sampling, full-frame canvas and persistent Sprite IDs. Guard boundaries copy the approved FightIdle frame0. Surrender starts with the approved Baby FightIdle frame0. This is asset registration, not procedural character drawing. Opaque pure-black pixels are converted to charcoal for the Windows color-key contract.

Guard clip0.45s; surrender0.90s. Controllers gain one independent state each. The Baby portrait slot in desktop/web UI roots uses the same green boundary sprite, so capture does not switch to the legacy orange silhouette. Other portrait slots and layouts remain unchanged.
