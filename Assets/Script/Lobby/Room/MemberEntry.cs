using FishNet;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS.Lobby
{
    public class MemberEntry : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Member this entry is for.
        /// </summary>
        public NetworkObject MemberId { get; private set; }
        /// <summary>
        /// PlayerSettings for the MemberId.
        /// </summary>
        public PlayerSettings PlayerSettings { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// Icon to show if member is currently started.
        /// </summary>
        [Tooltip("Icon to show if member is currently started.")]
        [SerializeField]
        private Image _startedIcon;

        /// <summary>
        /// Icon to show if member is currently ready.
        /// </summary>
        [Tooltip("Icon to show if member is currently ready.")]
        [SerializeField]
        private Image _readyIcon = null;

        /// <summary>
        /// Name of member.
        /// </summary>
        [Tooltip("Name of member.")]
        [SerializeField]
        private TextMeshProUGUI _name;

        /// <summary>
        /// Kick button.
        /// </summary>
        [Tooltip("Kick button.")]
        [SerializeField]
        private GameObject _kickButton;

        /// <summary>
        /// Ready button.
        /// </summary>
        [Tooltip("Ready button.")]
        [SerializeField]
        private GameObject _readyButton;

        [Tooltip("Color to show when start.")]
        [SerializeField]
        private Color _startColor = Color.green;
        /// <summary>
        /// Color to show when not ready.
        /// </summary>
        [Tooltip("Color to show when not start.")]
        [SerializeField]
        private Color _unstartColor = Color.white;
        #endregion


        #region Private.
        /// <summary>
        /// Current ready state of this object.
        /// </summary>
        private bool _ready = false;
        /// <summary>
        /// True if awaiting for a ready response.
        /// </summary>
        private bool _awaitingReadyResponse = false;

        /// <summary>
        /// CurrentRoomMenu parenting this object.
        /// </summary>
        private RoomWindow roomWindow;
        #endregion

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        /// <param name="id"></param>
        public virtual void FirstInitialize(RoomWindow crm, NetworkObject id, bool started)
        {
            if (started)
            {
                _startedIcon.color = _startColor;
            }
            else
            {
                _startedIcon.color = _unstartColor;
            }

            roomWindow = crm;
            MemberId = id;
            PlayerSettings = id.GetComponent<PlayerSettings>();

            _name.text = PlayerSettings.GetUsername();


            NetworkObject localNob = InstanceFinder.ClientManager.Connection.FirstObject;

            bool local = (id == localNob);
            //If for local player.
            if (local)
            {
                /* Multiple conditions will set the player as automatically
                 * ready.
                 * Being the host of the room.
                 * Or if the match has already started. */
                if (LobbyNetwork.CurrentRoom.IsStarted || LobbyNetwork.IsRoomHost(LobbyNetwork.CurrentRoom, localNob))
                {
                    _readyButton.SetActive(false);
                    _readyIcon.gameObject.SetActive(true);
                    SetLocalReady(true);
                }
                //Otherwise show ready button.
                else
                {
                    _readyButton.SetActive(true);
                    _readyIcon.gameObject.SetActive(false);

                }
            }
            //Not for local player.
            else
            {
                _readyButton.SetActive(false);
                _readyIcon.gameObject.SetActive(false);
            }
        }
        private void Awake()
        {
            LobbyNetwork.OnMemberSetReady += ReadyLobbyNetwork_OnMemberSetReady;
        }

        private void OnDestroy()
        {
            LobbyNetwork.OnMemberSetReady -= ReadyLobbyNetwork_OnMemberSetReady;
        }


        /// <summary>
        /// Sets kick button active state.
        /// </summary>
        /// <param name="visible"></param>
        public void SetKickActive(bool active)
        {
            _kickButton.SetActive(active);
        }

        /// <summary>
        /// Called when Kick is pressed.
        /// </summary>
        public void OnClick_Kick()
        {
            roomWindow.KickMember(this);
        }

        

        /// <summary>
        /// Called when a member changes their ready state.
        /// </summary>
        private void ReadyLobbyNetwork_OnMemberSetReady(NetworkObject arg1, bool ready)
        {
            //Not for this member.
            if (arg1 != MemberId)
                return;

            //For local client.
            if (MemberId == InstanceFinder.ClientManager.Connection.FirstObject)
            {
                _awaitingReadyResponse = false;
                _ready = ready;
                _readyIcon.gameObject.SetActive(ready);
            }
            //For another player.
            else
            {
                _readyIcon.gameObject.SetActive(ready);
            }
        }

        /// <summary>
        /// Called when ready is pressed.
        /// </summary>
        public void OnClick_Ready()
        {
            SetLocalReady(!_ready);
        }

        /// <summary>
        /// Changes the locla state of ready.
        /// </summary>
        /// <param name="ready"></param>
        private void SetLocalReady(bool ready)
        {
            //Don't do anything if not for local client. Only local client should be able to click this anyway.
            if (MemberId != InstanceFinder.ClientManager.Connection.FirstObject)
                return;
            if (_awaitingReadyResponse)
                return;
            _awaitingReadyResponse = true;
            LobbyNetwork.SetReady(!_ready);
        }
    }

}

