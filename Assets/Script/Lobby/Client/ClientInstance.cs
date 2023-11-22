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

        #region Constants.
        /// <summary>
        /// Version of this build.
        /// </summary>
        private const int VERSION_CODE = 0;
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

        /// <summary>
        /// /// Called when the local player object has been set up.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner)
                CmdVerifyVersion(VERSION_CODE);
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        private void FirstInitialize()
        {
            PlayerSettings = GetComponent<PlayerSettings>();
        }

        /// <summary>
        /// Returns the current client instance for the connection.
        /// </summary>
        /// <returns></returns>
        public static ClientInstance ReturnClientInstance(NetworkConnection conn)
        {
            /* If server and connection isnt null.
             * When trying to access as server connection
             * will always contain a value. But if client it will be
             * null. */
            if (InstanceFinder.IsServer && conn != null)
            {
                NetworkObject nob = conn.FirstObject;
                return (nob == null) ? null : nob.GetComponent<ClientInstance>();
            }
            //If not server or connection is null, then is client.
            else
            {
                return Instance;
            }
        }

        /// <summary>
        /// Verifies with the server the client version.
        /// </summary>
        /// <param name="versionCode"></param>
        [ServerRpc]
        private void CmdVerifyVersion(int versionCode)
        {
            bool pass = (versionCode == VERSION_CODE);
            TargetVerifyVersion(base.Owner, pass);

            //If not pass then find offending client and give them the boot.
            if (!pass)
                base.NetworkManager.TransportManager.Transport.StopConnection(base.Owner.ClientId, false);
        }


        /// <summary>
        /// Response from server if version matches.
        /// </summary>
        /// <param name="pass"></param>
        [TargetRpc]
        private void TargetVerifyVersion(NetworkConnection conn, bool pass)
        {
            Initialized = pass;
            if (!pass)
            {
                base.NetworkManager.ClientManager.StopConnection();

                Debug.LogWarning("Your executable is out of date. Please update.");
            }
        }
    }

}

