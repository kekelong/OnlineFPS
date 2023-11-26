using FirstGearGames.FPSLand.Characters.Vitals;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace FirstGearGames.FPSLand.Characters.Motors
{
    /// <summary>
    /// 控制角色动画相关，暴露对外的调用接口
    /// </summary>
    public class AnimatorController : NetworkBehaviour
    {

        #region Serialized.
        /// <summary>
        /// 第一人称的 Animator
        /// </summary>
        [Tooltip("Animator used for first person.")]
        [SerializeField]
        private Animator _firstPersonAnimator;
        /// <summary>
        /// 第三人的 Animator
        /// </summary>
        [Tooltip("Animator used for third person.")]
        [SerializeField]
        private Animator _thirdPersonAnimator;
        #endregion

        #region Animator hashes.
        private int HORIZONTAL_HASH = Animator.StringToHash("Horizontal");
        private int FORWARD_HASH = Animator.StringToHash("Forward");
        private int JUMP_HASH = Animator.StringToHash("Jump");
        private int RELOAD_HASH = Animator.StringToHash("Reload");
        private int WEAPONCHANGE_HASH = Animator.StringToHash("WeaponChange");
        private int WEAPONINDEX_HASH = Animator.StringToHash("WeaponIndex");
        private int PITCH_HASH = Animator.StringToHash("Pitch");
        private int ATTACK_HASH = Animator.StringToHash("Attack");
        #endregion

        /// <summary>
        /// Current pitch to move towards.
        /// </summary>
        /// 不可靠，传输速率，更新到那些客户度（除了自己）  
        [SyncVar(Channel = Channel.Unreliable, SendRate = 0.02f, ReadPermissions = ReadPermission.ExcludeOwner)]
        private float _pitch;
        /// <summary>
        /// Direction the character is moving.
        /// </summary>
        [SyncVar(Channel = Channel.Unreliable, SendRate = 0.02f, ReadPermissions = ReadPermission.ExcludeOwner)]
        private Vector3 _movementDirection;
        /// <summary>
        /// Current weapon index.
        /// </summary>
        /// OnChange ---- 值更改时将在服务器和客户端上调用的方法。
        [SyncVar(OnChange =  nameof(OnWeaponIndex), Channel = Channel.Reliable, SendRate = 0.02f, ReadPermissions = ReadPermission.ExcludeOwner)]
        private int _weaponIndex = -1;

        private void Awake()
        {
            FirstInitialize();
        }


        private void Update()
        {
            SmoothFloats();
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        private void FirstInitialize()
        {
            Health health = GetComponent<Health>();
            health.OnDeath += Health_OnDeath;
            health.OnRespawned += Health_OnRespawned;
        }

        /// <summary>
        /// 当角色重生时回调
        /// </summary>
        private void Health_OnRespawned()
        {
            _thirdPersonAnimator.enabled = true;

            if (base.IsOwner)
                _firstPersonAnimator.enabled = true;
        }

        /// <summary>
        /// 死亡
        /// </summary>
        private void Health_OnDeath()
        {
            SetWeaponIndex(-1);
            _thirdPersonAnimator.enabled = false;
            
            if (base.IsOwner)
                _firstPersonAnimator.enabled = false;
        }

        /// <summary>
        /// 平滑插值属性
        /// </summary>
        private void SmoothFloats()
        {
            float deltaTime = Time.deltaTime;

            //目标的距离。
            float distance;
            
            float currentF;

            Animator[] animators = ReturnAnimators();

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                // 仅针对第三人称模型
                if (animators[i] == _thirdPersonAnimator)
                {
                    /* TargetDirection. */
                    float directionRate = 6f;
                    //Horizontal.
                    currentF = animators[i].GetFloat(HORIZONTAL_HASH);
                    if (currentF != _movementDirection.x)
                    {
                        distance = Mathf.Max(1f, Mathf.Abs(currentF - _movementDirection.x));
                        animators[i].SetFloat(HORIZONTAL_HASH, Mathf.MoveTowards(currentF, _movementDirection.x, directionRate * distance * deltaTime));
                    }
                    //Forward.
                    currentF = animators[i].GetFloat(FORWARD_HASH);
                    if (currentF != _movementDirection.z)
                    {
                        distance = Mathf.Max(1f, Mathf.Abs(currentF - _movementDirection.z));
                        animators[i].SetFloat(FORWARD_HASH, Mathf.MoveTowards(currentF, _movementDirection.z, directionRate * distance * deltaTime));
                    }

                    /* Pitch. */
                    float pitchRate = 15f;
                    currentF = animators[i].GetFloat(PITCH_HASH);
                    if (currentF != _pitch)
                    {
                        distance = Mathf.Max(1f, Mathf.Abs(currentF - _pitch));
                        animators[i].SetFloat(PITCH_HASH, Mathf.MoveTowards(currentF, _pitch, pitchRate * distance * deltaTime));
                    }
                }
            }

        }


        /// <summary>
        /// Sets characters movement direction.
        /// </summary>
        /// <param name="direction"></param>
        public void SetMovementDirection(Vector3 direction)
        {
            _movementDirection = direction;
        }

        /// <summary>
        /// Sets characters looking pitch.
        /// </summary>
        /// <param name="pitch"></param>
        public void SetPitch(float pitch)
        {
            _pitch = pitch;
        }

        /// <summary>
        /// Called when WeaponIndex changes.
        /// </summary>
        private void OnWeaponIndex(int prev, int next, bool asServer)
        {
 
            if (!asServer && base.IsServer && !base.IsOwner)
                return;
            if (prev == next)
                return;

            Animator[] animators = ReturnAnimators();
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                animators[i].SetInteger(WEAPONINDEX_HASH, next);
                animators[i].SetTrigger(WEAPONCHANGE_HASH);
            }
        }

        /// <summary>
        /// Changes the current weapon.
        /// </summary>
        /// <param name="index"></param>
        public void SetWeaponIndex(int index)
        {
            int prev = _weaponIndex;
            _weaponIndex = index;

            //Prev isnt used in the callback but just to future proof.
            if (base.IsOwner)
                OnWeaponIndex(prev, index, false);
        }

        /// <summary>
        /// Sets jump trigger.
        /// </summary>
        public void Jump()
        {
            Animator[] animators = ReturnAnimators();
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                //Only show jump animation for third person.
                if (animators[i] == _thirdPersonAnimator)
                    animators[i].SetTrigger(JUMP_HASH);
            }

            //Also send to other clients.
            if (base.IsServer)
                ObserversJump();
        }

        /// <summary>
        /// Sets reload trigger.
        /// </summary>
        public void Reload()
        {
            Animator[] animators = ReturnAnimators();
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                animators[i].SetTrigger(RELOAD_HASH);
            }
        }

        /// <summary>
        /// Activates Attack trigger in animators.
        /// </summary>
        public void Attack()
        {
            Animator[] animators = ReturnAnimators();
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                animators[i].SetTrigger(ATTACK_HASH);
            }
        }

        /// <summary>
        /// Calls Jump over the network.
        /// </summary>
        [ObserversRpc]
        private void ObserversJump()
        {
            
            /* Owner and server would have already 
             * run jump. */
            if (base.IsOwner || base.IsServer)
                return;

            Jump();
        }

        /// <summary>
        /// 返回要使用的动画器数组。如果作为client host.，则可能包含多个动画器。
        /// 本项目采用CS分离，不存在Client host.
        /// </summary>
        /// <returns></returns>
        private Animator[] ReturnAnimators()
        {
            //Just client or server, only need default animator.
            if (base.IsClientOnly || base.IsServerOnly)
                return new Animator[] { ReturnAnimator() };
            // Client host.
            else
                return new Animator[] { _firstPersonAnimator, _thirdPersonAnimator };
        }

        /// <summary>
        /// Returns which animator to set values to. Used if clientOnly or serverOnly.
        /// </summary>
        /// <returns></returns>
        private Animator ReturnAnimator()
        {
            return (IsFirstperson()) ? _firstPersonAnimator : _thirdPersonAnimator;
        }

        /// <summary>
        /// Returns true if using the first person animator.
        /// </summary>
        /// <returns></returns>
        private bool IsFirstperson()
        {
            return (base.IsOwner) ? true : false;
        }
    }


}