#if UNITY_EDITOR
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 엘리트 변종 2종(2026-10-06) — 08-content-roadmap 「엘리트 변종 2종 (광폭화·소환사)」.
    ///
    /// 새 그림을 만들지 않는다(이미지는 Codex 담당). 원본 적의 그림·애니메이션을 빌리고(<see cref="EnemyData.artSourceId"/>)
    /// 몸 색만 곱해(<see cref="EnemyData.bodyTint"/>) 구별한다. 예비동작·회복은 원본과 같게 둬 빌린 클립 길이가 맞는다.
    ///
    /// 배치는 사용자 검증이 끝난 Stage1을 건드리지 않고, 분기가 아닌 고정 방의 <b>증원 무리</b> 한 자리를 바꾼다
    /// (엘리트 사냥꾼이 증원으로 늦게 등장하는 것과 같은 리듬).
    /// </summary>
    public static partial class ContentBuilder
    {
        private const string EliteBerserkerPath = AbyssPaths.Enemies + "/EliteBerserker.asset";
        private const string EliteSummonerPath = AbyssPaths.Enemies + "/EliteSummoner.asset";

        private static void CreateEliteVariants()
        {
            CreateOrSkip<EnemyData>(EliteBerserkerPath, so =>
            {
                so.enemyId = "elite_berserker";
                so.displayName = "광폭한 중장";
                so.baseHp = 150;
                so.baseDamage = 22;
                so.moveSpeed = 2.6f;
                so.detectionRange = 7f;
                so.attackRange = 1.5f;
                so.attackCooldown = 1.8f;
                // 🔴 중장 강적과 같은 값 — 빌린 공격 클립(windup + recovery)이 그대로 맞는다.
                so.attackWindup = 0.8f;
                so.attackRecovery = 0.4f;
                so.staggerDuration = 0.3f;
                so.deathLingerDuration = 1.2f;
                so.expReward = 60;
                so.goldReward = 15;
                so.tier = EnemyTier.Elite;
                so.artSourceId = "melee_brute";
                so.bodyTint = new Color(1f, 0.62f, 0.55f, 1f);   // 광폭화 전에도 붉은 기 — 원본 중장과 구별
            });

            CreateOrSkip<EnemyData>(EliteSummonerPath, so =>
            {
                so.enemyId = "elite_summoner";
                so.displayName = "망령 소환사";
                so.baseHp = 100;
                so.baseDamage = 8;
                so.moveSpeed = 2f;
                so.detectionRange = 9f;
                so.attackRange = 6f;
                so.attackCooldown = 3f;            // 공허 술사(2.6)보다 길다 — 소환이 주 위협이다
                so.expReward = 60;
                so.goldReward = 15;
                so.tier = EnemyTier.Elite;
                so.isRanged = true;
                so.projectileSpeed = 7f;
                so.projectileLifetime = 3f;
                so.burstCount = 3;
                so.burstInterval = 0.18f;
                // 🔴 공허 술사와 같은 값 — 빌린 공격 클립과 손끝 위치가 맞는다(ContentBuilder.Enemies VoidCaster 주석).
                so.attackWindup = 0.6f;
                so.attackRecovery = 0.75f;
                so.staggerDuration = 0.3f;
                so.deathLingerDuration = 1f;
                so.projectileOrigin = new Vector2(0.94f, 0.28f);
                so.artSourceId = "void_caster";
                so.bodyTint = new Color(0.78f, 0.62f, 1f, 1f);
            });
        }

        /// <summary>
        /// 변종을 방에 한 자리씩 넣는다. 이미 들어가 있으면 건너뛴다(재실행 멱등).
        /// 방 에셋이 없으면(StageBuilder 미실행) 경고만 한다.
        /// </summary>
        private static void PlaceEliteVariants()
        {
            ReplaceOneEnemyInRoom("Stage2_Room3_Phalanx", "melee_brute", EliteBerserkerPath);
            ReplaceOneEnemyInRoom("Stage3_Room2_Corridor", "ranged_archer", EliteSummonerPath);
        }

        /// <summary>
        /// 방의 <paramref name="fromEnemyId"/> 한 자리를 변종으로 바꾼다. 증원 무리의 뒤쪽부터 찾고, 없으면 처음 배치의 뒤쪽.
        /// 요약 목록(<see cref="RoomData.enemies"/>)도 같이 맞춘다 — 배치와 요약이 갈리면 도구·검증이 다른 수를 본다.
        /// </summary>
        private static void ReplaceOneEnemyInRoom(string roomFile, string fromEnemyId, string variantPath)
        {
            string roomPath = $"{AbyssPaths.Rooms}/{roomFile}.asset";
            var room = AssetDatabase.LoadAssetAtPath<RoomData>(roomPath);
            var variant = AssetDatabase.LoadAssetAtPath<EnemyData>(variantPath);
            if (room == null || variant == null)
            {
                Debug.LogWarning($"[ContentBuilder] 엘리트 변종 배치 건너뜀 — 방({roomPath}) 또는 변종({variantPath}) 없음");
                return;
            }

            if (RoomContains(room, variant))
            {
                Debug.Log($"[ContentBuilder] 엘리트 변종 이미 배치됨: {roomFile} ← {variant.enemyId}");
                return;
            }

            if (!ReplaceInReinforcements(room, fromEnemyId, variant) && !ReplaceInPlacements(room, fromEnemyId, variant))
            {
                Debug.LogWarning($"[ContentBuilder] 엘리트 변종 배치 실패 — {roomFile} 에 '{fromEnemyId}' 자리가 없다");
                return;
            }

            var entry = room.enemies.Find(e => e.data != null && e.data.enemyId == fromEnemyId);
            if (entry != null)
            {
                entry.count -= 1;
                if (entry.count <= 0) room.enemies.Remove(entry);
            }
            room.enemies.Add(new EnemySpawnEntry { data = variant, count = 1 });

            EditorUtility.SetDirty(room);
            Debug.Log($"[ContentBuilder] 엘리트 변종 배치: {roomFile} 의 {fromEnemyId} 1 → {variant.enemyId}");
        }

        private static bool ReplaceInReinforcements(RoomData room, string fromEnemyId, EnemyData variant)
        {
            for (int r = room.reinforcements.Count - 1; r >= 0; r--)
            {
                var group = room.reinforcements[r].enemies;
                for (int i = group.Count - 1; i >= 0; i--)
                {
                    if (group[i].data == null || group[i].data.enemyId != fromEnemyId) continue;
                    group[i].data = variant;
                    return true;
                }
            }
            return false;
        }

        private static bool ReplaceInPlacements(RoomData room, string fromEnemyId, EnemyData variant)
        {
            for (int i = room.placements.Count - 1; i >= 0; i--)
            {
                if (room.placements[i].data == null || room.placements[i].data.enemyId != fromEnemyId) continue;
                room.placements[i].data = variant;
                return true;
            }
            return false;
        }

        private static bool RoomContains(RoomData room, EnemyData enemy)
        {
            if (room.enemies.Exists(e => e.data == enemy)) return true;
            if (room.placements.Exists(p => p.data == enemy)) return true;
            return room.reinforcements.Exists(r => r.enemies.Exists(p => p.data == enemy));
        }
    }
}
#endif
