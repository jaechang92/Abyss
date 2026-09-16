#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using Abyss.Runtime.Weapon;
using UnityEditor;

namespace Abyss.EditorTools
{
    /// <summary>플레이어 스탯 그리기. 창의 틀·줄 그리기는 <c>StatInspectorWindow.cs</c> 가 갖는다.</summary>
    internal sealed partial class StatInspectorWindow
    {
        private void DrawPlayer(PlayerCharacter player)
        {
            bool isLive = EditorApplication.isPlaying;
            var form = player.Form != null ? player.Form.CurrentForm : null;

            Section("요약");
            Row("폼", form != null ? $"{form.displayName} ({form.formId})" : "없음");
            if (isLive)
            {
                Row("HP", $"{player.CurrentHp} / {player.BaseHp}");
                Row("상태", PlayerStateText(player));
            }
            else
            {
                Row("HP (폴백)", player.BaseHp);
            }

            DrawPlayerAttack(player, isLive);
            DrawPlayerWeapon(player, form, isLive);
            DrawPlayerMobility(player, isLive);

            if (!isLive) return;

            DrawPlayerBuffs(player);
            DrawRunState();
        }

        private void DrawPlayerAttack(PlayerCharacter player, bool isLive)
        {
            Section("공격력");

            if (!isLive)
            {
                Row("약공격 (기본)", player.BaseLightAttackDamage);
                Row("강공격 (기본)", player.BaseHeavyAttackDamage);
                Row("쿨다운", $"약 {player.AttackCooldownLight:0.00}s · 강 {player.AttackCooldownHeavy:0.00}s");
                return;
            }

            string total = Mult(player.TotalAttackMult);
            Row("약공격", $"{player.LightAttackDamage}    (기본 {player.BaseLightAttackDamage} {total})");
            Row("강공격", $"{player.HeavyAttackDamage}    (기본 {player.BaseHeavyAttackDamage} {total})");

            // 층마다 나눈다 — 합계만 보이면 어느 층이 얼마를 줬는지 못 읽는다.
            MultRow("   버프", player.AttackMultiplier);
            MultRow("   메타 업그레이드", player.MetaAttackMult);
            MultRow("   무기", player.WeaponAttackMult);
            MultRow("   합계", player.TotalAttackMult);

            Row("쿨다운", $"약 {player.AttackCooldownLight:0.00}s · 강 {player.AttackCooldownHeavy:0.00}s");
            if (player.IsAbyssChargeReady) Row("심연 충전", "장전됨 — 다음 적중에 추가 배율");
            MultRow("연소 피해", player.BurnDamageMultiplier);
        }

        private void DrawPlayerWeapon(PlayerCharacter player, FormData form, bool isLive)
        {
            Section("무기");

            if (!isLive)
            {
                var starting = form != null ? form.defaultWeapon : null;
                Row("기본 무기", starting != null ? WeaponText.NameOf(starting) : "없음");
                return;
            }

            var weapon = player.CurrentWeapon;
            if (weapon == null)
            {
                Row("배율 무기", "없음 (×1.00)");
            }
            else
            {
                Row("이름", $"{WeaponText.NameOf(weapon)} ({weapon.weaponId})");
                Row("등급", weapon.rarity.ToString());
                Row("강화", $"+{player.WeaponUpgradeLevel}");
                Row("배율", $"{Mult(player.WeaponAttackMult)}    (기본 {Mult(weapon.attackMultiplier)} · 단계당 +{weapon.upgradeMultiplierStep:0.00})");

                // 교차 확인 — 식을 복제하는 게 아니라 같은 식(MultiplierAt)을 한 번 더 부른다.
                if (!UnityEngine.Mathf.Approximately(weapon.MultiplierAt(player.WeaponUpgradeLevel), player.WeaponAttackMult))
                {
                    Warn("무기 배율이 강화 단계와 안 맞는다 — SetWeapon 이후 값이 따로 바뀌었다.");
                }
            }

            // 🔴 그림과 위력이 같은 무기인지. 둘은 같은 진입점(FormVisualApplier.Apply)에서 꽂히지만,
            //    어긋나면 손에 든 무기와 실제 대미지가 달라지고 그건 오류가 안 난다.
            var socket = player.GetComponentInChildren<WeaponSocket>(true);
            if (socket != null && socket.EquippedWeapon != null && socket.EquippedWeapon != weapon)
            {
                Warn($"손에 그려진 무기({WeaponText.NameOf(socket.EquippedWeapon)})와 " +
                     $"배율을 준 무기({(weapon != null ? WeaponText.NameOf(weapon) : "없음")})가 다르다.");
            }

            DrawWeaponInventory(form);
        }

