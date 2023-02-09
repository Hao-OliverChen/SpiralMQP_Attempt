using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawner : SingletonAbstract<EnemySpawner>
{
    private int enemiesToSpawn;
    private int currentEnemyCount;
    private int enemiesSpawnedSoFar;
    private int enemyMaxConcurrentSpawnNumber;
    private Room currentRoom;
    private RoomEnemySpawnParameters roomEnemySpawnParameters;

    private void OnEnable()
    {
        // Subscribe to room changed event
        StaticEventHandler.OnRoomChanged += StaticEventHandler_OnRoomChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe from room changed event
        StaticEventHandler.OnRoomChanged -= StaticEventHandler_OnRoomChanged;
    }

    /// <summary>
    /// Process a change in room
    /// </summary>
    private void StaticEventHandler_OnRoomChanged(RoomChangedEventArgs roomChangedEventArgs)
    {
        // Initializing some parameters
        enemiesSpawnedSoFar = 0;
        currentEnemyCount = 0;
        currentRoom = roomChangedEventArgs.room;


        // If the room is a corridor or the entrance then return
        if (currentRoom.roomNodeType.isCorridorEW || currentRoom.roomNodeType.isCorridorNS || currentRoom.roomNodeType.isEntrance) return;

        // If the room has already been defeated then return
        if (currentRoom.isClearedOfEnemies) return;

        // Get random number of enemies to spawn
        enemiesToSpawn = currentRoom.GetNumberOfEnemiesToSpawn(GameManager.Instance.GetCurrentDungeonLevel());

        // Get room enemy spawn parameters
        roomEnemySpawnParameters = currentRoom.GetRoomEnemySpawnParameters(GameManager.Instance.GetCurrentDungeonLevel());

        // If no enemies to spawn return
        if (enemiesToSpawn == 0)
        {
            // Mark the room as cleared
            currentRoom.isClearedOfEnemies = true;

            return;
        }

        // Get concurrent number of enemies to spawn
        enemyMaxConcurrentSpawnNumber = GetConcurrentEnemies();

        // Lock doors - if we get here, there are enemies in the room
        currentRoom.instantiatedRoom.LockDoors();

        // Spawn enemies
        SpawnEnemies();
    }

    /// <summary>
    /// Spawn the enemies
    /// </summary>
    private void SpawnEnemies()
    {
        // Set gamestate engaging boss
        if (GameManager.Instance.gameState == GameState.bossStage)
        {
            GameManager.Instance.previousGameState = GameState.bossStage;
            GameManager.Instance.gameState = GameState.engagingBoss;
        }

        // Set gamestate engaging enemies
        else if (GameManager.Instance.gameState == GameState.playingLevel)
        {
            GameManager.Instance.previousGameState = GameState.playingLevel;
            GameManager.Instance.gameState = GameState.engagingEnemies;
        }

        StartCoroutine(SpawnEnemiesRoutine());
    }

    /// <summary>
    /// Spawn the enemies coroutine
    /// </summary>
    private IEnumerator SpawnEnemiesRoutine()
    {
        Grid grid = currentRoom.instantiatedRoom.grid;

        // Create an instance of the helper class used to select a random enemy
        RandomSpawnableObject<EnemyDetailsSO> randomEnemyHelperClass = new RandomSpawnableObject<EnemyDetailsSO>(currentRoom.enemiesByLevelList);

        // Check we have somewhere to spawn the enemies
        if (currentRoom.spawnPositionArray.Length > 0)
        {
            // Loop through to create all the enemeies
            for (int i = 0; i < enemiesToSpawn; i++)
            {
                // Wait until current enemy count is less than max concurrent enemies
                while (currentEnemyCount >= enemyMaxConcurrentSpawnNumber)
                {
                    yield return null;
                }

                Vector3Int cellPosition = (Vector3Int)currentRoom.spawnPositionArray[Random.Range(0, currentRoom.spawnPositionArray.Length)];

                // Create Enemy - Get next enemy type to spawn 
                CreateEnemy(randomEnemyHelperClass.GetItem(), grid.CellToWorld(cellPosition));

                yield return new WaitForSeconds(GetEnemySpawnInterval());
            }
        }
    }

    /// <summary>
    /// Get a random spawn interval between the minimum and maximum values
    /// </summary>
    private float GetEnemySpawnInterval()
    {
        return (Random.Range(roomEnemySpawnParameters.minSpawnInterval, roomEnemySpawnParameters.maxSpawnInterval));
    }

    /// <summary>
    /// Get a random number of concurrent enemies between the minimum and maximum values
    /// </summary>
    private int GetConcurrentEnemies()
    {
        return (Random.Range(roomEnemySpawnParameters.minConcurrentEnemies, roomEnemySpawnParameters.maxConcurrentEnemies));
    }

    /// <summary>
    /// Create an enemy in the specified position
    /// </summary>
    private void CreateEnemy(EnemyDetailsSO enemyDetails, Vector3 position)
    {
        // Keep track of the number of enemies spawned so far 
        enemiesSpawnedSoFar++;

        // Add one to the current enemy count - this is reduced when an enemy is destroyed
        currentEnemyCount++;

        // Get current dungeon level
        DungeonLevelSO dungeonLevel = GameManager.Instance.GetCurrentDungeonLevel();

        // Instantiate enemy
        GameObject enemy = Instantiate(enemyDetails.enemyPrefab, position, Quaternion.identity, transform);

        // Initialize Enemy
        enemy.GetComponent<Enemy>().EnemyInitialization(enemyDetails, enemiesSpawnedSoFar, dungeonLevel);

        // Subscribe to enemy destroyed event
        enemy.GetComponent<DestroyedEvent>().OnDestroyed += Enemy_OnDestroyed;
    }


    /// <summary>
    /// Process enemy destroyed
    /// </summary>
    private void Enemy_OnDestroyed(DestroyedEvent destroyedEvent, DestroyedEventArgs destroyedEventArgs)
    {
        // Unsubscribe from event
        destroyedEvent.OnDestroyed -= Enemy_OnDestroyed;

        // Reduce current enemy count
        currentEnemyCount--;


        if (currentEnemyCount <= 0 && enemiesSpawnedSoFar == enemiesToSpawn)
        {
            currentRoom.isClearedOfEnemies = true;

            // Set game state
            if (GameManager.Instance.gameState == GameState.engagingEnemies)
            {
                GameManager.Instance.gameState = GameState.playingLevel;
                GameManager.Instance.previousGameState = GameState.engagingEnemies;
            }

            else if (GameManager.Instance.gameState == GameState.engagingBoss)
            {
                GameManager.Instance.gameState = GameState.bossStage;
                GameManager.Instance.previousGameState = GameState.engagingBoss;
            }

            // Unlock doors
            currentRoom.instantiatedRoom.UnlockDoors(Settings.doorUnlockDelay);
            
            // Trigger room enemies defeated event
            StaticEventHandler.CallRoomEnemiesDefeatedEvent(currentRoom);
        }
    }
}