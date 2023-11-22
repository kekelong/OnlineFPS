using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS.Lobby
{
    public class RoomEntry : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// RoomDetails for this entry.
        /// </summary>
        public RoomDetails RoomDetails { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// Image to show when a room is passworded.
        /// </summary>
        [Tooltip("Image to show when a room is passworded.")]
        [SerializeField]
        private Image _passwordedImage = null;
        /// <summary>
        /// Text for room name.
        /// </summary>
        [Tooltip("Text for room name.")]
        [SerializeField]
        private TextMeshProUGUI _roomNameText = null;
        /// <summary>
        /// Text for member count.
        /// </summary>
        [Tooltip("Text for member count.")]
        [SerializeField]
        private TextMeshProUGUI _playerCountText = null;
        #endregion

        #region Private.
        /// <summary>
        /// JoinRoomMenu in parent.
        /// </summary>
        private LobbyWindow _lobbyWindow;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="roomDetails"></param>
        public void Initialize(LobbyWindow lobbyWindow, RoomDetails roomDetails)
        {
            _lobbyWindow = lobbyWindow;
            RoomDetails = roomDetails;

            _roomNameText.text = roomDetails.Name;
            _playerCountText.text = roomDetails.MemberIds.Count + " / " + roomDetails.MaxPlayers;
            _passwordedImage.gameObject.SetActive(roomDetails.IsPassworded);
        }

        /// <summary>
        /// Called when this button is pressed.
        /// </summary>
        public void OnClick_JoinButton()
        {
            /* Make sure room isn't full. Shouldn't be
             * displayed if it is but check anyway. */
            if (RoomDetails.MemberIds.Count >= RoomDetails.MaxPlayers)
            {
                _lobbyWindow.RoomPlayerCountFull();
                return;
            }

            _lobbyWindow.JoinRoom(this);
        }
    }

}
