using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ETAPA 4 - Entrada batch para o QA da loja do mercador em modo play:
// Unity.exe -batchmode -projectPath <proj> -executeMethod ShopPlayQAEntry.RunFromCommandLine -logFile <log>
// (SEM -quit: o editor entra no play mode, o driver roda a suite e chama
// EditorApplication.Exit com o codigo de saida.)
public static class ShopPlayQAEntry
{
    private const string ArmedKey = "ShopPlayQA_Armed";
    private const string ArmedAtKey = "ShopPlayQA_ArmedAt";
    private const float ArmedTimeout = 180f;

    public static void RunFromCommandLine()
    {
        SessionState.SetBool(ArmedKey, true);
        SessionState.SetString(ArmedAtKey, EditorApplication.timeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture));
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Run Shop QA (Play)")]
    public static void RunFromMenu()
    {
        RunFromCommandLine();
    }

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ArmedKey, false)) return;

        double armedAt;
        string armedRaw = SessionState.GetString(ArmedAtKey, string.Empty);
        if (string.IsNullOrEmpty(armedRaw) ||
            !double.TryParse(armedRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out armedAt))
        {
            armedAt = EditorApplication.timeSinceStartup;
        }
        if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup - armedAt > ArmedTimeout)
        {
            SessionState.SetBool(ArmedKey, false);
            Debug.LogError("ShopPlayQA: timeout esperando entrar no play mode.");
            EditorApplication.Exit(2);
            return;
        }

        if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode == false) return;
        if (Object.FindAnyObjectByType<MerchantShopQADriver>() != null) return;

        SessionState.SetBool(ArmedKey, false);
        var go = new GameObject("~MerchantShopQADriver");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<MerchantShopQADriver>();
    }
}
