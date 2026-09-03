using System;
using System.Collections.Generic;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 유물 상점(가차) 패널. <see cref="RelicShopNpc"/> 상호작용 시 열린다.
    /// 심연 조각으로 <b>확률</b> 감정을 하고, 보유 유물을 장착 슬롯 3칸에 끼운다.
    ///
    /// 🔑 <b>심연의 제단과 역할이 갈린다.</b> 제단은 같은 조각으로 <i>확정</i>을 사고
    /// 여기서는 <i>확률</i>을 산다. 둘 다 효과 어휘는 <see cref="MetaUpgradeType"/>로 같고,
    /// 합산도 <see cref="MetaUpgrades"/> 한 곳에서 이뤄진다 — 화면만 둘이다.
    ///
    /// 🔴 <b>보유한다고 효과가 붙지 않는다.</b> 장착 슬롯에 끼운 것만 런에 반영된다.
    /// 그래서 이 패널의 절반은 상점이 아니라 <b>빌드 편집기</b>다.
    ///
    /// 조작은 토글 하나로 통일했다 — 보유 타일을 누르면 빈 슬롯에 들어가고,
    /// 이미 장착된 것을 누르면 빠진다. 슬롯을 눌러도 빠진다.
    /// 드래그·2단계 선택을 쓰지 않은 이유는 슬롯이 셋뿐이라 그 복잡도가 값을 못 하기 때문이다.
    ///
    /// 박스·타일은 최초 Open 시 런타임 생성한다(<see cref="MetaUpgradePanel"/>과 같은 패턴).
    /// </summary>
    public sealed partial class RelicShopPanel : MonoBehaviour
    {
        [Header("루트")]
        [Tooltip("토글 대상. 비우면 이 컴포넌트의 GameObject.")]
        [SerializeField] private GameObject root;

        [Header("배치 기준")]
        [Tooltip("패널 박스가 생성될 부모. 비우면 root 의 RectTransform.")]
        [SerializeField] private RectTransform boxParent;

        public bool IsOpen { get; private set; }

        private Action onClosed;
        private Font uiFont;

        private bool built;
        private Text shardsLabel;
        private Text messageLabel;
        private Button drawButton;
        private Image drawButtonImage;
        private Text ownedHeaderLabel;

        private readonly List<SlotView> slots = new();
        private readonly List<TileView> tiles = new();

        /// <summary>장착 슬롯 1칸의 갱신 대상.</summary>
        private sealed class SlotView
        {
            public int Index;
            public Image Background;
            public Text NameLabel;
            public Text LevelLabel;
            public Button Button;
        }

        /// <summary>
        /// 보유 유물 타일 1개의 갱신 대상.
        ///
        /// 타일은 <b>카탈로그 전체만큼</b> 만들고 미보유는 숨긴다. 보유 수에 맞춰 그때그때
        /// 만들면 그리드 높이가 프레임마다 바뀌어 박스 크기를 미리 잡을 수 없다 —
        /// 반대로 카탈로그 수는 고정이라 <b>박스가 잘릴 일이 원천적으로 없다</b>.
        /// </summary>
        private sealed class TileView
        {
            public RelicData Data;
            public GameObject Root;
            public Image Background;
            public Text NameLabel;
            public Text LevelLabel;
        }

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
            IsOpen = false;
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Close();
        }

        /// <summary>패널을 연다. closed는 닫힐 때(Esc/떠난다) 호출되는 콜백.</summary>
        public void Open(Action closed)
        {
            onClosed = closed;
            EnsureBuilt();
            SetMessage(string.Empty);
            Refresh();
            if (root != null) root.SetActive(true);
            IsOpen = true;
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            IsOpen = false;
            var cb = onClosed;
            onClosed = null;
            cb?.Invoke();
        }

        // ───────────────────────── 감정(뽑기) ─────────────────────────

        /// <summary>
        /// 1회 감정. 실패하면 <b>아무것도 바뀌지 않는다</b>(서비스가 보장) — 여기서는 이유만 말한다.
        ///
        /// 결과 문구가 셋으로 갈리는 이유: "처음 보는 것"과 "겹쳐서 레벨이 올랐다"와
        /// "최대치라 더 못 겹친다"는 플레이어에게 전혀 다른 소식이다.
        /// 하나로 뭉뚱그리면 조각을 왜 썼는지 알 수 없다.
        /// </summary>
        private void OnDraw()
        {
            var meta = MetaSaveService.Instance;
            if (meta == null) return;

            if (meta.Current.abyssShardsTotal < RelicGacha.DRAW_COST)
            {
                SetMessage(Loc.Get(StringKey.Relic_NotEnough));
                return;
            }

            if (!meta.TryDrawRelic(out var drawn, out bool isDuplicate) || drawn == null)
            {
                // 잔액은 위에서 봤으므로 여기 오면 후보가 없다는 뜻이다(에셋 미생성 등).
                SetMessage(Loc.Get(StringKey.Relic_NotEnough));
                Debug.LogWarning("[RelicShopPanel] 감정 실패 — 유물 카탈로그가 비어 있는지 확인할 것 " +
                                 "(Generate > Relic Content).");
                return;
            }

            string relicName = Loc.Get(drawn.nameKey);
            int level = meta.GetRelicLevel(drawn.relicId);

            if (!isDuplicate)
            {
                SetMessage(Loc.GetFormat(StringKey.Relic_GainedFormat, relicName));
                // 처음 얻은 유물은 빈 슬롯이 있으면 바로 끼워 준다 — 뽑아 놓고 효과가 없는
                // 상태로 나가는 것이 이 시스템에서 가장 흔한 오해다.
                TryAutoEquip(drawn.relicId);
            }
            else if (level >= drawn.maxLevel)
            {
                SetMessage(Loc.GetFormat(StringKey.Relic_MaxedFormat, relicName));
            }
            else
            {
                SetMessage(Loc.GetFormat(StringKey.Relic_LevelUpFormat, relicName, level));
            }

            Refresh();
        }

        private void TryAutoEquip(string relicId)
        {
            var meta = MetaSaveService.Instance;
            if (meta == null) return;

            var equipped = meta.EquippedRelicIds;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (!string.IsNullOrEmpty(equipped[i])) continue;
                meta.TryEquipRelic(relicId, i);
                return;
            }
        }

        // ───────────────────────── 장착 ─────────────────────────

        /// <summary>
        /// 보유 타일 클릭 = 토글. 장착돼 있으면 빼고, 아니면 첫 빈 슬롯에 넣는다.
        /// 빈 슬롯이 없으면 아무것도 하지 않고 이유를 말한다 —
        /// 임의로 하나를 밀어내면 방금 맞춘 빌드가 클릭 한 번에 무너진다.
        /// </summary>
        private void OnTileClicked(TileView tile)
        {
            var meta = MetaSaveService.Instance;
            if (meta == null || tile?.Data == null) return;

            string relicId = tile.Data.relicId;
            if (meta.IsRelicEquipped(relicId))
            {
                var equipped = meta.EquippedRelicIds;
                for (int i = 0; i < equipped.Count; i++)
                {
                    if (equipped[i] != relicId) continue;
                    meta.UnequipRelicSlot(i);
                    break;
                }
                SetMessage(string.Empty);
                Refresh();
                return;
            }

            var current = meta.EquippedRelicIds;
            for (int i = 0; i < current.Count; i++)
            {
                if (!string.IsNullOrEmpty(current[i])) continue;
                meta.TryEquipRelic(relicId, i);
                SetMessage(string.Empty);
                Refresh();
                return;
            }

            SetMessage(Loc.Get(StringKey.Relic_SlotsFull));
        }

        private void OnSlotClicked(SlotView slot)
        {
            var meta = MetaSaveService.Instance;
            if (meta == null || slot == null) return;
            if (!meta.UnequipRelicSlot(slot.Index)) return;

            SetMessage(string.Empty);
            Refresh();
        }

        // ───────────────────────── 갱신 ─────────────────────────

        private void SetMessage(string text)
        {
            if (messageLabel != null) messageLabel.text = text;
        }

        /// <summary>잔액·슬롯·보유 타일·버튼 상태를 전체 재계산한다.</summary>
        private void Refresh()
        {
            var meta = MetaSaveService.Instance;
            if (meta == null) return;

            int shards = meta.Current.abyssShardsTotal;
            if (shardsLabel != null) shardsLabel.text = Loc.GetFormat(StringKey.Altar_ShardsFormat, shards);

            bool canDraw = shards >= RelicGacha.DRAW_COST;
            if (drawButton != null) drawButton.interactable = canDraw;
            if (drawButtonImage != null) drawButtonImage.color = canDraw ? DrawBase : DrawDisabled;

            RefreshSlots(meta);
            RefreshTiles(meta);
        }

        private void RefreshSlots(MetaSaveService meta)
        {
            var equipped = meta.EquippedRelicIds;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                string relicId = i < equipped.Count ? equipped[i] : string.Empty;
                var relic = RelicCatalog.GetById(relicId);
                bool filled = relic != null;

                if (slot.Background != null) slot.Background.color = filled ? RelicDisplay.RarityColor(relic.rarity) : SlotEmpty;
                if (slot.NameLabel != null)
                {
                    slot.NameLabel.text = filled ? Loc.Get(relic.nameKey) : Loc.Get(StringKey.Relic_EmptySlot);
                    slot.NameLabel.color = filled ? Color.white : MutedText;
                }
                if (slot.LevelLabel != null)
                {
                    slot.LevelLabel.text = filled
                        ? Loc.GetFormat(StringKey.Relic_LevelFormat, meta.GetRelicLevel(relic.relicId), relic.maxLevel)
                        : string.Empty;
                }
                if (slot.Button != null) slot.Button.interactable = filled;
            }
        }

        private void RefreshTiles(MetaSaveService meta)
        {
            int ownedCount = 0;
            foreach (var tile in tiles)
            {
                if (tile?.Data == null) continue;

                int level = meta.GetRelicLevel(tile.Data.relicId);
                bool owned = level > 0;
                if (tile.Root != null) tile.Root.SetActive(owned);
                if (!owned) continue;

                ownedCount += 1;
                bool equipped = meta.IsRelicEquipped(tile.Data.relicId);
                if (tile.Background != null)
                {
                    var baseColor = RelicDisplay.RarityColor(tile.Data.rarity);
                    // 장착된 것은 밝게 — 같은 그리드 안에서 "지금 쓰는 것"이 한눈에 갈려야 한다.
                    tile.Background.color = equipped ? baseColor : Color.Lerp(baseColor, TileDim, 0.6f);
                }
                if (tile.NameLabel != null) tile.NameLabel.text = Loc.Get(tile.Data.nameKey);
                if (tile.LevelLabel != null)
                {
                    tile.LevelLabel.text = Loc.GetFormat(StringKey.Relic_LevelFormat, level, tile.Data.maxLevel);
                }
            }

            if (ownedHeaderLabel != null)
            {
                ownedHeaderLabel.text = ownedCount > 0
                    ? Loc.Get(StringKey.Relic_OwnedHeader)
                    : Loc.Get(StringKey.Relic_NoneOwned);
            }
        }
    }
}
