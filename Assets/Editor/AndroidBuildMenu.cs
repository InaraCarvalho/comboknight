using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build do APK do Combo Knight: menu (Build > Combo Knight APK) ou linha de
// comando via -executeMethod AndroidBuildMenu.BuildApk.
public static class AndroidBuildMenu
{
    private const string OutputDir = "Builds";
    private const string OutputName = "ComboKnight.apk";

    [MenuItem("Build/Combo Knight APK (Android)")]
    public static void BuildApk()
    {
        // O jogo e em RETRATO (arena vertical, background portrait).
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("Nao foi possivel trocar a plataforma ativa para Android.");
            return;
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("Nenhuma cena habilitada no Build Settings.");
            return;
        }

        string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? ".";
        string buildDir = Path.Combine(projectRoot, OutputDir);
        Directory.CreateDirectory(buildDir);
        string output = Path.Combine(buildDir, OutputName);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"APK gerado em: {output}");
        }
        else
        {
            Debug.LogError($"Falha no build ({report.summary.result}). Veja o log do Player.");
        }
    }
}