# Stage1 대표방 미술 후보

2026-09-30. 대상 Room3_Crowd. 미술 방향 확인용이며 런타임 채택본이 아니다.

사용자 선택: **첫 번째 후보(v1)로 진행**. 밝은 원경과 중앙 받침 구조를 포함한 v1의 공간감이 채택된 방향이다. v2는 비교 기록으로 보존한다. v1의 발판 위치를 실제 충돌 배치로 간주하지 않고 배경/지면/발판을 분리 제작한다. 중앙 받침은 통과 가능한 배경 장식으로 구별한다.

## 후보 v1

- 파일: room3_workshop_concept_v1.png
- 제작: built-in image_gen, 로비 채택본 Assets/Art/Environment/lobby_refuge/lobby_refuge_v2.png를 스타일 참조로 사용.
- 총괄 시각 검토: 석조 두께·픽셀 면·생활 도구·원경 깊이는 로비와 연결된다. 중앙 받침 기둥과 밝은 원경의 대비는 실제 적/폼 효과와 합성해 확인해야 한다. 생성된 발판은 실제 Room3 좌표에 맞춘 에셋이 아니며 그대로 충돌 그림으로 사용하지 않는다.
- 다음 보완 후보: 중앙 지지 구조의 존재감을 줄이고 원경 대비를 낮춘 구도. 배경과 충돌 지면/발판을 분리해서 제작한다.

## 사용한 생성 프롬프트

### v2 — 가독성 보완 후보

파일: room3_workshop_concept_v2.png. v1을 built-in image_gen으로 편집했다. 중앙 바닥까지 내려오던 기둥을 제거하고, 배경을 어둡고 단순하게 정리했다. 두 후보 모두 스타일/구도 판단용이며 사용자의 미술 선택 대기 상태다. 플랫폼 그림과 실제 충돌 배치의 정합성은 제작 후 사용자 확인이 필요하다.

Use case: precise-object-edit. Edit this Abyss Stage1 side-scrolling pixel-art combat-room concept. Preserve exact full panorama dimensions, stone material and pixel clusters, thick continuous floor and its horizontal top height, three platform top heights and widths, left/right workshop everyday objects, and cool blue-gray palette. Make only these gameplay readability changes: remove the vertical center support pillar beneath the high central platform; support that platform by a slender diagonal stone corbel that visually recedes toward the upper rear wall and never forms a barrier down to the walking lane. Reduce the distant cavern and bridge brightness and contrast by about one third, unify distant shapes into fewer broad blue-gray tonal bands while retaining architectural depth. Keep the central standing-height combat area spacious and visually quiet. Preserve crisp square pixels, avoid soft blur or gradients. Keep platform edges and walk surface cleanly readable. No characters, UI, text, lamps, light beams, green moss, temple symbols. This is a composition candidate, not a validated tileset.

### v1 — 최초 생성

Use case: style-transfer. Transform the supplied Abyss lobby environment into a NEW representative Stage1 combat-room concept, using the reference only as a strict pixel-art craft quality benchmark: crisp clustered square pixels, substantial chiseled stone, stepped contours, rich authored architecture and depth. Asset type: full-width side-on 2D action roguelite environment concept panorama, landscape wide about 3:1, no characters, no UI, no readable text. Scene: an ordinary stone-built workshop passage that was occupied yesterday, with familiar shelves, stools, tool racks and doorframes subtly placed at incompatible heights. Not an ancient temple; no altar, banners, mystical sigils or green moss. Palette cool blue-gray and indigo with muted wood; no lamps, no sunbeams, no directional light source, no high contrast lighting effects. Composition: continuous substantial thick stone ground along the lower quarter, exact flat horizontal walking surface; large simple readable central combat negative space; left a subdued workbench alcove, center large open architectural recess revealing distant staggered stone passageways, right a misaligned household doorframe and stair hint leading onward. Three credible stone platforms: low supported ledge left, high central cantilever ledge, low supported ledge right, all behind the hypothetical player plane. Avoid huge pillars blocking the play lane. The floor foundation has large coherent stone blocks and clean edges rather than tiny tile noise. Distant background lighter and lower contrast, foreground terrain clearly readable. Match the reference richness, material craft and pixel clusters, but replace its warm safe refuge mood with quiet unsettling cold familiar surroundings. No painted smoothing, no gradients, no generic collapsed ruins. This is a concept composition for later asset extraction, not an automatically accepted playable tileset.
