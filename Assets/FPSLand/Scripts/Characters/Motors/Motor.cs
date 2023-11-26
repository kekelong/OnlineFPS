using FirstGearGames.FPSLand.Characters.Vitals;
using FirstGearGames.FPSLand.Characters.Weapons;
using FirstGearGames.FPSLand.Managers.Gameplay;
using FirstGearGames.FPSLand.Weapons;
using FirstGearGames.Managers.Global;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Utilities;
using System.Linq;
using UnityEngine;

namespace FirstGearGames.FPSLand.Characters.Motors
{

    /// <summary>
    /// 服务器权威 控制移动。客户端预测
    /// </summary>
    public class Motor : NetworkBehaviour
    {
        #region Serialized.
        [Header("Motor")]
        /// <summary>
        /// 每个固定更新周期中用于减弱外部力的强度。应用于固定更新的开始阶段
        /// </summary>
        [Tooltip("How much to dampen external forces by per fixed update. This is applied at the beginning of fixed update.")]
        [SerializeField]
        private float _forceDampening = 5f;
        /// <summary>
        /// 运动的基础速度。可以是一个速度值，也可以是一个力的值.
        /// </summary>
        [Tooltip("How fast to move.")]
        [SerializeField]
        private float _baseMoveRate = 1.65f;
        /// <summary>
        /// How high to jump.
        /// </summary>
        [Space(5f)]
        [Tooltip("How high to jump.")]
        [SerializeField]
        private float _jumpHeight = 2f;
        /// <summary>
        /// 玩家可以进行跳跃的时间间隔
        /// </summary>
        [Tooltip("How often player can jump.")]
        [SerializeField]
        private float _jumpInterval = 1f;
        /// <summary>
        /// 当在与初始跳跃方向相反的方向移动时，最大减弱空中移动的程度。
        /// </summary>
        [Tooltip("Maximum amount to dampen air movement when moving in the opposite direction of initial jump direction.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _maximumAirDampening = 0.75f;

        [Header("Audio")]
        /// <summary>
        /// 在着陆时播放的音频预制体。
        /// </summary>
        [Tooltip("Audio to play when landing.")]
        [SerializeField]
        private GameObject _landingAudioPrefab;
        /// <summary>
        /// 在奔跑时播放的音频预制体。
        /// </summary>
        [Tooltip("Audio to play when running.")]
        [SerializeField]
        private GameObject _runningAudioPrefab;
        #endregion

        #region Private.
        /// <summary>
        /// CharacterController on this object.
        /// </summary>
        private CharacterController _controller;
        /// <summary>
        /// AnimatorController on this object.
        /// </summary>
        private AnimatorController _animatorController;
        /// <summary>
        /// Vertical velocity to apply.
        /// </summary>
        private float _verticalVelocity;
        /// <summary>
        ///下次允许跳跃的时间
        /// </summary>
        private float _nextAllowedJumpTime = 0f;
        /// <summary>
        /// True to jump next frame.
        /// </summary>
        private bool _jump = false;
        /// <summary>
        /// Starting step offset for the controller.
        /// </summary>
        private float _defaultStepOffset;
        /// <summary>
        /// True if the controller is grounded during the update tick, or according to the controller.
        /// </summary>
        private bool _isGrounded;
        /// <summary>
        /// 应用于角色运动的外部力。可以用于模拟击退效果。
        /// </summary>
        private Vector3 _externalForces = Vector3.zero;
        /// <summary>
        /// 跳跃时的移动方向。用于在空中减弱移动。
        /// </summary>
        private Vector3 _lastJumpDirection = Vector3.zero;
        /// <summary>
        /// True if running, as determined by inputs.
        /// </summary>
        private bool _running = false;
        /// <summary>
        /// Next time running audio may play.
        /// </summary>
        private float _nextRunningAudioTime;
        /// <summary>
        /// Becomes true when grounded has changed. Can be set in fixed update or update since isUpdateGrounded is set in both.
        /// 当 grounded 变了，就会变成真的。可以在fixed update或update中设置，因为在两者中都设置了isUpdateGrounded。
        /// </summary>
        private bool _groundedChanged = false;
        /// <summary>
        /// WeaponHandler on this object.
        /// </summary>
        private WeaponHandler _weaponHandler;
        /// <summary>
        /// 上次播放着陆音频的时间。用于防止在连续从悬崖上掉落时快速播放着陆音频
        /// </summary>
        private float _lastLandAudioTime = -1f;
        /// <summary>
        /// Last rotation on owner when replicate data was built.
        /// </summary>
        private float _lastOwnerRotation;
        #endregion

        #region Constants.
        /// <summary>
        /// 在行走或蹲伏时应用于移动速率的乘法因子。
        /// </summary>
        private const float WALK_CROUCH_PERCENT = 0.525f;
        /// <summary>
        /// 奔跑音频播放的频率限制。表示多久才能再次播放奔跑音频。
        /// </summary>
        private const float RUNNING_AUDIO_INTERVAL = 0.35f;
        /// <summary>
        /// 应用于重力的乘法因子，用于使跳跃更加灵敏。
        /// </summary>
        private const float GRAVITY_MULTIPLIER = 2f;
        #endregion

        private void Awake()
        {
            this.enabled = false;
        }

        /// <summary>
        /// 当网络初始化该对象时调用。可能会为服务器或客户端调用，但只会调用一次
        /// 当作为主机或服务器时，此方法将在OnStartServer之前运行。
        /// 当作为客户端时，此方法将在OnStartClient之前运行。
        /// </summary>
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            base.TimeManager.OnTick += TimeManager_OnTick;

            //是本地客户端或服务器
            NetworkInitialize(base.Owner.IsLocalClient || base.IsServer);
        }

        /// <summary>
        /// 初始化，具有权限的用户或服务器使用。
        /// </summary>
        private void NetworkInitialize(bool authoritiveOrServer)
        {
            _controller = GetComponent<CharacterController>();
            if (authoritiveOrServer)
            {
                _weaponHandler = GetComponent<WeaponHandler>();
                _animatorController = GetComponent<AnimatorController>();
                _defaultStepOffset = _controller.stepOffset;

                Health health = GetComponent<Health>();
                health.OnDeath += Health_OnDeath;
                health.OnRespawned += Health_OnRespawned;
            }
            else
            {
                _controller.enabled = false;
            }

            //激活
            this.enabled = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            base.TimeManager.OnTick -= TimeManager_OnTick;
        }

        /// <summary>
        /// Phsyics update step. Called before Update.
        /// </summary>
        private void TimeManager_OnTick()
        {
            //Authoritive client or server.
            if (base.IsOwner || base.IsServer)
            {
                CheckPlayLandedAudio();
            }

            //客户端预测

            //Authoritive client.
            if (base.IsOwner)
            {
                Reconcile(default, false);
                CheckInput(out ReplicateData rd);
                Replicate(rd, false);
            }
            //If server, and not owner.
            if (base.IsServer)
            {
                Replicate(default, true);
                ReconcileData rd = new ReconcileData(transform.position, _verticalVelocity, _externalForces);
                Reconcile(rd, true);
                //发送运行状态到其他客户端，以便他们可以在本地播放奔跑的声音。
                ObserversSetRunning(_running);
            }
        }


        /// <summary>
        /// Frame update step.
        /// </summary>
        private void Update()
        {
            //If owner.
            if (base.IsOwner)
            {
                CheckJump();
            }
            //If any client.
            if (base.IsClient)
            {
                CheckPlayRunningAudio();
            }
        }


        /// <summary>
        /// 检查是否应该在 owner 上播放落地音频。
        /// </summary>
        /// <param name="groundedChanged"></param>
        private void CheckPlayLandedAudio()
        {
            if (!_groundedChanged)
                return;
            _groundedChanged = false;

            //如果速度足够低，则播放音频。
            if (_verticalVelocity < -5f)
                PlayLandingAudio();
        }



        /// <summary>
        /// 播放用于着陆的音频。之后广播给其他玩家。
        /// </summary>
        private void PlayLandingAudio()
        {
            float groundAudioInterval = 0.250f;
            if (Time.time - _lastLandAudioTime < groundAudioInterval)
                return;
            _lastLandAudioTime = Time.time;

            //如果不只有服务器（说明是 host / client ），则播放音频。
            if (!base.IsServerOnly)
            {
                //First person if owner.
                if (base.IsOwner)
                    OfflineGameplayDependencies.AudioManager.PlayFirstPerson(_landingAudioPrefab);
                else
                    OfflineGameplayDependencies.AudioManager.PlayAtPoint(_landingAudioPrefab, transform.position);
            }

            //If server then send RPC to play audio.
            if (base.IsServer)
            {
                ObserversPlayLandingAudio();
            }
        }

        /// <summary>
        /// Plays landing audio over clients.
        /// </summary>
        [ObserversRpc]
        private void ObserversPlayLandingAudio()
        {
            //如果所有者或服务器不播放，
            if (base.IsOwner || base.IsServer)
                return;
            PlayLandingAudio();
        }

        /// <summary>
        /// Received when character is respawned.
        /// </summary>
        private void Health_OnRespawned()
        {
            this.enabled = true;
            _controller.enabled = true;
        }

        /// <summary>
        /// Received when character is dead.
        /// </summary>
        private void Health_OnDeath()
        {
            _verticalVelocity = 0f;
            _controller.enabled = false;
            this.enabled = false;
        }

        /// <summary>
        /// 当在地面上时，将速度设置为0f。
        /// </summary>
        private void SetGroundedVelocity(float deltaTime, bool asServer, bool replaying)
        {
            /* 在角色落地时快速减小重力，以避免在移动到边缘时突然下落。
             * 同时，在角色着陆后逐渐向这个重力值靠近，而不是立即重置重力，以给角色失去动量的感觉。
             */
            if (_controller.isGrounded && _verticalVelocity < -1f)
                _verticalVelocity = Mathf.MoveTowards(_verticalVelocity, -1f, (-Physics.gravity.y * GRAVITY_MULTIPLIER * 2f) * deltaTime);
        }

        /// <summary>
        /// 当角色控制器在移动过程中碰到物体。
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            //如果碰到上方的物体，取消跳跃速度。
            if (_verticalVelocity > 0f && hit.moveDirection.y > 0f)
            {
                //如果碰撞点在角色控制器的中间以上，可以安全地假设它在上方
                if (hit.point.y > transform.position.y + (_controller.height / 2f))
                    _verticalVelocity = 0f;
            }
        }

