using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Scened;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using GameKit.Utilities;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using FPS.Game;
using FPS.Game.Managers;

namespace FPS.Lobby
{
    public class LobbyNetwork : NetworkBehaviour
    {
        #region Types.
        private enum ParamsTypes
        {
            ServerLoad,
            MemberLeft
        }
        #endregion

        #region Public 
        /// <summary>
        /// 用户离开房间时调用     LobbyNetwork_OnMemberLeft
        /// </summary>
        public static event Action<NetworkObject> OnMemberLeft;
        /// <summary>
        /// 房间中的成员加载游戏场景后调用。
        /// </summary>
        public static event Action<NetworkObject> OnMemberStarted;
        /// <summary>
        /// 加入房间时调用
        /// </summary>
        public static event Action<NetworkObject> OnMemberJoined;
        /// <summary>
        /// 当你当前房间的成员改变他们的准备状态时调用。
        /// </summary>
        public static event Action<NetworkObject, bool> OnMemberSetReady;
        /// <summary>
        /// 当前所在房间
        /// </summary>
        public RoomDetails _currentRoom { get; private set; } = null;
        /// <summary>
        /// Current room of local client.
        /// </summary>
        public static RoomDetails CurrentRoom
        {
            get { return _instance._currentRoom; }
            private set { _instance._currentRoom = value; }
        }
        /// <summary>
        /// 当服务器为一个房间加载场景时调用。
        /// </summary>
        public event Action<RoomDetails, SceneLoadEndEventArgs> OnServerLoadedScenes;
        /// <summary>
        /// 当客户端离开一个房间时调用。这在客户端从 RoomDetails 中移除后调用。
        /// </summary>
        public event Action<RoomDetails, NetworkObject> OnClientLeftRoom;
        /// <summary>
        /// Called when a client joins a room. This is called after the client has been sent a successful join response.
        /// </summary>
        public event Action<RoomDetails, NetworkObject> OnClientJoinedRoom;
        /// <summary>
        /// Called when a client starts a game after being in a room.
        /// </summary>
        public event Action<RoomDetails, NetworkObject> OnClientStarted;
        /// <summary>
        /// Called when a client logs-in with a username.
        /// </summary>
        public event Action<NetworkObject> OnClientLoggedIn;
        /// <summary>
        /// Called when a client creates a room.
        /// </summary>
        public event Action<RoomDetails, NetworkObject> OnClientCreatedRoom;
        /// <summary>
        /// Currently logged in usernames.
        /// </summary>
        public Dictionary<NetworkConnection, string> LoggedInUsernames = new Dictionary<NetworkConnection, string>();
        /// <summary>
        /// Currently created rooms.
        /// </summary>
        public List<RoomDetails> CreatedRooms = new List<RoomDetails>();
        /// <summary>
        /// Contains current room of clients.
        /// </summary>
        public Dictionary<NetworkConnection, RoomDetails> ConnectionRooms = new Dictionary<NetworkConnection, RoomDetails>();
        #endregion

        #region Serialized.
        /// <summary>
        /// Reference to LobbyWindowsManager.
        /// </summary>
        [Tooltip("Reference to LoobyWindowsManager.")]
        protected LobbyWindowsManager _lobbyWindowsManager;


        public LobbyWindowsManager LobbyWindowsManager { get { return _lobbyWindowsManager; } }
        /// <summary>
        /// GameSceneConfigurations to get loading scenes from.
        /// </summary>
        [Tooltip("GameSceneConfigurations to get loading scenes from.")]
        [SerializeField]
        private GameSceneConfigurations _gameSceneConfigurations;
        #endregion

        #region Private.
        /// <summary>
        /// Players which have readied for each room.
        /// </summary>
        private Dictionary<RoomDetails, HashSet<NetworkObject>> _serverReadyPlayers = new Dictionary<RoomDetails, HashSet<NetworkObject>>();
        /// <summary>
        /// Players which have readied for local players current room.
        /// </summary>
        private HashSet<NetworkObject> _clientReadyPlayers = new HashSet<NetworkObject>();
        /// <summary>
        /// Singleton instance to this script.
        /// </summary>
        public static LobbyNetwork _instance;
        #endregion


        #region Initialization.
        protected virtual void Awake()
        {
            FirstInitialize();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ChangeSubscriptions(false);
            ChangeSubscriptions(true);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ChangeSubscriptions(false);
        }
        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        private void FirstInitialize()
        {
            _instance = this;
            //Find LobbyWindowsManager.
            _lobbyWindowsManager = GameObject.FindObjectOfType<LobbyWindowsManager>();
            if (_lobbyWindowsManager == null)
            {
                Debug.LogError("LobbyCanvases script not found. LobbyNetwork cannot initialize.");
                return;
            }

            _lobbyWindowsManager.FirstInitialize();
            InstanceFinder.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            InstanceFinder.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            InstanceFinder.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            InstanceFinder.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;

            OnClientLeftRoom += LobbyNetwork_OnClientLeftRoom;
            OnClientJoinedRoom += LobbyNetwork_OnClientJoinedRoom;
            OnMemberLeft += LobbyNetwork_OnMemberLeft;
        }





        private void OnDestroy()
        {
            if (InstanceFinder.ClientManager == null)
                return;
            InstanceFinder.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
            OnMemberLeft -= LobbyNetwork_OnMemberLeft;
        }


        /// <summary>
        /// Called when a client loads initial scenes after connecting. Boolean will be true if asServer.
        /// </summary>
        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection arg1, bool asServer)
        {
            if (asServer)
                SendRooms(arg1);
        }

