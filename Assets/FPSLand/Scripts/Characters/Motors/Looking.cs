using FirstGearGames.FPSLand.Characters.Vitals;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;


namespace FirstGearGames.FPSLand.Characters.Motors
{

    /// <summary>
    /// 控制 transform 的观察方向并为摄像机提供信息。
    /// </summary>
    public class Looking : NetworkBehaviour
    {

        #region Public.
        /// <summary>
        /// 当前相机应该看向的方向。
        /// </summary>
        public Vector3 _lookDirection { get; private set; }
        /// <summary>
        /// 相机当前应该处于的位置。
        /// </summary>
        public Vector3 LookPosition { get; private set; }
        #endregion

        #region Serialized.
        [SerializeField]
        private Transform _thirdPersonHipBone;
        /// <summary>
        /// 相机的偏移位置（相对于当前transform）
        /// </summary>
        [Tooltip("Offset camera should be from this transform.")]
        [SerializeField]
        private Vector3 _cameraOffset = new Vector3(0f, 1.55f, 0f);
        /// <summary>
        /// 本地位置偏移，第一人称手臂应该在相机下方。
        /// </summary>
        [Tooltip("Local positional offset first person arms should be under camera.")]
        [SerializeField]
        private Vector3 _firstPersonPositionalOffset = new Vector3(0f, 1.55f, 0f);
        /// <summary>
        /// 本地位置偏移，第一人称手臂应该在相机下方。
        /// </summary>
        public Vector3 FirstPersonPositionalOffset { get { return _firstPersonPositionalOffset; } }
        /// <summary>
        /// 本地旋转偏移，第一人称手臂应该在相机下方。
        /// </summary>
        [Tooltip("Local rotational offset first person arms should be under the camera.")]
        [SerializeField]
        private Vector3 _firstPersonRotationalOffset = Vector3.zero;
        /// <summary>
        /// 本地旋转偏移，第一人称手臂应该在相机下方。
        /// </summary>
        public Vector3 FirstPersonRotaionalOffset { get { return _firstPersonRotationalOffset; } }
        /// <summary>
        /// How quickly to rotate yaw.
        /// </summary>
        [Tooltip("How quickly to rotate yaw.")]
        [SerializeField]
        private float _yawRate = 3f;
        /// <summary>
        /// How quickly to look up and down.
        /// </summary>
        [Tooltip("How quickly to look up and down.")]
        [SerializeField]
        private float _pitchRate = 3f;
        #endregion

        #region Private.
        /// <summary>
        /// Health component on this object.
        /// </summary>
        private Health _health;
        /// <summary>
        /// AnimatorController on this object.
        /// </summary>
        private AnimatorController _animatorController;
        /// <summary>
        /// Next time to set spectator pitch.
        /// </summary>
        private float _nextSpectatorPitchTime = 0f;
        #endregion

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkInitialize();
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkInitialize();
        }

        private void Update()
        {
            if (base.IsOwner)
            {
                UpdateLooking();
                CheckSendSpectatorPitch();
            }
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        private void NetworkInitialize()
        {
            //If owner.
            if (base.IsOwner)
            {
                LookPosition = transform.position + _cameraOffset;
                _health = GetComponent<Health>();
                _health.OnRespawned += Health_OnRespawn;
            }
            if (!base.IsOwner || base.IsServer)
            {
                _animatorController = GetComponent<AnimatorController>();
            }
        }

        /// <summary>
        /// Called when the character is respawned.
        /// </summary>
        private void Health_OnRespawn()
        {
            //Reset look direction.
            _lookDirection = Vector3.zero;
        }

        /// <summary>
        /// 检查 owner 是否应该将数据发送到服务器。
        /// </summary>
        [Client(Logging = LoggingType.Off)]
        private void CheckSendSpectatorPitch()
        {
            if (Time.time < _nextSpectatorPitchTime)
                return;

            CmdSendSpectatorPitch(_lookDirection.x);
        }

        /// <summary>
        /// 发送到服务器，服务器转发到其他玩家
        /// </summary>
        [ServerRpc]
        private void CmdSendSpectatorPitch(float pitch, Channel c = Channel.Unreliable)
        {
            _animatorController.SetPitch(pitch);
        }

        /// <summary>
        /// Updates looking using input.
        /// </summary>
        [Client(Logging = LoggingType.Off)]
        private void UpdateLooking()
        {
            LookPosition = transform.position + _cameraOffset;

            //If has health then look normally.
            if (_health.CurrentHealth > 0)
            {
                //Yaw.
                transform.Rotate(new Vector3(0f, Input.GetAxis("Mouse X"), 0f) * _yawRate);
                //Pitch.
                float pitch = _lookDirection.x + (Input.GetAxis("Mouse Y") * _pitchRate);
                /* If not signed on X then make it
                 * signed for easy clamping. */
                if (pitch > 180f)
                    pitch -= 360f;
                pitch = Mathf.Clamp(pitch, -89f, 89f);

                _lookDirection = new Vector3(pitch, transform.eulerAngles.y, transform.eulerAngles.z);
            }
            //Otherwise look at transform position.
            else
            {
                //Add onto the look position to be a little higher.
                LookPosition += new Vector3(0f, 0.75f, 0f);
                //Rotate towards transform base.
                Quaternion rot = Quaternion.LookRotation(_thirdPersonHipBone.position - (_thirdPersonHipBone.position + new Vector3(0f, 1.5f, 0f)));
                _lookDirection = Quaternion.RotateTowards(Quaternion.Euler(_lookDirection), rot, 180f * Time.deltaTime).eulerAngles;
            }
        }

        /// <summary>
        /// Adds pitch to LookDirection.
        /// </summary>
        /// <param name="pitch"></param>
        public void AddLookDirectionPitch(float pitch)
        {
            pitch += _lookDirection.x;
            /* If not signed on X then make it
             * signed for easy clamping. */
            if (pitch > 180f)
                pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            _lookDirection = new Vector3(pitch, transform.eulerAngles.y, transform.eulerAngles.z);
        }

    }


}