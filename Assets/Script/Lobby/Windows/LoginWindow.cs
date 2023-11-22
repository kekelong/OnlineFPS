using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS.Lobby
{
    public class LoginWindow : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField _usernameText;

        [SerializeField]
        private TMP_InputField _passwordText;

        private LobbyWindowsManager loobyWindowsManager;


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

        }

        /// <summary>
        /// Called when LogIn button is pressed.
        /// </summary>
        public void OnClick_LogIn()
        {
            string username = _usernameText.text.Trim();
            string failedReason = string.Empty;
            //Local sanitization failed.
            if (!loobyWindowsManager.SanitizeUsername(ref username, ref failedReason))
            {
                loobyWindowsManager.NotificationMessage(failedReason);
            }
            //Try to login through server.
            else
            {
                LobbyNetwork.Login(username);
               
            }
        }



        /// <summary>
        /// Called after successfully login.
        /// </summary>
        public void LogInSuccess(string username)
        {
            loobyWindowsManager.OpenWindow("Lobby");
            loobyWindowsManager.NotificationMessage("Welcome "+ username);
        }

        /// <summary>
        /// Called after failing to sign in.
        /// </summary>
        public void LogInFailed(string failedReason)
        {
            if (failedReason != string.Empty)
            {
                loobyWindowsManager.NotificationMessage(failedReason);
            }

        }
    }
}