        /// <summary>
        /// Sets if the controller is grounded.
        /// </summary>
        /// <returns>Returns if grounded has changed.</returns>
        private void SetIsGrounded(bool replaying, out bool changed)
        {
            //State before checking for ground.
            bool previousGrounded = _isGrounded;
            _isGrounded = CastForGround();
            changed = (previousGrounded != _isGrounded);
        }

        /// <summary>
        /// Checks for ground beneath the player.
        /// </summary>
        /// <param name="extraDistance"></param>
        /// <returns></returns>
        private bool CastForGround(float extraDistance = 0.05f)
        {
            float radius = _controller.radius + (_controller.skinWidth / 2f);
            //Start right in the center.
            Vector3 start = transform.position + new Vector3(0f, (_controller.height / 2f), 0f);
            float distance = (_controller.height / 2f) - (radius / 2f);

            //Check for ground.
            Ray ray = new Ray(start, Vector3.down);
            RaycastHit hit;
            SetCharacterLayer(true);
            //Disable colliders on self.
            bool isGrounded = Physics.SphereCast(ray, radius, out hit, distance + extraDistance, (GlobalManager.LayerManager.MovementBlockingLayers | GlobalManager.LayerManager.CharacterLayer));
            if (isGrounded && hit.collider.gameObject.name.Contains("Character"))
                Debug.Log(hit.collider.gameObject.name);
            SetCharacterLayer(false);
            return isGrounded;
        }

