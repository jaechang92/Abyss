# elite_hunter — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/elite_hunter.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   f42907de-1c54-43d5-83b4-2341b4346ecc   (Elite Hunter R4 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          🔴 120x120 · south-east 한 방향 + flipX   ← 앞 6종(92)과 다르다
mode           v3 · 8프레임 2 gen · 4프레임 1 gen (120 캔버스 실측 · 92 캔버스와 같은 값)
후처리          ❌ **없다** — 몸이 이미 무채 재 검정(R4 평균채도 0.05)이고, 같은 방의 적(S1 근접 병사 건메탈 · S3 원거리 사수 흙빛)과 안 겹친다
정체성 어휘      a quiet grey ash figure with thin spindly limbs and an oversized round head, one small pale paper panel
                with a simple drawn face of two dots and a line resting crooked on the front of that head,
                all one single connected body
```

🔴 **이 적만 캔버스가 120 이다.** `_ROSTER §4` 의 92 규약에 대한 **정예 예외**이고,
근거는 몸 R1~R4 실측이다 — **크기는 문구가 아니라 `size` 가 정하고, 계단이 112 와 120 사이에 있다**
(92→64px · 112→62px · **120→84px**). 정예 80px 은 **120 아니면 안 나온다**(`elite_hunter.md` R3 절).

🔑 **「얼굴판」을 매 문구에 다시 쓴다.** 이 적의 정체 표지는 **하나**이고(빌린 얼굴), 그것이
**비뚤게 얹혀 있다**는 것이 어긋남 전부를 맡는다. 몸 R1·R2 에서 **어깨 어긋남을 배수로도 랜드마크로도
두 번 실패**했고, 포기하고 얼굴판으로 옮기자 R4 에서 어깨까지 따라왔다 — 애니메이션에서 판이
평평·온전하게 유지되지 않으면 **그 자리가 통째로 사라진다.**

🔴 **v3 는 사람으로 회귀한다**(중장 강적의 「일어서서 걷는다」 · 공허 술사의 「걷는 다리」).
이 적은 **사람이 아니라 사람이 되다 만 것**이라 회귀 방향이 특히 가깝다 — `thin spindly limbs` ·
`grey ash` 를 매번 앞에 다시 쓴다.

🔴 **무기를 그리게 하지 않는다.** `isRanged = 0` 이고 발사체가 없다. 공격에 `empty-handed with no
weapon of any kind` 를 **긍정형으로** 넣었다(공허 술사 공격 v2 에서 얻은 규칙 — 「없다」는
「무엇이 비어 있다」로 쓴다).

🔴 **얼굴판을 「벗겨지는 것」으로 그리지 않는다.** 사망 외에는 늘 붙어 있어야 한다 —
프레임마다 붙는 자리가 바뀌면 원거리 사수 v2 의 「땅에 박힌 화살」과 같은 깜빡임이 난다.
사망에서만 **마지막에 더미 위에 내려앉는다**(떨어져 나가는 것이 아니라 **얹히는 것**).

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | moving forward quickly with the upper body leading and the thin legs dragging along behind it, the pale face panel staying flat and level and facing forward the whole time, everything staying one connected body | `8a59fc6c-d5de-4a14-a193-8999ae589b09` | end=R4 | ✅ 채택 |
| 공격 | 9 (1+8) | `attack_southeast_v1` | reaching one long thin arm straight forward to take hold of something in front of it, empty-handed with no weapon of any kind, the pale face panel staying fixed on what is in front the whole time, then drawing the arm back down to its side | `2223ec94-138a-42cd-beb2-ddb2510f775e` | end=R4 | ✅ 채택 · **f4 에서 팔 최대**(폭 49→61px) = 피해 시점 |
| 피격 | 5 (1+4) | `hit_southeast_v1` | the thin body folding and slumping over to one side as the packed ash loosens, the pale face panel staying flat and unchanged and still resting on the head the whole time, then settling back upright into the same standing shape | `5fd49ccc-b0fc-4a05-9f17-93aec7df3fa1` | end=R4 | ✅ 채택 · ⚠️ 진폭 작음(⑤-ⓗ) |
| 사망 | 9 (1+8) | `dead_southeast_v1` | the thin body sinking and collapsing straight down into a low flat heap of loose grey ash, the pale face panel staying whole and unbroken and coming to rest flat on top of the heap last of all | `c5be99d5-4071-4cd6-97a7-c462c0cc95f4` | 없음 | ✅ 채택 · 🔑 **기획대로 나왔다** — 몸이 먼저 무너지고 얼굴판이 마지막에 더미 위에 얹힌다(키 84→48px) |

📌 **보간(`end_frame_url` = R4 의 south-east 회전 이미지)** 은 원거리 사수 v2 에서 얻은 수다 —
`end_frame` 을 시작과 같게 주면 **자연스러운 동작으로 회귀하는 것을 구조적으로 붙잡는다.**
사망만 뺀다(돌아올 자세가 없다).

📌 **이동에 「관절이 한 박자 늦음」을 안 넣었다** — `elite_hunter.md` ⑤-ⓔ.
9프레임에 「끌려오는 다리」와 「늦는 관절」 둘을 요구하면 공허 술사의 「세 번」처럼 뭉개진다.
지금은 **`the upper body leading and the thin legs dragging behind`** 하나만 쓰고,
플레이에서 「그냥 빠른 적」으로 읽히면 그때 더한다(2 gen).
