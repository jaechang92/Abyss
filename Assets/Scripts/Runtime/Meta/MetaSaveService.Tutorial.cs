namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// <see cref="MetaSaveService"/>의 첫 플레이 튜토리얼 부분 — 완료·생략 기록 조회·갱신.
    /// (본체 MetaSaveService.cs와 같은 partial 클래스라 EnsureLoaded·current·Save를 공유한다.)
    ///
    /// 저장은 본체 <see cref="MetaSaveService.Save"/> 한 경로만 탄다 — 보류·쓰기 실패 알림도 거기서 난다.
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>학습 6행동을 모두 마쳤는가.</summary>
        public bool HasCompletedFirstPlayTutorial
        {
            get
            {
                EnsureLoaded();
                return current.hasCompletedFirstPlayTutorial;
            }
        }

        /// <summary>학습을 건너뛰었는가.</summary>
        public bool HasSkippedFirstPlayTutorial
        {
            get
            {
                EnsureLoaded();
                return current.hasSkippedFirstPlayTutorial;
            }
        }

        /// <summary>이번 런에 학습 안내를 띄워야 하는가 — 완료도 생략도 아닐 때만.</summary>
        public bool ShouldShowFirstPlayTutorial => !HasCompletedFirstPlayTutorial && !HasSkippedFirstPlayTutorial;

        /// <summary>학습 완료 기록. 이미 완료 상태면 저장을 건너뛴다(<see cref="MarkPrologueSeen"/>과 같은 형태).</summary>
        public void MarkFirstPlayTutorialCompleted(bool autoSave = true)
        {
            EnsureLoaded();
            if (current.hasCompletedFirstPlayTutorial) return;
            current.hasCompletedFirstPlayTutorial = true;
            if (autoSave) Save();
        }

        /// <summary>학습 생략 기록. 완료 플래그는 건드리지 않는다 — 둘은 다른 사실이다.</summary>
        public void MarkFirstPlayTutorialSkipped(bool autoSave = true)
        {
            EnsureLoaded();
            if (current.hasSkippedFirstPlayTutorial) return;
            current.hasSkippedFirstPlayTutorial = true;
            if (autoSave) Save();
        }

        /// <summary>
        /// 다시 배우게 되돌린다. <b>치트 메뉴 전용</b> — 두 학습 플래그만 지우고 다른 진행·프롤로그는 그대로 둔다.
        /// </summary>
        public void ResetFirstPlayTutorial(bool autoSave = true)
        {
            EnsureLoaded();
            if (!current.hasCompletedFirstPlayTutorial && !current.hasSkippedFirstPlayTutorial) return;
            current.hasCompletedFirstPlayTutorial = false;
            current.hasSkippedFirstPlayTutorial = false;
            if (autoSave) Save();
        }
    }
}
