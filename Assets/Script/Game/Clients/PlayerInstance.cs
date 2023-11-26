using FishNet.Connection;
using FishNet.Object;
using FishNet;
using UnityEngine;

namespace FPS.Game.Clients
{

    public class PlayerInstance : NetworkBehaviour
    {
        #region Public.
        public static PlayerInstance Instance;
        /// <summary>
        /// PlayerSpawner on this object.
        /// </summary>
        public PlayerSpawner PlayerSpawner { get; private set; }
        #endregion

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner)
            {
                Instance = this;
                PlayerSpawner = GetComponent<PlayerSpawner>();
                //生成具体的角色
                PlayerSpawner.TryRespawn();
            }
        }

            


        public static PlayerInstance ReturnClientInstance(NetworkConnection conn)
        {

            if (InstanceFinder.IsServer && conn != null)
            {
                NetworkObject nob = conn.FirstObject;
                return (nob == null) ? null : nob.GetComponent<PlayerInstance>();
            }

            else
            {
                return Instance;
            }
        }

    }

}
