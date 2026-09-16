using System.Collections.Generic;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 런 한 판 동안의 무기 보유 상태. <b>획득 3창구(제단·상점·가차)가 전부 여기에 쓴다</b>
    /// (<c>14-weapon-equipment-system.md</c> §3-B).
    ///
    /// 🔴 <b>이것이 없으면 「준다」가 아무 데도 안 남는다.</b> 지금까지 무기를 꽂는 경로는
    /// <c>FormData.defaultWeapon</c> 한 줄뿐이라, 제단에서 무기를 줘도 다음 폼 교체에서
    /// 기본 무기로 되돌아갔다 — 오류도 로그도 없이 보상만 사라지는 종류다.
    ///
    /// 🔑 <b>표를 둘로 나눈 이유.</b>
    /// <list type="bullet">
    /// <item><b>강화 단계는 무기에 붙는다</b>(<c>weaponId</c> 기준) — 다른 무기로 갈아탔다가
    /// 돌아와도 쌓아 둔 단계가 남는다. 단계를 폼에 붙이면 무기를 바꾼 순간 증발한다</item>
    /// <item><b>장착은 폼에 붙는다</b>(<c>formId</c> 기준) — 「폼마다 기억」이 확정 정책이다
    /// (2026-09-16 사용자 결정). 검사로 돌아오면 아까 주운 검이 그대로 손에 있다</item>
    /// </list>
    ///
    /// ⚠️ <b>Unity 타입을 안 쓴다</b>(<see cref="WeaponData"/> 참조뿐). <see cref="WeaponDraw"/>와
    /// 같은 태도로, 씬 없이 EditMode 테스트가 성질을 고정할 수 있게 둔다.
    ///
    /// ⚠️ <b><c>FormData</c> 타입을 받지 않는다.</b> <c>FormData</c>가 이미 <c>WeaponData</c>를
    /// 참조하므로(<c>FormData.defaultWeapon</c>) 여기서 <c>FormData</c>를 받으면 방향이 맞물린다.
    /// 폼에서 뽑아낸 <c>formId</c>와 기본 무기만 인자로 받는다.
    /// </summary>
    public sealed class WeaponInventory
    {
        // weaponId → 강화 단계. 키가 있으면 「보유」다(0단계가 첫 획득).
        private readonly Dictionary<string, int> levels = new();

        // formId → 그 폼이 들고 있는 무기.
        private readonly Dictionary<string, WeaponData> equipped = new();

        /// <summary>
        /// 내용이 바뀔 때마다 오르는 값.
        ///
        /// 🔑 <b>뷰가 이 숫자만 보고 다시 그린다</b> — <c>FormVisualPresenter</c>는 폼이 바뀔 때만
        /// 다시 그리는데, 제단에서 무기를 주는 순간에는 <b>폼이 안 바뀐다.</b> 이벤트를 새로 파면
        /// 발행을 빠뜨린 창구에서 조용히 안 바뀌므로(이 프로젝트가 죽은 게이트로 여러 번 겪은 모양),
        /// 뷰가 상태에서 파생되게 둔다. 비용은 프레임당 정수 비교 하나다.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// 무기를 획득한다. 이미 가진 무기면 <b>강화 단계가 1 오른다</b>(§7 중복=강화).
        /// 어느 쪽이든 그 폼의 장착 무기가 이것으로 바뀐다 — 방금 준 것이 손에 안 들리면
        /// 보상이 일어난 것처럼 안 보인다.
        /// </summary>
        /// <returns>처음 얻은 무기면 <c>true</c>, 중복 강화면 <c>false</c>. 획득에 실패하면 <c>false</c>.</returns>
        public bool Grant(WeaponData weapon)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.weaponId)) return false;

            bool isNew = !levels.ContainsKey(weapon.weaponId);
            levels[weapon.weaponId] = isNew ? 0 : levels[weapon.weaponId] + 1;

            // formBound 가 비면 어느 폼도 못 쓰는 무기다(WeaponData 규약). 보유만 기록하고 안 꽂는다 —
            // 빈 문자열을 키로 넣으면 formId 없는 폼이 그걸 집어 든다.
            if (!string.IsNullOrEmpty(weapon.formBound)) equipped[weapon.formBound] = weapon;

            Version++;
            return isNew;
        }

        /// <summary>
        /// <paramref name="formId"/>가 들 무기. 획득한 것이 없으면 <paramref name="fallback"/>
        /// (그 폼의 기본 무기)으로 물러난다 — 런 시작 시점에는 전부 이 경로다.
        /// </summary>
        public WeaponData ResolveFor(string formId, WeaponData fallback)
        {
            if (string.IsNullOrEmpty(formId)) return fallback;
            return equipped.TryGetValue(formId, out WeaponData weapon) && weapon != null ? weapon : fallback;
        }

        /// <summary>
        /// <paramref name="weapon"/>의 강화 단계. 안 가진 무기는 0이다.
        ///
        /// ⚠️ <b>단계를 배율로 바꾸는 식은 여기 없다.</b> <see cref="WeaponData.MultiplierAt"/> 하나뿐이고,
        /// 그 식이 전투·상점·툴팁에 흩어지지 않게 하는 것이 그 메서드가 있는 이유다.
        /// </summary>
        public int UpgradeLevelOf(WeaponData weapon)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.weaponId)) return 0;
            return levels.TryGetValue(weapon.weaponId, out int level) ? level : 0;
        }

        /// <summary>한 번이라도 획득한 무기인지. 지금 장착 중인지와는 별개다.</summary>
        public bool Owns(WeaponData weapon)
            => weapon != null && !string.IsNullOrEmpty(weapon.weaponId) && levels.ContainsKey(weapon.weaponId);

        /// <summary>런이 새로 시작될 때 비운다. 무기는 런 스코프다 — 메타에 남지 않는다.</summary>
        public void Clear()
        {
            if (levels.Count == 0 && equipped.Count == 0) return;

            levels.Clear();
            equipped.Clear();
            Version++;
        }
    }
}
