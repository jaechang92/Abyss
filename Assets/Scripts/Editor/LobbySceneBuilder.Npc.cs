#if UNITY_EDITOR
using Abyss.Runtime.Form;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Player;
using FSM.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using static Abyss.EditorTools.SceneBuilderUtil;

namespace Abyss.EditorTools
{
    /// <summary>
    /// LobbySceneBuilder의 로비 캐릭터(플레이어·NPC/포털) 생성 부분.
    /// <list type="bullet">
    /// <item>플레이어 — Run 의 <c>Player.prefab</c>과 <b>같은 PlayerCharacter·FSM·폼 컨트롤러</b>로 만든다(2026-10-12).</item>
    /// <item>NPC·포털 — "GameObject + 스프라이트 + 트리거 콜라이더 + 컴포넌트" 생성 패턴과 promptKey 꼬리.</item>
    /// </list>
    /// (메인 LobbySceneBuilder.cs와 같은 partial 클래스라 SetObject 등 헬퍼를 공유한다.)
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        private const string RunPlayerPrefabPath = AbyssPaths.PlayerPrefabs + "/Player.prefab";

        /// <summary>
        /// 로비 플레이어 생성. <b>Run 의 Player.prefab 을 기준 자료로 읽어 같은 구성·같은 수치로</b> 만든다 —
        /// 사용자 지정: 로비와 Run 은 같은 캐릭터다. 이동·점프 횟수·대시·폼 이동 배율·FSM·애니메이션 경로가 전부
        /// 공유 컴포넌트에서 나오고, 로비 전용 차이는 <see cref="PlayerContext.Lobby"/>(런 기록·보상 차단) 하나다.
        ///
        /// 프리팹을 인스턴스로 두지 않는 이유: 프리팹에 로비 씬 의존 필드를 넣지 않고, NPC 가 들고 있는
        /// <see cref="LobbyPlayerController"/> 어댑터·로비 그림(<see cref="LobbyFormVisual"/>)을 씬 오브젝트로 유지하기 위해서다.
        /// 수치는 <see cref="EditorUtility.CopySerialized"/>로 프리팹에서 옮기고, 오브젝트 참조만 이 몸으로 다시 잇는다.
        ///
        /// <b>원점 = 발밑</b>(Run 과 같다). 폼은 시작 폼 하나만 든다 — 로비에 두 번째 폼을 지급하지 않는다.
        /// </summary>
        private static GameObject CreatePlayer(
            FormData defaultForm, out LobbyPlayerController controller, out PlayerInteractor interactor)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RunPlayerPrefabPath);
            if (source == null)
            {
                Debug.LogError($"[LobbySceneBuilder] {RunPlayerPrefabPath} 없음 — 로비 플레이어 수치를 Run 과 맞출 수 없다. 코드 기본값으로 만든다.");
            }

            var go = new GameObject("LobbyPlayer");
            // 바닥 바로 위에서 시작해 한 프레임 만에 내려앉는다. 정확히 바닥면에 두면 시작부터 겹친다.
            go.transform.position = new Vector3(0f, FloorTopY + 0.1f, 0f);

            var body = AddCopied<Rigidbody2D>(go, source);
            var box = AddCopied<BoxCollider2D>(go, source);
            var trigger = AddCopied<CircleCollider2D>(go, source);   // 상호작용 트리거(Run 과 같은 반경)
            if (source == null) ApplyFallbackBody(body, box, trigger);

            AddCopied<StateMachine>(go, source);
            var form = go.AddComponent<FormController>();          // 슬롯은 복사하지 않는다 — 시작 폼 하나만
            var input = go.AddComponent<PlayerInput>();
            ConfigurePlayerInput(input);
            var player = AddCopied<PlayerCharacter>(go, source);
            var fsm = AddCopied<PlayerStateMachine>(go, source);
            var animationBinder = go.AddComponent<PlayerAnimationBinder>();
            controller = go.AddComponent<LobbyPlayerController>();
            interactor = go.AddComponent<PlayerInteractor>();

            // 자식 순서: Visual 이 첫째다 — 귀환 연출 등이 GetComponentInChildren<SpriteRenderer>로 몸 그림을 찾는다.
            var driver = CreatePlayerVisual(go.transform, defaultForm, form);
            var groundCheck = CreateChildLike(go.transform, source, "GroundCheck", new Vector3(0f, -0.06f, 0f));
            var attackPoint = CreateChildLike(go.transform, source, "AttackPoint", new Vector3(0.9f, 1f, 0f));
            var attackEffect = CreateAttackEffect(attackPoint, source);

            WireLobbyPlayer(player, body, form, fsm, groundCheck, attackPoint, attackEffect);
            WireFormSlots(form, defaultForm);

            var fsmSo = new SerializedObject(fsm);
            SetObject(fsmSo, "player", player);
            SetObject(fsmSo, "formController", form);
            fsmSo.ApplyModifiedProperties();

            var binderSo = new SerializedObject(animationBinder);
            SetObject(binderSo, "stateMachine", fsm);
            SetObject(binderSo, "animatorDriver", driver);
            binderSo.ApplyModifiedProperties();

            var controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "player", player);
            controllerSo.ApplyModifiedProperties();

            return go;
        }

        /// <summary>컴포넌트를 붙이고 Run 프리팹의 같은 컴포넌트 값을 그대로 옮긴다. 원본이 없으면 기본값으로 둔다.</summary>
        private static T AddCopied<T>(GameObject go, GameObject source) where T : Component
        {
            var component = go.AddComponent<T>();
            var original = source != null ? source.GetComponent<T>() : null;
            if (original != null) EditorUtility.CopySerialized(original, component);
            return component;
        }

        private static void ApplyFallbackBody(Rigidbody2D body, BoxCollider2D box, CircleCollider2D trigger)
        {
            body.gravityScale = 3f;
            body.freezeRotation = true;
            box.size = new Vector2(1f, 2f);
            box.offset = new Vector2(0f, 1f);
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
        }

        /// <summary>Run 프리팹의 같은 이름 자식과 같은 로컬 배치의 빈 자식. 원본이 없으면 <paramref name="fallback"/>.</summary>
        private static Transform CreateChildLike(Transform parent, GameObject source, string name, Vector3 fallback)
        {
            var original = source != null ? source.transform.Find(name) : null;
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = original != null ? original.localPosition : fallback;
            child.localScale = original != null ? original.localScale : Vector3.one;
            return child;
        }

        /// <summary>공격 이펙트 자식 — Run 프리팹의 AttackPoint/AttackEffect 를 같은 값으로 옮긴다(평소 꺼져 있다).</summary>
        private static AttackEffect CreateAttackEffect(Transform attackPoint, GameObject source)
        {
            var original = source != null ? source.transform.Find("AttackPoint/AttackEffect") : null;
            var go = new GameObject("AttackEffect");
            go.transform.SetParent(attackPoint, false);
            go.transform.localScale = original != null ? original.localScale : new Vector3(1.44f, 0.9f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            var originalSr = original != null ? original.GetComponent<SpriteRenderer>() : null;
            if (originalSr != null) EditorUtility.CopySerialized(originalSr, sr);
            else sr.enabled = false;

            var effect = go.AddComponent<AttackEffect>();
            var originalEffect = original != null ? original.GetComponent<AttackEffect>() : null;
            if (originalEffect != null) EditorUtility.CopySerialized(originalEffect, effect);
            var so = new SerializedObject(effect);
            SetObject(so, "sr", sr);   // 복사된 참조는 프리팹 안을 가리킨다 — 이 몸의 렌더러로 다시 잇는다
            so.ApplyModifiedProperties();
            return effect;
        }

        /// <summary>
        /// 공유 PlayerCharacter 의 오브젝트 참조를 이 몸으로 다시 잇고 맥락을 로비로 둔다.
        /// CopySerialized 는 참조까지 복사하므로(프리팹 내부를 가리킨다) 여기서 전부 덮는다.
        /// </summary>
        private static void WireLobbyPlayer(PlayerCharacter player, Rigidbody2D body, FormController form,
            PlayerStateMachine fsm, Transform groundCheck, Transform attackPoint, AttackEffect attackEffect)
        {
            var so = new SerializedObject(player);
            SetObject(so, "body", body);
            SetObject(so, "formController", form);
            SetObject(so, "stateMachine", fsm);
            SetObject(so, "groundCheck", groundCheck);
            SetObject(so, "attackPoint", attackPoint);
            SetObject(so, "attackEffect", attackEffect);
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null) layerProp.intValue = 1 << EditorPlatformFactory.GetGroundLayer();
            var contextProp = so.FindProperty("context");
            if (contextProp != null) contextProp.intValue = (int)PlayerContext.Lobby;
            else Debug.LogError("[LobbySceneBuilder] PlayerCharacter.context 필드 없음 — 로비 맥락을 못 둬 런 기록이 남을 수 있다.");
            so.ApplyModifiedProperties();
        }

        /// <summary>시작 폼 하나만 든다(slot0). 각인사 선택은 런타임에 LobbyPlayerController 가 같은 슬롯에 꽂는다.</summary>
        private static void WireFormSlots(FormController form, FormData defaultForm)
        {
            var so = new SerializedObject(form);
            var slots = so.FindProperty("slots");
            if (slots != null)
            {
                slots.arraySize = 2;
                slots.GetArrayElementAtIndex(0).objectReferenceValue = defaultForm;
                slots.GetArrayElementAtIndex(1).objectReferenceValue = null;
            }
            var active = so.FindProperty("activeSlot");
            if (active != null) active.intValue = 0;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// 그림을 담는 Visual 자식. Run 의 Player.prefab 과 같은 자리·같은 재생기 구성이다.
        /// 그림 writer 는 <see cref="LobbyFormVisual"/>(출처 = 공유 폼 컨트롤러), 상태별 재생은 공유 PlayerAnimationBinder 다.
        /// </summary>
        private static Anim.Core.AnimatorDriver CreatePlayerVisual(Transform parent, FormData defaultForm, FormController form)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            // 초기값은 예전 그대로. LobbyFormVisual 이 이것을 폴백으로 집어 가므로
            // 그림 없는 폼이 생겨도 화면에서 사라지지 않는다.
            sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
            sr.color = new Color(0.4f, 0.7f, 1f);

            // 폼의 오버라이드 컨트롤러는 LobbyFormVisual 이 폼을 적용할 때 AnimatorDriver.SetController 로 꽂는다.
            var animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
            var driver = go.AddComponent<Anim.Core.AnimatorDriver>();
            var driverSo = new SerializedObject(driver);
            SetObject(driverSo, "animator", animator);
            driverSo.ApplyModifiedProperties();

            var visual = go.AddComponent<LobbyFormVisual>();
            var so = new SerializedObject(visual);
            SetObject(so, "target", sr);
            SetObject(so, "defaultForm", defaultForm);
            SetObject(so, "animatorDriver", driver);
            SetObject(so, "formController", form);
            so.ApplyModifiedProperties();

            if (defaultForm == null)
            {
                Debug.LogWarning("[LobbySceneBuilder] 기본 폼 미해석 — 시작 폼을 고르기 전까지 로비 캐릭터가 흰 사각형으로 남는다.");
            }
            return driver;
        }

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