        /// <summary>
        /// Sets the layer for Hitboxes.
        /// </summary>
        /// <param name="toIgnore">如果为真，则设置为忽略物理；如果为假，则设置为默认值。</param>
        private void SetCharacterLayer(bool toIgnore)
        {
            int layer = (toIgnore) ?
                Layers.LayerMaskToLayerNumber(GlobalManager.LayerManager.IgnoreCollisionLayer) :
                Layers.LayerMaskToLayerNumber(GlobalManager.LayerManager.CharacterLayer);

            gameObject.layer = layer;
        }

        /// <summary>
        /// 有条件地调整步幅高度。
        /// </summary>
        private void SetStepOffset()
        {
            /*当在空中时不允许跳跃。这是为了防止客户端在掉落到悬崖前跳上去。
             * 这是一个Unity角色控制器的问题，可能适合用于攀爬悬崖，但不适合用于第一人称射击游戏。*/
            _controller.stepOffset = (_isGrounded && _verticalVelocity <= 0f) ? _defaultStepOffset : 0f;
        }

        /// <summary>
        /// Applies gravity to verticalVelocity.
        /// </summary>
        private void ApplyGravity(ref float verticalVelocity, float deltaTime)
        {
            //Multiply gravity by 2 for snappier jumps.
            verticalVelocity += (Physics.gravity.y * GRAVITY_MULTIPLIER) * deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, Physics.gravity.y * GRAVITY_MULTIPLIER);
        }

