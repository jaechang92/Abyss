# 폼 자산 대장 (2026-09-10 정리)

> PixelLab 계정에 실제로 있는 것. **여기가 「무엇이 있나」의 SoT다.**
> 폼별 배정·처방은 각 주제 파일(`dark_blade.md` 등)이 갖는다.

---

## 기준 세트 — 92x92

🔑 **그림 세로는 60px 이고 캔버스의 나머지는 여백이다.** 한때 64x64 세트가 병행됐는데
**그림 크기가 똑같아서** 캔버스만 작을 뿐 이득이 없었다. 92px 쪽에 state·애니메이션이
더 갖춰져 있어 그쪽으로 확정했다(삭제분은 `_legacy/gen4-pixellab-characters/`).

| 폼 | id | 방향 | state / 애니메이션 | 상태 |
|---|---|---|---|---|
| **dark_blade** (붉음) | `bc6ce6d5-79a7-42d4-a34f-944e9b856720` | 8 | **그룹 `566c2942`** — Idle · Walk · Vertical Slash · Horizontal Slash / idle 9프레임(east) | ✅ **앵커** |
| **void_archer** (보라) | `93262325-a193-474a-b042-3a3384e5ab35` | 8 | anim 1 | ✅ 확정 |
| **ancient_shield** (금색) | `d20e6a49-f283-4b6d-b180-abc102b15cb2` | **4** | anim 1 | 🔴 **무기가 검이다** |
| **void_thrower** | — | — | — | 🔴 **없다** |

### east 실측 (2026-09-10 · `measure_silhouette.py`)

```
            채움      vs 검    vs 활    vs 방패
활 (보라)   38.1%     0.51      —       0.37   <- 역대 최저, 가장 잘 갈린다
검 (붉음)   43.9%      —       0.51     0.58
방패 (금색) 49.7%     0.58     0.37      —
```

✅ 채움이 **「밀도 = 무게」 순서**(hp 0.9 → 1.2 → 1.5)와 맞는다.
🔴 검 vs 방패 0.58 은 **둘 다 검을 들었기 때문**이다 — 방패 폼을 고치면 내려갈 값이다.

---

## 남은 작업

| | 무엇을 | 어떻게 | 비용 |
|---|---|---|---|
| 1 | **방패 폼의 무기를 방패로** | 앵커에서 `create_character_state` — 「검 대신 큰 탑실드」 | 20~40 gen |
| 2 | **투척사 신규** | 앵커에서 state — 「작은 투척 단검 · 어깨 좁고 밑단 넓은 망토」 | 20~40 gen |
| 3 | 상태 9종 애니메이션 | `animate_character` · **east 1방향만**(flip) | ~1 gen/개 |
| 4 | Unity 임포트 | 알파 크롭 + 발밑 피벗 + PPU 32 | — |

⚠️ **state 파생은 앵커의 붉은색을 물려받는다.** 금색·보라를 유지하려면 편집 문구에 색을
넣어야 한다(`use_color_palette_from_reference` 는 끈다). 색 규약은 `04-palette §4-B`.

⚠️ **방패 폼이 4방향뿐이다.** 지금은 `east` 만 쓰므로 당장 문제는 아니지만,
state 로 다시 만들면 앵커를 따라 8방향이 되어 규격도 같이 맞는다.

---

## 웹에서 쓴 프롬프트 (계정에 남아 있는 것)

```
1boy, solo, hooded knight, faceless, face in shadow,
<색> plate armor, spiked pauldrons, tattered tabard, glowing orange trim,
single accent color only, full body, side view, facing right, eye level,
flat even lighting, transparent background, no ground, no shadow, {{무기}}
```

🔑 **이 문구가 내가 쓴 것보다 나았던 이유는 넷이다** — 「얼굴 안 보임」을 후드 그림자라는
**형태**로, 방향을 문구로 못박음, 조명을 도구 어휘로, 그리고 **자세**.

⚠️ **`{{무기}}` 중괄호 강조는 안 먹었다** — 금색 폼에 방패가 아니라 검이 들려 있다.

⚠️ `tattered tabard`(해진 겉옷)가 넷의 공통 밑단을 만든다.
`08-silhouette §4` 가 **투척사만 밑단을 넓히고 셋은 줄이라**고 정했으므로 여기가 갈릴 자리다.

