using UnityEngine;

public class PrototypeInfoDisplay : MonoBehaviour
{
    public string prototypeMessage = "Protótipo";
    public string debugInfo = "";
    public Vector2 scrollPosition;

    void OnGUI()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            normal = { textColor = Color.white },
            alignment = TextAnchor.UpperLeft
        };

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow }
        };

        GUILayout.BeginArea(new Rect(10, 10, 400, 270), GUI.skin.box);
        GUILayout.Label(prototypeMessage, titleStyle);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        GUILayout.Label(debugInfo, boxStyle);
        GUILayout.EndScrollView();

        GUILayout.EndArea();

        UpdateDebugInfo();
    }

    void UpdateDebugInfo()
    {
        long totalMemory = System.GC.GetTotalMemory(false) / (1024 * 1024);

        debugInfo =
            $"FPS: {(1.0f / Time.deltaTime):F1}\n" +
            $"Memória usada (GC): {totalMemory} MB\n" +
            $"CPU: {SystemInfo.processorType}\n" +
            $"Núcleos: {SystemInfo.processorCount}\n" +
            $"Frequência estimada: {SystemInfo.processorFrequency} MHz\n" +
            $"RAM Total: {SystemInfo.systemMemorySize} MB\n" +
            $"Plataforma: {Application.platform}\n" +
            $"Versão: {"S/V Protótipo"}\n" +
            $"Modo: {(Application.isEditor ? "Editor" : "Build")}\n" +
            $"Resolução: {Screen.width}x{Screen.height}\n" +
            $"Tempo de execução: {Time.timeSinceLevelLoad:F1} s";
    }
}
