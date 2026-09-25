using System;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// <see cref="MetaSaveService"/>의 로드 상태 부분 — 복구 출처, 저장 보류, 테스트 격리용 메모리 스냅샷.
    /// (본체 MetaSaveService.cs와 같은 partial 클래스라 current·isLoaded를 공유한다.)
    ///
    /// <b>저장 보류 계약</b> — 로드 때 본 파일(또는 복구에 필요한 직전 정상본)을 <b>열지 못하면</b>
    /// (SaveFileStatus.Inaccessible: 잠금·권한, SaveSystem이 정해진 횟수만 재시도한 뒤):
    /// <list type="number">
    /// <item>옛 직전 정상본도, 새 세이브도 "현재값"으로 채택하지 않는다. 메모리는 화면이 깨지지 않을 임시 빈 세이브다.</item>
    /// <item><see cref="IsSaveBlocked"/> = true, <see cref="MetaSaveService.LastLoadResult"/> = Inaccessible.
    /// 이 동안 <see cref="MetaSaveService.Save"/>는 디스크에 쓰지 않고 false — autoSave 호출도 모두 막힌다.</item>
    /// <item>풀리는 길은 <see cref="MetaSaveService.Reload"/> 성공(명시적 재시도)과 ResetAll(사용자의 초기화 선택)뿐이다.
    /// 자동·무제한 재시도는 없다. 보류 중 메모리에 쌓인 진행은 Reload가 디스크 값으로 바꾸면서 버려진다.</item>
    /// <item>손상(Unreadable)은 다르다 — 다시 읽어도 같으므로 기존 정책대로 직전 정상본 복구·새 세이브 시작을 한다.</item>
    /// </list>
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>마지막 로드가 직전 정상본에서 왔는가. 직전 정상본이 구버전·미래 버전이면 LastLoadResult는 그 결과를 보인다.</summary>
        public bool LastLoadUsedBackup { get; private set; }

        /// <summary>세이브를 열지 못해 저장을 보류 중인가. 위 계약 참조.</summary>
        public bool IsSaveBlocked { get; private set; }

        /// <summary>
        /// <see cref="IsSaveBlocked"/>가 바뀌었다(보류 시작 · 재시도 성공 · 초기화). 재시도 성공 때는
        /// <see cref="MetaSaveService.Current"/>도 디스크 값으로 바뀌었으므로 화면은 이때 다시 그린다.
        /// 로드가 끝난 뒤에 발생한다 — 구독자가 Current를 읽어도 안전하다.
        /// 테스트 격리 API(Restore·Unload)는 실제 상태 변화가 아니라서 발생시키지 않는다.
        /// </summary>
        public event Action OnSaveStatusChanged;

        /// <summary>
        /// 보류가 아닌 상태에서 디스크 쓰기가 실패했다. 디스크의 직전 진행은 그대로다(SaveSystem.Save).
        /// 보류 중의 저장 거부는 여기로 오지 않는다 — 매 autoSave마다 같은 알림이 쏟아지기 때문이다.
        /// </summary>
        public event Action OnSaveWriteFailed;

        /// <summary>
        /// 강제 재로드. 옵션 초기화 등 예외 경로에서만 사용. 세이브를 열지 못해 저장이 보류된 상태에서는
        /// 이것이 <b>유일한 재시도 경로</b>다(자동 재시도 없음) — 성공하면 보류가 풀린다.
        /// UI의 재시도는 <see cref="RetrySaveAccess"/>를 쓴다.
        /// </summary>
        public MetaSave Reload()
        {
            isLoaded = false;
            EnsureLoaded();
            return current;
        }

        /// <summary>
        /// 사용자의 명시적 재시도(저장 알림의 [다시 시도]). 성공하면 true — 보류가 풀리고
        /// 보류 중 메모리에 쌓인 진행은 디스크 값으로 바뀌며 버려진다(계약 3번).
        /// 보류가 아니면 아무것도 하지 않고 true다 — 여기서 Reload하면 저장 전 메모리 진행을 디스크 값으로 덮는다.
        /// </summary>
        public bool RetrySaveAccess()
        {
            if (!IsSaveBlocked) return true;
            Reload();
            return !IsSaveBlocked;
        }

        private void SetSaveBlocked(bool isBlocked)
        {
            bool isWasBlocked = IsSaveBlocked;
            IsSaveBlocked = isBlocked;
            NotifySaveStatusIfChanged(isWasBlocked);
        }

        private void NotifySaveStatusIfChanged(bool wasBlocked)
        {
            if (wasBlocked != IsSaveBlocked) OnSaveStatusChanged?.Invoke();
        }

        // ───────────────────────── 테스트 격리 ─────────────────────────
        //
        // PlayModeSaveGuard가 저장 폴더를 임시 폴더로 돌리는 동안 이 서비스의 메모리가 사용자 세이브 값을 들고 있으면
        // 테스트가 그 값을 임시 폴더에 쓰거나, 반대로 테스트 값이 가드 해제 뒤 사용자 폴더에 저장된다.
        // 그래서 가드는 들어올 때 메모리를 잡아 두고 비우며, 나갈 때 그대로 되돌린다. 셋 다 디스크를 건드리지 않는다.

        /// <summary><b>테스트 격리 전용</b> 메모리 상태. 내용은 <see cref="RestoreMemoryState"/>만 읽는다.</summary>
        public sealed class MemorySnapshot
        {
            internal readonly MetaSave savedCurrent;
            internal readonly bool wasLoaded;
            internal readonly MetaSaveLoadResult loadResult;
            internal readonly bool usedBackup;
            internal readonly bool wasSaveBlocked;

            internal MemorySnapshot(MetaSave current, bool isLoaded, MetaSaveLoadResult result, bool isBackupUsed, bool isBlocked)
            {
                savedCurrent = current;
                wasLoaded = isLoaded;
                loadResult = result;
                usedBackup = isBackupUsed;
                wasSaveBlocked = isBlocked;
            }
        }

        /// <summary><b>테스트 격리 전용</b> — 현재 메모리 상태를 그대로 잡아 둔다(참조 보관, 디스크 접근 없음).</summary>
        public MemorySnapshot CaptureMemoryState() =>
            new MemorySnapshot(current, isLoaded, LastLoadResult, LastLoadUsedBackup, IsSaveBlocked);

        /// <summary><b>테스트 격리 전용</b> — 잡아 둔 상태로 되돌린다. 저장하지 않는다.</summary>
        public void RestoreMemoryState(MemorySnapshot snapshot)
        {
            if (snapshot == null) return;
            current = snapshot.savedCurrent;
            isLoaded = snapshot.wasLoaded;
            LastLoadResult = snapshot.loadResult;
            LastLoadUsedBackup = snapshot.usedBackup;
            IsSaveBlocked = snapshot.wasSaveBlocked;
        }

        /// <summary>
        /// <b>테스트 격리 전용</b> — 저장 없이 "아직 로드하지 않음"으로 되돌린다.
        /// 다음 접근은 <b>그때의</b> 저장 폴더에서 다시 읽는다.
        /// </summary>
        public void UnloadWithoutSaving()
        {
            current = null;
            isLoaded = false;
            LastLoadResult = MetaSaveLoadResult.NotLoaded;
            LastLoadUsedBackup = false;
            IsSaveBlocked = false;
        }
    }
}
