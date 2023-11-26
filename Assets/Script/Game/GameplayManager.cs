using FishNet.Connection;
using FishNet.Object;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityEngine;
using FPS.Lobby;
using System.Collections.Generic;

namespace FPS.Game.Managers
{
    //控制房间的玩家的生成和退出
    public class GameplayManager : NetworkBehaviour
    {
        #region Serialized.
        /// <summary>
        /// Character prefab to spawn.
        /// </summary>
        [Tooltip("Character prefab to spawn.")]
        [SerializeField]
        private NetworkObject _playerInstancePrefab;
        #endregion

        /// <summary>
        /// RoomDetails for this game. Only available on the server.
        /// </summary>
        private RoomDetails _roomDetails = null;
        /// <summary>
        /// LobbyNetwork.
        /// </summary>
        private LobbyNetwork _lobbyNetwork = null;


        /// <summary>
        /// Currently spawned player objects. Only exist on the server.
        /// </summary>
        private List<NetworkObject> _spawnedPlayerObjects = new List<NetworkObject>();


        private LobbyWindowsManager _lobbyWindowsManager = null;
        #region Initialization and Deinitialization.
        private void OnDestroy()
        {
            if (_lobbyNetwork != null)
            {
                _lobbyNetwork.OnClientJoinedRoom -= LobbyNetwork_OnClientStarted;
                _lobbyNetwork.OnClientLeftRoom -= LobbyNetwork_OnClientLeftRoom;
            }
        }

        public void FirstInitialize(RoomDetails roomDetails, LobbyNetwork lobbyNetwork)
        {
            _lobbyWindowsManager = GameObject.FindObjectOfType<LobbyWindowsManager>();
            if (_lobbyWindowsManager == null)
            {
                Debug.LogError("_lobbyWindowsManager script not found. GameplayManager cannot initialize.");
                return;
            }
            _roomDetails = roomDetails;
            _lobbyNetwork = lobbyNetwork;
            _lobbyNetwork.OnClientStarted += LobbyNetwork_OnClientStarted;
            _lobbyNetwork.OnClientLeftRoom += LobbyNetwork_OnClientLeftRoom;
            _lobbyWindowsManager.StartGame();
        }

        /// <summary>
        /// 离开房间时调用
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        private void LobbyNetwork_OnClientLeftRoom(RoomDetails arg1, NetworkObject arg2)
        {

            for (int i = 0; i < _spawnedPlayerObjects.Count; i++)
            {
                NetworkObject entry = _spawnedPlayerObjects[i];
                //Entry is null. Remove and iterate next.
                if (entry == null)
                {
                    _spawnedPlayerObjects.RemoveAt(i);
                    i--;
                    continue;
                }

                //If same connection to client (owner) as client instance of leaving player.
                if (_spawnedPlayerObjects[i].Owner == arg2.Owner)
                {
                    //Destroy entry then remove from collection.
                    entry.Despawn();
                    _spawnedPlayerObjects.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// 当客户端开始游戏时调用。LobbyNetwork调用所有客户端
        /// </summary>
        private void LobbyNetwork_OnClientStarted(RoomDetails roomDetails, NetworkObject client)
        {
            //Not for this room.
            if (roomDetails != _roomDetails)
                return;
            //NetIdent is null or not a player.
            if (client == null || client.Owner == null)
                return;
            //隐藏大厅房间UI
            SetLobbyWindowsVisible();
            //服务器生成玩家实例
            SpawnPlayer(client.Owner);
        }
        #endregion

        #region Spawning.

        /// <summary>
        /// Spawns  player 
        /// </summary>
        private void SpawnPlayer(NetworkConnection conn)
        {
            //生成玩家并且移动到游戏场景
            NetworkObject nob = Instantiate<NetworkObject>(_playerInstancePrefab, transform.position, Quaternion.identity);
            UnitySceneManager.MoveGameObjectToScene(nob.gameObject, gameObject.scene);
            _spawnedPlayerObjects.Add(nob); 
            base.Spawn(nob.gameObject, conn);
        }


        [ObserversRpc]
        private void SetLobbyWindowsVisible()
        {
            
            if (_lobbyWindowsManager == null)
            {
                _lobbyWindowsManager = GameObject.FindObjectOfType<LobbyWindowsManager>();
                if (_lobbyWindowsManager == null)
                {
                    Debug.LogError("_lobbyWindowsManager script not found. GameplayManager cannot initialize.");
                    return;
                }
                
            }
            _lobbyWindowsManager.SetLobbyWindowsVisible(false);
        }
        #endregion
    }

}
