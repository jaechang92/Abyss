namespace Abyss.Runtime.Stage
{
    /// <summary>스테이지 환경 표현의 세 상태. <see cref="Hidden"/> 이면 그 스테이지의 그림이 화면에 하나도 없어야 한다.</summary>
    public enum StageEnvironmentLook
    {
        /// <summary>이 스테이지 방이 아니다 — 그레이박스(공용 Run 씬 기본 모습)로 돌아간다.</summary>
        Hidden,
        /// <summary>이 스테이지의 일반 방(전투·이벤트·상점·휴식) — 공용 배경 + 지면·발판.</summary>
        Field,
        /// <summary>이 스테이지의 보스 방 — 보스 방 배경 + 지면·발판.</summary>
        BossArena,
    }

    /// <summary>
    /// 들어간 방으로 스테이지 환경 표현을 정한다. MonoBehaviour 가 아니다 — EditMode 에서 규칙만 고정하려는 것이다.
    ///
    /// 🔴 <b>판정 기준은 방 이름이 아니라 StageData 의 선택지 목록이다.</b> Run 은 S1~S3 공용 씬이라
    /// 「Stage1 방인가」를 이름 규칙(Room1_…/Stage2_…)으로 가르면 방을 추가할 때 조용히 틀린다.
    /// 진행 순서의 SoT(<see cref="StageData.steps"/>)를 그대로 읽는다.
    /// </summary>
    public static class StageEnvironmentRule
    {
        /// <summary>
        /// <paramref name="stage"/> 의 표현을 <paramref name="enteredRoom"/> 에서 어떻게 보일지.
        /// 스테이지 밖의 방·null 은 전부 <see cref="StageEnvironmentLook.Hidden"/>.
        /// 보스 방 배경은 <paramref name="bossRoom"/> 과 <b>같은 방</b>일 때만 — 다른 스테이지 보스 방에는 안 붙는다.
        /// </summary>
        public static StageEnvironmentLook Resolve(StageData stage, RoomData bossRoom, RoomData enteredRoom)
        {
            if (stage == null || enteredRoom == null) return StageEnvironmentLook.Hidden;
            if (!Contains(stage, enteredRoom)) return StageEnvironmentLook.Hidden;
            return bossRoom != null && enteredRoom == bossRoom
                ? StageEnvironmentLook.BossArena
                : StageEnvironmentLook.Field;
        }

        /// <summary>방이 이 스테이지의 어느 단계 선택지에라도 있는가(분기 선택지 포함).</summary>
        public static bool Contains(StageData stage, RoomData room)
        {
            if (stage == null || room == null || stage.steps == null) return false;

            foreach (var step in stage.steps)
            {
                if (step?.options == null) continue;
                foreach (var option in step.options)
                {
                    if (option == room) return true;
                }
            }
            return false;
        }

        /// <summary>공용 층(하늘·지면·발판 스킨)을 켜는가. 보스 방도 지면·발판은 같다.</summary>
        public static bool ShowsShared(StageEnvironmentLook look) => look != StageEnvironmentLook.Hidden;

        /// <summary>일반 방 배경(먼 층·가까운 층)을 켜는가.</summary>
        public static bool ShowsField(StageEnvironmentLook look) => look == StageEnvironmentLook.Field;

        /// <summary>보스 방 배경을 켜는가.</summary>
        public static bool ShowsBossArena(StageEnvironmentLook look) => look == StageEnvironmentLook.BossArena;

        /// <summary>그레이박스 렌더러(흰 사각 지면·발판)를 켜는가 — 스킨이 꺼지면 반드시 돌아와야 한다.</summary>
        public static bool ShowsGraybox(StageEnvironmentLook look) => look == StageEnvironmentLook.Hidden;
    }
}