        /// <summary>
        /// 抑制当前外力。
        /// </summary>
        private void DampenExternalForces(float deltaTime)
        {
            _externalForces = Vector3.MoveTowards(_externalForces, Vector3.zero, _forceDampening * deltaTime);
        }

        /// <summary>
        /// Returns if can jump.
        /// </summary>
        /// <returns></returns>
        private bool CanJump()
        {
            /* Check for ground using a tiny bit of extra distance. This is for when going down slopes
             * where the player may break ground slightly. */
            if (!_isGrounded)
                return false;
            //Jump already queued.
            if (_jump)
                return false;

            /* If owner require exact jump intervals, if server allow a little leanancy.
             * When server allow to jump 150ms sooner to compensate for slower packets. */
            float nextAllowedJumpTime = (base.IsServer && !base.IsOwner) ? _nextAllowedJumpTime - 0.15f : _nextAllowedJumpTime;
            if (Time.time < nextAllowedJumpTime)
                return false;

            return true;
        }
        /// <summary>
        /// Applies jump.
        /// </summary>
        private void Jump(bool replaying)
        {
            _verticalVelocity = _jumpHeight;

            //if not replay.，则只增加时间并取消跳跃。
            if (!replaying)
            {
                _jump = false;
                _nextAllowedJumpTime = Time.time + _jumpInterval;
                _animatorController.Jump();
            }
        }


