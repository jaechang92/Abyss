#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// LobbySceneBuilder의 NPC/포털 생성 공용 헬퍼 부분 —
    /// Create*Npc/CreatePortal에서 반복되는 "GameObject + 스프라이트 + 트리거 콜라이더 + 컴포넌트" 생성 패턴과
    /// Wire*Npc에서 반복되는 promptKey 적용 꼬리를 추출한다.
    /// (메인 LobbySceneBuilder.cs와 같은 partial 클래스라 SetObject 등 헬퍼를 공유한다.)
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        /// <summary>
        /// 이름/위치/스케일/색상만 다른 NPC·포털류 오브젝트 생성 공통 헬퍼.
        /// SpriteRenderer(흰 사각형) + 트리거 BoxCollider2D를 부착한 뒤 T 컴포넌트를 붙여 반환한다.
        /// </summary>
        private static T CreateNpcObject<T>(string name, Vector2 position, Vector2 scale, Color color) where T : Component
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
            sr.color = color;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            return go.AddComponent<T>();
        }

        /// <summary>Wire*Npc 공통 꼬리 — promptKey 필드에 문자열 키를 설정하고 SerializedObject를 적용한다.</summary>
        private static void ApplyNpcPrompt(SerializedObject so, string promptKey)
        {
            var p = so.FindProperty("promptKey");
            if (p != null) p.stringValue = promptKey;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
