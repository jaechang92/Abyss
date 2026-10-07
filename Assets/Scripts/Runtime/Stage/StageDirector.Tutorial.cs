using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>첫 방 전용 안전 학습. 예약된 첫 무리도 아직 끝나지 않은 전투다.</summary>
    public sealed partial class StageDirector
    {
        public const float TUTORIAL_COMBAT_X = -2f;
        private bool isTutorialOpeningReserved;
        private int tutorialRoomVersion;
        private bool isTutorialReleaseRequested;
        private GameObject tutorialGeometry;

        public bool IsTutorialRoom => currentRoom != null && currentRoom.roomId == "room1_intro"
            && currentStageIndex == 0 && currentStepIndex == 0 && !isRoomSessionClosed;
        public int TutorialCombatKills { get; private set; }
        public int TutorialRoomVersion => roomEntryVersion;

        private void PrepareTutorialRoom(RoomData room)
        {
            ResetTutorialRoom();
            if (!IsTutorialRoom || !room.IsMapRoom) return;
            var meta = MetaSaveService.GetInstanceSafe();
            if (meta == null || !meta.ShouldShowFirstPlayTutorial || !FirstPlayTutorialController.HasActiveInstance) return;

            tutorialRoomVersion = roomEntryVersion;
            isTutorialOpeningReserved = true;
            tutorialGeometry = new GameObject("FirstRoomLearningGeometry");
            // 기존 테라스 위의 낮은 턱. 낙사 틈이나 입력 잠금을 만들지 않는다.
            float floor = ProbeFloorY(-12f);
            var ledge = CreateTutorialShape("JumpLedge", new Vector2(-12f, floor + 0.375f), new Vector2(1.5f, 0.75f));
            ledge.layer = LayerMask.NameToLayer("Ground");
            ledge.AddComponent<BoxCollider2D>();
            CreateTutorialShape("MoveGoal", new Vector2(-14f, ProbeFloorY(-14f) + 0.05f), new Vector2(0.6f, 0.1f));
            CreateTutorialShape("DashStart", new Vector2(-9.2f, ProbeFloorY(-9.2f) + 0.05f), new Vector2(0.5f, 0.1f));
            CreateTutorialShape("DashGoal", new Vector2(-6.8f, ProbeFloorY(-6.8f) + 0.05f), new Vector2(0.5f, 0.1f));
        }

        private GameObject CreateTutorialShape(string shapeName, Vector2 position, Vector2 size)
        {
            var go = new GameObject(shapeName);
            go.transform.SetParent(tutorialGeometry.transform, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetTutorialSquare();
            renderer.color = new Color(0.65f, 0.48f, 0.24f);
            renderer.sortingOrder = 5;
            return go;
        }

        private static Sprite tutorialSquare;
        private static Sprite GetTutorialSquare()
        {
            if (tutorialSquare == null)
                tutorialSquare = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width,
                    Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
            return tutorialSquare;
        }

        private void TickTutorialOpening()
        {
            if (!isTutorialOpeningReserved || tutorialRoomVersion != roomEntryVersion || IsWorldInputBlocked) return;
            var actor = ResolvePlayer();
            if (actor != null && (isTutorialReleaseRequested || actor.transform.position.x >= TUTORIAL_COMBAT_X || !FirstPlayTutorialController.HasActiveInstance))
                ReleaseTutorialOpening();
        }

        /// <summary>스킵/학습 종료는 적을 한 번 활성화할 뿐 보상·클리어를 해결하지 않는다.</summary>
        public void ReleaseTutorialOpening()
        {
            if (!isTutorialOpeningReserved || tutorialRoomVersion != roomEntryVersion || isRoomSessionClosed) return;
            isTutorialReleaseRequested = true;
            if (IsWorldInputBlocked || !isActiveAndEnabled) return;
            isTutorialOpeningReserved = false;
            if (tutorialGeometry != null)
                foreach (Transform child in tutorialGeometry.transform)
                    if (child.name != "JumpLedge") child.gameObject.SetActive(false);
            SpawnEnemies(currentRoom);
        }

        private void ResetTutorialRoom()
        {
            isTutorialOpeningReserved = false;
            isTutorialReleaseRequested = false;
            TutorialCombatKills = 0;
            if (tutorialGeometry != null)
            {
                tutorialGeometry.SetActive(false);
                Destroy(tutorialGeometry);
            }
            tutorialGeometry = null;
        }
    }
}
