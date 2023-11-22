
using FishNet;
using Michsky.MUIP;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FPS.Lobby
{
    public class RoomWindow : MonoBehaviour
    {

        #region Types.
        /// <summary>
        /// Ways to process a room status response.
        /// </summary>
        private enum RoomProcessingTypes
        {
            Unset,
            Create,
            Join,
            Leave,
            Start
        }

        #endregion

        #region Public.
        /// <summary>
        /// Current member entries.
        /// </summary>
        [HideInInspector]
        public List<MemberEntry> MemberEntries = new List<MemberEntry>();
        #endregion

        #region Serialized.
        /// <summary>
        /// Button used to start the game.
        /// </summary>
        [Tooltip("Button used to start the game.")]
        [SerializeField]
        private ButtonManager _startButton;
        /// <summary>
        /// Content to hold room member listings.
        /// </summary>
        [Tooltip("Content to hold room member listings.")]
        [SerializeField]
        private Transform _membersContent;
        /// <summary>
        /// Prefab to spawn for each member entry.
        /// </summary>
        [Tooltip("Prefab to spawn for each member entry.")]
        [SerializeField]
        private MemberEntry _memberEntryPrefab;
        #endregion

        #region Private.
        /// <summary>
        /// True when waiting for start response.
        /// </summary>
        private bool _awaitingStartResponse = false;
        /// <summary>
        /// True when waiting for kick response.
        /// </summary>
        private bool _awaitingkickResponse = false;

        private LobbyWindowsManager loobyWindowsManager;
        #endregion




        public void FirstInitialize(LobbyWindowsManager loobyWindowsManager)
        {
            this.loobyWindowsManager = loobyWindowsManager;
            Reset();
        }


        public void Reset()
        {
            _awaitingkickResponse = false;
            _awaitingStartResponse = false;

            //Destroy children of content. This is just to get rid of any placeholders.
            foreach (Transform c in _membersContent)
                Destroy(c.gameObject);
            MemberEntries.Clear();
            ProcessRoomStatus(RoomProcessingTypes.Unset, false, null, string.Empty);
        }


        /// <summary>
        /// Shows canvases for a successful room creation.
        /// </summary>
        /// <param name="show"></param>
        public void RoomCreatedSuccess(RoomDetails roomDetails)
        {
            ProcessRoomStatus(RoomProcessingTypes.Create, true, roomDetails, string.Empty);
        }

        /// <summary>
        /// Called when successfully joined a room.
        /// </summary>
        /// <param name="roomDetails"></param>
        public void RoomJoinedSuccess(RoomDetails roomDetails)
        {
            ProcessRoomStatus(RoomProcessingTypes.Join, true, roomDetails, string.Empty);
        }


        /// <summary>
        /// Called when successfully leaving a room.
        /// </summary>
        public void RoomLeftSuccess()
        {
            ProcessRoomStatus(RoomProcessingTypes.Leave, true, null, string.Empty);
            loobyWindowsManager.OpenWindow("Lobby");
        }

        /// <summary>
        /// Called when failing to leave a room.
        /// </summary>
        public void RoomLeftFailed()
        {
            ProcessRoomStatus(RoomProcessingTypes.Leave, false, null, string.Empty);
        }

        /// <summary>
        /// Shows canvases based on start game status.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="failedReason"></param>
        public void ShowStartGame(bool success, RoomDetails roomDetails, string failedReason)
        {
            _awaitingStartResponse = false;
            ProcessRoomStatus(RoomProcessingTypes.Start, success, roomDetails, failedReason);
        }

        /// <summary>
        /// Processes the results of room actions.
        /// </summary>
        private void ProcessRoomStatus(RoomProcessingTypes processType, bool success, RoomDetails roomDetails, string failedReason)
        {
            
            bool hideCondition = (processType == RoomProcessingTypes.Unset) ||
                (processType == RoomProcessingTypes.Leave && success) ||
                (processType == RoomProcessingTypes.Start && success) ||
                (processType == RoomProcessingTypes.Join && !success) ||
                (processType == RoomProcessingTypes.Create && !success);

            bool show = !hideCondition;

            if (show)
            {
                loobyWindowsManager.OpenWindow("Room");
            }

            //If hiding also destroy entries.
            if (!show)
                DestroyMemberEntries();

            /* StartButton may become interactable for a number of reasons.
            * Such as, being the host, host leaving, or joining an already
            * started game. */
            UpdateStartButton();

            //If failed to create room.
            if (failedReason != string.Empty)
                loobyWindowsManager.NotificationMessage(failedReason);
        }

        /// <summary>
        /// Destroys current member entries.
        /// </summary>
        private void DestroyMemberEntries()
        {
            for (int i = 0; i < MemberEntries.Count; i++)
                Destroy(MemberEntries[i].gameObject);

            MemberEntries.Clear();
        }

        /// <summary>
        /// Creates member entries for all members in roomDetails.
        /// </summary>
        /// <param name="roomDetails"></param>
        private void CreateMemberEntries(RoomDetails roomDetails)
        {
            DestroyMemberEntries();
            UpdateStartButton();

            bool host = LobbyNetwork.IsRoomHost(roomDetails, InstanceFinder.ClientManager.Connection.FirstObject);
            //Add current members to content.
            for (int i = 0; i < roomDetails.MemberIds.Count; i++)
            {
                MemberEntry entry = Instantiate(_memberEntryPrefab, _membersContent);
                entry.FirstInitialize(this, roomDetails.MemberIds[i], roomDetails.StartedMembers.Contains(roomDetails.MemberIds[i]));
                /* Set kick active if member isnt self, match hasnt already started,
                 * and if host. */
                entry.SetKickActive(
                    roomDetails.MemberIds[i] != InstanceFinder.ClientManager.Connection.FirstObject &&
                    host &&
                    !roomDetails.IsStarted
                    );

                MemberEntries.Add(entry);
            }
        }

        /// <summary>
        /// Updates if start button is enabled.
        /// </summary>
        private void UpdateStartButton()
        {
            string startFailedString = string.Empty;
            _startButton.isInteractable = LobbyNetwork.CanUseStartButton(LobbyNetwork.CurrentRoom, InstanceFinder.ClientManager.Connection.FirstObject);
        }

        /// <summary>
        /// Updates the rooms list.
        /// </summary>
        /// <param name="roomDetails"></param>
        public void UpdateRoom(RoomDetails[] roomDetails)
        {
            RoomDetails currentRoom = LobbyNetwork.CurrentRoom;
            //Not in a room, nothing to update.
            if (currentRoom == null)
                return;

            for (int i = 0; i < roomDetails.Length; i++)
            {
                //Not current room.
                if (roomDetails[i].Name != currentRoom.Name)
                    continue;

                /* It's easier to just re-add entries so 
                 * that's what we'll do. */
                CreateMemberEntries(roomDetails[i]);
            }
        }

        /// <summary>
        /// Received when Start Game is pressed.
        /// </summary>
        public void OnClick_StartGame()
        {
            //Still waiting for a server response.
            if (_awaitingStartResponse)
                return;

            string failedReason = string.Empty;
            bool result = LobbyNetwork.CanStartRoom(LobbyNetwork.CurrentRoom, InstanceFinder.ClientManager.Connection.FirstObject, ref failedReason, false);
            if (!result)
            {
                loobyWindowsManager.NotificationMessage(failedReason);
            }
            else
            {
                _awaitingStartResponse = true;
                _startButton.isInteractable = false;
                LobbyNetwork.StartGame();
            }
        }


        /// <summary>
        /// Received when Leave is pressed.
        /// </summary>
        public void OnClick_Leave()
        {
            LobbyNetwork.LeaveRoom();
        }


        /// <summary>
        /// Tries to kick a member.
        /// </summary>
        /// <param name="entry"></param>
        public void KickMember(MemberEntry entry)
        {
            if (_awaitingkickResponse)
                return;
            if (entry.MemberId == null)
                return;

            _awaitingkickResponse = true;
            //Try to kick member Id on entry.
            LobbyNetwork.KickMember(entry.MemberId);
        }

        /// <summary>
        /// Received after successfully kicking a member.
        /// </summary>
        public void ProcessKickMemberSuccess()
        {
            _awaitingkickResponse = false;
        }


        /// <summary>
        /// Received after failing to kick a member.
        /// </summary>
        /// <param name="failedReason"></param>
        public void ProcessKickMemberFailed(string failedReason)
        {
            _awaitingkickResponse = false;
            if (failedReason != string.Empty)
                loobyWindowsManager.NotificationMessage(failedReason);
        }
    }
}
