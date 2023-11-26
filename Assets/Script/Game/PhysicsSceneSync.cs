using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FPS.Game
{

    public class PhysicsSceneSync : NetworkBehaviour
    {
        /// <summary>
        /// True to synchronize physics 3d.
        /// </summary>
        [SerializeField]
        private bool _physics = true;
        /// <summary>
        /// Scenes which have physics handled by this script.
        /// </summary>
        private static HashSet<int> _synchronizedScenes = new HashSet<int>();

        private void Awake()
        {
            Physics.autoSimulation = false;
        }

        public override void OnStartNetwork()
        {
            //如果场景已经同步，return 
            int sceneHandle = gameObject.scene.handle;
            if (_synchronizedScenes.Contains(sceneHandle))
                return;

            _physics = (gameObject.scene.GetPhysicsScene() != Physics.defaultPhysicsScene);
            if (_physics)
            {
                _synchronizedScenes.Add(sceneHandle);
                Debug.Log(gameObject.scene.GetPhysicsScene());
                base.TimeManager.OnPrePhysicsSimulation += TimeManager_OnPrePhysicsSimulation;
            }
        }

        public override void OnStopNetwork()
        {
            //unsubscribe.
            if (_physics)
            {
                _synchronizedScenes.Add(gameObject.scene.handle);
                base.TimeManager.OnPrePhysicsSimulation -= TimeManager_OnPrePhysicsSimulation;
            }
        }

        private void TimeManager_OnPrePhysicsSimulation(float delta)
        {
            //在此对象上进行模拟物理场景
            if (_physics)
            {
                gameObject.scene.GetPhysicsScene().Simulate(delta);

            }

        }

    }
}
