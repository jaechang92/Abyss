using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// P04 폼 연계(A 예약 · B 준비 · C 표식)의 짧은 월드 문구. 색 + 글자로 알린다 — 색만으로 구분하지 않는다.
    ///
    /// 🔑 <b>대상의 자식으로 붙이지 않고 따라간다.</b> 플레이어는 방향 전환을 <c>localScale.x</c> 부호로 하므로
    /// 자식 글자는 뒤집혀 거울 글씨가 된다. 대상이 파괴되면 스스로 사라진다.
    /// 글자 모양은 <c>RoomExitDoor</c> 의 TextMesh 문구와 같은 방식(공용 기본 폰트)이다.
    /// </summary>
    public sealed class FormComboLabel : MonoBehaviour
    {
        private const int SORTING_ORDER = 50;
        private const int FONT_SIZE = 48;

        // 선택 옵션(기본 꺼짐 — C 표식 문구는 쓰지 않는다): 어두운 배경 · 약한 펄스
        private const float BACKDROP_PADDING_X = 0.16f;
        private const float BACKDROP_PADDING_Y = 0.08f;
        private const float PULSE_SPEED = 7f;          // rad/s ≈ 1.1Hz
        private const float PULSE_MIN_ALPHA = 0.7f;

        private Transform target;
        private Vector3 offset;
        private TextMesh mesh;
        private MeshRenderer textRenderer;
        private SpriteRenderer backdrop;
        private Color textColor;
        private bool isPulsing;

        /// <summary>문구 하나를 만든다. 처음에는 숨겨 둔다.</summary>
        public static FormComboLabel Create(string name, Transform target, Vector3 offset, float characterSize)
        {
            var go = new GameObject(name);
            var label = go.AddComponent<FormComboLabel>();
            label.target = target;
            label.offset = offset;

            label.mesh = go.AddComponent<TextMesh>();
            label.mesh.font = UiFactory.GetDefaultFont();
            label.mesh.fontSize = FONT_SIZE;
            label.mesh.characterSize = characterSize;
            label.mesh.anchor = TextAnchor.MiddleCenter;
            label.mesh.alignment = TextAlignment.Center;

            var renderer = go.GetComponent<MeshRenderer>();
            if (label.mesh.font != null) renderer.sharedMaterial = label.mesh.font.material;
            renderer.sortingOrder = SORTING_ORDER;
            label.textRenderer = renderer;

            go.SetActive(false);
            return label;
        }

        /// <summary>
        /// 글자 뒤에 어두운 반투명 배경을 깐다(1회, 자식 오브젝트). 크기는 글자 경계에 맞춰 매 프레임 따라간다.
        /// 부르지 않으면 기존과 같다.
        /// </summary>
        public void EnableBackdrop(Color color)
        {
            if (backdrop != null) return;
            var go = new GameObject("Backdrop");
            go.transform.SetParent(transform, false);
            backdrop = go.AddComponent<SpriteRenderer>();
            backdrop.sprite = FormComboSprites.White;
            backdrop.color = color;
            backdrop.sortingOrder = SORTING_ORDER - 1;
            FitBackdrop();
        }

        /// <summary>따라갈 대상을 바꾼다(C 표식이 다른 적으로 옮겨 갈 때).</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        /// <summary>대상과 높이를 함께 바꾼다(적마다 키가 다르다).</summary>
        public void SetTarget(Transform newTarget, Vector3 newOffset)
        {
            target = newTarget;
            offset = newOffset;
        }

        public void Show(string text, Color color) => Show(text, color, false);

        /// <summary><paramref name="pulse"/> 가 참이면 글자 알파가 약하게 오르내린다(A 예약 유지 표시).</summary>
        public void Show(string text, Color color, bool pulse)
        {
            if (mesh == null) return;
            mesh.text = text;
            mesh.color = color;
            textColor = color;
            isPulsing = pulse;
            Follow();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            FitBackdrop();
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Hide();
                return;
            }
            Follow();
            if (isPulsing && mesh != null)
            {
                Color c = textColor;
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * PULSE_SPEED);
                c.a = textColor.a * Mathf.Lerp(PULSE_MIN_ALPHA, 1f, wave);
                mesh.color = c;
            }
            FitBackdrop();
        }

        private void Follow()
        {
            if (target != null) transform.position = target.position + offset;
        }

        /// <summary>배경을 글자 경계 + 여백으로 맞춘다. 라벨 자체는 회전 · 배율이 없으므로 월드 크기 = 로컬 크기.</summary>
        private void FitBackdrop()
        {
            if (backdrop == null || textRenderer == null) return;
            Bounds bounds = textRenderer.bounds;
            backdrop.transform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
            backdrop.transform.localScale = new Vector3(bounds.size.x + BACKDROP_PADDING_X * 2f,
                bounds.size.y + BACKDROP_PADDING_Y * 2f, 1f);
        }
    }
}
