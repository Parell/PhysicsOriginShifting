using UnityEngine;

public class ExamplePhysicsHUD : MonoBehaviour
{
    [SerializeField] private ExamplePhysicsManager manager;
    [SerializeField] private Rect panelRect = new Rect(16f, 336f, 420f, 240f);

    public void SetManager(ExamplePhysicsManager newManager)
    {
        manager = newManager;
    }

    private void OnGUI()
    {
        GUI.Box(panelRect, GUIContent.none);

        GUILayout.BeginArea(panelRect);
        GUILayout.Space(8f);
        GUILayout.Label("Keys:");
        GUILayout.Label("wasd / arrows - movement");
        GUILayout.Label("q/e - up/down (local space)");
        GUILayout.Label("r/f - up/down (world space)");
        GUILayout.Label("pageup/pagedown - up/down (world space)");
        GUILayout.Label("hold shift - enable fast movement mode");
        GUILayout.Label("right mouse - enable free look");
        GUILayout.Label("mouse - free look / rotation");
        GUILayout.Space(8f);
        GUILayout.Label("Inputs and Body State");

        if (manager == null)
        {
            GUILayout.Label("Waiting for ExamplePhysicsManager...");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("None", GUILayout.Height(28f)))
        {
            manager.SetRebasingState(ExamplePhysicsManager.RebasingState.None);
        }

        if (GUILayout.Button("Floating", GUILayout.Height(28f)))
        {
            manager.SetRebasingState(ExamplePhysicsManager.RebasingState.FloatingOrigin);
        }

        if (GUILayout.Button("Physics", GUILayout.Height(28f)))
        {
            manager.SetRebasingState(ExamplePhysicsManager.RebasingState.PhysicsFloatingOrigin);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label($"State: {manager.CurrentRebasingState}");
        GUILayout.Label($"Position: {FormatVector(manager.MainBodyPhysicalPosition)}");
        GUILayout.Label($"Velocity: {FormatVector(manager.MainBodyPhysicalVelocity)}");
        GUILayout.Label($"Acceleration: {FormatVector(manager.MainBodyPhysicalAcceleration)}");
        GUILayout.EndArea();
    }

    private void Awake()
    {
        if (manager == null)
        {
            manager = FindObjectOfType<ExamplePhysicsManager>();
        }
    }

    private void Update()
    {
        if (manager == null)
        {
            manager = FindObjectOfType<ExamplePhysicsManager>();
        }
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.magnitude})";
    }
}
