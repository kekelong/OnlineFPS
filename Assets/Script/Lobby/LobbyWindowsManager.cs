using Michsky.MUIP;
using System;
using System.Linq;
using UnityEngine;

namespace FPS.Lobby
{
    public class LobbyWindowsManager : MonoBehaviour
    {

        #region
        [SerializeField]
        private Camera MainCamera;

        [SerializeField]
        private Transform MainCanvas;

        [SerializeField]
        private NotificationManager notification; 

        [Tooltip("WindowManager reference.")]
        [SerializeField]
        private WindowManager _windowManager;
        public WindowManager WindowManager { get { return _windowManager; } }


        [Tooltip("LoginWindow reference.")]
        [SerializeField]
        private LoginWindow _loginWindow;
        public LoginWindow LoginWindow { get { return _loginWindow; } }

        [Tooltip("LobbyWindow reference.")]
        [SerializeField]
        private LobbyWindow _lobbyWindow;
        public LobbyWindow LobbyWindow { get { return _lobbyWindow; } }

        [Tooltip("LoginWindow reference.")]
        [SerializeField]
        private CreateRoomWindow _createRoomWindow;
        public CreateRoomWindow CreateRoomWindow { get { return _createRoomWindow; } }

        [Tooltip("LoginWindow reference.")]
        [SerializeField]
        private RoomWindow _roomWindow;
        public RoomWindow RoomWindow { get { return _roomWindow; } }
        #endregion


        public void FirstInitialize()
        {
            _loginWindow.FirstInitialize(this);
            _lobbyWindow.FirstInitialize(this);
            _createRoomWindow.FirstInitialize(this);
            _roomWindow.FirstInitialize(this);
            Reset();
        }

        public void Reset()
        {
            
        }


        public void OpenWindow(string windowName)
        {
            _windowManager.OpenWindow(windowName);
        }



        public void StartGame()
        {
            SetLobbyWindowsVisible(false);
        }

        public void SetLobbyWindowsVisible(bool visible)
        {
            MainCanvas.gameObject.SetActive(visible);
            MainCamera.gameObject.SetActive(visible);
        }

        # region Message notification

        public void NotificationMessage(string message)
        {
            //notification.icon = spriteVariable; 
            notification.title = "System Info"; // Change title
            notification.description = message; // Change desc
            notification.UpdateUI(); // Update UI
            notification.Open(); // Open notification
            //notification.Close(); // Close notification         
        }
        #endregion

        #region Security verification
        /// <summary>
        /// Sanitizes username.
        /// </summary>
        /// <returns>An empty string if username passed sanitization. Returns failed reason otherwise.</returns>
        public bool SanitizeUsername(ref string value, ref string failedReason)
        {
            return OnSanitizeUsername(ref value, ref failedReason);
        }
        /// <summary>
        /// Sanitizes username.
        /// </summary>
        /// <returns>True if sanitization passed.</returns>
        private bool OnSanitizeUsername(ref string value, ref string failedReason)
        {
            value = value.Trim();
            SanitizeTextMeshProString(ref value);
            //Length check.
            if (value.Length < 3)
            {
                failedReason = "Username must be at least 3 letters long.";
                return false;
            }
            if (value.Length > 15)
            {
                failedReason = "Username must be at most 15 letters long.";
                return false;
            }
            bool letters = value.All(c => Char.IsLetter(c));
            if (!letters)
            {
                failedReason = "Username may only contain letters.";
                return false;
            }

            return true;
        }
        public bool SanitizeRoomName(ref string value, ref string failedReason)
        {
            return OnSanitizeRoomName(ref value, ref failedReason);
        }
        /// <summary>
        /// Sanitizes username.
        /// </summary>
        /// <returns>True if passed sanitization.</returns>
        private bool OnSanitizeRoomName(ref string value, ref string failedReason)
        {
            value = value.Trim();
            /* Textmesh pro seems to add on an unknown char at the end.
             * If last char is an invalid ascii then remove it. */
            if ((int)value[value.Length - 1] > 255)
                value = value.Substring(0, value.Length - 1);
            //Length check.
            if (value.Length > 25)
            {
                failedReason = "Room name must be at most 25 characters long.";
                return false;
            }
            if (value.Length < 3)
            {
                failedReason = "Room name must be at least 3 characters long.";
                return false;
            }
            bool letters = value.All(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c));
            if (!letters)
            {
                failedReason = "Room name may only contain letters and numbers.";
                return false;
            }

            //All checks passed.
            return true;
        }
        /// <summary>
        /// Sanitizes bad characters from textmeshpro string.
        /// </summary>
        /// <param name="value"></param>
        public void SanitizeTextMeshProString(ref string value)
        {
            if (value.Length == 0)
                return;
            /* Textmesh pro seems to add on an unknown char at the end.
            * If last char is an invalid ascii then remove it. */
            if ((int)value[value.Length - 1] > 255)
                value = value.Substring(0, value.Length - 1);
        }
        #endregion
    }
}
