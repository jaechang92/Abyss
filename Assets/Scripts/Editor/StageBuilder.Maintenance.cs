#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 정비 공간(R2) 방·데이터 생성. <see cref="MaintenanceData"/>는 기존 상점·휴식 에셋을 참조만 하고,
    /// 지형은 원래 상점 방의 레이아웃을 빌린다(<see cref="RoomLayoutController"/> 별칭) — 그래서 원래 방 에셋은 계속 만든다.
    /// 기존 GUID·표시·양수 맵 길이·상인/불씨 좌표는 보존하되 정비 방의 비전투·무보상 계약은 복구한다.
    /// </summary>
    public static partial class StageBuilder
    {
        private const float DEFAULT_MERCHANT_X = -3f;
        private const float DEFAULT_EMBER_X = 3f;
        private const float DEFAULT_MAINTENANCE_LENGTH = 24f;

        private static RoomData CreateOrLoadMaintenanceRoom(
            string roomFile, string dataFile, string maintenanceId,
            ShopData shop, EventData rest, RoomData layoutSource, string displayName, string hint)
        {
            var data = CreateOrLoadMaintenanceData(dataFile, maintenanceId, shop, rest, layoutSource);
            // 원본 상점도 신규 생성이면 mapLength가 0이다. 정비는 양수 맵 길이가 있어야 서비스가 열린다.
            float mapLength = layoutSource != null && layoutSource.IsMapRoom
                ? layoutSource.mapLength : DEFAULT_MAINTENANCE_LENGTH;

            string path = $"{AbyssPaths.Rooms}/{roomFile}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                if (existing.maintenanceData != data || existing.roomType != RoomType.Shop ||
                    existing.eventData != null || existing.shopData != null || existing.clearGoldReward != 0 ||
                    existing.IsFormRewardRoom || existing.IsWeaponRewardRoom || existing.isCombatDiscovery ||
                    existing.enemies == null || existing.enemies.Count > 0 ||
                    existing.placements == null || existing.placements.Count > 0 ||
                    existing.reinforcements == null || existing.reinforcements.Count > 0 || !existing.IsMapRoom)
                {
                    existing.maintenanceData = data;
                    existing.roomType = RoomType.Shop;
                    existing.eventData = null;
                    existing.shopData = null;
                    existing.clearGoldReward = 0;
                    existing.hasFormReward = false;
                    existing.formReward = null;
                    existing.hasWeaponReward = false;
                    existing.weaponReward = null;
                    existing.isCombatDiscovery = false;
                    existing.enemies = new List<EnemySpawnEntry>();
                    existing.placements = new List<EnemyPlacement>();
                    existing.reinforcements = new List<Reinforcement>();
                    if (!existing.IsMapRoom) existing.mapLength = mapLength;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] 정비 룸 갱신: {path} → {data.MaintenanceId}");
                }
                else Debug.Log($"[StageBuilder] 건너뜀 (존재): {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = roomFile.ToLowerInvariant();
            so.roomType = RoomType.Shop;
            so.displayName = displayName;
            so.hint = hint;
            so.clearGoldReward = 0;
            so.enemies = new List<EnemySpawnEntry>();
            // 지형을 빌리는 방과 같은 맵 길이 — 벽·출구·상인/불씨 좌표가 빌린 바닥 안에 선다.
            so.mapLength = mapLength;
            so.maintenanceData = data;
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        private static MaintenanceData CreateOrLoadMaintenanceData(
            string fileName, string maintenanceId, ShopData shop, EventData rest, RoomData layoutSource)
        {
            EnsureDir(AbyssPaths.Shops);
            string path = $"{AbyssPaths.Shops}/{fileName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<MaintenanceData>(path);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<MaintenanceData>();
                AssetDatabase.CreateAsset(data, path);
            }

            // private 직렬화 필드라 SerializedObject로 맞춘다. 참조는 원본 에셋 그대로(복사 금지).
            var so = new SerializedObject(data);
            so.FindProperty("maintenanceId").stringValue = maintenanceId;
            so.FindProperty("shopData").objectReferenceValue = shop;
            so.FindProperty("restEventData").objectReferenceValue = rest;
            so.FindProperty("layoutSourceRoom").objectReferenceValue = layoutSource;
            if (isNew)
            {
                so.FindProperty("merchantX").floatValue = DEFAULT_MERCHANT_X;
                so.FindProperty("emberX").floatValue = DEFAULT_EMBER_X;
            }
            if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(data);

            Debug.Log($"[StageBuilder] {(isNew ? "생성" : "확인")}: {path}");
            return data;
        }
    }
}
#endif
