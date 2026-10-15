using System;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="EventRoomPanel"/>의 월드 소유 확인(R2 attempt2) — 연 쪽이 넘긴 판정이 거짓이 되면(방 이동·런 종료·치트 이동)
    /// 선택 효과·드래프트·확인 콜백을 하나도 내지 않고 닫는다. <see cref="ShopRoomPanel"/>.WorldInteraction과 같은 규약이다.
    ///
    /// 판정은 선택(<see cref="OnChoiceClicked"/>)·[확인](<see cref="OnContinueClicked"/>) 효과 앞과 매 프레임 <see cref="Update"/>에서 한다 —
    /// 마우스 클릭·키보드/패드 Submit·ESC/패드 B가 모두 이 두 메서드를 지나므로 입력 경로마다 따로 막지 않는다.
    /// Update 판정은 상위 모달(저장·설정·도감)이 입력을 쥔 동안에도 돈다 — 낡은 패널이 그 뒤에 남아 있지 않게.
    ///
    /// 🔴 무효화 전에 정당하게 적용된 선택 효과는 되돌리지 않는다. 막는 것은 그 뒤의 효과·대기 드래프트·확인 알림뿐이다.
    /// 판정이 없는 열림(legacy <see cref="Open"/>, R1 월드 이벤트)은 항상 유효로 본다.
    /// </summary>
    public sealed partial class EventRoomPanel
    {
        // 연 쪽의 소유 판정. null이면 확인하지 않는다. 열 때마다 덮어쓴다(Show가 비우고 월드 열기가 새로 건다).
        private Func<bool> isWorldOwnerValid;

        /// <summary>연 쪽이 아직 이 열림의 주인인가. 판정 없는 열림은 항상 true.</summary>
        private bool IsWorldOwnerValid() => isWorldOwnerValid == null || isWorldOwnerValid();

        /// <summary>
        /// 열린 채 소유가 낡았으면 닫는다. 닫았으면 true — 호출자는 효과·지급·알림을 이어 가지 않는다.
        /// 현재 이벤트·선택·대기 드래프트·월드 콜백·그림 훅·판정을 비우고, 패널이 쥔 포커스만 내준 뒤 본체를 한 번 숨긴다.
        /// EventResolved·GrantDrafts·확인 콜백은 내지 않는다.
        /// </summary>
        private bool CloseIfWorldOwnerStale()
        {
            if (current == null || IsWorldOwnerValid()) return false;

            string eventId = current.eventId;
            current = null;
            chosen = null;
            pendingDrafts = 0;
            worldConfirmed = null;
            worldArtResolved = null;
            isWorldOwnerValid = null;

            // 뒤 화면 선택은 낡은 방의 것일 수 있다 — 돌려주지 않고 패널이 쥔 선택만 비운다.
            previousSelection = null;
            hasEnteredFocus = false;
            ClearOwnedSelection();
            HideBodyWithPauseRelease(releasePause: false);

            Debug.Log($"[EventRoomPanel] 낡은 월드 이벤트 닫힘 — 효과·지급·알림 없음({eventId})");
            return true;
        }
    }
}
