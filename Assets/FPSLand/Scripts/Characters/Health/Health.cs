﻿
using FishNet.Object;
using System;
using UnityEngine;

namespace FirstGearGames.FPSLand.Characters.Vitals
{

    public class Health : NetworkBehaviour
    {
        #region Public.
        /// <summary>
        /// Dispatched when health changes with old, new, and max health values.
        /// </summary>
        public event Action<int, int, int> OnHealthChanged;
        /// <summary>
        /// Dispatched when health is depleted.
        /// </summary>
        public event Action OnDeath;
        /// <summary>
        /// Dispatched after being respawned.
        /// </summary>
        public event Action OnRespawned;
        /// <summary>
        /// Current health.
        /// </summary>
        public int CurrentHealth { get; private set; }
        /// <summary>
        /// Maximum amount of health character can currently achieve.
        /// </summary>
        public int MaximumHealth { get { return _baseHealth; } }
        /// <summary>
        /// Hitboxes on the character.
        /// </summary>
        public Hitbox[] Hitboxes { get; private set; } = new Hitbox[0];
        #endregion

        #region Serialized.
        /// <summary>
        /// Health to start with.
        /// </summary>
        [Tooltip("Health to start with.")]
        [SerializeField]
        private int _baseHealth = 100;
        #endregion

        private void Awake()
        {
            SetHitboxes();
            CurrentHealth = MaximumHealth;
        }

        /// <summary>
        /// Finds hitboxes on this object.
        /// </summary>
        private void SetHitboxes()
        {
            Hitboxes = GetComponentsInChildren<Hitbox>();
            for (int i = 0; i < Hitboxes.Length; i++)
            { 
                Hitboxes[i].OnHit += Health_OnHit;
                Hitboxes[i].SetTopmostParent(transform);
            }
        }

        /// <summary>
        /// Received when a hitbox is hit.
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        private void Health_OnHit(Hitbox hitbox, int damage)
        {
            RemoveHealth(damage, hitbox.Multiplier);
        }


        /// <summary>
        /// Restores health to maximum health.
        /// </summary>
        public void RestoreHealth()
        {
            int oldHealth = CurrentHealth;
            CurrentHealth = MaximumHealth;

            OnHealthChanged?.Invoke(oldHealth, CurrentHealth, MaximumHealth);

            if (base.IsServer)
                ObserversRestoreHealth();
        }

        /// <summary>
        /// Called when respawned.
        /// </summary>
        public void Respawned()
        {
            OnRespawned?.Invoke();

            if (base.IsServer)
                ObserversRespawned();
        }

        /// <summary>
        /// Removes health.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="multiplier"></param>
        public void RemoveHealth(int value, float multiplier)
        {
            RemoveHealth(Mathf.CeilToInt(value * multiplier));
        }

        /// <summary>
        /// Removes health.
        /// </summary>
        /// <param name="value"></param>
        public void RemoveHealth(int value)
        {
            int oldHealth = CurrentHealth;
            CurrentHealth -= value;

            OnHealthChanged?.Invoke(oldHealth, CurrentHealth, MaximumHealth);

            if (CurrentHealth <= 0f)
                HealthDepleted();

            if (base.IsServer)
                ObserversRemoveHealth(value, oldHealth);
        }

        /// <summary>
        /// Called when health is depleted.
        /// </summary>
        public virtual void HealthDepleted()
        {
            OnDeath?.Invoke();
        }

        /// <summary>
        /// Sent to clients when health is restored.
        /// </summary>
        [ObserversRpc]
        private void ObserversRestoreHealth()
        {
            //Server already restored health. If we don't exit this will be an endless loop. This is for client host.
            if (base.IsServer)
                return;

            RestoreHealth();
        }

        /// <summary>
        /// Sent to clients when character is respawned.
        /// </summary>
        [ObserversRpc]
        private void ObserversRespawned()
        {
            if (base.IsServer)
                return;

            Respawned();
        }


        /// <summary>
        /// 发送给客户以扣除生命值。
        /// </summary>
        /// <param name="value"></param>
        [ObserversRpc]
        private void ObserversRemoveHealth(int value, int priorHealth)
        {
            //服务器已经删除了生命值。如果不退出，这将是一个无限循环。这是client host.
            if (base.IsServer)
                return;

            /* 将当前健康值设置为先前的健康值，以防客户端以某种方式意外地失去同步
             * 在尝试删除健康值之前，这将修复它。*/
            CurrentHealth = priorHealth;

            RemoveHealth(value);
        }

    }


}