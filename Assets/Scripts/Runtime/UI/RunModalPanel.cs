using Abyss.Runtime.Events;
using UnityEngine;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// Run 씬에서 코드로 만들어 띄우는 선택 모달(<see cref="EventRoomPanel"/>·<see cref="ShopRoomPanel"/>·
    /// <see cref="NodeMapPanel"/>)의 공통 뼈대.
    ///
    /// 세 패널이 같은 규약을 한 벌씩 복제하고 있었다:
    /// <list type="bullet">
    /// <item>정적 인스턴스를 처음 열 때 만든다 — 오버레이 캔버스 + 딤 배경(<see cref="UiFactory"/>). 씬 배선·HudBuilder 수정이 없다.</item>
    /// <item>정지는 <see cref="GameEvents.RaiseDraftOpened"/>/<c>Closed</c>로 기존 DraftOpen FSM 상태를 빌린다(전용 정지 로직 없음).</item>
    /// <item>열림 플래그로 발행을 한 번씩만 한다 — 열린 채 다시 열어도 정지가 두 번 걸리지 않는다.</item>
    /// </list>
    /// 이 규약을 한 곳에 둔다. 무엇을 보여 주고 언제 닫는지는 각 패널이 정한다.
    ///
    /// 🔴 <b>닫는 순서는 호출하는 쪽 책임이다.</b> 드래프트를 여는 효과는 <see cref="HideBody"/>(= <c>DraftClosed</c>)
    /// <b>뒤에</b> 적용해야 두 모달이 겹치지 않는다 — 이 클래스는 순서를 강제하지 않는다.
    ///
    /// 📌 <b>도메인 리로드 리셋은 파생 클래스에 남는다.</b> <c>[RuntimeInitializeOnLoadMethod]</c>는
    /// 제네릭 클래스에서 호출되지 않는다. 파생 클래스가 그 특성을 단 메서드에서 <see cref="ResetInstance"/>를 부른다.
    /// </summary>
    /// <typeparam name="T">파생 패널 자신(CRTP). 정적 인스턴스가 패널 종류마다 따로 생긴다.</typeparam>
    public abstract class RunModalPanel<T> : MonoBehaviour where T : RunModalPanel<T>
    {
        private static T instance;

        // 표시/숨김 토글 대상(딤 배경). 패널 내용은 전부 이 아래에 만든다.
        private GameObject body;
        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>
        /// 인스턴스를 확보한다. 씬 전환으로 파괴된 인스턴스는 Unity의 == 오버로드 덕에 null로 판정되어 다시 만들어진다.
        /// Run 씬 전용이므로 DontDestroyOnLoad 하지 않는다.
        /// </summary>
        protected static T EnsureInstance()
        {
            if (instance != null) return instance;

            var go = CreateOverlayCanvas(typeof(T).Name, UiSortingOrder.Modal);
            instance = go.AddComponent<T>();
            instance.body = CreateDimBody(go.transform);
            instance.BuildContent(instance.body.transform);
            instance.body.SetActive(false);
            return instance;
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋. 파생 클래스의 리셋 메서드에서 부른다(클래스 주석 참조).</summary>
        protected static void ResetInstance() => instance = null;

        /// <summary>딤 배경(<paramref name="body"/>) 아래에 패널 내용을 만든다. 인스턴스 생성 시 한 번 불린다.</summary>
        protected abstract void BuildContent(Transform body);

        /// <summary>내용을 채운 <b>뒤에</b> 부른다. 처음 열 때만 전역 정지를 건다.</summary>
        protected void ShowBody()
        {
            body.SetActive(true);

            if (isOpen) return;
            isOpen = true;
            GameEvents.RaiseDraftOpened();   // 기존 DraftOpen 상태로 전역 정지
        }

        /// <summary>숨기고 정지를 푼다. 열려 있지 않았으면 발행하지 않는다.</summary>
        protected void HideBody()
        {
            body.SetActive(false);

            if (!isOpen) return;
            isOpen = false;
            GameEvents.RaiseDraftClosed();
        }
    }
}
