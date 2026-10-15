from pathlib import Path
import json, html, hashlib

root = Path(__file__).resolve().parent
names = {'melee_grunt':'근접 병사','melee_brute':'중장 강적','ranged_archer':'원거리 사수','bone_archer':'뼈 궁수','elite_hunter':'엘리트 사냥꾼','boss_abyss_keeper':'심연의 수호자','void_caster':'공허 술사','elite_summoner':'망령 소환사','elite_berserker':'광폭한 중장','midboss_sentinel':'감시자 거인','boss_flame_serpent':'화염 뱀','flame_mortar':'화염 박격포','midboss_throne_warden':'왕좌의 파수관','boss_thronebound':'왕좌의 영혼'}
notes = {'boss_abyss_keeper':'빈 면의 정체는 유지되나 갑옷/골렘처럼 읽힐 수 있음.','void_caster':'뒤 머리 윤곽이 후드처럼 읽힐 수 있음. 손의 선 발광은 별도 효과와 대조 필요.','elite_summoner':'원래 술사 공유 변형에서 독립 외형 제안으로 제작. 눈 같은 표식 발생 및 외형 승인 필요.','elite_berserker':'이동/공격 첫 원본은 프레임 맞닿음으로 추출 실패해 재생성. 공격 시 자세에 따른 두 투구 가림 확인 필요.','midboss_sentinel':'하반신 없음 유지. 회전 날의 원근 길이 변화 및 실제360도 회전 표현 미확인.','boss_flame_serpent':'갑옷 행렬 유지. 넓은 몸을248셀 사용, 기존124셀 고정 슬라이서와 비호환.','flame_mortar':'기와/그릇 잔해 유지. 공격에서 실제 그릇 분리/투척은 기존 투사체 연결 필요.','midboss_throne_warden':'문 가슴/기둥 팔 유지. 정면에 가까운 몸의 방향성 및 발 접지 확인 필요.','boss_thronebound':'얼굴 겹침 유지. 블링크는 준비/타격 자세만 제작, 소실·재등장 효과는 별도 시스템 담당.','elite_hunter':'팔 길이/얼굴각도 변화와 얼굴 마지막 분리 낙하 미충족.','bone_archer':'공격 후 영구침하/피격 국소파임/사망 시위만 남음 미충족.','ranged_archer':'사망 화살 잔존/시위 풀림 약함.','melee_brute':'사망 두 몸 완전 분리 미충족.','melee_grunt':'이동 무릎 전진 약함, 피격 상체/검 길이 변화 확인 필요.'}
cards=[]; records=[]; frame_count=0; state_count=0
for id,name in names.items():
    runs=[root/id/'run']+sorted((root/id).glob('pattern-*'))
    sections=[]
    for run in runs:
        manifest=json.loads((run/'manifest.json').read_text(encoding='utf-8'))
        extraction=json.loads((run/'frames/frames-manifest.json').read_text(encoding='utf-8'))
        report=json.loads((run/'sprite-sheet-alpha.report.json').read_text(encoding='utf-8'))
        assert extraction['ok'] and report['ok'], run
        for state,entry in manifest['animation']['rows'].items():
            assert entry['frames']==4
            frames=list((run/'frames'/state).glob('frame-*.png'))
            assert len(frames)==4, (run,state,len(frames))
            assert (run/'qa'/f'{state}.gif').exists()
            frame_count+=4; state_count+=1
            rel=(run/'qa'/f'{state}.gif').relative_to(root).as_posix()
            contact=(run/'qa'/f'{state}-contact.png').relative_to(root).as_posix()
            sections.append(f'<figure><figcaption>{html.escape(state)}</figcaption><img class="gif" src="{rel}" loading="lazy"><a href="{contact}">프레임 펼쳐 보기</a></figure>')
        records.append({'enemy':id,'run':run.relative_to(root).as_posix(),'extraction_ok':True,'atlas_ok':True,'motion_status':'user_validation_pending','atlas_sha256':hashlib.sha256((run/'sprite-sheet-alpha.png').read_bytes()).hexdigest()})
    text=f'# {name} 제작 검토 — 2026-10-08\n\n기본4상태 각각4프레임 생성. 원본/투명프레임/atlas/명시적 frame_layout/GIF 보존. 자동 추출/시트 검사 통과. 정적 외형 검토, 실제 연속 재생·Unity/실전 검증 미실행.\n\nmove: experimental. 나머지: best-effort. 단순 포즈 변화와 실제 자연스러운 이동을 동급 통과로 집계하지 않는다.\n\n{notes[id]}\n\n사용자가 전체 목록 계속 제작 승인, 새 외형은 Codex 제안으로 선정했으며 사용자가 각 외형을 선택한 것으로 기록하지 않는다. 기존 v1/v2/v3 승인 기록은 유지.\n\n게임 공격 준비/회복·피격·사망체류 수치 무변경. preview fps는 실제 타격타이밍 계약이 아니다. 기존 southeast 상태별 시트/124셀 슬라이서와 새4행 atlas 차이, 발pivot/크기/투사체 발사 위치를 별도 연결해야 한다. 기존 게임 코드·런타임 참조 무변경.\n'
    (root/id/'run'/'qa-notes.md').write_text(text,encoding='utf-8')
    cards.append(f'<section><h2>{name}</h2><p>{html.escape(notes[id])}</p><div class="rows">'+''.join(sections)+'</div></section>')
assert len(names)==14 and state_count==60 and frame_count==240
page='<!doctype html><html lang="ko"><meta charset="utf-8"><title>Abyss 적 이미지·동작 목록</title><style>body{background:#151b23;color:#e8e4da;font:16px sans-serif;margin:24px}section{background:#252d37;padding:18px;margin:18px 0;border-radius:8px}.rows{display:flex;flex-wrap:wrap;gap:12px}figure{margin:0;padding:12px;background:#303b48;min-width:200px}img.gif{display:block;width:auto;height:248px;image-rendering:pixelated;max-width:496px}a{color:#c4d8ee;display:block}p{line-height:1.6}</style><h1>Abyss 적 이미지·동작 목록</h1><p>14종 · 기본56동작 + 보스추가4동작 · 240프레임. 제작 산출과 추출 검사 통과. 이동 품질 및 게임 적용/Unity 검증대기.</p><p>한 번 재생되는 공격·사망 GIF는 새로고침하면 다시 재생됩니다. 이동은 반복됩니다.</p>'+''.join(cards)+'</html>'
(root/'gallery.html').write_text(page,encoding='utf-8')
(root/'catalog.json').write_text(json.dumps({'date':'2026-10-08','enemies':14,'states':state_count,'frames':frame_count,'runtime_integrated':False,'unity_verified':False,'runs':records},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'enemies':14,'states':state_count,'frames':frame_count,'reports_ok':len(records)},ensure_ascii=False))