        /// <summary>
        /// Called after the local server connection state changes.
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            ServerReset();
            //Also reset client incase acting as client host.
            ClientReset();
        }

        /// <summary>
        /// Called when a client state changes with the server.
        /// </summary>
        private void ServerManager_OnRemoteConnectionState(NetworkConnection arg1, RemoteConnectionStateArgs arg2)
        {
            if (arg2.ConnectionState == RemoteConnectionState.Stopped)
                ClientDisconnected(arg1);
        }
        
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(FishNet.Transporting.ClientConnectionStateArgs obj)
        {
            if (!ApplicationState.IsQuitting() && obj.ConnectionState != LocalConnectionState.Started)
                ClientReset();

            if (obj.ConnectionState == LocalConnectionState.Started)
                return;

            //Clear local ready players.
            _clientReadyPlayers.Clear();

            /* 卸载除大厅外的所有场景。
             * Since they are disconnected this doesn't ened to be done through
             * FSM. Also, FSM scene changes MUST only be done via server,
             * this is being run on client. */
            for (int i = 0; i < UnitySceneManager.sceneCount; i++)
            {
                Scene s = UnitySceneManager.GetSceneAt(i);
                if (s != gameObject.scene)
                    UnitySceneManager.UnloadSceneAsync(s);
            }
            _lobbyWindowsManager.SetLobbyWindowsVisible(false);
        }


        /// <summary>
        /// Changes subscriptions needed to operate.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeSubscriptions(bool subscribe)
        {
            
            if (base.NetworkManager == null)
                return;

            if (subscribe)
            {
                base.NetworkManager.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
                base.NetworkManager.SceneManager.OnClientPresenceChangeEnd += SceneManager_OnClientPresenceChangeEnd;
            }
            else
            {
                base.NetworkManager.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
                base.NetworkManager.SceneManager.OnClientPresenceChangeEnd -= SceneManager_OnClientPresenceChangeEnd;
            }
        }
        #endregion


        #region SceneManager callbacks.
        /// <summary>
        /// Called when a clients presence changes for a scene.
        /// </summary>
        private void SceneManager_OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs obj)
        {
            Debug.Log("SceneManager_OnClientPresenceChangeEnd=========================" + IsServer);
            if (obj.Added)
                HandleClientLoadedScene(obj);
        }

        /// <summary>
        /// Called after one or more scenes have loaded.
        /// </summary>
        private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs obj)
        {
            Debug.Log("SceneManager_OnLoadEnd========================="+ IsServer);
            if (!obj.QueueData.AsServer)
                return;

            HandleServerLoadedScenes(obj);

            /* When the server loads a scene try to find the SceneRoomDetails script in it
             * and pass in the RoomDetails for the scene loaded. This isn't required by any
             * means but it shows how you can have a reference to the RoomDetails which
             * the scene is for on the server. */
            object[] p = obj.QueueData.SceneLoadData.Params.ServerParams;
            if (p != null && p.Length > 1)
            {
                RoomDetails rd = (RoomDetails)p[1];
                //Try to find script in scene.
                foreach (Scene s in obj.LoadedScenes)
                {

                    GameObject[] gos = s.GetRootGameObjects();
                    for (int i = 0; i < gos.Length; i++)
                    {
                        //If found.
                        if (gos[i].TryGetComponent<GameplayManager>(out GameplayManager gpm))
                        {
                            gpm.FirstInitialize(rd, this);
                            
                            break;
                        }

                    }
                }
            }
        }
        #endregion

        #region NetworkManager callbacks.

        private void ClientDisconnected(NetworkConnection obj)
        {
            ClientLeftServer(obj);
            LoggedInUsernames.Remove(obj);
            ConnectionRooms.Remove(obj);
        }


        /// <summary>
        /// Resets client as though just connecting. Can be called from server as ClientHost. Intended to reset client settings when they disconnect from the server. This is not required if quitting the game.
        /// </summary>
        //[Client]
        /* [Client] attribute may be bugged. Even though this method definitely calls from client it's being blocked as Mirror believes the server is making the call.
         * Perhaps it's because LobbyNetwork is server owned?. */
        private void ClientReset()
        {
            _lobbyWindowsManager.Reset();
            CurrentRoom = null;
            //Can be null if stopping server.
            ClientInstance ci = ClientInstance.ReturnClientInstance(null);
            if (ci != null)
                ci.PlayerSettings.SetUsername(string.Empty);
        }

        /// <summary>
        /// Resets server as though just starting.
        /// </summary>
        private void ServerReset()
        {
            CreatedRooms.Clear();
            LoggedInUsernames.Clear();
        }
        #endregion

        #region Ready
        /// <summary>
        /// Called when a client joins a room. This is called after the client has been sent a successful join response.
        /// </summary>
        private void LobbyNetwork_OnClientJoinedRoom(RoomDetails roomDetails, NetworkObject joiner)
        {
            HashSet<NetworkObject> readyPlayers;
            //If any players have readied up send it to joining player.
            if (_serverReadyPlayers.TryGetValue(roomDetails, out readyPlayers))
            {
                foreach (NetworkObject item in readyPlayers)
                    TargetSetReady(joiner.Owner, item, true);
            }
        }
        /// <summary>
        /// Tries ready state over the network for local client.
        /// </summary>
        /// <param name="ready"></param>
        [Client]
        public static void SetReady(bool ready)
        {
            _instance.SetReadyInternal(ready);
        }
        private void SetReadyInternal(bool ready)
        {
            CmdSetReady(ready);
        }

        /// <summary>
        /// Sets ready state for player in their room.
        /// </summary>
        /// <param name="sender"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdSetReady(bool ready, NetworkConnection sender = null)
        {
            SetReady(ready, sender.FirstObject, true);
        }

        /// <summary>
        /// Sets ready state for a client.
        /// </summary>
        /// <param name="changingPlayer"></param>
        private void SetReady(bool ready, NetworkObject changingPlayer, bool asServer)
        {
            //Running on server.
            if (asServer)
            {
                RoomDetails roomDetails;
                //Not in a room.
                if (!ConnectionRooms.TryGetValue(changingPlayer.Owner, out roomDetails))
                {
                    Debug.LogWarning($"Cannot ready client as they are not in a room.");
                }
                //In a room, find room in ready list and add player.
                else
                {
                    HashSet<NetworkObject> readyPlayers;
                    //If not found make new hashset.
                    if (!_serverReadyPlayers.TryGetValue(roomDetails, out readyPlayers))
                    {
                        readyPlayers = new HashSet<NetworkObject>();
                        _serverReadyPlayers[roomDetails] = readyPlayers;
                    }

                    if (ready)
                        readyPlayers.Add(changingPlayer);
                    else
                        readyPlayers.Remove(changingPlayer);

                    foreach (NetworkObject item in roomDetails.MemberIds)
                        TargetSetReady(item.Owner, changingPlayer, ready);
                }
            }
            //Running on client.
            else
            {
                if (ready)
                    _clientReadyPlayers.Add(changingPlayer);
                else
                    _clientReadyPlayers.Remove(changingPlayer);

                OnMemberSetReady?.Invoke(changingPlayer, ready);
            }
        }

        /// <summary>
        /// Tells a client to set a player ready or not ready.
        /// Used so that players can visualize who has readied, and who has not.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="identity"></param>
        /// <param name="ready"></param>
        [TargetRpc]
        private void TargetSetReady(NetworkConnection conn, NetworkObject identity, bool ready)
        {
            SetReady(ready, identity, false);
        }

        /// <summary>
        /// Called on server when a client leaves a room.
        /// </summary>
        /// <param name="roomDetails"></param>
        /// <param name="leaver"></param>
        private void LobbyNetwork_OnClientLeftRoom(RoomDetails roomDetails, NetworkObject leaver)
        {
            //If no more members in room then try to remove from readyplayers.
            if (roomDetails.MemberIds.Count == 0)
            {
                _serverReadyPlayers.Remove(roomDetails);
            }
            //If still has members then remove leaving member.
            else
            {
                if (_serverReadyPlayers.TryGetValue(roomDetails, out HashSet<NetworkObject> idents))
                    idents.Remove(leaver);
            }
        }

        /// <summary>
        /// Called on client when a member leaves their current room.
        /// </summary>
        /// <param name="obj"></param>
        private void LobbyNetwork_OnMemberLeft(NetworkObject obj)
        {
            /* If self that left then clear ready players.
             * Otherwise remove leaving player. */
            if (obj == InstanceFinder.ClientManager.Connection.FirstObject)
                _clientReadyPlayers.Clear();
            else
                _clientReadyPlayers.Remove(obj);
        }

        
        #endregion

        #region Sign in.
        /// <summary>
        /// Called on client when trying to sign in.
        /// </summary>
        /// <param name="username"></param>
        [Client]
        public static void Login(string username)
        {
            _instance.LogInInternal(username);
        }

        private void LogInInternal(string username)
        {
            CmdLogIn(username);
        }

        /// <summary>
        /// Tries to sign in.
        /// </summary>
        /// <param name="username"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdLogIn(string username, NetworkConnection sender = null)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            string failedReason = string.Empty;
            bool success = OnLogIn(ref username, ref failedReason);
            //If nothing failed.
            if (success)
            {
                //Add to usernames on server.
                LoggedInUsernames[ci.Owner] = username;
                ci.PlayerSettings.SetUsername(username);
                OnClientLoggedIn?.Invoke(ci.NetworkObject);
                TargetSignInSuccess(ci.Owner, username);
            }
            else
            {
                TargetSignInFailed(ci.Owner, failedReason);
            }
        }

        /// <summary>
        /// Returns if a user can sign in.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="failedReason"></param>
        /// <returns>True if can sign in.</returns>
        protected bool OnLogIn(ref string username, ref string failedReason)
        {
            //Check if in logged in users already.
            foreach (KeyValuePair<NetworkConnection, string> item in LoggedInUsernames)
            {
                //Username is already taken.
                if (item.Value.ToLower() == username.ToLower())
                {
                    failedReason = "Username is already taken.";
                    return false;
                }
            }

            //All checks passed.
            return true;
        }


        /// <summary>
        /// Received when successfully signed in.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="username"></param>
        [TargetRpc]
        private void TargetSignInSuccess(NetworkConnection conn, string username)
        {
            _lobbyWindowsManager.LoginWindow.LogInSuccess(username);
        }

        /// <summary>
        /// Received when failed to sign in.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="username"></param>
        [TargetRpc]
        private void TargetSignInFailed(NetworkConnection conn, string failedReason)
        {
            if (failedReason == string.Empty)
                failedReason = "Sign in failed.";
            _lobbyWindowsManager.LoginWindow.LogInFailed(failedReason);
        }


        #endregion

        #region Create room.
        /// <summary>
        /// Called on client when trying to create a room.
        /// </summary>
        /// <param name="joinAlreadyCreated">True to join roomName if it could not be created due to already existing.</param>
        [Client]
        public static void CreateRoom(string roomName, string password, bool lockOnStart, int playerCount, bool joinAlreadyCreated = false)
        {
            _instance.CreateRoomInternal(roomName, password, lockOnStart, playerCount, joinAlreadyCreated);
        }
        private void CreateRoomInternal(string roomName, string password, bool lockOnStart, int playerCount, bool joinAlreadyCreated)
        {
            CmdCreateRoom(roomName, password, lockOnStart, playerCount, joinAlreadyCreated);
        }

        /// <summary>
        /// Tries to create a room.
        /// </summary>
        /// <param name="username"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdCreateRoom(string roomName, string password, bool lockOnStart, int playerCount, bool joinAlreadyCreated, NetworkConnection sender = null)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            bool success = false;
            string failedReason = string.Empty;
            //Already in a room.
            if (ReturnRoomDetails(ci.NetworkObject) != null)
                failedReason = "You are already in a room.";
            else
                success = OnCreateRoom(ref roomName, password, playerCount, ref failedReason);

            //If nothing failed.
            if (success)
            {
                /* Make a new room details.
                 * Add creator to members and
                 * assign room name. */
                RoomDetails roomDetails = new RoomDetails(roomName, password, lockOnStart, playerCount);
                roomDetails.AddMember(ci.NetworkObject);
                CreatedRooms.Add(roomDetails);
                ConnectionRooms[ci.Owner] = roomDetails;

                OnClientCreatedRoom?.Invoke(roomDetails, ci.NetworkObject);
                TargetCreateRoomSuccess(ci.Owner, roomDetails);
                RpcUpdateRooms(new RoomDetails[] { roomDetails });
            }
            //Room creation failed.
            else
            {
                if (!joinAlreadyCreated)
                    TargetCreateRoomFailed(ci.Owner, failedReason);
                else
                    JoinRoom_Logic(roomName, password, sender);
            }
        }

        /// <summary>
        /// Returns if a room can be created.
        /// </summary>
        protected bool OnCreateRoom(ref string roomName, string password, int playerCount, ref string failedReason)
        {
            if (InstanceFinder.IsServer)
            {
                RoomDetails roomDetails = ReturnRoomDetails(roomName);
                if (roomDetails != null)
                {
                    failedReason = "Room name already exist.";
                    return false;
                }
            }

            //All checks passed.
            return true;
        }

      

        /// <summary>
        /// Received when creating a room.
        /// </summary>
        /// <param name="success"></param>
        [TargetRpc]
        private void TargetCreateRoomSuccess(NetworkConnection conn, RoomDetails roomDetails)
        {
            CurrentRoom = roomDetails;
            _lobbyWindowsManager.CreateRoomWindow.RoomCreatedSuccess();
            _lobbyWindowsManager.RoomWindow.RoomCreatedSuccess(roomDetails);
            //Also send member joined to self.
            MemberJoined(InstanceFinder.ClientManager.Connection.FirstObject);
        }
        /// <summary>
        /// Received when failed to create a room.
        /// </summary>
        /// <param name="success"></param>
        [TargetRpc]
        private void TargetCreateRoomFailed(NetworkConnection conn, string failedReason)
        {
            CurrentRoom = null;
            _lobbyWindowsManager.CreateRoomWindow.RoomCreatedFailed(failedReason);
        }
        #endregion

        #region Join room.
        /// <summary>
        /// Called on client when they try to join a room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="playerCount"></param>
        [Client]
        public static void JoinRoom(string roomName, string password)
        {
            _instance.JoinRoomInternal(roomName, password);
        }
        private void JoinRoomInternal(string roomName, string password)
        {
            CmdJoinRoom(roomName, password);
        }

        /// <summary>
        /// Tries to join a room.
        /// </summary>
        /// <param name="username"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdJoinRoom(string roomName, string password, NetworkConnection sender = null)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            JoinRoom_Logic(roomName, password, sender);
        }

        private void JoinRoom_Logic(string roomName, string password, NetworkConnection sender)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            string failedReason = string.Empty;
            RoomDetails roomDetails = null;
            bool success = OnJoinRoom(roomName, password, ci.NetworkObject, ref failedReason, ref roomDetails);
            //If nothing failed.
            if (success)
            {
                roomDetails.AddMember(ci.NetworkObject);
                ConnectionRooms[ci.Owner] = roomDetails;
                TargetJoinRoomSuccess(ci.Owner, roomDetails);
                OnClientJoinedRoom?.Invoke(roomDetails, ci.NetworkObject);

                RpcUpdateRooms(new RoomDetails[] { roomDetails });
                /* Tell each member of the room that someone joined.
                 * This is also sent to joiner. */
                foreach (NetworkObject item in roomDetails.MemberIds)
                    TargetMemberJoined(item.Owner, ci.NetworkObject);
            }
            //Room creation failed.
            else
            {
                TargetJoinRoomFailed(ci.Owner, failedReason);
            }
        }

        /// <summary>
        /// Returns if the roomName may be joined.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="joiner"></param>
        /// <returns></returns>
        protected bool OnJoinRoom(string roomName, string password, NetworkObject joiner, ref string failedReason, ref RoomDetails roomDetails)
        {
            //Already in a room. 
            if (ReturnRoomDetails(joiner) != null)
            {
                failedReason = "You are already in a room.";
                return false;
            }

            roomDetails = ReturnRoomDetails(roomName);
            //Room doesn't exist.
            if (roomDetails == null)
            {
                failedReason = "Room does not exist.";
                return false;
            }
            //Room exist.
            else
            {
                //Wrong password.
                if (roomDetails.IsPassworded && password != roomDetails.Password)
                {
                    failedReason = "Incorrect room password.";
                    return false;
                }
                //Full.
                if (roomDetails.MemberIds.Count >= roomDetails.MaxPlayers)
                {
                    failedReason = "Room is full.";
                    return false;
                }
                //If started and locked on start.
                if (roomDetails.IsStarted && roomDetails.LockOnStart)
                {
                    failedReason = "Room has already started.";
                    return false;
                }
                //Kicked from room.
                if (roomDetails.IsKickedMember(joiner))
                {
                    failedReason = "You are kicked from that room.";
                    return false;
                }
            }

            //All checks passed.
            return true;
        }

        /// <summary>
        /// Called when a member joins your room.
        /// </summary>
        /// <param name="member"></param>
        [TargetRpc]
        private void TargetMemberJoined(NetworkConnection conn, NetworkObject member)
        {
            //Not in a room, shouldn't have got this. Likely left as someone joined.
            if (CurrentRoom == null)
                return;

            MemberJoined(member);
        }

        /// <summary>
        /// Called when a member joins your room. Can also be called after creating a room.
        /// </summary>
        /// <param name="member"></param>
        private void MemberJoined(NetworkObject member)
        {
            CurrentRoom.AddMember(member);
            OnMemberJoined?.Invoke(member);
        }

        /// <summary>
        /// Called when successfully joined a room.
        /// </summary>
        /// <param name="roomDetails"></param>
        [TargetRpc]
        private void TargetJoinRoomSuccess(NetworkConnection conn, RoomDetails roomDetails)
        {
            CurrentRoom = roomDetails;
            _lobbyWindowsManager.LobbyWindow.RoomJoinedSuccess(roomDetails);
            _lobbyWindowsManager.RoomWindow.RoomJoinedSuccess(roomDetails);
        }
        /// <summary>
        /// Called when failed to join a room.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="username"></param>
        [TargetRpc]
        private void TargetJoinRoomFailed(NetworkConnection conn, string failedReason)
        {
            CurrentRoom = null;
            _lobbyWindowsManager.LobbyWindow.RoomJoinedFailed(failedReason);

        }
        #endregion

        #region Leave room.
        /// <summary> 
        /// Called when a client quits the server.
        /// </summary>
        [Server]
        private void ClientLeftServer(NetworkConnection conn)
        {
            if (FindClientInstance(conn, out ClientInstance ci))
                RemoveFromRoom(ci.NetworkObject, true);
        }

        /// <summary>
        /// Called on client when they try to leave a room.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="playerCount"></param>
        [Client]
        public static void LeaveRoom()
        {
            _instance.LeaveRoomInternal();
        }
        private void LeaveRoomInternal()
        {
            if (CurrentRoom != null)
                CmdLeaveRoom();
        }

        /// <summary>
        /// Tries to make a client leave a room.
        /// </summary>
        /// <param name="username"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdLeaveRoom(NetworkConnection sender = null)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            TryLeaveRoom(ci.NetworkObject);
        }

        /// <summary>
        /// Tries to remove client from a room on server.
        /// </summary>
        /// <param name="clientId"></param>
        [Server]
        public void TryLeaveRoom(NetworkObject clientId)
        {
            RoomDetails roomDetails = RemoveFromRoom(clientId, false);
            bool success = (roomDetails != null);

            if (success)
                TargetLeaveRoomSuccess(clientId.Owner);
            else
                TargetLeaveRoomFailed(clientId.Owner);
        }

        /// <summary>
        /// Called after succesfully leaving a room.
        /// </summary>
        [TargetRpc]
        private void TargetLeaveRoomSuccess(NetworkConnection conn)
        {
            _lobbyWindowsManager.RoomWindow.RoomLeftSuccess();
        }
        /// <summary>
        /// Called after failing to leave a room.
        /// </summary>
        [TargetRpc]
        private void TargetLeaveRoomFailed(NetworkConnection conn)
        {
            _lobbyWindowsManager.RoomWindow.RoomLeftFailed();
        }

        /// <summary>
        /// Called after a member has left your room.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="member"></param>
        [TargetRpc]
        private void TargetMemberLeft(NetworkConnection conn, NetworkObject member)
        {
            //Not in a room, shouldn't have got this. Likely left as someone joined.
            if (CurrentRoom == null)
                return;

            CurrentRoom.RemoveMember(member);
            OnMemberLeft?.Invoke(member);
        }
        #endregion

        #region KickMember
        /// <summary>
        /// Tries to kick a member.
        /// </summary>
        [Client]
        public static void KickMember(NetworkObject target)
        {
            _instance.KickMemberInternal(target);
        }
        private void KickMemberInternal(NetworkObject target)
        {
            string failedReason = string.Empty;
            if (OnCanKickMember(CurrentRoom, InstanceFinder.ClientManager.Connection.FirstObject, target, ref failedReason))
                CmdKickMember(target);
            else
                _lobbyWindowsManager.NotificationMessage(failedReason);
        }

        /// <summary>
        /// Tries to join a room.
        /// </summary>
        /// <param name="username"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdKickMember(NetworkObject target, NetworkConnection sender = null)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            NetworkObject kicker = ci.NetworkObject;
            RoomDetails targetRoom = ReturnRoomDetails(target);
            RoomDetails kickerRoom = ReturnRoomDetails(kicker);
            if (targetRoom != null && kickerRoom != null)
            {
                //If trying to kick someone in a different room simply debug locally and ignore client.
                if (kickerRoom != targetRoom)
                {
                    Debug.LogWarning("Client is trying to kick members from a different room.");
                    return;
                }
            }
            else
            {
                /* Kicker or target isn't in a room.
                 * This might happen if leaving as a kick occurs. */
                if (kickerRoom != targetRoom)
                {
                    Debug.LogWarning("Kicker or target is not in a room.");
                    return;
                }
            }

            string failedReason = string.Empty;
            if (OnCanKickMember(kickerRoom, kicker, target, ref failedReason))
            {
                kickerRoom.AddKicked(target);
                TryLeaveRoom(target);
                TargetKickMemberSuccess(kicker.Owner);
            }
            else
            {
                TargetKickMemberFailed(kicker.Owner, failedReason);
            }
        }

        /// <summary>
        /// Returns if target may be kicked from the room.
        /// </summary>
        /// <param name="roomDetails"></param>
        /// <param name="kicker"></param>
        /// <param name="target"></param>
        /// <param name="failedReason"></param>
        /// <returns></returns>
        protected virtual bool OnCanKickMember(RoomDetails roomDetails, NetworkObject kicker, NetworkObject target, ref string failedReason)
        {
            if (!IsRoomHost(roomDetails, kicker))
                failedReason = "Only host may kick.";

            return (failedReason == string.Empty);
        }

        /// <summary>
        /// Received after successfully kicking a member.
        /// </summary>
        /// <param name="conn"></param>
        [TargetRpc]
        private void TargetKickMemberSuccess(NetworkConnection conn)
        {
            _lobbyWindowsManager.RoomWindow.ProcessKickMemberSuccess();
           
        }
        /// <summary>
        /// Received after failing to kicking a member.
        /// </summary>
        /// <param name="conn"></param>
        /// <paramref name="failedReason"/></param>
        private void TargetKickMemberFailed(NetworkConnection conn, string failedReason)
        {
            if (failedReason == string.Empty)
                failedReason = "Kick failed.";
            _lobbyWindowsManager.RoomWindow.ProcessKickMemberFailed(failedReason);
        }

        #endregion

        #region Start room.
        /// <summary>
        /// Returns if a client can use the start button.
        /// </summary>
        /// <param name="roomDetails"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [Client(Logging = LoggingType.Off)]
        public static bool CanUseStartButton(RoomDetails roomDetails, NetworkObject clientId)
        {
            return _instance.OnCanUseStartButton(roomDetails, clientId);
        }
        /// <summary>
        /// Returns if a client can use the start button.
        /// </summary>
        /// <param name="roomDetails"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        protected virtual bool OnCanUseStartButton(RoomDetails roomDetails, NetworkObject clientId)
        {
            //RoomDetails is null; shouldn't happen but may under extremely rare race conditions.
            if (roomDetails == null)
                return false;
            //Joined room after it started and is lock on start. Shouldn't be possible.
            if (roomDetails.IsStarted && roomDetails.LockOnStart)
                return false;
            /* Not host, and room hasn't started yet.
             * Only host can initialize first start. */
            if (!IsRoomHost(roomDetails, clientId) && !roomDetails.IsStarted)
                return false;

            return true;
        }
        /// <summary>
        /// Returns the first failed message when trying to start a game.
        /// </summary>
        /// <param name="roomDetails"></param>
        /// <param name="clientId"></param>
        /// <param name="failedReason"></param>
        /// <param name="asServer">True if running method as server.</param>
        /// <returns>True if can start room.</returns>
        public static bool CanStartRoom(RoomDetails roomDetails, NetworkObject clientId, ref string failedReason, bool asServer)
        {
            return _instance.OnCanStartRoom(roomDetails, clientId, ref failedReason, asServer);
        }
        /// <summary>
        /// Returns the first failed message when trying to start a game.
        /// </summary>
        /// <param name="roomDetails"></param>
        /// <param name="clientId"></param>
        /// <param name="failedReason"></param>
        /// <param name="asServer">True if running method as server.</param>
        /// <returns>True if can start room.</returns> //todo fix al lreturn messages which sya return empty string blah blah.
        protected bool OnCanStartRoom(RoomDetails roomDetails, NetworkObject clientId, ref string failedReason, bool asServer)
        {
            //RoomDetails is null; shouldn't happen but may under extremely rare race conditions.
            if (roomDetails == null)
            {
                failedReason = "Room information is missing.";
                return false;
            }
            //Joined room after it started and is lock on start. Shouldn't be possible.
            if (roomDetails.IsStarted && roomDetails.LockOnStart)
            {
                failedReason = "Room has already started. Try another room.";
                return false;
            }
            /* Not host, and room hasn't started yet.
             * Only host can initialize first start. */
            if (!IsRoomHost(roomDetails, clientId) && !roomDetails.IsStarted)
            {
                failedReason = "You are not the host of your current room.";
                return false;
            }
            //Not enough players.
            if (roomDetails.MemberIds.Count < 1)
            {
                failedReason = "There must be at least two players to start a game.";
                return false;
            }
            //No configured scenes.
            string[] scenes = _gameSceneConfigurations.GetGameScenes();
            if (scenes == null || scenes.Length == 0)
            {
                failedReason = "No scenes are specified as the game scene.";
                return false;
            }

            ///ready

            //Not enough members to start room.
            if (roomDetails.MemberIds.Count < 1)
            {
                failedReason = "Not enough members to start.";
                return false;
            }

            /* If match has already started and this far then lock on start is false,
             * meaning players can join and leave as they wish. At this point there is
             * no reason to require players ready since the game started. */
            if (roomDetails.IsStarted)
                return true;

            //Try to get current ready for this room. If doesn't exist yet then make.
            HashSet<NetworkObject> readyPlayers;
            //If running as server then use server hashset.
            if (asServer)
            {
                if (!_serverReadyPlayers.TryGetValue(roomDetails, out readyPlayers))
                {
                    readyPlayers = new HashSet<NetworkObject>();
                    _serverReadyPlayers[roomDetails] = readyPlayers;
                }
            }
            //Otherwise use client hashset.
            else
            {
                readyPlayers = _clientReadyPlayers;
            }

            //clientId是房主，将其设置为准备好的状态
            if (roomDetails.MemberIds[0] == clientId)
                SetReady(true, clientId, asServer);

            //Ready players count is same as member count for room.
            if (readyPlayers.Count == roomDetails.MemberIds.Count)
            {
                return true;
            }
            else
            {
                failedReason = "Not all players are ready.";
                return false;
            }
        }

        /// <summary>
        /// Returns if a client is host of a members list.
        /// </summary>
        /// <param name="members"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public static bool IsRoomHost(RoomDetails roomDetails, NetworkObject clientId)
        {
            if (roomDetails == null || roomDetails.MemberIds == null || roomDetails.MemberIds.Count == 0)
                return false;

            return (roomDetails.MemberIds[0] == clientId);
        }


        [Client]
        public static void StartGame()
        {
            _instance.StartGameInternal();
        }
        private void StartGameInternal()
        {
            CmdStartGame();
        }

        /// <summary>
        /// Tries to start a game.
        /// </summary>
        /// <param name="username"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CmdStartGame(NetworkConnection sender = null)
        {
            ClientInstance ci;
            if (!FindClientInstance(sender, out ci))
                return;

            RoomDetails roomDetails = ReturnRoomDetails(ci.NetworkObject);
            string failedReason = string.Empty;
            bool success = OnCanStartRoom(roomDetails, ci.NetworkObject, ref failedReason, true);
            
            //If still successful.
            if (success)
            {
                if (!roomDetails.IsStarted)
                {
                    //Set started immediately.
                    roomDetails.IsStarted = true;
                    SceneLoadData sld = new SceneLoadData(_gameSceneConfigurations.GetGameScenes());
                    LoadOptions loadOptions = new LoadOptions
                    {
                        LocalPhysics = _gameSceneConfigurations.PhysicsMode,
                        AllowStacking = true,
                    };
                    LoadParams loadParams = new LoadParams
                    {
                        ServerParams = new object[]
                        {
                             ParamsTypes.ServerLoad,
                             roomDetails,
                             sld
                        }
                    };
                    sld.Options = loadOptions;
                    sld.Params = loadParams;

                    /* Only load scene for the server. This is to ensure that the server
                     * can load the scene fine, and once it does it will load for clients. */
                    InstanceFinder.SceneManager.LoadConnectionScenes(sld);
                }
                /* If game has started then we must be sure to not re-initialize everything on server.
                 * Only new client needs to be caught up. */
                else
                {
                    SceneLookupData[] lookups = SceneLookupData.CreateData(roomDetails.Scenes.ToArray());
                    SceneLoadData sld = new SceneLoadData(lookups);
                    //Load for joining client.
                    InstanceFinder.SceneManager.LoadConnectionScenes(ci.Owner, sld);
                }
            }
            //Failed.
            else
            {
                //Inform person trying to start that response is failed.
                TargetStartGameFailed(ci.Owner, roomDetails, failedReason);
            }
        }

        /// <summary>
        /// Called on the server when the client finishes a scene load or unload.
        /// </summary>
        /// <param name="args"></param>
        private void HandleClientLoadedScene(ClientPresenceChangeEventArgs args)
        {

            /* If client is loading into lobby scene then there's no reason
             * to check if they are in a room or not. */
            if (args.Scene == gameObject.scene)
                return;
            Debug.Log("HandleClientLoadedScene=========================" + IsServer);
            NetworkConnection joinerConn = args.Connection;
            //Find roomDetails for the scene client loaded.
            if (ConnectionRooms.TryGetValue(args.Connection, out RoomDetails roomDetails))
            {
                /* Only need to initialize 'started' if not already started.
                 * This method will run even when multiple scenes are being loaded.
                 * So if two scenes were loaded for the game then this would run twice. */
                NetworkObject firstObject = joinerConn.FirstObject;
                if (!roomDetails.StartedMembers.Contains(firstObject))
                {
                    roomDetails.AddStartedMember(firstObject);
                    OnClientStarted?.Invoke(roomDetails, firstObject);
                    TargetLeaveLobby(joinerConn, roomDetails);
                    //Send that args.Connection joined to all other members.
                    foreach (NetworkObject item in roomDetails.MemberIds)
                    {
                        if (item.Owner != joinerConn)
                            TargetMemberStarted(item.Owner, firstObject);
                    }
                    /* Send all members to args.Connection,
                     * that is if there are other members. */
                    if (roomDetails.StartedMembers.Count > 1)
                        TargetMembersStarted(joinerConn, roomDetails.StartedMembers);

                }
            }
            //RoomDetails not found, tell client to unload the scene.
            else
            {
                SceneUnloadData sud = new SceneUnloadData(args.Scene.name);
                InstanceFinder.SceneManager.UnloadConnectionScenes(joinerConn, sud);
                /* Also tell the client they successfully left the room 
                 * so they clean up everything on their end. */
                TargetLeaveRoomSuccess(joinerConn);
            }
        }

        /// <summary>
        /// Called when a member has loaded the game scenes for your room.
        /// </summary>
        /// <param name="member"></param>
        [TargetRpc]
        private void TargetMemberStarted(NetworkConnection conn, NetworkObject member)
        {
            //Not in a room, shouldn't have got this. Likely left as someone joined.
            if (CurrentRoom == null)
                return;

            CurrentRoom.AddStartedMember(member);
            OnMemberStarted?.Invoke(member);
        }

        /// <summary>
        /// Called on a member which just joined a room.
        /// </summary>
        /// <param name="members">All members in the room, including the just joining member.</param>
        [TargetRpc]
        private void TargetMembersStarted(NetworkConnection conn, List<NetworkObject> members)
        {
            //Not in a room, shouldn't have got this. Likely left as someone joined.
            if (CurrentRoom == null)
                return;

            for (int i = 0; i < members.Count; i++)
                CurrentRoom.AddStartedMember(members[i]);
        }

        /// <summary>
        /// Called on the server when the server finishes a scene load.
        /// </summary>
        /// <param name="args"></param>
        private void HandleServerLoadedScenes(SceneLoadEndEventArgs args)
        {
            Debug.Log("HandleServerLoadedScenes=========================" + IsServer);
            object[] parameters = args.QueueData.SceneLoadData.Params.ServerParams;
            //No parameters. This can occur when loading for client after server has loaded the scene.
            if (parameters == null || parameters.Length == 0)
                return;

            ParamsTypes pt = (ParamsTypes)parameters[0];
            //Should never happen but check just in case.
            if (pt == ParamsTypes.ServerLoad)
                ServerLoadedScene(args);
        }

        /// <summary>
        /// Called once server has loaded scenes for a room.
        /// </summary>
        private void ServerLoadedScene(SceneLoadEndEventArgs args)
        {
            Debug.Log("ServerLoadedScene========================="+ IsServer);
            LoadParams lp = args.QueueData.SceneLoadData.Params;
            if (lp == null || lp.ServerParams.Length < 2)
                return;

            object[] parameters = lp.ServerParams;
            RoomDetails roomDetails = (RoomDetails)parameters[1];

            //Connection for who is room host.
            NetworkConnection roomHost = null;
            //Find room host.
            if (roomDetails.MemberIds.Count > 0 && roomDetails.MemberIds[0] != null)
                roomHost = roomDetails.MemberIds[0].Owner;

            /* Scenes werent loaded and none were skipped.
             * Shouldn't happen, but add response just incase. */
            if (args.LoadedScenes.Length == 0 && (args.SkippedSceneNames == null || args.SkippedSceneNames.Length == 0))
            {
                //Tell starter than creation failed. first index in members will always be host.
                if (roomHost != null)
                    TargetStartGameFailed(roomHost, roomDetails, "Server failed to load game scene.");

                return;
            }

            /* If here then scenes were loaded. */
            HashSet<Scene> scenes = new HashSet<Scene>();
            foreach (Scene s in args.LoadedScenes)
                scenes.Add(s);
            /* Scenes must be stored in the roomdetails so the server knows
             * which to unload when the room is empty. This data only exist on the
             * server. */
            roomDetails.Scenes = scenes;
            OnServerLoadedScenes?.Invoke(roomDetails, args);

            //Load clients into scenes.
            NetworkConnection[] conns = new NetworkConnection[roomDetails.MemberIds.Count];
            for (int i = 0; i < roomDetails.MemberIds.Count; i++)
                conns[i] = roomDetails.MemberIds[i].Owner;
            //Build sceneloaddata.
            SceneLoadData sld = new SceneLoadData(args.LoadedScenes)
            {
                Options = new LoadOptions()
                {
                    AllowStacking = true
                },
                PreferredActiveScene = new SceneLookupData(args.LoadedScenes[0]),
            };
            //Load connections in.
            InstanceFinder.SceneManager.LoadConnectionScenes(conns, sld);

            //Update rooms to clients so that their lobby is up to date.
            RpcUpdateRooms(new RoomDetails[] { roomDetails });
        }

        /// <summary>
        /// Called on room creator when start game fails on server.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="username"></param>
        [TargetRpc]
        private void TargetStartGameFailed(NetworkConnection conn, RoomDetails roomDetails, string failedReason)
        {
            _lobbyWindowsManager.RoomWindow.ShowStartGame(false, roomDetails, failedReason);
        }

        /// <summary>
        /// Tells a client to leave the lobby.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="roomDetails"></param>
        /// <param name="gameInstance"></param>
        [TargetRpc]
        private void TargetLeaveLobby(NetworkConnection conn, RoomDetails roomDetails)
        {
            _lobbyWindowsManager.RoomWindow.ShowStartGame(true, roomDetails, string.Empty);
        }
        #endregion

        #region Manage rooms
        /// <summary>
        /// 查找 roomName 返回 RoomDetails
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        private RoomDetails ReturnRoomDetails(string roomName)
        {
            for (int i = 0; i < CreatedRooms.Count; i++)
            {
                //Roomname exist.
                if (CreatedRooms[i].Name.Equals(roomName, System.StringComparison.CurrentCultureIgnoreCase))
                    return CreatedRooms[i];
            }

            //Fall through, doesn't exist.
            return null;
        }

        /// <summary>
        /// Returns RoomDetails of which a client resides in.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        private RoomDetails ReturnRoomDetails(NetworkObject clientId)
        {
            for (int i = 0; i < CreatedRooms.Count; i++)
            {
                for (int z = 0; z < CreatedRooms[i].MemberIds.Count; z++)
                {
                    if (CreatedRooms[i].MemberIds[z] == clientId)
                        return CreatedRooms[i];
                }
            }

            //Fall through, client isnt in a room.
            return null;
        }


        /// <summary>
        /// Tries to remove a clientId from a room.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns>RoomDetails removed from.</returns>
        [Server]
        private RoomDetails RemoveFromRoom(NetworkObject clientId, bool clientDisconnected)
        {
            RoomDetails roomDetails = ReturnRoomDetails(clientId);
            if (roomDetails != null)
            {
                //Let members know someone left.
                foreach (NetworkObject item in roomDetails.MemberIds)
                {
                    //Don't send to disconnecting client.
                    if (clientDisconnected && item == clientId)
                        continue;

                    TargetMemberLeft(item.Owner, item);
                }

                //Remove on server.
                roomDetails.RemoveMember(clientId);
                ConnectionRooms.Remove(clientId.Owner);

                OnClientLeftRoom?.Invoke(roomDetails, clientId);

                //If not disconnecting tell client to unload scenes.
                if (!clientDisconnected)
                {
                    SceneLookupData[] lookups = SceneLookupData.CreateData(roomDetails.Scenes.ToArray());
                    SceneUnloadData sud = new SceneUnloadData(lookups);

                    //If there are scenes to unload then do so.
                    if (lookups.Length > 0)
                        InstanceFinder.SceneManager.UnloadConnectionScenes(clientId.Owner, sud);
                }

                //If room is empty remove room.
                if (roomDetails.MemberIds.Count == 0)
                    CreatedRooms.Remove(roomDetails);
                //If not started, or started but not lock on start.
                if (!roomDetails.IsStarted || (roomDetails.IsStarted && !roomDetails.LockOnStart))
                    RpcUpdateRooms(new RoomDetails[] { roomDetails });
            }
            return roomDetails;
        }

        /// <summary>
        /// 更新客户的房间。这将发送给所有客户。
        /// </summary>
        /// <param name="roomDetails"></param>
        [ObserversRpc]
        public void RpcUpdateRooms(RoomDetails[] roomDetails)
        {
            //See if CurrentRoom needs to be updated.
            if (CurrentRoom != null)
            {
                for (int i = 0; i < roomDetails.Length; i++)
                {
                    //If current room.
                    if (roomDetails[i].Name == CurrentRoom.Name)
                    {
                        CurrentRoom = roomDetails[i];
                        break;
                    }
                }
            }

             _lobbyWindowsManager.RoomWindow.UpdateRoom(roomDetails);
             _lobbyWindowsManager.LobbyWindow.UpdateRooms(roomDetails);
        }

        /// <summary>
        /// Sends current rooms to a connection.
        /// </summary>
        /// <param name="conn"></param>
        private void SendRooms(NetworkConnection conn)
        {
            /* Send current rooms to new client. */
            List<RoomDetails> rooms = new List<RoomDetails>();
            for (int i = 0; i < CreatedRooms.Count; i++)
                rooms.Add(CreatedRooms[i]);

            //Send remaining rooms.
            if (rooms.Count > 0)
                TargetInitialRooms(conn, rooms.ToArray());
        }

        /// <summary>
        /// Sets rooms on a targeted client.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="roomDetails"></param>
        [TargetRpc]
        public void TargetInitialRooms(NetworkConnection conn, RoomDetails[] roomDetails)
        {
            _lobbyWindowsManager.LobbyWindow.UpdateRooms(roomDetails);
        }
        #endregion     

        #region Helpers.
        /// <summary>
        /// 寻找并且输出 Connection 的 ClientInstance
        /// </summary>
        /// <param name="conn"></param>
        /// <returns>True if ClientInstnace was found.</returns>
        private bool FindClientInstance(NetworkConnection conn, out ClientInstance ci)
        {
            ci = null;
            if (conn == null)
            {
                Debug.Log("Connection is null. Mirror broke something.");
                return false;
            }
            ci = ClientInstance.ReturnClientInstance(conn);
            if (ci == null)
            {
                Debug.LogWarning("ClientInstance not found for connection.");
                return false;
            }

            return true;
        }
        #endregion

    }

}
