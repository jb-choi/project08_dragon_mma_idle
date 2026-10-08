# Mat button asset

Generated with the built-in image-generation tool. The uploaded image is a style reference; its English placeholder is not used as game text. Output transparency is preserved. Runtime labels stay editable/localizable in uGUI Text.

Runtime image: `Assets/DragonMMA/UIArt/mat_button_plate.png`

## Prompt

Use case: ui-mockup. Asset type: production Unity uGUI 9-slice button background, not a screenshot.
Input Image 1 is a STYLE REFERENCE only: worn black combat mat button with reinforced rubber rim and distressed beige athletic tape on all four corners.
Generate ONE new reusable EMPTY button plate inspired very closely by this reference. Keep the charcoal pebble-grain rubber mat, double raised stitched black rubber perimeter, subtle rubbed beige scuffs, and four diagonal dirty beige cloth tape corners. Remove all typography completely: NO BUTTON word, NO letters, NO symbols, NO logos. The middle 60% should be clean quiet dark textured mat for legible Korean UI labels added later.
Perfectly front-facing orthographic 2D rectangle, horizontal parallel top/bottom and vertical sides, symmetrical rounded corners, width-to-height ratio 3.3:1. It is a thin padded physical mat plate, not a huge 3D slab. Soft very subtle highlight on the raised rim, minimal shadow strictly close to the edge. No perspective or tilt. Make the straight middle edges and texture work well for 9-slice horizontal resizing. Corner tapes confined to the outermost 16% of width so they won't intrude on a centered short label. All darkest visible opaque rubber should be charcoal around RGB18,18,18 or brighter; no pure opaque black because game has a black transparency key.
Output a genuinely TRANSPARENT RGBA background outside the isolated button, not checkerboard or a dark studio scene. One centered plate, entire silhouette visible, tiny even transparent margin (3%); fill the wide canvas. No extra objects or UI. Suggested canvas 1536x512. Preserve believable worn tactile material, polished game UI asset.

## Selected correction pass

The first generated image is retained as `plate_generation_v1.png`. It had 11 fully opaque RGB0 pixels, incompatible with the native overlay black color key. The built-in image-generation tool made the following shading correction; no manual pixel repaint/background removal was used.

Use case: precise-object-edit. Image 1 is the edit target, a transparent Unity game UI mat button plate. Make ONE targeted shading correction only: raise the darkest opaque materials to clearly visible charcoal gray, minimum approximately RGB40,40,40. No pure black opaque pixels, no pitch-black flecks, scratches or stitched pits anywhere, including dirty marks on tape. This is for a Windows black color-key UI and pure RGB0,0,0 must not occur in any opaque part. The worn rubber should still look dark charcoal, not pale or glossy. Preserve the exact plate silhouette, dimensions, orthographic viewpoint, four beige taped corners, pebble-grain texture, edge reinforcement and empty central label area. Preserve the genuinely transparent exterior. Do not add any text, symbols, scene, backdrop or extra objects. Keep color and wear of the beige cloth tape otherwise unchanged.

Selected output: 2172×724 RGBA. Source pixel check: 390,481 alpha-zero pixels; zero fully opaque RGB0 pixels. Its darkest opaque material is not guaranteed to reach the requested RGB40; the measured nonzero minimum is 10. The production sprite is uncompressed, non-readable, no mipmaps, FullRect mesh, Bilinear sampling; border L/R360 and T/B140 source pixels. Unity references one shared sprite from both existing overlay prefabs. Original reference and generation intermediates are outside runtime Resources and are not referenced by the player UI.
