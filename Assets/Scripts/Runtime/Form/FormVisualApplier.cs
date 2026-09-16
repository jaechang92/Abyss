using Anim.Core;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// "폼 하나를 <see cref="SpriteRenderer"/>에 비추는 규칙"의 단일 출처(SoT).
    ///
    /// 규칙 자체는 짧지만 <b>비추는 곳이 둘</b>이다 — 런의 <see cref="FormVisualPresenter"/>
    /// (현재 폼)와 로비의 <c>LobbyFormVisual</c>(시작 폼). 규칙을 양쪽에 적어 두면
    /// 한쪽만 고쳐지고 다른 쪽이 남는다. 이 프로젝트가 UI 정렬 순위·화폐 표기에서
    /// 이미 겪은 파편화 모양이라, 규칙은 여기 하나로 모으고 <b>출처만 각자 다르게</b> 둔다.
    ///
    /// <b>색은 그림이 있을 때 한 번만 건드린다.</b> 런에서는 피격 플래시·시전 플래시가
    /// <c>sr.color</c>를 자기 것으로 쓰므로, 흰색으로 되돌린 뒤로는 놔둬야 한다.
    ///
    /// 🔴 <b>스케일은 건드리지 않는다.</b> 호출부는 폼 그림을 담는 자식(Visual)이고
    /// 그 자식의 로컬 스케일은 부모의 늘림을 상쇄하는 값일 수 있다. 여기서 만지면 상쇄가 풀린다.
    /// </summary>
    public static class FormVisualApplier
    {
        /// <summary>
        /// <paramref name="form"/>의 그림을 <paramref name="target"/>에 적용한다.
        /// <see cref="FormData.bodySprite"/>가 없으면 <paramref name="fallback"/> + 폼 색으로 물러난다 —
        /// 그림이 아직 없는 폼도 화면에서 사라지지 않게.
        /// </summary>
        public static void Apply(SpriteRenderer target, FormData form, Sprite fallback)
        {
            Apply(target, form, fallback, null);
        }

        /// <summary>
        /// 그림에 더해 <b>애니메이션 한 벌까지</b> 적용한다.
        ///
        /// 🔑 <b>규칙을 나누지 않으려고 같은 메서드에 둔다.</b> 스프라이트와 애니메이션을 따로 부르게 하면
        /// 호출자가 둘 다 불러야 하고, 하나를 빠뜨린 곳이 생기면 <b>정지 그림과 움직이는 그림이 서로 다른 폼</b>이 된다.
        /// 이 프로젝트가 UI 정렬 순위·화폐 표기에서 이미 겪은 파편화 모양이라, 진입점을 하나로 둔다.
        ///
        /// 🔴 <b>순서가 있다.</b> 애니메이션을 먼저 정리해야 스프라이트 대입이 살아남는다 —
        /// 애니메이션이 없는 폼인데 재생기를 켜 둔 채로 두면, 매 프레임 이전 폼의 그림을 다시 써서
        /// 방금 넣은 정지 그림을 덮는다.
        /// </summary>
        /// <param name="socket">
        /// 무기 소켓. 🔑 <b>앵커도 같은 진입점을 타야 한다</b> — 따로 부르게 하면 폼을 바꿨을 때
        /// 몸은 새 폼인데 무기만 이전 폼의 배치로 남는 곳이 생긴다. 정지 그림과 움직이는 그림을
        /// 한 곳에 모은 것과 같은 이유다. 안 쓰는 호출부는 비워 두면 된다(로비 등).
        /// </param>
        /// <param name="combat">
        /// 전투 배율을 받을 플레이어. 🔑 <b>이것도 같은 진입점이어야 한다</b> — 그림과 배율이
        /// 따로 꽂히면 <b>손에 든 무기와 실제 위력이 어긋나고, 그건 오류가 안 난다.</b>
        /// (실제로 <c>PlayerCharacter.SetWeapon</c>은 호출자가 하나도 없어 배율이 계속 1이었다.)
        /// 전투가 없는 호출부는 비워 두면 된다(로비 등).
        /// </param>
        public static void Apply(SpriteRenderer target, FormData form, Sprite fallback,
                                 AnimatorDriver driver, Player.WeaponSocket socket = null,
                                 Player.PlayerCharacter combat = null)
        {
            if (driver != null) driver.SetController(form != null ? form.animatorController : null);

            // 🔑 무기는 <b>보유 상태가 먼저</b>고 폼의 기본 무기는 그 폴백이다(§3-B).
            //    런이 없는 곳(로비·테스트 씬)에서는 인벤토리가 없으므로 기본 무기로 물러난다.
            //    무기를 받을 곳이 하나도 없으면(로비 초상) 조회 자체를 건너뛴다.
            if (socket != null || combat != null)
            {
                var inventory = ResolveInventory();

                // 🔴 시작 무기를 보유로 들인 뒤에 조회한다 — 안 들이면 그 폼의 첫 획득이 같은 무기를
                //    「첫 획득(0단계)」으로 받아 빈손이 된다(WeaponInventory.Adopt).
                //    런 시작의 Clear 가 이것을 지워도 Clear 가 판 번호를 올려 다시 그리며 다시 들인다.
                if (form != null) inventory?.Adopt(form.defaultWeapon);

                Weapon.WeaponData weapon = ResolveWeapon(form, inventory);

                if (socket != null)
                {
                    // 🔑 앵커와 무기를 <b>같은 줄에서</b> 꽂는다. 따로 부르게 하면 폼을 바꿨을 때
                    //    배치는 새 폼인데 무기는 이전 폼 것으로 남는 곳이 생긴다.
                    socket.SetAnchors(form != null ? form.weaponAnchors : null);
                    socket.SetWeapon(weapon);
                }

                if (combat != null) combat.SetWeapon(weapon, inventory?.UpgradeLevelOf(weapon) ?? 0);
            }

            if (target == null) return;

            var sprite = form != null ? form.bodySprite : null;

            // 애니메이션이 이 폼을 맡았다면 스프라이트는 클립이 정한다. 여기서는 틴트만 걷는다.
            if (form != null && form.animatorController != null)
            {
                target.color = Color.white;
                return;
            }

            if (sprite != null)
            {
                target.sprite = sprite;
                // 폴백 시절의 폼 색 틴트를 걷는다 — 그림 위에 곱해지면 전부 물든다.
                target.color = Color.white;
                return;
            }

            // 폴백 — 흰 사각형 + 폼 색. 새 폼의 그림이 아직 없을 때 쓰인다.
            if (fallback != null) target.sprite = fallback;
            if (form != null) target.color = form.castColor;
        }

        /// <summary>
        /// 이 폼이 들 무기. 런에서 획득한 것이 있으면 그것, 없으면 <see cref="FormData.defaultWeapon"/>.
        ///
        /// 🔴 <b>「장비가 미리보기를 이긴다」의 한 층 위다.</b> 저쪽(<c>WeaponSocket.ResolveWeapon</c>)은
        /// 인스펙터 배선 대비 우선순위였고, 여기는 <b>획득 무기 대비 폼 기본 무기</b>다.
        /// </summary>
        private static Weapon.WeaponData ResolveWeapon(FormData form, Weapon.WeaponInventory inventory)
        {
            if (form == null) return null;
            return inventory != null ? inventory.ResolveFor(form.formId, form.defaultWeapon) : form.defaultWeapon;
        }

        /// <summary>
        /// 이번 런의 무기 보유 상태. 런이 없으면 <c>null</c>이다.
        ///
        /// ⚠️ <c>HasInstance</c>로 묻는다 — <c>Instance</c>는 없으면 만들어 버리므로,
        /// 로비나 테스트 씬에서 그림을 그리려다 <c>RunManager</c>가 생겨난다
        /// (싱글톤 접근 정책: 코어는 <c>Instance</c>, 조회는 <c>HasInstance</c>).
        /// </summary>
        private static Weapon.WeaponInventory ResolveInventory()
            => Run.RunManager.HasInstance ? Run.RunManager.Instance.Weapons : null;
    }
}
