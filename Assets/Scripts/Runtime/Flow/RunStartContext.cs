using Abyss.Runtime.Form;

namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 로비 → Run 씬 전환 시 선택한 시작 폼을 전달하는 정적 컨텍스트.
    /// 정적 필드라 씬 전환(Single 로드)에도 값이 유지된다(FormData는 에셋이라 파괴되지 않음).
    /// Run 진입 시 FormController가 읽어 활성 슬롯에 주입/선택한다.
    /// 1회성 소비 대신 마지막 선택을 보유 — Run 재시작 시에도 직전 폼이 유지되도록.
    /// 미설정(직접 Run 플레이/최초) 시 StartingForm은 null → 씬 기본 활성 슬롯 유지.
    /// </summary>
    public static class RunStartContext
    {
        /// <summary>로비에서 선택한 시작 폼 에셋. 비어 있으면 미선택.</summary>
        public static FormData StartingForm { get; set; }

        /// <summary>선택한 시작 폼의 formId. 미선택 시 null(읽기 전용 파생값).</summary>
        public static string StartingFormId => StartingForm != null ? StartingForm.formId : null;

        public static bool HasStartingForm => StartingForm != null;

        /// <summary>명시적 리셋(필요 시). 일반 흐름에서는 호출하지 않는다.</summary>
        public static void Clear() => StartingForm = null;
    }
}
