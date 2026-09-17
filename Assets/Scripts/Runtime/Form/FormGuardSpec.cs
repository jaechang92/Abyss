using System;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 폼의 가드 설정. 켜진 폼은 <b>강공격 키를 누르고 있으면 가드</b>하고, 강공격은 가드 성공 시 자동 반격으로만 나간다.
    /// 기획: <c>Docs/game-design/16-shield-guard.md</c>.
    ///
    /// 🔑 공격 방식(<see cref="FormAttackStyle"/>)과 같은 이유로 무기가 아니라 <b>폼</b>이 갖는다.
    /// 기존 폼 에셋은 필드가 없어 <see cref="isEnabled"/> = false 로 읽힌다 — X 는 그대로 강공격이다.
    /// </summary>
    [Serializable]
    public struct FormGuardSpec
    {
        [Tooltip("켜면 강공격 키 = 가드(누르고 있기). 끄면 강공격 그대로")]
        public bool isEnabled;

        [Tooltip("일반 가드(저스트 창 밖) 피해 배율. 최소 1 은 남는다")]
        [Range(0f, 1f)] public float holdDamageScale;

        [Tooltip("누른 순간부터 저스트 가드가 되는 시간(초). 이 안에 막으면 피해 0")]
        [Min(0f)] public float justGuardWindow;

        [Tooltip("뗀 뒤 이 시간(초) 안에 다시 누르면 저스트 창이 안 열린다(일반 가드는 된다). 연타 방지")]
        [Min(0f)] public float justGuardRearmDelay;

        [Tooltip("자동 반격 피해 = 강공격 피해(세 층 배율 반영) × 이 값")]
        [Min(0f)] public float counterDamageScale;
    }
}