        /// <summary>
        /// 如果方向被阻挡，返回true。当控制器不应该允许移动的路径上遇到阻碍时，可能为真。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private bool BlockedDirection(ref ReplicateData input, float moveRate, float deltaTime)
        {
            /* ref is abused here because the structure is so large it's faster
             * to pass by reference rather than have the structure values copied. */

            /* Check for too steep of a slope. Character control is broken and
             * allows you to climb slopes you shouldn't normally be able to.
             * Only check if slope is too steep when jumping or not grounded. */
            if (!_isGrounded || _verticalVelocity > 0f)
            {
                //Start in the center of the character controller.
                Vector3 start = transform.position + new Vector3(0f, _controller.height / 2f, 0f);
                Vector3 estimatedImpact = transform.position + (input.WorldDirection.normalized * (_controller.radius + _controller.skinWidth + moveRate) * deltaTime);
                float distance = (start - estimatedImpact).magnitude;
                Vector3 direction = (estimatedImpact - start).normalized;
                Ray ray = new Ray(start, direction);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, distance, GlobalManager.LayerManager.MovementBlockingLayers))
                {
                    float angle = Vector3.Angle(hit.normal, Vector3.up);
                    if (angle > _controller.slopeLimit)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Dampens a vector if it differs from facing when character jumped.
        /// This prevents characters from swaying in the air, which would be very obnoxious in a shooting game.
        /// </summary>
        /// <param name="v"></param>
        private void DampenAirMovement(ref Vector3 v, bool server)
        {
            //Not in the air.
            if (_isGrounded)
                return;

            //If directions are the same then there is no reason to dampen.
            if (v == _lastJumpDirection)
                return;

            //If magnitude hasn't been reduced yet from client.
            if (server)
            {
                /* if magnitude is low then it's likely
                 * the movement has already been dampened,
                 * which means client is not cheating. */
                if (v.magnitude < 0.98f)
                    return;
                else
                {
                    v = v.normalized;
                }
            }

            float angle;
            if (_lastJumpDirection == Vector3.zero)
                angle = 90f;
            else
                angle = Vector3.Angle(_lastJumpDirection, v);

            float dampener = Mathf.Max(1f - _maximumAirDampening, Mathf.InverseLerp(180f, 0f, angle));
            v = new Vector3(
                    v.x * dampener,
                    v.y,
                    v.z * dampener);
        }

        /// <summary>
        /// Returns a move rate after being modified by action codes.
        /// </summary>
        /// <param name="actionCodes"></param>
        /// <returns></returns>
        private float GetMoveRate(ActionCodes actionCodes, Weapon weaponOverride = null)
        {
            float moveRate = _baseMoveRate;
            //Action code alterations.
            if (actionCodes.Contains(ActionCodes.Crouch) || actionCodes.Contains(ActionCodes.Walking))
            {
                moveRate *= WALK_CROUCH_PERCENT;
            }
            //Alterations from weapons.
            else
            {
                if (weaponOverride != null)
                    moveRate *= weaponOverride.MoveRateModifier;
                else if (_weaponHandler.WeaponIndexValid())
                    moveRate *= _weaponHandler.Weapon.MoveRateModifier;
            }

            return moveRate;
        }

        /// <summary>
        /// Moves using data.
        /// </summary>
        [Replicate]
        private void Replicate(ReplicateData input, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false)
        {
            //If not enabled. Can occur when player is out of health, possible a command go through after.
            if (!this.enabled)
                return;
            //Could be disabled on despawn or death.
            if (!_controller.enabled)
                return;

            //Update grounded, and set if grounded changed.
            SetIsGrounded(replaying, out _groundedChanged);
            /* True if data is default. Some things shouldn't be 
             * applied if data is default. */
            bool defaultData = Comparers.IsDefault(input);
            float deltaTime = (float)base.TimeManager.TickDelta;
            //Simulate velocities.
            ApplyGravity(ref _verticalVelocity, deltaTime);
            DampenExternalForces(deltaTime);
            //When grounded use a different velocity. This gives a better feel to the motor.
            SetGroundedVelocity(deltaTime, asServer, replaying);
            //Change step offset based on if grounded or not.
            SetStepOffset();

            /* Only update rotation if asServer or not replaying.
             * Replaying would be false if asServer but having
             * the separate checks shows intent more clearly. We
             * do this because we do not want the client to update rotation
             * while replaying inputs, that would cause the camera
             * to jitter if they desyncd. Instead the character moves using
             * world positions and the client gets to retain their rotation. 
             * If you wanted to limit how fast the client could rotate you
             * would need to change this behavior. 
             *
             * Also do not apply if the data is default because this will
             * just keep resetting the client to v3.Zero. */
            if (!defaultData && (asServer || !replaying))
                transform.eulerAngles = new Vector3(transform.eulerAngles.x, input.Rotation, transform.eulerAngles.z);

            bool jumping = input.ActionCodes.Contains(ActionCodes.Jump);
            if (jumping)
            {
                //Check if can jump as server to maintain server authority.
                if (asServer)
                {
                    if (CanJump())
                    {
                        _lastJumpDirection = input.WorldDirection;
                        Jump(replaying);
                    }
                }
                //Processing as client. Already went through client validation.
                else
                {
                    _lastJumpDirection = input.WorldDirection;
                    Jump(replaying);
                }
            }

            //Move rate can vary based on modifiers such as weapon or walking.
            float moveRate = GetMoveRate(input.ActionCodes, input.Weapon);
            /* If direction isn't 0f make sure character can move that way.
             * Don't process if a replay as original input would have already cleared
             * direction if blocked. */
            if (!replaying && input.WorldDirection != Vector3.zero)
            {
                if (BlockedDirection(ref input, moveRate, deltaTime))
                    input.WorldDirection = Vector3.zero;
            }

            Vector3 v = input.WorldDirection;
            DampenAirMovement(ref v, true);

            //Multiply inputs by move rate.
            v.x *= moveRate;
            v.z *= moveRate;
            //Add on vertical velocity and external forces.
            v.y += _verticalVelocity;
            v += _externalForces;
            //Apply movement to the character controller.
            _controller.Move(v * deltaTime);

            //Update running which will send in another method.
            bool running = (_isGrounded && input.LocalDirection != Vector3.zero && !input.ActionCodes.Contains(ActionCodes.Walking));
            SetRunning(running, replaying);
            if (asServer)
                ServerUpdateAnimator(input);
        }


        /// <summary>
        /// Checks if running audio should play.
        /// </summary>
        [Client(Logging = LoggingType.Off)]
        private void CheckPlayRunningAudio()
        {
            //Not running or cannot play running audio yet due to time restrictions.
            if (!_running || Time.time < _nextRunningAudioTime)
                return;

            _nextRunningAudioTime = Time.time + RUNNING_AUDIO_INTERVAL;

            if (base.IsOwner)
                OfflineGameplayDependencies.AudioManager.PlayFirstPerson(_runningAudioPrefab);
            else
                OfflineGameplayDependencies.AudioManager.PlayAtPoint(_runningAudioPrefab, transform.position);
        }

        /// <summary>
        /// Reconciles using data.
        /// </summary>
        [Reconcile]
        private void Reconcile(ReconcileData rd, bool asServer, Channel channel = Channel.Unreliable)
        {
            //Server doesn't actually reconcile, it just sends the data to the client.
            //Nothing to process.
            if (Comparers.IsDefault(rd))
                return;

            _verticalVelocity = rd.VerticalVelocity;
            _externalForces = rd.ExternalForces;
            transform.position = rd.Position;
        }


        /// <summary>
        /// Tries to jump using input.
        /// </summary>
        [Client(Logging = LoggingType.Off)]
        private void CheckJump()
        {
            if (!Input.GetKeyDown(KeyCode.Space))
                return;
            if (!CanJump())
                return;

            _jump = true;
        }

        /// <summary>
        /// Sets the running value and resets last run audio time when appropriate.
        /// </summary>
        /// <param name="value"></param>
        private void SetRunning(bool value, bool replaying)
        {
            if (value == _running)
                return;

            _running = value;
            //If now running reset audio time to play slightly ahead of start time.
            if (!replaying && _running)
                _nextRunningAudioTime = Time.time + (RUNNING_AUDIO_INTERVAL / 2f);
        }


        /// <summary>
        /// Updates queued input on owning client.
        /// </summary>
        [Client(Logging = LoggingType.Off)]
        private void CheckInput(out ReplicateData rd)
        {
            float hor = Input.GetAxisRaw("Horizontal");
            float ver = Input.GetAxisRaw("Vertical");

            Vector3 localDirection = new Vector3(hor, 0f, ver);
            Vector3 worldDirection = transform.TransformDirection(localDirection.normalized);

            //Update animator with movement direction.
            _animatorController.SetMovementDirection(localDirection);
            //Default to move action code.
            ActionCodes actionCodes = ActionCodes.None;

            bool crouched = false; //Doesn't actually do anything yet.
            bool walking = Input.GetKey(KeyCode.LeftShift);
            crouched = Input.GetKey(KeyCode.LeftControl);

            //Jump action code.
            if (_jump)
                actionCodes |= ActionCodes.Jump;
            if (walking)
                actionCodes |= ActionCodes.Walking;
            if (crouched)
                actionCodes |= ActionCodes.Crouch;
            float rotY = transform.eulerAngles.y;
            if (localDirection != Vector3.zero || rotY != _lastOwnerRotation)
                actionCodes |= ActionCodes.Move;
            _lastOwnerRotation = rotY;

            rd = new ReplicateData(
                worldDirection,
                localDirection,
                rotY,
                _weaponHandler.Weapon,
                actionCodes
                );
        }

        /// <summary>
        /// Update AnimatorController using input.
        /// </summary>
        [Server(Logging = LoggingType.Off)]
        private void ServerUpdateAnimator(ReplicateData input)
        {
            _animatorController.SetMovementDirection(input.LocalDirection);
        }


        /// <summary>
        /// Sent to all clients containing extra effects data after server has processed user input.
        /// </summary>
        [ObserversRpc]
        private void ObserversSetRunning(bool running, Channel c = Channel.Unreliable)
        {
            //Do not update for owner.
            if (base.IsOwner)
                return;

            SetRunning(running, false);
        }


    }


}