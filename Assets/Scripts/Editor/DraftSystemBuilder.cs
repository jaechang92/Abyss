#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 현재 씬에 Draft 시스템 GameObject를 생성·연결.
    /// 메뉴 경로는 <see cref="AbyssMenu.BuildDraftSystem"/>.
    /// - Draft GameObject (DraftSessionController + DraftPoolManager) 생성
    /// - 모든 SkillData 에셋을 pool에 자동 할당
    /// - 씬의 PlayerCharacter에서 FormController 탐색해 session.formController 연결
    /// - 씬의 HUDPresenter 발견 시 Draft Session 필드에 역참조 연결
    /// - 씬의 DraftPanelPresenter 발견 시 session 필드에 역참조 연결
    /// </summary>
    public static class DraftSystemBuilder
    {
        private const string DraftGameObjectName = "Draft";
        private const string UndoLabel = "Setup Draft System";

        [MenuItem(AbyssMenu.BuildDraftSystem)]
        public static void Setup()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "DraftSystemBuilder",
                "Draft GameObject 생성 + SkillData 풀 할당 + HUD/DraftPanel 역참조 연결.\n" +
                "이미 있는 Draft GameObject는 그대로 두고 필드만 갱신합니다.",
                "실행", "취소");
            if (!proceed) return;

            var sessionGo = FindOrCreateDraftGameObject();
            var pool = sessionGo.GetComponent<DraftPoolManager>();
            var session = sessionGo.GetComponent<DraftSessionController>();

            AssignSkillPool(pool);
            LinkSessionReferences(session);
            LinkConsumers(session);

            EditorUtility.SetDirty(sessionGo);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sessionGo.scene);
            }

            Selection.activeGameObject = sessionGo;
            Debug.Log($"[DraftSystemBuilder] 완료 — '{sessionGo.name}' 에 DraftPoolManager/DraftSessionController 구성됨.");
        }

        private static GameObject FindOrCreateDraftGameObject()
        {
            var existing = GameObject.Find(DraftGameObjectName);
            if (existing != null && existing.GetComponent<DraftSessionController>() != null)
            {
                return existing;
            }

            var go = new GameObject(DraftGameObjectName);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.AddComponent<DraftPoolManager>();
            go.AddComponent<DraftSessionController>();
            Debug.Log($"[DraftSystemBuilder] '{DraftGameObjectName}' GameObject 생성");
            return go;
        }

        private static void AssignSkillPool(DraftPoolManager pool)
        {
            if (pool == null) return;

            string[] guids = AssetDatabase.FindAssets("t:SkillData");
            var skills = new List<SkillData>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (data != null) skills.Add(data);
            }

            var so = new SerializedObject(pool);
            var prop = so.FindProperty("pool");
            if (prop != null)
            {
                prop.arraySize = skills.Count;
                for (int i = 0; i < skills.Count; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
                }
                so.ApplyModifiedProperties();
                Debug.Log($"[DraftSystemBuilder] SkillData {skills.Count}개 pool에 할당");
            }
        }

        private static void LinkSessionReferences(DraftSessionController session)
        {
            if (session == null) return;

            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            var formController = player != null ? player.GetComponent<FormController>() : null;

            if (formController == null)
            {
                Debug.LogWarning("[DraftSystemBuilder] 씬에 FormController 없음 — DraftSessionController.formController 미연결");
                return;
            }

            var so = new SerializedObject(session);
            var prop = so.FindProperty("formController");
            if (prop != null)
            {
                prop.objectReferenceValue = formController;
                so.ApplyModifiedProperties();
                Debug.Log("[DraftSystemBuilder] session.formController → 씬 Player 연결");
            }
        }

        private static void LinkConsumers(DraftSessionController session)
        {
            if (session == null) return;

            var hud = Object.FindAnyObjectByType<Abyss.Runtime.UI.HUDPresenter>();
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                var prop = so.FindProperty("draftSession");
                if (prop != null)
                {
                    prop.objectReferenceValue = session;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(hud);
                    Debug.Log("[DraftSystemBuilder] HUDPresenter.draftSession → Draft 연결");
                }
            }

            var draftPanel = Object.FindAnyObjectByType<Abyss.Runtime.UI.DraftPanelPresenter>();
            if (draftPanel != null)
            {
                var so = new SerializedObject(draftPanel);
                var prop = so.FindProperty("session");
                if (prop != null)
                {
                    prop.objectReferenceValue = session;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(draftPanel);
                    Debug.Log("[DraftSystemBuilder] DraftPanelPresenter.session → Draft 연결");
                }
            }
        }
    }
}
#endif
