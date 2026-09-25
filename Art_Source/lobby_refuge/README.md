# Lobby refuge revision — 2026-09-24

Requested change: improve the lobby background and floor craft quality, with the dense pixel-art environment treatment of Skul as a quality target.

Generated with the built-in image_gen tool. `lobby_refuge_v1.png` is the first, more painterly candidate. The selected pixel-art revision is `Assets/Art/Environment/lobby_refuge/lobby_refuge_v2.png` (2172 × 724). No Stage1 image was replaced.

The lobby uses one authored panorama including its ground foundation. It is not a modular tile set or independently moving parallax layers. NPCs, interactions, collision geometry and UI retain their existing wiring. The scene builder prefers this image and falls back to the former Stage1 art if it is missing.

World width: 44 units. The walking surface is measured at source row 431 from the top and aligned to world Y = -3. Point sampling, no compression or mipmaps. The camera follows within the illustration bounds. Unity review images are generated at 1920 × 1080 in `Art_Source/lobby_refuge/review` by `Abyss.EditorTools.LobbySceneBuilder.ApplyRefugeBatch`. These are editor camera renders with the starting form sampled for scale review, not a gameplay recording. Existing placeholder NPCs remain visible.

## Initial generation brief

Original wide side-on stone refuge at the abyss entrance, richly authored pixel-art action roguelite environment. Cool blue-grey and indigo stone, small warm amber lantern pools, book alcove and patched canopy at the left, survivor tally marks, shelves and stool, spacious central arches into a distant cavern, a workshop and empty equipment racks, a carved doorway and low altar on the right. Open walk lane, continuous horizontal platform, substantial irregular stone foundation, no characters, UI or text. The requested floor height was not followed by generation; integration uses the actual measured surface instead.

## Final edit prompt

Use case: style-transfer. Edit this original game lobby environment to actual crisp low-resolution PIXEL ART. Preserve the exact wide composition, building and prop positions, flat walking surface height, palette, lighting and layout. The current image is too painterly and high resolution. Redraw using a consistent visible square pixel grid, as if a native 1086x362 pixel scene displayed at exactly 2x nearest-neighbor scale. Each small pixel cluster should describe a deliberate shape. Broad 3-tone stone planes with stepped contours, distinct chiseled rim highlights, coherent large stones and hand-placed cracks, cloth with clean angular fold clusters, no smooth microtexture, no antialiasing, no soft paint strokes. Reduce visual noise and tiny flecks. Keep the rich detailed side-scrolling action roguelite environment quality of Skul, original architecture, no characters or UI. Strengthen modest separation of cool distant cavern from midground architecture and amber-lit alcoves, while leaving the walk lane open. Keep original full panorama and floor height. Output the finished full-bleed game environment.
