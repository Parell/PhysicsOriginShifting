using UnityEngine;
using UnityEngine.UI;

namespace UnityPhysicsFloatingOrigin
{
    public class VersionText : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Text>().text = Application.version;
        }
    }
}
