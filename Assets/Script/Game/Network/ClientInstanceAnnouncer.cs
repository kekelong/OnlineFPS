﻿using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPS.Game.Network
{

    public class ClientInstanceAnnouncer : NetworkBehaviour
    {

        #region Public.
        /// <summary>
        /// Dispatched when the player object spawns or is enabled/disabled.
        /// </summary>
        public static event Action<NetworkObject> OnPlayerUpdated;
        #endregion

        #region Start/Destroy
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner)
                OnPlayerUpdated?.Invoke(base.NetworkObject);
        }
        #endregion

        #region OnEnable/Disable.
        private void OnEnable()
        {
            if (base.IsOwner)
                OnPlayerUpdated?.Invoke(base.NetworkObject);
        }

        private void OnDisable()
        {
            if (base.IsOwner)
                OnPlayerUpdated?.Invoke(null);
        }
        #endregion

    }

}
