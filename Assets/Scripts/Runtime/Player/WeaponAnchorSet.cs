using System;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 프레임마다 무기가 어디에 어떤 각도로 놓이는가. 폼 한 벌분을 담는다.
    ///
    /// 🔴 <b>클립에 커브로 굽지 않고 별도 에셋에 둔 이유</b>:
    /// <c>PlayerAnimationBuilder</c>가 클립을 <see cref="UnityEditor.AssetDatabase.DeleteAsset"/> 후
    /// 다시 만든다. 실제로 다시 돌린 적이 있고(Bug-039 재슬라이스), 그때 손으로 다듬은 값이
    /// 클립 안에 있었다면 통째로 날아갔다. 앵커는 <b>클립 재생성과 독립</b>이어야 한다.
    ///
    /// 🔑 <b>각도를 회전으로 굽지 않고 값으로 들고 있는 이유</b>:
    /// 무기 회전을 지금은 <c>Transform</c>으로 돌리지만, 어색하면 <b>각도별로 새로 그린 그림</b>으로
    /// 바꿀 예정이다(사용자 결정 2026-09-14). 둘의 입력이 <see cref="WeaponAnchorFrame.angle"/>로
    /// 같으므로 <b>데이터를 다시 안 뽑고 렌더러만 갈아끼운다.</b>
    ///
    /// 원본은 <c>Tools/ArtPipeline/hand_anchors.py</c> 가 낸 JSON이고,
    /// 규약은 <c>Docs/technical/weapon-attachment.md</c> 에 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponAnchorSet", menuName = "Abyss/Data/Weapon Anchor Set")]
    public sealed class WeaponAnchorSet : ScriptableObject
    {
        [Header("식별자")]
        [Tooltip("이 앵커가 붙는 폼. FormData.formId 와 같아야 한다.")]
        public string formId;

        [Tooltip("애니메이션별 앵커. 키는 PlayerAnimationIds 의 상수(=Animator 상태 이름)다.")]
        public WeaponAnchorClip[] clips = Array.Empty<WeaponAnchorClip>();

        /// <summary>
        /// <paramref name="animationId"/> 의 <paramref name="normalizedTime"/> 지점 앵커.
        /// 없으면 false — 호출자는 <b>무기를 숨긴다</b>(잘못된 자리에 박힌 것보다 없는 게 낫다).
        /// </summary>
        public bool TrySample(string animationId, float normalizedTime, out WeaponAnchorFrame frame)
        {
            frame = default;
            WeaponAnchorClip clip = FindClip(animationId);
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return false;

            frame = clip.frames[FrameIndexOf(normalizedTime, clip.frames.Length, clip.isLooping)];
            frame.position += clip.handOffset;
            return true;
        }

        public WeaponAnchorClip FindClip(string animationId)
        {
            if (string.IsNullOrEmpty(animationId) || clips == null) return null;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].animationId == animationId) return clips[i];
            }
            return null;
        }

        /// <summary>
        /// 재생 진행도 → 프레임 번호. <b>Unity 타입을 안 쓰는 순수 함수</b>라
        /// <c>Animator</c> 없이 테스트한다(<c>AnimationChainResolver</c>와 같은 태도).
        ///
        /// ⚠️ 루프와 한 번 재생이 <b>끝에서 갈린다</b> — 루프는 되감고(<c>1.0</c> → 0번),
        /// 한 번 재생은 마지막 프레임에 머문다. 루프를 물리면 공격 끝에 그림이 처음으로 튄다.
        /// </summary>
        public static int FrameIndexOf(float normalizedTime, int frameCount, bool isLooping)
        {
            if (frameCount <= 1) return 0;

            float t = isLooping ? normalizedTime - Mathf.Floor(normalizedTime)
                                : Mathf.Clamp01(normalizedTime);
            int index = Mathf.FloorToInt(t * frameCount);
            return Mathf.Clamp(index, 0, frameCount - 1);
        }
    }

    /// <summary>한 애니메이션의 앵커 묶음.</summary>
    [Serializable]
    public sealed class WeaponAnchorClip
    {
        [Tooltip("PlayerAnimationIds 의 상수. Animator 상태 이름과 같다.")]
        public string animationId;

        [Tooltip("루프 클립인가. 끝에서 되감을지 마지막 프레임에 머물지를 가른다.")]
        public bool isLooping;

        [Tooltip("체크 시 이 애니메이션의 앵커는 손으로 잡아야 한다. 검출이 키를 하나도 못 얻었다는 뜻.")]
        public bool needsManual;

        /// <summary>
        /// 프레임 전체를 같은 만큼 옮긴다.
        ///
        /// 🔴 <b>검출은 앞쪽 손만 잡는다.</b> 뒤쪽 손은 몸통에 가려 실루엣에 돌출로 안 나오고
        /// 뒤쪽 최외곽은 망토다 — 원리상 한계지 파라미터 문제가 아니다. 그런데 이 캐릭터는
        /// <b>무장 원본이 뒤쪽 손으로 검을 쥐고 있다</b>(자루 x≈41, 검출 x≈55~60. 칸 중심은 46).
        /// 그래서 검출값을 그대로 쓰면 반대 손에 붙는다.
        ///
        /// 🔑 <b>프레임별로 맞추지 않고 클립 하나에 한 값만 둔다</b> — 프레임마다 다르게 주면
        /// 무기가 떤다(공중 루프에서 발끝을 일괄로 내린 것과 같은 이유).
        ///
        /// 시작값 <b>(-0.447, -0.176)</b>: 무장/비무장 정지본에서 자루와 앞손 검출의 차이를 잰 값이다.
        /// 두 정지본은 자세가 조금 다르므로 <b>출발점일 뿐</b>이고, 에디터 미리보기에서 최종으로 맞춘다.
        /// </summary>
        [Tooltip("이 클립의 앵커 전체를 평행이동한다. 뒤쪽 손으로 옮길 때 쓴다.")]
        public Vector2 handOffset;

        public WeaponAnchorFrame[] frames = Array.Empty<WeaponAnchorFrame>();
    }

    /// <summary>프레임 하나의 무기 배치.</summary>
    [Serializable]
    public struct WeaponAnchorFrame
    {
        [Tooltip("스프라이트 피벗(발밑) 기준 지역 좌표. 단위는 유닛.")]
        public Vector2 position;

        [Tooltip("무기 기울기(도). 지금은 Transform 회전으로 쓰지만 각도별 그림으로 바뀔 수 있다.")]
        public float angle;

        [Tooltip("몸 앞에 그릴까. 끄면 몸 뒤 — 팔 뒤로 지나가는 검이 된다.")]
        public bool isInFront;

        [Tooltip("검출이 실제로 잡은 프레임인가. 끄면 앞뒤 키의 보간값이라 추정이다.")]
        public bool isKey;
    }
}
