using UnityEngine;

namespace PhysicsFloatingOrigin
{
    public class FpsHUD : MonoBehaviour
    {
        [SerializeField] private Rect panelRect = new Rect(16f, 16f, 140f, 52f);
        [SerializeField] private float refreshInterval = 0.5f;

        private float timer;
        private int frameCount;
        private int fps;

        private void OnGUI()
        {
            GUI.Box(panelRect, GUIContent.none);

            GUILayout.BeginArea(panelRect);
            GUILayout.Space(8f);
            GUILayout.Label($"FPS: {fps}");
            GUILayout.EndArea();
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            frameCount++;

            if (refreshInterval > 0f && timer >= refreshInterval)
            {
                fps = Mathf.RoundToInt(frameCount / timer);
                frameCount = 0;
                timer = 0f;
            }
        }
    }
}
