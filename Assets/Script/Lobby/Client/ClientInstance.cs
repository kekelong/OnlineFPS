using FishNet.Connection;
using FishNet.Object;
using FishNet;
using UnityEngine;

namespace FPS.Lobby
{
    
    public class ClientInstance : NetworkBehaviour
    {
        #region Public.
        /// <summary>
        /// Singleton reference to the client instance.
        /// </summary>
        public static ClientInstance Instance;
        /// <summary>
        /// True if initialized.
        /// </summary>
        public bool Initialized { get; private set; } = false;
        #endregion

        #region Private.
        /// <summary>
        /// PlayerSettings reference.
        /// </summary>
        public PlayerSettings PlayerSettings { get; private set; }
        #endregion

        private void Awake()
        {
            FirstInitialize();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (base.IsOwner)
                Instance = this;
        }


        private void FirstInitialize()
        {
            PlayerSettings = GetComponent<PlayerSettings>();
        }

        /// <summary>
        /// 返回连接的当前客户端实例。
        /// </summary>
        /// <returns></returns>
        public static ClientInstance ReturnClientInstance(NetworkConnection conn)
        {

            if (InstanceFinder.IsServer && conn != null)
            {
                NetworkObject nob = conn.FirstObject;
                return (nob == null) ? null : nob.GetComponent<ClientInstance>();
            }
            else
            {
                return Instance;
            }
        }

  
    }

}

