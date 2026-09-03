using Abyss.Runtime.Form;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 로비 캐릭터가 <b>런에서 실제로 시작할 폼</b>을 보여주는지 지탱하는 불변식 테스트.
    ///
    /// 배경: 로비 캐릭터가 흰 사각형이던 시절에는 폼 선택 패널의 기본 강조(목록 첫 폼)와
    /// 런의 실제 시작 폼(Player 프리팹 <c>slots[activeSlot]</c>)이 <b>달라도 아무도 못 봤다.</b>
    /// 정체 없는 사각형에는 어긋날 표시 자체가 없었기 때문이다. 실제 폼 그림을 세우는 순간
    /// "패널은 A 를 강조하는데 서 있는 건 B"가 보인다.
    ///
    /// 그래서 로비 빌더는 기본 폼을 목록 순서가 아니라 <b>프리팹에서</b> 읽는다
    /// (<c>LobbySceneBuilder.ResolveDefaultForm</c>). 여기서는 그 판단이 딛고 선
    /// 전제 — "프리팹에 시작 폼이 있고, 그것이 카탈로그에 있다" — 를 고정한다.
    /// 빌더 자체는 Editor 어셈블리라 이 테스트에서 부를 수 없다.
    /// </summary>
    public sealed class LobbyFormDefaultTests
    {
        private const string PLAYER_PREFAB = "Assets/Prefabs/Player/Player.prefab";

        private static FormData PrefabStartingForm()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            Assert.IsNotNull(prefab, $"{PLAYER_PREFAB} 미발견 — 프리팹 빌더를 먼저 실행할 것.");

            var controller = prefab.GetComponentInChildren<FormController>(true);
            Assert.IsNotNull(controller, "Player 프리팹에 FormController 가 없다.");
            return controller.CurrentForm;
        }

        /// <summary>
        /// 비어 있으면 로비는 보여 줄 폼을 못 찾아 흰 사각형으로 남고, 런은 폼 없이 시작한다.
        /// 둘 다 오류 없이 조용히 어긋나는 종류라 여기서 잡는다.
        /// </summary>
        [Test]
        public void 런_시작_폼은_비어있지_않다()
        {
            Assert.IsNotNull(PrefabStartingForm(), "Player 프리팹의 활성 슬롯이 비어 있다.");
        }

        /// <summary>
        /// 폼 선택 패널은 <b>카탈로그에서 온 목록</b>에서 같은 formId 를 찾아 강조한다.
        /// 프리팹의 시작 폼이 카탈로그 밖 에셋이면 찾지 못해 첫 폼으로 되돌아가고,
        /// 로비 캐릭터와 강조된 버튼이 다시 갈라진다.
        /// </summary>
        [Test]
        public void 런_시작_폼은_폼_카탈로그에_있다()
        {
            var form = PrefabStartingForm();
            Assert.IsNotNull(form);
            Assert.AreSame(form, FormCatalog.GetById(form.formId),
                $"'{form.formId}' 가 Resources/Data/Forms 의 같은 에셋이 아니다 — 폼 선택 패널이 못 찾는다.");
        }
    }
}
