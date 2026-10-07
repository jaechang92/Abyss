using System;
using System.Collections.Generic;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 첫 보스(boss_abyss_keeper) 조우·처치 장면(E1 · 24-skul-gap-design §8). 다른 보스는 기존 카드 경로 그대로다.
    ///
    /// <list type="bullet">
    /// <item><b>조우</b> — Start 에서 카드를 띄우지 않는다. 보스는 대기(<see cref="AbyssKeeperBoss.IsHoldingForEncounter"/>)로 선공하지 않고,
    /// 플레이어가 가까이 오거나 먼저 때리면 소개를 연다. 정지·모달·히트스톱·저장 오버레이·씬 전환 중에는 열지 않고 기다린다.</item>
    /// <item><b>최초(약 3.8초)</b> — BGM 임시 감쇠 → 보스 그림 잔상·먼지 반응 → 하단 이름·별칭 → 첫 대사 →
    /// 카메라 전투 복귀 → 모달 종료 → 대기 해제·체력바. 나머지 대사는 ↑/Y 로 펼쳐 읽는다(삭제하지 않음).</item>
    /// <item><b>재도전(약 0.8초)</b> — 같은 앱 실행 안에서 정상 종료한 기록이 있으면 이름만 짧게. 저장·PlayerPrefs 에 남기지 않는다 —
    /// 앱을 다시 켜면 최초 장면이 다시 나온다.</item>
    /// <item><b>중단</b> — 런 종료·사망·포기·씬 이탈은 정지를 풀지 않고 자기 것(카메라·음악·패널)만 정리한다.
    /// 보스 소멸·방 이동·외부 비활성은 자기 모달 정지만 풀고 전투로 수렴한다. 중단은 프레임마다 확인한다(콜백에서 재진입하지 않는다).</item>
    /// <item><b>처치</b> — 기존 슬로·플래시·사망음·보상은 그대로 두고 잔향(<see cref="KeeperEncounterFx.PlayAftermath"/>)만 더한다.</item>
    /// </list>
    /// </summary>
    public sealed partial class BossPresenter
    {
        // Release = 자기 모달 정지를 1회 풀고 전투로 / Yield = 다른 모달이 정지를 가져감, 닫기 없이 물러나 전투로 수렴
        // KeepPause = 결과·포기·사망·씬 이탈, 닫기 없이 물러나고 체력바도 띄우지 않음
        private enum KeeperAbort { None, Release, Yield, KeepPause }
        private enum StepResult { Elapsed, Confirm, More }

        private const string KEEPER_ENEMY_ID = "boss_abyss_keeper";
        private const float ENCOUNTER_DISTANCE = 7f;
        private const float FIRST_BGM_DUCK = 0.35f;
        private const float RETRY_BGM_DUCK = 0.6f;
        private const float PAN_LIMIT_RATIO = 0.45f;
        private const float NOTICE_SECONDS = 1.4f;
        private const float PAN_IN_SECONDS = 0.6f;
        private const float REACTION_AT_SECONDS = 0.3f;
        private const float TITLE_AT_SECONDS = 0.7f;
        private const float TITLE_FADE_SECONDS = 0.25f;
        private const float FIRST_LINE_SECONDS = 1.9f;
        private const float READ_LINE_SECONDS = 8f;
        private const float HANDOFF_SECONDS = 0.5f;
        private const float SKIP_HANDOFF_SECONDS = 0.2f;
        private const float RETRY_SECONDS = 0.8f;

        // 현재 앱 실행 안에서 정상 종료한 소개. 영속화하지 않는다(E1 범위 — 저장 스키마 변경 금지).
        private static readonly HashSet<string> introducedThisSession = new();

        private AbyssKeeperBoss keeper;
        private string keeperTitle;
        private string keeperEpithet;
        private List<string> keeperLines;
        private bool isKeeperDamaged;
        private bool isKeeperSequenceRunning;
        private KeeperAbort keeperAbort;
        private PlayerCameraFollow keeperCamera;
        private float keeperFocusWeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetKeeperStatics() => introducedThisSession.Clear();

        // ───────────────────────── 진입 · 대기 ─────────────────────────

        /// <summary>첫 보스면 대기를 쥐고 조우를 기다린다. 아니면(또는 쥐지 못하면) false — 기존 카드 경로로 간다.</summary>
        private bool TryBeginKeeperEncounter(BossEnemy spawned, string title, string epithet, List<string> lines)
        {
            if (spawned is not AbyssKeeperBoss candidate) return false;
            if (spawned.Data == null || spawned.Data.enemyId != KEEPER_ENEMY_ID) return false;
            if (!candidate.TryClaimEncounter()) return false;

            keeper = candidate;
            keeperTitle = title;
            keeperEpithet = epithet;
            keeperLines = lines;
            isKeeperDamaged = false;
            return true;
        }

        /// <summary>대기 중 첫 피격 — 다음 프레임에 소개를 연다(피해·경직은 이미 정상 적용됐다).</summary>
        private void NoteKeeperDamaged()
        {
            if (keeper != null && boss == keeper) isKeeperDamaged = true;
        }

        private void TickKeeperEncounter()
        {
            if (keeper == null || isKeeperSequenceRunning) return;
            if (!keeper.IsHoldingForEncounter)
            {
                keeper = null;
                return;
            }
            if (!isKeeperDamaged && !IsPlayerNearKeeper(keeper)) return;
            if (!CanOpenKeeperIntro()) return;

            isKeeperSequenceRunning = true;
            _ = RunKeeperIntroAsync(keeper);
        }

        private static bool IsPlayerNearKeeper(AbyssKeeperBoss target)
        {
            if (target.Target == null || target.Data == null) return false;
            float range = Mathf.Min(ENCOUNTER_DISTANCE, target.Data.detectionRange);
            return Vector2.Distance(target.transform.position, target.Target.position) <= range;
        }

        /// <summary>다른 정지·모달·히트스톱·슬로·저장 오버레이·씬 전환 중에는 열지 않는다 — 남의 정지·입력을 빼앗지 않는다.</summary>
        private static bool CanOpenKeeperIntro()
        {
            if (Time.timeScale < 0.99f) return false;
            if (SaveStatusOverlay.IsCapturingInput) return false;
            if (BossIntroPanel.IsOpen || KeeperIntroPanel.IsOpen) return false;
            return !IsSceneLoading();
        }

        private static bool IsSceneLoading() => SceneFlowController.HasInstance && SceneFlowController.Instance.IsLoading;

        // ───────────────────────── 중단 ─────────────────────────

        /// <summary>
        /// 재생 중이면 중단 사유만 남긴다 — 재생 루프가 다음 프레임에 보고 정리한다(이벤트 콜백 안에서 정리하지 않는다).
        /// 재생 전(대기 중)이면 대기를 풀어 보스를 전투로 돌린다 — 영원히 서 있는 보스를 남기지 않는다.
        /// </summary>
        private void CancelKeeperEncounter(KeeperAbort mode)
        {
            if (isKeeperSequenceRunning)
            {
                // 정지를 지키는 중단(런 종료·사망·씬 이탈)이 정지를 푸는 중단보다 우선한다.
                if (keeperAbort == KeeperAbort.None || mode == KeeperAbort.KeepPause) keeperAbort = mode;
                return;
            }

            if (keeper != null && !keeper.IsDead) keeper.ReleaseEncounterHold();
            keeper = null;
        }

        private void OnDisable()
        {
            bool isLeavingScene = !gameObject.scene.isLoaded || IsSceneLoading();
            CancelKeeperEncounter(isLeavingScene ? KeeperAbort.KeepPause : KeeperAbort.Release);
        }

        private void AbortKeeperKeepPause() => CancelKeeperEncounter(KeeperAbort.KeepPause);

        private void AbortKeeperForRoom(RoomData room) => CancelKeeperEncounter(KeeperAbort.Release);

        private void SubscribeKeeperAbort()
        {
            GameEvents.OnRunEnded += AbortKeeperKeepPause;
            GameEvents.OnPlayerDead += AbortKeeperKeepPause;
            GameEvents.OnRunAbandoned += AbortKeeperKeepPause;
            GameEvents.OnRoomEntered += AbortKeeperForRoom;
        }

        private void UnsubscribeKeeperAbort()
        {
            GameEvents.OnRunEnded -= AbortKeeperKeepPause;
            GameEvents.OnPlayerDead -= AbortKeeperKeepPause;
            GameEvents.OnRunAbandoned -= AbortKeeperKeepPause;
            GameEvents.OnRoomEntered -= AbortKeeperForRoom;
        }

        // ───────────────────────── 장면 ─────────────────────────

        private async Awaitable RunKeeperIntroAsync(AbyssKeeperBoss target)
        {
            bool isFirst = !introducedThisSession.Contains(KEEPER_ENEMY_ID);
            var destroyToken = destroyCancellationToken;
            keeperAbort = KeeperAbort.None;
            SubscribeKeeperAbort();

            KeeperIntroPanel.Lease lease = null;
            bool isCompleted = false;
            try
            {
                lease = KeeperIntroPanel.Open(keeperTitle, keeperEpithet);
                if (AudioManager.HasInstance) AudioManager.Instance.SetBgmDuck(this, isFirst ? FIRST_BGM_DUCK : RETRY_BGM_DUCK);

                // 열자마자 정지를 쥐지 못했으면(공용 열림 기록 잔존 등) 남의 정지다 — 첫 프레임 판정에서 양보한다.
                if (isFirst)
                {
                    BeginKeeperFocus(target);
                    await PlayFirstKeeperIntroAsync(lease, target);
                }
                else
                {
                    await PlayRetryKeeperIntroAsync(lease, target);
                }
                isCompleted = true;
            }
            catch (OperationCanceledException)
            {
                // 중단 사유는 keeperAbort. 사유 없이 취소됐다면 이 연출 자체가 파괴된 것(씬 이탈)이다.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (keeperAbort == KeeperAbort.None) keeperAbort = KeeperAbort.Release;
            }
            finally
            {
                UnsubscribeKeeperAbort();
                // 연출 자체가 파괴됐으면(씬 이탈) 사유와 무관하게 정지를 풀지 않는다.
                var mode = isCompleted ? KeeperAbort.None
                    : destroyToken.IsCancellationRequested || keeperAbort == KeeperAbort.None ? KeeperAbort.KeepPause
                    : keeperAbort;
                EndKeeperPresentation(lease, mode);
                keeperAbort = mode;
                isKeeperSequenceRunning = false;
            }

            if (isCompleted) introducedThisSession.Add(KEEPER_ENEMY_ID);
            if (keeper == target) keeper = null;

            // 끝났든 중단됐든 살아 있는 보스는 전투로 수렴한다 — 대기 해제는 모달이 닫힌 뒤 한 번.
            if (target != null && !target.IsDead) target.ReleaseEncounterHold();
            if (keeperAbort != KeeperAbort.KeepPause) ShowBar();
            keeperAbort = KeeperAbort.None;
        }

        private async Awaitable PlayFirstKeeperIntroAsync(KeeperIntroPanel.Lease lease, AbyssKeeperBoss target)
        {
            var panel = lease.Panel;
            bool hasReacted = false;
            var notice = await WaitKeeperStepAsync(lease, target, NOTICE_SECONDS, false, elapsed =>
            {
                SetKeeperFocusWeight(Mathf.SmoothStep(0f, 1f, elapsed / PAN_IN_SECONDS));
                if (!hasReacted && elapsed >= REACTION_AT_SECONDS)
                {
                    hasReacted = true;
                    PlayKeeperReaction(target);
                }
                panel.SetTitleAlpha((elapsed - TITLE_AT_SECONDS) / TITLE_FADE_SECONDS);
            });

            bool isSkipped = notice == StepResult.Confirm;
            if (!isSkipped)
            {
                panel.SetTitleAlpha(1f);
                await PlayKeeperLinesAsync(lease, target, false);
            }

            await HandOffKeeperAsync(lease, target, isSkipped ? SKIP_HANDOFF_SECONDS : HANDOFF_SECONDS);
        }

        private async Awaitable PlayRetryKeeperIntroAsync(KeeperIntroPanel.Lease lease, AbyssKeeperBoss target)
        {
            var panel = lease.Panel;
            panel.SetTitleAlpha(1f);
            panel.SetLine(string.Empty, HasKeeperLines() ? MoreHint(0) : string.Empty, false);
            PlayKeeperReaction(target);

            var result = await WaitKeeperStepAsync(lease, target, RETRY_SECONDS, HasKeeperLines(), null);
            if (result == StepResult.More) await PlayKeeperLinesAsync(lease, target, true);
        }

        /// <summary>
        /// 대사. 기본 흐름은 첫 줄만 보이고 전투로 간다(확인 또는 시간 경과). ↑/Y 를 누르면 읽기로 바뀌어
        /// 남은 줄을 기존 카드와 같은 의미(확인 = 다음, 줄당 상한 대기)로 넘긴다.
        /// </summary>
        private async Awaitable PlayKeeperLinesAsync(KeeperIntroPanel.Lease lease, AbyssKeeperBoss target, bool isReading)
        {
            if (!HasKeeperLines()) return;

            var panel = lease.Panel;
            int index = 0;
            while (index < keeperLines.Count)
            {
                bool hasMore = index + 1 < keeperLines.Count;
                string pager = hasMore && !isReading ? MoreHint(index + 1) : isReading ? $"{index + 1}/{keeperLines.Count}" : string.Empty;
                panel.SetLine(keeperLines[index], pager, isReading);

                var result = await WaitKeeperStepAsync(lease, target, isReading ? READ_LINE_SECONDS : FIRST_LINE_SECONDS, hasMore && !isReading, null);
                if (!isReading && result != StepResult.More) return;

                isReading = true;
                index++;
            }
        }

        /// <summary>전투 인계 — 카메라를 전투 추적으로 되돌린다. 확인으로 끊어도 마지막에 0으로 맞춘다.</summary>
        private async Awaitable HandOffKeeperAsync(KeeperIntroPanel.Lease lease, AbyssKeeperBoss target, float seconds)
        {
            float startWeight = keeperFocusWeight;
            lease.Panel.SetLine(string.Empty, string.Empty, false);
            await WaitKeeperStepAsync(lease, target, seconds, false, elapsed =>
                SetKeeperFocusWeight(startWeight * (1f - Mathf.SmoothStep(0f, 1f, elapsed / seconds))));
            SetKeeperFocusWeight(0f);
        }

        /// <summary>
        /// 실시간 <paramref name="seconds"/> 또는 입력까지 기다린다. 매 프레임 <b>중단·양보 판정을 입력보다 먼저</b> 본다 —
        /// 다른 모달·결과·씬이 가져간 프레임에는 확인 입력을 읽지 않는다. 각 호출은 자기 Awaitable 을 한 번만 await 한다.
        /// </summary>
        private async Awaitable<StepResult> WaitKeeperStepAsync(KeeperIntroPanel.Lease lease, AbyssKeeperBoss target, float seconds,
            bool allowMore, Action<float> tick)
        {
            float elapsed = 0f;
            while (true)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                ThrowIfKeeperAborted(lease, target);
                // 최상위 저장 모달이 화면과 입력을 차지한 동안 소개 시간을 소비하지 않는다.
                if (SaveStatusOverlay.IsCapturingInput) continue;

                elapsed += Time.unscaledDeltaTime;
                tick?.Invoke(elapsed);

                var panel = lease.Panel;
                if (panel.WasConfirmPressed()) return StepResult.Confirm;
                if (allowMore && panel.WasMorePressed()) return StepResult.More;
                if (elapsed >= seconds) return StepResult.Elapsed;
            }
        }

        /// <summary>
        /// 중단 판정 우선순위: 이벤트로 기록된 사유(결과·사망·포기 = KeepPause) → 씬 로딩(KeepPause) →
        /// 다른 모달이 정지를 가져감·처음부터 정지를 못 쥠(Yield) → 패널이 밖에서 꺼짐/파괴/body 비활성(Release, 자기 모달 1회 정리) →
        /// 보스 소멸·사망(Release).
        /// </summary>
        private void ThrowIfKeeperAborted(KeeperIntroPanel.Lease lease, AbyssKeeperBoss target)
        {
            if (keeperAbort == KeeperAbort.None)
            {
                if (IsSceneLoading()) keeperAbort = KeeperAbort.KeepPause;
                else if (!lease.OwnsPause || lease.IsTakenOver) keeperAbort = KeeperAbort.Yield;
                else if (!lease.IsUsable) keeperAbort = KeeperAbort.Release;
                else if (target == null || target.IsDead) keeperAbort = KeeperAbort.Release;
            }
            if (keeperAbort != KeeperAbort.None) throw new OperationCanceledException();
        }

        /// <summary>
        /// 자기 몫(카메라·음악·패널)만 정리한다. 정상 종료·Release 만 자기 모달을 닫는다(<see cref="KeeperIntroPanel.Lease.Close"/> —
        /// 남이 가져갔으면 닫기 이벤트 없음). Yield·KeepPause 는 정지를 풀지 않는다.
        /// </summary>
        private void EndKeeperPresentation(KeeperIntroPanel.Lease lease, KeeperAbort mode)
        {
            if (keeperCamera != null) keeperCamera.EndPresentationFocus(this);
            keeperCamera = null;
            keeperFocusWeight = 0f;

            if (AudioManager.HasInstance) AudioManager.Instance.ReleaseBgmDuck(this);

            if (lease == null) return;
            if (mode == KeeperAbort.None || mode == KeeperAbort.Release) lease.Close();
            else lease.Abandon();
        }

        // ───────────────────────── 카메라 · 효과 ─────────────────────────

        /// <summary>보스와 플레이어의 가운데로 가로 이동만. 화면 반폭의 일부로 묶어 둘 다 화면 안에 남긴다. 카메라가 없으면 생략.</summary>
        private void BeginKeeperFocus(AbyssKeeperBoss target)
        {
            keeperFocusWeight = 0f;
            var follow = FindAnyObjectByType<PlayerCameraFollow>();
            if (follow == null || target.Target == null) return;

            float focusX = (target.transform.position.x + target.Target.position.x) * 0.5f;
            float maxPan = follow.ViewHalfWidth * PAN_LIMIT_RATIO;
            if (follow.TryBeginPresentationFocus(this, focusX, maxPan)) keeperCamera = follow;
        }

        private void SetKeeperFocusWeight(float weight)
        {
            keeperFocusWeight = Mathf.Clamp01(weight);
            if (keeperCamera != null) keeperCamera.SetPresentationWeight(this, keeperFocusWeight);
        }

        private static void PlayKeeperReaction(AbyssKeeperBoss target)
        {
            if (target == null) return;
            target.PlayEncounterCue();
            KeeperEncounterFx.PlayReaction(target.BodyRenderer);
        }

        /// <summary>첫 보스 실제 사망에서만 — 보스가 파괴되기 전에 그림을 복제해 둔다. 슬로·사망음·보상은 기존 소유자 몫.</summary>
        private void PlayKeeperAftermath()
        {
            if (boss is not AbyssKeeperBoss dying || dying.Data == null || dying.Data.enemyId != KEEPER_ENEMY_ID) return;
            KeeperEncounterFx.PlayAftermath(dying.BodyRenderer);
        }

        private bool HasKeeperLines() => keeperLines != null && keeperLines.Count > 0;

        // 「더 보기」 안내 — 문구 키가 없어 기호와 쪽수만(임시 표기).
        private string MoreHint(int shown) => shown > 0 ? $"↑ / Y   {shown}/{keeperLines.Count}" : "↑ / Y";
    }
}
