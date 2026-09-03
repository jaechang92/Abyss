using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// <see cref="MetaSaveService"/>의 <b>유물</b> 부분(로비 상점 가차).
    /// (본체와 같은 partial 클래스라 EnsureLoaded·current·Save를 공유한다. 500줄 규약으로 분리.)
    ///
    /// 🔑 <b>보유와 장착은 다른 것이다.</b> 보유는 뽑기의 결과라 되돌릴 수 없고,
    /// 장착은 빌드 선택이라 언제든 바꾼다. 효과 합산은 <b>장착된 것만</b> 본다.
    /// 한 목록으로 합치면 슬롯에서 빼는 순간 뽑은 것을 잃게 된다.
    ///
    /// 🔑 <b>차감·저장 규칙은 <c>TryPurchaseUpgrade</c>와 같은 자리에 둔다.</b> 잔액 검사와
    /// 저장 시점이 같은데 클래스를 나누면 한쪽만 고친 규칙이 남는다(해금을 구매 경로에
    /// 얹은 것과 같은 판단).
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>보유 유물의 레벨. 미보유면 0.</summary>
        public int GetRelicLevel(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return 0;
            EnsureLoaded();

            for (int i = 0; i < current.relics.Count; i++)
            {
                if (current.relics[i].relicId == relicId) return current.relics[i].level;
            }
            return 0;
        }

        /// <summary>이 유물을 가지고 있는가.</summary>
        public bool OwnsRelic(string relicId) => GetRelicLevel(relicId) > 0;

        /// <summary>
        /// 유물 1회 뽑기. 잔액 부족·후보 없음이면 false이고 <b>아무것도 바뀌지 않는다</b>.
        ///
        /// <paramref name="drawn"/>은 뽑힌 유물, <paramref name="isDuplicate"/>는 이미 갖고
        /// 있던 것인지다. 화면이 "새로 얻었다"와 "레벨이 올랐다"를 다르게 말해야 하기 때문에
        /// 호출부가 결과만 보고는 구분할 수 없는 정보를 함께 돌려준다.
        ///
        /// ⚠️ <b>차감은 추첨 뒤가 아니라 앞이다.</b> 뽑고 나서 차감하면 후보가 0인 상황에서
        /// 조각만 사라진다 — 그래서 후보 확보를 먼저 확인한다.
        /// </summary>
        public bool TryDrawRelic(out RelicData drawn, out bool isDuplicate)
        {
            drawn = null;
            isDuplicate = false;
            EnsureLoaded();

            if (current.abyssShardsTotal < RelicGacha.DRAW_COST) return false;

            var candidate = RelicGacha.Draw(RelicCatalog.All);
            if (candidate == null || string.IsNullOrEmpty(candidate.relicId)) return false;

            current.abyssShardsTotal -= RelicGacha.DRAW_COST;

            int level = GetRelicLevel(candidate.relicId);
            isDuplicate = level > 0;
            // 최대 레벨이면 레벨은 그대로 둔다. 조각은 이미 나갔고, 그 사실을 화면이
            // "최대치"로 말한다 — 여기서 조용히 되돌리면 표시와 잔액이 어긋난다.
            SetRelicLevel(candidate.relicId, Mathf.Min(level + 1, candidate.maxLevel));

            drawn = candidate;
            Save();
            return true;
        }

        /// <summary>
        /// 장착 슬롯 목록(길이 <see cref="MetaSave.EquippedRelicSlots"/> 고정, 빈 칸은 빈 문자열).
        /// 저장된 목록이 짧거나 길어도 여기서 길이를 맞춘다 —
        /// 슬롯 수를 늘렸을 때 옛 세이브가 인덱스 예외를 내지 않게.
        /// </summary>
        public IReadOnlyList<string> EquippedRelicIds
        {
            get
            {
                EnsureLoaded();
                NormalizeEquipSlots();
                return current.equippedRelicIds;
            }
        }

        /// <summary>
        /// 유물을 슬롯에 끼운다. 미보유·슬롯 범위 밖이면 false.
        ///
        /// 같은 유물이 이미 <b>다른</b> 슬롯에 있으면 그 슬롯을 비운다(자리 이동).
        /// 두 슬롯에 같은 유물이 앉으면 효과가 두 번 더해져, 화면상 슬롯 3칸으로
        /// 유물 하나를 3배로 쓰는 길이 열린다.
        /// </summary>
        public bool TryEquipRelic(string relicId, int slot, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(relicId)) return false;
            EnsureLoaded();
            NormalizeEquipSlots();

            if (slot < 0 || slot >= MetaSave.EquippedRelicSlots) return false;
            if (!OwnsRelic(relicId)) return false;

            for (int i = 0; i < current.equippedRelicIds.Count; i++)
            {
                if (i != slot && current.equippedRelicIds[i] == relicId) current.equippedRelicIds[i] = string.Empty;
            }
            current.equippedRelicIds[slot] = relicId;

            if (autoSave) Save();
            return true;
        }

        /// <summary>슬롯을 비운다. 이미 비어 있으면 false(저장도 하지 않는다).</summary>
        public bool UnequipRelicSlot(int slot, bool autoSave = true)
        {
            EnsureLoaded();
            NormalizeEquipSlots();

            if (slot < 0 || slot >= MetaSave.EquippedRelicSlots) return false;
            if (string.IsNullOrEmpty(current.equippedRelicIds[slot])) return false;

            current.equippedRelicIds[slot] = string.Empty;
            if (autoSave) Save();
            return true;
        }

        /// <summary>이 유물이 어느 슬롯에든 장착돼 있는가.</summary>
        public bool IsRelicEquipped(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return false;
            EnsureLoaded();
            return current.equippedRelicIds.Contains(relicId);
        }

        private void SetRelicLevel(string relicId, int level)
        {
            for (int i = 0; i < current.relics.Count; i++)
            {
                if (current.relics[i].relicId != relicId) continue;
                var e = current.relics[i];
                e.level = level;
                current.relics[i] = e;
                return;
            }
            current.relics.Add(new RelicEntry { relicId = relicId, level = level });
        }

        /// <summary>
        /// 장착 목록의 길이를 슬롯 수에 맞춘다. 유물을 모르던 세이브는 목록이 비어 있고,
        /// 슬롯 수를 바꾸면 길이가 어긋난다. 읽기 직전에 맞춰 두면 호출부마다 방어할 필요가 없다.
        /// </summary>
        private void NormalizeEquipSlots()
        {
            var list = current.equippedRelicIds;
            while (list.Count < MetaSave.EquippedRelicSlots) list.Add(string.Empty);
            while (list.Count > MetaSave.EquippedRelicSlots) list.RemoveAt(list.Count - 1);
        }
    }
}
