using UnityEngine;

namespace FirstGearGames.FPSLand.Characters.Bodies
{

    /// <summary>
    /// 根据所有权（第一人称和第三人称）更改游戏对象的可见性。
    /// </summary>
    public class BodiesConfigurations : MonoBehaviour
    {

        [Tooltip("GameObjects to enabled as first person.")]
        [SerializeField]
        private GameObject _firstPersonObject;
        /// <summary>
        /// GameObjects to enabled as first person.
        /// </summary>
        public GameObject FirstPersonObject { get { return _firstPersonObject; } }


        [Tooltip("GameObjects to enabled as third person.")]
        [SerializeField]
        private GameObject _thirdPersonObject;
        /// <summary>
        /// GameObjects to enabled as third person.
        /// </summary>
        public GameObject ThirdPersonObject { get { return _thirdPersonObject; } }
    }

}