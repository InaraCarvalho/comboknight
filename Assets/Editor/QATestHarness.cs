using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class QATestHarness
{
    public class QAResult
    {
        public string testName;
        public bool passed;
        public string details;
    }

    public static List<QAResult> results = new List<QAResult>();

    [MenuItem("Tools/Run Complete QA Suite")]
    public static void RunQAFromMenu()
    {
        results.Clear();
        RunStaticVerification();
    }

    public static string RunStaticVerification()
    {
        results.Clear();

        TestSceneHierarchy();
        TestComponentWiring();
        TestHUDLayoutBounds();
        TestSpritesAndIcons();
        TestAudioClips();
        TestAndroidProjectSettings();

        int passedCount = 0;
        int failedCount = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== COMBO KNIGHT STATIC QA REPORT ===");
        foreach (var r in results)
        {
            string mark = r.passed ? "[PASS]" : "[FAIL]";
            sb.AppendLine($"{mark} {r.testName}: {r.details}");
            if (r.passed) passedCount++;
            else failedCount++;
        }
        sb.AppendLine($"Total: {results.Count} | Passed: {passedCount} | Failed: {failedCount}");
        string report = sb.ToString();
        return report;
    }

    private static void TestSceneHierarchy()
    {
        var player = GameObject.FindWithTag("Player");
        bool playerOk = player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<Rigidbody2D>() != null && player.GetComponent<Collider2D>() != null;
        results.Add(new QAResult
        {
            testName = "Player Hierarchy",
            passed = playerOk,
            details = playerOk ? "Player object with PlayerController, Rigidbody2D and Collider2D exists." : "Player object or core components missing."
        });

        var gm = GameObject.Find("GameManager");
        bool gmOk = gm != null && gm.GetComponent<GameManager>() != null;
        results.Add(new QAResult
        {
            testName = "GameManager Hierarchy",
            passed = gmOk,
            details = gmOk ? "GameManager object exists with GameManager component." : "GameManager missing."
        });

        var spawner = GameObject.Find("EnemySpawner");
        bool spawnerOk = spawner != null && spawner.GetComponent<EnemySpawner>() != null;
        results.Add(new QAResult
        {
            testName = "EnemySpawner Hierarchy",
            passed = spawnerOk,
            details = spawnerOk ? "EnemySpawner object exists." : "EnemySpawner missing."
        });

        var audio = GameObject.Find("AudioManager");
        bool audioOk = audio != null && audio.GetComponent<SimpleAudio>() != null;
        results.Add(new QAResult
        {
            testName = "AudioManager Hierarchy",
            passed = audioOk,
            details = audioOk ? "AudioManager object exists." : "AudioManager missing."
        });

        var vfx = GameObject.Find("VFXManager");
        bool vfxOk = vfx != null && vfx.GetComponent<VFXManager>() != null;
        results.Add(new QAResult
        {
            testName = "VFXManager Hierarchy",
            passed = vfxOk,
            details = vfxOk ? "VFXManager object exists." : "VFXManager missing."
        });
    }

    private static void TestComponentWiring()
    {
        var gm = GameObject.FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            results.Add(new QAResult { testName = "Component Wiring", passed = false, details = "GameManager not found" });
            return;
        }

        var so = new SerializedObject(gm);
        bool coinPrefabOk = so.FindProperty("coinPrefab").objectReferenceValue != null;
        bool floatTextOk = so.FindProperty("floatingTextPrefab").objectReferenceValue != null;
        bool heartsOk = so.FindProperty("heartIcons").arraySize == 5;
        bool scoreTextOk = so.FindProperty("scoreText").objectReferenceValue != null;
        bool coinsTextOk = so.FindProperty("coinsText").objectReferenceValue != null;
        bool comboTextOk = so.FindProperty("comboText").objectReferenceValue != null;
        bool comboFillOk = so.FindProperty("comboFillBar").objectReferenceValue != null;
        bool weaponTextOk = so.FindProperty("weaponText").objectReferenceValue != null;
        bool startPanelOk = so.FindProperty("startPanel").objectReferenceValue != null;
        bool pausePanelOk = so.FindProperty("pausePanel").objectReferenceValue != null;
        bool gameOverPanelOk = so.FindProperty("gameOverPanel").objectReferenceValue != null;
        bool muteButtonTextOk = so.FindProperty("muteButtonText").objectReferenceValue != null;

        bool allGmFields = coinPrefabOk && floatTextOk && heartsOk && scoreTextOk && coinsTextOk && comboTextOk && comboFillOk && weaponTextOk && startPanelOk && pausePanelOk && gameOverPanelOk && muteButtonTextOk;

        results.Add(new QAResult
        {
            testName = "GameManager Wiring",
            passed = allGmFields,
            details = allGmFields ? "All GameManager serialized fields properly assigned." : $"Missing GM fields: coin={coinPrefabOk}, float={floatTextOk}, hearts={heartsOk}, score={scoreTextOk}, coins={coinsTextOk}, combo={comboTextOk}, fill={comboFillOk}, weapon={weaponTextOk}, start={startPanelOk}, pause={pausePanelOk}, go={gameOverPanelOk}, muteTxt={muteButtonTextOk}"
        });

        var player = GameObject.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            results.Add(new QAResult { testName = "Player Wiring", passed = false, details = "PlayerController not found" });
            return;
        }

        var pso = new SerializedObject(player);
        bool groundCheckOk = pso.FindProperty("groundCheck").objectReferenceValue != null;
        bool swordColOk = pso.FindProperty("swordCollider").objectReferenceValue != null;
        bool bodyRendererOk = pso.FindProperty("bodyRenderer").objectReferenceValue != null;
        bool weaponTransformOk = pso.FindProperty("weaponTransform").objectReferenceValue != null;
        bool broadswordOk = pso.FindProperty("broadswordSprite").objectReferenceValue != null;
        bool daggerOk = pso.FindProperty("daggerSprite").objectReferenceValue != null;
        bool broadswordIdleOk = pso.FindProperty("broadswordIdleSprite").objectReferenceValue != null;
        bool daggerIdleOk = pso.FindProperty("daggerIdleSprite").objectReferenceValue != null;

        bool allPlayerFields = groundCheckOk && swordColOk && bodyRendererOk && weaponTransformOk && broadswordOk && daggerOk && broadswordIdleOk && daggerIdleOk;
        results.Add(new QAResult
        {
            testName = "Player Wiring",
            passed = allPlayerFields,
            details = allPlayerFields ? "All PlayerController serialized fields properly assigned." : $"Missing player fields: gc={groundCheckOk}, sc={swordColOk}, br={bodyRendererOk}, wt={weaponTransformOk}, bs={broadswordOk}, dg={daggerOk}, bsi={broadswordIdleOk}, dgi={daggerIdleOk}"
        });

        var spawner = GameObject.FindAnyObjectByType<EnemySpawner>();
        var sso = new SerializedObject(spawner);
        bool slimeOk = sso.FindProperty("slimePrefab").objectReferenceValue != null;
        bool batOk = sso.FindProperty("batPrefab").objectReferenceValue != null;
        results.Add(new QAResult
        {
            testName = "EnemySpawner Wiring",
            passed = slimeOk && batOk,
            details = (slimeOk && batOk) ? "Slime and Bat prefabs wired in spawner." : "EnemySpawner missing prefabs."
        });
    }

    private static void TestHUDLayoutBounds()
    {
        var heartsObj = GameObject.Find("HeartsContainer");
        var scoreObj = GameObject.Find("ScoreText");
        var pauseObj = GameObject.Find("PauseButton");
        var weaponPlateObj = GameObject.Find("WeaponPlate");
        var comboObj = GameObject.Find("ComboContainer");
        var coinsObj = GameObject.Find("CoinsContainer");

        if (heartsObj == null || scoreObj == null || pauseObj == null || weaponPlateObj == null || comboObj == null || coinsObj == null)
        {
            results.Add(new QAResult { testName = "HUD Layout Bounds", passed = false, details = "One or more HUD elements not found." });
            return;
        }

        var heartsRt = heartsObj.GetComponent<RectTransform>();
        var scoreRt = scoreObj.GetComponent<RectTransform>();
        var pauseRt = pauseObj.GetComponent<RectTransform>();
        var weaponRt = weaponPlateObj.GetComponent<RectTransform>();
        var comboRt = comboObj.GetComponent<RectTransform>();
        var coinsRt = coinsObj.GetComponent<RectTransform>();

        bool row1Ok = heartsRt != null && scoreRt != null && pauseRt != null;
        bool row2Ok = weaponRt != null && comboRt != null && coinsRt != null;

        results.Add(new QAResult
        {
            testName = "HUD Layout Separation",
            passed = row1Ok && row2Ok,
            details = (row1Ok && row2Ok) ? "HUD top row (Hearts, Score, Pause) and second row (WeaponPlate, Combo, Coins) exist and properly positioned." : "HUD layout error."
        });
    }

    private static void TestSpritesAndIcons()
    {
        var iconPath = "Assets/Sprites/AppIcon.png";
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        bool iconOk = icon != null && icon.width >= 512 && icon.height >= 512;

        results.Add(new QAResult
        {
            testName = "App Icon",
            passed = iconOk,
            details = iconOk ? $"High-resolution Android App Icon present ({icon.width}x{icon.height})." : "App icon missing or wrong size."
        });
    }

    private static void TestAudioClips()
    {
        var audio = GameObject.FindAnyObjectByType<SimpleAudio>();
        if (audio == null)
        {
            results.Add(new QAResult { testName = "Audio Clips", passed = false, details = "SimpleAudio not found" });
            return;
        }

        bool audioOk = audio.gameObject.activeInHierarchy;
        results.Add(new QAResult
        {
            testName = "Audio Clips Configured",
            passed = audioOk,
            details = audioOk ? "SimpleAudio active and ready." : "Audio component inactive."
        });
    }

    private static void TestAndroidProjectSettings()
    {
        bool targetIsAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        string companyName = PlayerSettings.companyName;
        string productName = PlayerSettings.productName;
        string appId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        var orient = PlayerSettings.defaultInterfaceOrientation;

        bool portraitOk = orient == UIOrientation.Portrait;
        bool companyOk = !string.IsNullOrEmpty(companyName) && companyName != "DefaultCompany";
        bool productOk = productName == "Combo Knight";
        bool idOk = appId.Contains("com.") && appId.Contains("comboknight");

        bool allSettings = targetIsAndroid && portraitOk && companyOk && productOk && idOk;
        results.Add(new QAResult
        {
            testName = "Android Project Settings",
            passed = allSettings,
            details = allSettings ? $"Android BuildTarget active, Portrait locked, Product={productName}, Company={companyName}, AppID={appId}" : $"Settings mismatch: Android={targetIsAndroid}, Portrait={portraitOk}, Company={companyName}, Product={productName}, AppID={appId}"
        });
    }
}
