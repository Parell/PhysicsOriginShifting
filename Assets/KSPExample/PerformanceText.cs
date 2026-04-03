using UnityEngine;
using UnityEngine.UI;

namespace UnityPhysicsFloatingOrigin
{
    public class PerformanceText : MonoBehaviour
    {
        [SerializeField] private float refresh = 0.5f;
        private float timer;
        private Text text;

        private void Start()
        {
            text = GetComponent<Text>();
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0)
            {
                text.text = string.Format("{0}",
                Mathf.RoundToInt(1f / Time.unscaledDeltaTime).ToString());
                timer = refresh;
            }
        }
    }
}