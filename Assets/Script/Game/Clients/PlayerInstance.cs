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
                PlayerSpawner.TryRespawn();
            }
        }

            

        /// <summary>
        /// Returns the current client instance for the connection.
        /// </summary>
        /// <returns></returns>
        public static PlayerInstance ReturnClientInstance(NetworkConnection conn)
        {
            /* If server and connection isnt null.
             * When trying to access as server connection
             * will always contain a value. But if client it will be
             * null. */
            if (InstanceFinder.IsServer && conn != null)
            {
                NetworkObject nob = conn.FirstObject;
                return (nob == null) ? null : nob.GetComponent<PlayerInstance>();
            }
            //If not server or connection is null, then is client.
            else
            {
                return Instance;
            }
        }

    }

}
