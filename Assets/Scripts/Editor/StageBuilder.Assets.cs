#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// <see cref="StageBuilder"/>의 SO 생성·갱신 헬퍼 파트.
    ///
    /// 상점 룸(1-2)이 더해지며 한 파일 500줄을 넘겨 분할했다(HudBuilder.Modals 선례).
    /// 경계는 "메뉴 진입점(어떤 방을 어디에 둘 것인가)" 대 "에셋 입출력(어떻게 만들고 갱신할 것인가)"이다 —
    /// 콘텐츠 구성이 바뀔 때 손대는 곳과 규약이 바뀔 때 손대는 곳이 갈린다.
    ///
    /// 공통 규약: 이미 존재하는 에셋은 <b>전체를 덮어쓰지 않는다</b>(인스펙터에서 만진 수치를
    /// 빌더 재실행이 되돌리면 안 된다). 다만 방 배치처럼 데이터 구동인 항목은 재실행으로 갱신한다.
    /// </summary>
    public static partial class StageBuilder
    {
        private static EnemyData LoadEnemyData(string fileName)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{AbyssPaths.Enemies}/{fileName}.asset");
            if (data == null) Debug.LogError($"[StageBuilder] EnemyData 누락: {AbyssPaths.Enemies}/{fileName}.asset");
            return data;
        }

        private static RoomData CreateOrLoadRoom(
            string fileName, RoomType roomType, int goldReward, (EnemyData data, int count)[] entries,
            bool hasFormReward = false, string displayName = null, string hint = null)
        {
            string path = $"{AbyssPaths.Rooms}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                // 기존 룸은 대부분 보존하되, 보상 룸 지정은 데이터 구동이므로 재실행 시 반영한다.
                // 레거시 고정 폼(formReward)은 비워 StageDirector의 미보유 우선 추첨으로 넘긴다.
                if (existing.hasFormReward != hasFormReward || existing.formReward != null)
                {
                    existing.hasFormReward = hasFormReward;
                    existing.formReward = null;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] 폼 보상 룸 갱신: {path} → {(hasFormReward ? "보상 룸(추첨)" : "none")}");
                }
                ApplyChoiceDisplay(existing, path, displayName, hint);
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = roomType;
            so.clearGoldReward = goldReward;
            so.hasFormReward = hasFormReward;
            so.displayName = displayName;
            so.hint = hint;
            so.enemies = entries.Select(e => new EnemySpawnEntry { data = e.data, count = e.count }).ToList();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 분기 선택지 표시 정보를 기존 에셋에 반영한다.
        /// 방 배치가 바뀌면 어떤 방이 선택지로 노출되는지도 바뀌므로 **재실행으로 갱신되어야 한다**
        /// (폼 보상 지정과 같은 규약). 인자가 null이면 손대지 않는다 — 인스펙터에서 손으로 적은 문구를
        /// 빌더가 지우면 안 된다.
        /// </summary>
        private static void ApplyChoiceDisplay(RoomData room, string path, string displayName, string hint)
        {
            bool changed = false;
            if (displayName != null && room.displayName != displayName)
            {
                room.displayName = displayName;
                changed = true;
            }
            if (hint != null && room.hint != hint)
            {
                room.hint = hint;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(room);
                Debug.Log($"[StageBuilder] 선택지 표시 갱신: {path} → \"{room.ChoiceTitle}\" / \"{room.hint}\"");
            }
            else Debug.Log($"[StageBuilder] 건너뜀 (존재): {path}");
        }

        /// <summary>
        /// 이벤트 방. 적을 두지 않는다 — 적이 있으면 전투가 끝나야 이벤트가 열려 '비전투 방'이 아니게 된다.
        /// 클리어 골드도 0이다(보상은 이벤트 선택이 준다).
        ///
        /// 기존 에셋이 있으면 eventData 참조만 갱신한다. 방 배치는 데이터 구동이라 재실행으로 반영되어야
        /// 하지만, 인스펙터에서 다른 값을 만졌을 수 있어 전체를 덮어쓰지는 않는다(폼 보상 룸과 같은 규약).
        /// </summary>
        private static RoomData CreateOrLoadEventRoom(
            string fileName, EventData eventData, RoomType roomType = RoomType.Event,
            string displayName = null, string hint = null)
        {
            string path = $"{AbyssPaths.Rooms}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                if (existing.eventData != eventData || existing.roomType != roomType)
                {
                    existing.eventData = eventData;
                    existing.roomType = roomType;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] {roomType} 룸 갱신: {path} → {(eventData != null ? eventData.eventId : "none")}");
                }
                ApplyChoiceDisplay(existing, path, displayName, hint);
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = roomType;
            so.clearGoldReward = 0;
            so.eventData = eventData;
            so.displayName = displayName;
            so.hint = hint;
            so.enemies = new List<EnemySpawnEntry>();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 상점 방. 이벤트 방과 같은 규약이다 — 적 0마리, 클리어 골드 0(보상은 진열대가 준다),
        /// 기존 에셋이 있으면 참조·타입만 갱신한다.
        ///
        /// 분기 선택지로 노출하지 않으므로 표시명·힌트는 받지 않는다(고정 배치).
        /// </summary>
        private static RoomData CreateOrLoadShopRoom(string fileName, ShopData shopData)
        {
            string path = $"{AbyssPaths.Rooms}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                if (existing.shopData != shopData || existing.roomType != RoomType.Shop)
                {
                    existing.shopData = shopData;
                    existing.roomType = RoomType.Shop;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] 상점 룸 갱신: {path} → {(shopData != null ? shopData.shopId : "none")}");
                }
                else Debug.Log($"[StageBuilder] 건너뜀 (존재): {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = RoomType.Shop;
            so.clearGoldReward = 0;
            so.shopData = shopData;
            so.enemies = new List<EnemySpawnEntry>();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 진행 단계 하나. 방 1개면 고정 진행, 2개 이상이면 갈림길이다.
        /// 호출부에서 <c>Step(a)</c> / <c>Step(a, b)</c>로 읽히도록 params로 받는다.
        /// </summary>
        private static StageStep Step(params RoomData[] options)
        {
            return new StageStep { options = options.Where(r => r != null).ToList() };
        }

        private static StageData CreateOrLoadStage(string fileName, string stageId, string displayName, StageStep[] steps)
        {
            string path = $"{AbyssPaths.Stages}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (existing != null)
            {
                // 기존 StageData에 steps만 갱신 (방 추가·순서 변경·분기 구성 반영)
                existing.stageId = stageId;
                existing.displayName = displayName;
                existing.steps = steps.ToList();
                EditorUtility.SetDirty(existing);
                Debug.Log($"[StageBuilder] steps 갱신: {path} ({steps.Length}단계)");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<StageData>();
            so.stageId = stageId;
            so.displayName = displayName;
            so.steps = steps.ToList();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 디스크에 존재하는 스테이지 에셋을 <see cref="StageBuilder.StageFilesInOrder"/> 순서대로 모아
        /// 시퀀스를 갱신한다.
        ///
        /// <b>인자로 받지 않고 경로에서 읽는 이유</b>: 메뉴를 어떤 순서로 실행하든 결과가 같아야 한다.
        /// 이전에는 Stage2 빌더가 <c>(stage1, stage2)</c>를 넘겼는데, 그 방식이면 Stage3을 만든 뒤
        /// Stage2를 다시 돌리는 순간 시퀀스에서 <b>Stage3이 조용히 빠진다</b>(완주 경로가 끊긴다).
        /// 빌더는 여러 번·아무 순서로 돌게 되어 있으므로 순서 의존을 남겨두면 안 된다.
        /// </summary>
        private static StageSequenceData RebuildSequence()
        {
            var stages = new List<StageData>();
            foreach (var file in StageFilesInOrder)
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageData>($"{AbyssPaths.Stages}/{file}.asset");
                if (stage != null) stages.Add(stage);
                else Debug.LogWarning($"[StageBuilder] 시퀀스에서 제외 — 에셋 없음: {file}. 해당 스테이지 빌더를 먼저 실행하세요.");
            }
            return CreateOrUpdateSequence(stages.ToArray());
        }

        /// <summary>
        /// 멀티 스테이지 시퀀스 SO를 생성하거나(없으면), 기존 시퀀스의 stages 목록을 갱신한다.
        /// null 스테이지는 자동 제외(누락 자산 방어). 기존 SO는 식별자·목록만 덮어쓴다(멱등).
        /// </summary>
        private static StageSequenceData CreateOrUpdateSequence(params StageData[] stages)
        {
            var list = stages.Where(s => s != null).ToList();
            string path = $"{AbyssPaths.Stages}/{SequenceFile}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageSequenceData>(path);
            if (existing != null)
            {
                existing.sequenceId = SequenceId;
                existing.displayName = SequenceName;
                existing.stages = list;
                EditorUtility.SetDirty(existing);
                Debug.Log($"[StageBuilder] 시퀀스 갱신: {path} ({list.Count} 스테이지)");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<StageSequenceData>();
            so.sequenceId = SequenceId;
            so.displayName = SequenceName;
            so.stages = list;
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 시퀀스 생성: {path} ({list.Count} 스테이지)");
            return so;
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif
