using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace FPS.Lobby
{

    public class LobbyNetworkSpawner : MonoBehaviour
    {
        /// <summary>
        /// Prefab to spawn for LobbyNetwork.
        /// </summary>
        [Tooltip("Prefab to spawn for LobbyNetwork.")]
        [SerializeField]
        private NetworkObject _lobbyNetworkPrefab;

        /// <summary>
        /// NetworkManager on this object.
        /// </summary>
        private NetworkManager _networkManager;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
        }

        /// <summary>
        ///在本地服务器连接状态发生变化后调用。
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            if (obj.ConnectionState != LocalConnectionState.Started)
                return;
            if (!_networkManager.ServerManager.OneServerStarted())
                return;
            Debug.Log("生成_lobbyNetworkPrefab");
            NetworkObject nob = Instantiate(_lobbyNetworkPrefab);
            Scene scene = UnitySceneManager.GetSceneByName("Main");
            UnitySceneManager.MoveGameObjectToScene(nob.gameObject, scene);
            //通过网络生成一个对象。只能在服务器上调用。
            _networkManager.ServerManager.Spawn(nob.gameObject);
        }

    }


}