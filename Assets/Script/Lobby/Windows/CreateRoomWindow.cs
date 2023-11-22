using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS.Lobby
{
    public class CreateRoomWindow : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// Text holding the room name.
        /// </summary>
        [Tooltip("Text holding the room name.")]
        [SerializeField]
        private TMP_InputField _nameText;
        /// <summary>
        /// Text holding the room password.
        /// </summary>
        [Tooltip("Text holding the room password.")]
        [SerializeField]
        private TMP_InputField _passwordText;
        /// <summary>
        /// Lock On Start toggle.
        /// </summary>
        [Tooltip("Lock On Start toggle.")]
        [SerializeField]
        private SwitchManager _lockOnStart;
        #endregion

        #region Private.
        /// <summary>
        /// True if awaiting a create response from the server.
        /// </summary>
        private bool _awaitingCreateResponse = false;

        private LobbyWindowsManager loobyWindowsManager;

        private const int PalyerCount = 8;
        #endregion



        /// <summary>
        /// Initialize this script for use. Should only be completed once.
        /// </summary>
        public void FirstInitialize(LobbyWindowsManager loobyWindowsManager)
        {
            this.loobyWindowsManager = loobyWindowsManager;
            Reset();
        }
        /// <summary>
        /// Resets canvases as though first login.
        /// </summary>
        public void Reset()
        {
            _awaitingCreateResponse = false;
        }

        /// <summary>
        /// Called when create room is clicked.
        /// </summary>
        public void OnClick_CreateRoom()
        {
            if (_awaitingCreateResponse)
                return;

            string roomName = _nameText.text.Trim();
            string password = _passwordText.text;

            //
            int playerCount = PalyerCount;
            string failedReason = string.Empty;
            //If cannot create,
            if (!loobyWindowsManager.SanitizeRoomName(ref roomName, ref failedReason))
            {
                loobyWindowsManager.NotificationMessage(failedReason);
            }
            //Can try to create.
            else
            {
                _awaitingCreateResponse = true;
                loobyWindowsManager.SanitizeTextMeshProString(ref password);
                LobbyNetwork.CreateRoom(roomName, password, _lockOnStart.isOn, playerCount, true);
            }
        }


        /// <summary>
        /// Shows canvases for a successful room creation.
        /// </summary>
        public void RoomCreatedSuccess()
        {
            loobyWindowsManager.NotificationMessage("create room success");
            _passwordText.text = string.Empty;
            _awaitingCreateResponse = false;
        }
        /// <summary>
        /// Shows canvases for a failed room creation.
        /// </summary>
        public void RoomCreatedFailed(string info)
        {
            loobyWindowsManager.NotificationMessage(info);
            _awaitingCreateResponse = false;
        }

    }
}
