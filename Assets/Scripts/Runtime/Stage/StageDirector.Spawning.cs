using Abyss.Runtime.Enemy;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    public sealed partial class StageDirector
    {
        private EnemyBase SpawnTracked(EnemyData data, Vector3 position)
        {
            // 예고 시작 때 검사를 통과했더라도 등장 시점에 다시 본다 — 예고 사이 참조가 비었으면 Instantiate가 던진다.
            if (data == null || data.spawnPrefab == null)
            {
                Debug.LogError($"[StageDirector] '{data?.enemyId ?? "null"}' 등장 시점에 스폰 프리팹이 없다 — 건너뜀");
                MarkRoomSpawnFailure();
                return null;
            }

            var go = Instantiate(data.spawnPrefab, position, Quaternion.identity);

            // 프리팹 루트가 아닌 자식에 EnemyBase가 붙어 있으면 GetComponent가 놓친다.
            // 예전에는 조용히 건너뛰어, 스폰은 됐는데 추적만 빠지는 상태가 됐다.
            var enemy = go.GetComponentInChildren<EnemyBase>(true);
            if (enemy == null)
            {
                Debug.LogError($"[StageDirector] '{data.enemyId}' 프리팹에 EnemyBase가 없다 " +
                               $"— 스폰은 됐지만 클리어 판정에서 빠져 방이 조기 클리어된다");
                MarkRoomSpawnFailure();
                return null;
            }

            activeEnemies.Add(enemy);
            if (enemy is BossEnemy) hasRoomBossEnemy = true;
            return enemy;
        }

        // 직접 배치한 거미도 처치 대상에 포함한다. 동작 준비는 다른 적처럼 자기 Start가 담당한다.
        private int RegisterSceneSpiders()
        {
            int count = 0;
            foreach (var spider in FindObjectsByType<BurrowSpiderEnemy>(FindObjectsSortMode.None))
            {
                if (!spider.isActiveAndEnabled || spider.IsDead || spider.gameObject.scene != gameObject.scene) continue;
                activeEnemies.Add(spider);
                count += 1;
            }
            return count;
        }
    }
}
