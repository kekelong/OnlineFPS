using FirstGearGames.FPSLand.Characters.Vitals;
using FirstGearGames.FPSLand.Managers.Gameplay;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object;
using System;
using UnityEngine;

namespace FPS.Game.Clients
{
    //游戏玩家生成方法
    //
    public class PlayerSpawner : NetworkBehaviour
    {
        #region Types.
        public class PlayerData
        {
            public NetworkObject NetworkObject;
            public Health Health;
        }
        #endregion

        #region Public.
        /// <summary>
        /// 当角色更新时调用。
        /// </summary>
        public static event Action<GameObject> OnCharacterUpdated;
        /// <summary>
        /// 当前生成的玩家数据。
        /// </summary>
        public PlayerData SpawnedCharacterData { get; private set; } = new PlayerData();
        #endregion

        #region Serialized.
        /// <summary>
        /// Character prefab。
        /// </summary>
        [Tooltip("Character prefab to spawn.")]
        [SerializeField]
        private GameObject _characterPrefab;
        #endregion


        [Client(Logging = LoggingType.Off)]
        public void TryRespawn()
        {
            CmdRespawn();
        }

        /// <summary>
        /// 设置玩家数据
        /// </summary>
        /// <param name="go"></param>
        private void SetupSpawnedCharacterData(GameObject go)
        {
            SpawnedCharacterData.NetworkObject = go.GetComponent<NetworkObject>();
            SpawnedCharacterData.Health = go.GetComponent<Health>();
        }


        /// <summary>
        /// 服务器调用
        /// </summary>
        [ServerRpc]
        private void CmdRespawn()
        {
            //获取坐标
            Transform spawn = OfflineGameplayDependencies.SpawnManager.ReturnSpawnPoint();
            if (spawn == null)
            {
                Debug.LogError("All spawns are occupied.");
            }
            else
            {
                //如果角色尚未生成。
                if (SpawnedCharacterData.NetworkObject == null)
                {
                    GameObject r = Instantiate(_characterPrefab, spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
                    base.Spawn(r, base.Owner);

                    SetupSpawnedCharacterData(r);
                    TargetCharacterSpawned(base.Owner, SpawnedCharacterData.NetworkObject);
                }
                //角色已经生成。刷新属性
                else
                {
                    SpawnedCharacterData.NetworkObject.transform.position = spawn.position;
                    SpawnedCharacterData.NetworkObject.transform.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
                    Physics.SyncTransforms();//同步物理引擎
                    //Restore health and set respawned.
                    SpawnedCharacterData.Health.RestoreHealth();
                    SpawnedCharacterData.Health.Respawned();
                }

            }
        }

        /// <summary>
        /// 当服务器生成角色时接收。
        /// </summary>
        /// <param name="character"></param>
        [TargetRpc]
        private void TargetCharacterSpawned(NetworkConnection conn, NetworkObject character)
        {
            GameObject playerObj = (character == null) ? null : playerObj = character.gameObject;
            OnCharacterUpdated?.Invoke(playerObj);

            //如果玩家被生成。
            if (playerObj != null)
                SetupSpawnedCharacterData(character.gameObject);
        }

    }

}