        /// <summary>
        /// 런 보유 무기. 🔑 <b>폼 교체 승계를 확인하는 자리다</b> — 다른 폼으로 갔다 돌아와도
        /// 단계와 장착이 남는지를 여기서 본다.
        /// </summary>
        private void DrawWeaponInventory(FormData currentForm)
        {
            if (!RunManager.HasInstance) return;

            var inventory = RunManager.Instance.Weapons;
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField($"보유 ({inventory.Levels.Count})", hintStyle);

            foreach (var pair in inventory.Levels)
            {
                Row($"   {pair.Key}", $"+{pair.Value}");
            }

            foreach (var pair in inventory.EquippedByForm)
            {
                bool isCurrent = currentForm != null && currentForm.formId == pair.Key;
                Row($"   {pair.Key}{(isCurrent ? " (지금)" : string.Empty)}", WeaponText.NameOf(pair.Value));
            }

            if (inventory.EquippedByForm.Count == 0)
            {
                EditorGUILayout.LabelField("   장착 기록 없음 — 모든 폼이 기본 무기를 든다", hintStyle);
            }
        }

        private void DrawPlayerMobility(PlayerCharacter player, bool isLive)
        {
            Section("방어 · 이동");

            if (!isLive)
            {
                Row("이동속도 (기본)", $"{player.BaseMoveSpeed:0.00}");
                return;
            }

            MultRow("받는 피해", player.DefenseMultiplier);
            Row("이동속도", $"{player.EffectiveMoveSpeed:0.00}    (기본 {player.BaseMoveSpeed:0.00} · " +
                          $"버프 {Mult(player.MoveSpeedMultiplier)} · 폼 {Mult(player.FormMoveSpeedMultiplier)})");
            Row("대시 쿨다운", $"{player.EffectiveDashCooldown:0.00}s");
            Row("남은 점프", player.JumpsRemaining);
        }

        private void DrawPlayerBuffs(PlayerCharacter player)
        {
            Section("버프");
            if (!player.HasActiveBuff)
            {
                Row("상태", "없음");
            }
            else
            {
                Row("남은 시간", $"{player.BuffRemaining:0.0}s");
                MultRow("   이동", player.MoveSpeedMultiplier);
                MultRow("   공격", player.AttackMultiplier);
                MultRow("   받는 피해", player.DefenseMultiplier);
            }

            Section("패시브 · 시너지");
            var active = new List<string>();
            if (player.IsFlameArmorActive) active.Add("불꽃 갑옷");
            if (player.IsBurnEnhancementActive) active.Add("연소 강화");
            if (player.IsAfterimageActive) active.Add("잔상");
            if (player.IsAbyssChargeActive) active.Add("심연 충전");
            if (player.IsSoulReclaimActive) active.Add("영혼 회수");
            if (player.IsExplosiveTheologyActive) active.Add("폭발 신학");
            if (player.IsAbyssAllyActive) active.Add("심연 동료");
            if (player.IsCounterStanceActive) active.Add("반격 태세");
            Row("활성", active.Count > 0 ? string.Join(" · ", active) : "없음");
        }

        private void DrawRunState()
        {
            Section("런");

            // ⚠️ HasInstance 로 묻는다 — Instance 는 없으면 만들어 버린다(싱글톤 접근 정책).
            if (!RunManager.HasInstance)
            {
                Row("상태", "RunManager 없음");
                return;
            }

            var run = RunManager.Instance;
            Row("진행", run.IsRunActive ? "런 진행 중" : "런 밖");
            Row("레벨", $"{run.CurrentLevel}    (EXP {run.CurrentExp} / {run.ExpToNextLevel})");
            Row("골드", run.GoldShards);
        }

        private static string PlayerStateText(PlayerCharacter player)
        {
            var parts = new List<string>();
            if (player.IsDead) parts.Add("사망");
            if (player.DebugInvincible) parts.Add("무적(치트)");
            parts.Add(player.IsGrounded ? "지상" : "공중");
            if (player.IsDashing) parts.Add("대시 중");
            return string.Join(" · ", parts);
        }
    }
}
#endif
