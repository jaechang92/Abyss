#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 테스트용 스테이지 이동 파트. 릴리스 빌드에서는 통째로 컴파일되지 않는다
    /// (<c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>) — 진행을 건너뛰는 API는 본편에 존재할 이유가 없다.
    ///
    /// 만든 이유: 스테이지 3의 방 하나를 확인하려고 <b>31방을 처음부터 도는</b> 검증 비용이
    /// 기능 자체보다 커졌다. <c>다음 룸으로</c> 치트가 있었지만 한 단계씩만 넘어가고,
    /// 갈림길 단계마다 선택 패널이 떠서 스테이지 3까지 스무 번을 눌러야 했다.
    /// </summary>
    public sealed partial class StageDirector
    {
        /// <summary>시퀀스의 스테이지 수(치트 UI가 버튼을 만들 때 쓴다). 시퀀스가 없으면 0.</summary>
        public int DebugStageCount => sequence != null ? sequence.stages.Count : 0;

        /// <summary>현재 스테이지의 단계 수. 스테이지가 없으면 0.</summary>
        public int DebugStepCount => CurrentStage != null ? CurrentStage.steps.Count : 0;

        /// <summary>
        /// 지정 스테이지·단계로 즉시 이동한다.
        ///
        /// 진행 중이던 예약·게이트·적을 모두 정리한 뒤 목표 단계에 진입한다. 정리를 빠뜨리면
        /// 이동 자체는 되는데 그 뒤가 조용히 망가진다 — 아래 세 가지가 그 목록이다.
        /// </summary>
        public void DebugJumpTo(int stageIndex, int stepIndex)
        {
            if (sequence == null || sequence.stages.Count == 0)
            {
                Debug.LogWarning("[StageDirector] 치트 이동 실패 — 시퀀스 미설정");
                return;
            }

            stageIndex = Mathf.Clamp(stageIndex, 0, sequence.stages.Count - 1);
            var targetStage = sequence.stages[stageIndex];
            if (targetStage == null || targetStage.steps.Count == 0)
            {
                Debug.LogWarning($"[StageDirector] 치트 이동 실패 — Stage {stageIndex + 1} 단계 없음");
                return;
            }
            stepIndex = Mathf.Clamp(stepIndex, 0, targetStage.steps.Count - 1);

            // ① 런을 연다. StartNewRun은 진행 중이면 스스로 무시하므로(내부 isRunActive 가드)
            //    조건 없이 부른다 — 덕분에 "사망 후 점프"도 새 런으로 이어진다.
            //    StartSequence가 아니라 이것을 부르는 이유: 저쪽은 1스테이지 1방으로 진입까지 해버린다.
            RunManager.Instance?.StartNewRun();

            // ② 예약된 진행을 취소한다. 남아 있으면 이동 직후에 발화해 단계를 한 번 더 넘긴다
            //    (delayBetweenRooms 2초·delayBetweenStages 3초 창이 열려 있을 수 있다).
            CancelInvoke();

            // ③ 현재 방의 적을 치운다.
            DespawnAllEnemies();

            // ④ 게이트·클리어 상태 해제. 이벤트/상점 방에서 점프하면 게이트가 잡힌 채 남아
            //    목표 방을 클리어해도 진행이 안 된다.
            isRoomClearing = false;
            isRoomGateHeld = false;

            currentStageIndex = stageIndex;
            currentStepIndex = stepIndex;

            Debug.Log($"[StageDirector] 치트 이동 → Stage {stageIndex + 1}/{sequence.stages.Count} " +
                      $"· Step {stepIndex + 1}/{targetStage.steps.Count}");
            EnterStep(stepIndex);
        }

        /// <summary>현재 스테이지 안에서 단계만 옮긴다(경계를 넘지 않는다).</summary>
        public void DebugStepBy(int delta)
        {
            if (CurrentStage == null) return;
            DebugJumpTo(currentStageIndex, currentStepIndex + delta);
        }

        /// <summary>
        /// 씬의 적을 <b>죽이지 않고</b> 제거한다.
        ///
        /// <c>TakeDamage</c>로 죽이면 <c>OnEnemyKilled</c>가 발행돼 룸 클리어가 돌고, 경험치·골드까지
        /// 들어와 이동과 진행이 겹친다. 반대로 그냥 두면 새 방에서 <b>추적되지 않는 적</b>이 되어
        /// Bug-033(살아 있는 적을 두고 방이 넘어감)을 그대로 재현한다.
        ///
        /// <c>activeEnemies</c>가 아니라 씬 전체를 훑는 이유도 같다 — 추적 밖 개체까지 치워야 한다.
        ///
        /// ⚠️ <c>SetActive(false)</c>를 먼저 하는 것이 핵심이다. <c>Destroy</c>는 프레임 끝에 반영되는데,
        /// 바로 이어지는 <c>SpawnEnemies</c>의 <c>WarnUntrackedEnemies</c>가 같은 프레임에 돌아
        /// 아직 살아 있는 이 개체들을 잔재로 오인해 경고를 쏟는다. 비활성화하면
        /// <c>FindObjectsByType</c>(기본값 Exclude)의 눈에서 즉시 사라진다.
        /// </summary>
        private void DespawnAllEnemies()
        {
            var all = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                all[i].gameObject.SetActive(false);
                Destroy(all[i].gameObject);
            }
            activeEnemies.Clear();
        }
    }
}
#endif
