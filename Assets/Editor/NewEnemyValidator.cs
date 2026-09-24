using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ETAPA 3 - Validacao (modo edicao) dos inimigos novos.
// Confere:
//  * gating de tipos por nivel (Slime nv1, Charger nv2, Bat nv3, Shooter nv4);
//  * prefabs procedurais criados pelo EnemySpawner (componentes, tags, cores);
//  * constantes publicas (telégrafo 0.6s / 3.5u / investida 12f; tiro 2.0s / mira 0.5s);
//  * defaults de scoreValue/xpValue (Charger 150/15, Shooter 200/20) via reflection.
public static class NewEnemyValidator
{
    private const float Epsilon = 0.001f;
    private const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Validate New Enemies")]
    public static void RunFromMenu()
    {
        Run();
    }

    // Unity.exe -batchmode -projectPath <proj> -executeMethod NewEnemyValidator.RunFromCommandLine -logFile <log>
    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Run()
    {
        var results = new List<string>();

        // ===== Gating por nivel =====
        CheckGating(results);

        // ===== Constantes publicas =====
        Check(results, Mathf.Abs(ChargerEnemy.DetectRange - 3.5f) < Epsilon,
            "Charger.DetectRange", $"={ChargerEnemy.DetectRange} (esperado 3.5)");
        Check(results, Mathf.Abs(ChargerEnemy.TelegraphDuration - 0.6f) < Epsilon,
            "Charger.TelegraphDuration", $"={ChargerEnemy.TelegraphDuration} (esperado 0.6)");
        Check(results, Mathf.Abs(ChargerEnemy.ChargeSpeed - 12f) < Epsilon,
            "Charger.ChargeSpeed", $"={ChargerEnemy.ChargeSpeed} (esperado 12)");
        Check(results, Mathf.Abs(ShooterEnemy.ShotInterval - 2.0f) < Epsilon,
            "Shooter.ShotInterval", $"={ShooterEnemy.ShotInterval} (esperado 2.0)");
        Check(results, Mathf.Abs(ShooterEnemy.AimDuration - 0.5f) < Epsilon,
            "Shooter.AimDuration", $"={ShooterEnemy.AimDuration} (esperado 0.5)");

        // ===== Defaults de score/xp por reflection =====
        CheckDefaultValues(results);

        // ===== Prefabs procedurais na cena real =====
        CheckRuntimePrefabs(results);

        int pass = 0, fail = 0;
        foreach (var line in results)
            if (line.StartsWith("[PASS]")) pass++;
            else fail++;

        var sb = new StringBuilder();
        sb.AppendLine("QA NEW ENEMIES - EDIT MODE (ETAPA 3)");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"SUMMARY: PASS={pass} FAIL={fail} TOTAL={pass + fail}");

        string report = Application.dataPath + "/QA_NEW_ENEMY_REPORT.txt";
        File.WriteAllText(report, sb.ToString());
        Debug.Log($"NewEnemyValidator: PASS={pass} FAIL={fail} -> {report}");
        return fail == 0;
    }

    private static void CheckGating(List<string> results)
    {
        var l1 = EnemySpawner.GetUnlockedKinds(1);
        Check(results, l1.Count == 1 && l1.Contains(EnemySpawner.EnemyKind.Slime),
            "Gating_Nv1", $"[{string.Join(",", l1)}] (esperado Slime)");

        var l2 = EnemySpawner.GetUnlockedKinds(2);
        Check(results, l2.Count == 2 && l2.Contains(EnemySpawner.EnemyKind.Slime) && l2.Contains(EnemySpawner.EnemyKind.Charger),
            "Gating_Nv2_Charger", $"[{string.Join(",", l2)}] (esperado Slime+Charger)");

        var l3 = EnemySpawner.GetUnlockedKinds(3);
        Check(results, l3.Count == 3 && l3.Contains(EnemySpawner.EnemyKind.Bat),
            "Gating_Nv3_Bat", $"[{string.Join(",", l3)}] (esperado Slime+Charger+Bat)");

        var l4 = EnemySpawner.GetUnlockedKinds(4);
        Check(results, l4.Count == 4 && l4.Contains(EnemySpawner.EnemyKind.Shooter),
            "Gating_Nv4_Shooter", $"[{string.Join(",", l4)}] (esperado Slime+Charger+Bat+Shooter)");

        var l5 = EnemySpawner.GetUnlockedKinds(5);
        Check(results, l5.Count == 4, "Gating_Nv5_Mantem", $"count={l5.Count} (esperado 4)");
    }

    private static void CheckDefaultValues(List<string> results)
    {
        var go = new GameObject("~ValidatorTemp");
        try
        {
            var charger = go.AddComponent<ChargerEnemy>();
            int score = (int)GetField(charger, "scoreValue");
            int xp = (int)GetField(charger, "xpValue");
            Check(results, score == 150, "Charger.scoreValue", $"={score} (esperado 150)");
            Check(results, xp == 15, "Charger.xpValue", $"={xp} (esperado 15)");

            var shooter = go.AddComponent<ShooterEnemy>();
            int sScore = (int)GetField(shooter, "scoreValue");
            int sXp = (int)GetField(shooter, "xpValue");
            Check(results, sScore == 200, "Shooter.scoreValue", $"={sScore} (esperado 200)");
            Check(results, sXp == 20, "Shooter.xpValue", $"={sXp} (esperado 20)");

            var proj = go.AddComponent<Projectile>();
            float speed = (float)GetField(proj, "speed");
            Check(results, Mathf.Abs(speed - 6f) < Epsilon, "Projectile.speed", $"={speed} (esperado 6)");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static object GetField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? field.GetValue(target) : null;
    }

    private static void CheckRuntimePrefabs(List<string> results)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var spawner = Object.FindAnyObjectByType<EnemySpawner>();
        if (spawner == null)
        {
            Check(results, false, "EnemySpawner_NaCena", "nao encontrado em MainScene");
            return;
        }

        // Cenas antigas podem ter slime/bat sem componente (asset errado): zera
        // antes do Ensure para o fallback procedural entrar.
        ForceNullIfInvalid(spawner, "slimePrefab", typeof(SlimeEnemy));
        ForceNullIfInvalid(spawner, "batPrefab", typeof(BatEnemy));

        spawner.EnsureRuntimePrefabs();

        try
        {
            // A cena deve continuar com slime/bat wired (exigencia do QATestHarness).
            Check(results, spawner.SlimePrefabWired, "Cena_SlimeWired", spawner.SlimePrefabWired ? "ok" : "null");
            Check(results, spawner.BatPrefabWired, "Cena_BatWired", spawner.BatPrefabWired ? "ok" : "null");

            var charger = spawner.ChargerPrefab;
            Check(results, charger != null, "Charger_PrefabCriado", charger != null ? charger.name : "null");
            if (charger != null)
            {
                Check(results, charger.CompareTag("Enemy"), "Charger_Tag", charger.tag);
                Check(results, !charger.activeSelf, "Charger_Inativo", $"active={charger.activeSelf}");
                Check(results, charger.GetComponent<ChargerEnemy>() != null, "Charger_Componente", "presente");
                Check(results, charger.GetComponent<Rigidbody2D>() != null, "Charger_Rigidbody", "presente");
                Check(results, charger.GetComponent<Collider2D>() != null, "Charger_Collider", "presente");
                Check(results, charger.GetComponent<HitFlash>() != null, "Charger_HitFlash", "presente");
                var csr = charger.GetComponent<SpriteRenderer>();
                bool artOk = csr != null && csr.sprite != null && csr.sprite.name.Contains("Runner");
                Check(results, artOk, "Charger_SpriteIdle",
                    csr != null && csr.sprite != null ? csr.sprite.name : "sem SpriteRenderer/sprite");
                var chargerComp = charger.GetComponent<ChargerEnemy>();
                Check(results, chargerComp != null && chargerComp.HasArtSprites,
                    "Charger_SpriteRun", chargerComp != null && chargerComp.HasArtSprites ? "ok" : "sem runSprite");
            }

            var shooter = spawner.ShooterPrefab;
            Check(results, shooter != null, "Shooter_PrefabCriado", shooter != null ? shooter.name : "null");
            if (shooter != null)
            {
                Check(results, shooter.CompareTag("Enemy"), "Shooter_Tag", shooter.tag);
                Check(results, !shooter.activeSelf, "Shooter_Inativo", $"active={shooter.activeSelf}");
                var shc = shooter.GetComponent<ShooterEnemy>();
                Check(results, shc != null, "Shooter_Componente", "presente");
                Check(results, shooter.GetComponent<Rigidbody2D>() != null, "Shooter_Rigidbody", "presente");
                Check(results, shooter.GetComponent<Collider2D>() != null, "Shooter_Collider", "presente");
                Check(results, shooter.GetComponent<HitFlash>() != null, "Shooter_HitFlash", "presente");
                Check(results, shc != null && shc.ProjectilePrefab != null,
                    "Shooter_WiringProjetil", shc != null && shc.ProjectilePrefab != null ? "ligado" : "null");
                var ssr = shooter.GetComponent<SpriteRenderer>();
                bool shooterArtOk = ssr != null && ssr.sprite != null && ssr.sprite.name.Contains("Archer");
                Check(results, shooterArtOk, "Shooter_SpriteIdle",
                    ssr != null && ssr.sprite != null ? ssr.sprite.name : "sem SpriteRenderer/sprite");
                var shooterComp = shooter.GetComponent<ShooterEnemy>();
                Check(results, shooterComp != null && shooterComp.HasArtSprites,
                    "Shooter_WiringPoses", shooterComp != null && shooterComp.HasArtSprites ? "ok" : "sem prepare/shoot");
            }

            var proj = spawner.ProjectilePrefab;
            Check(results, proj != null, "Projetil_PrefabCriado", proj != null ? proj.name : "null");
            if (proj != null)
            {
                Check(results, proj.CompareTag("Projectile"), "Projetil_Tag", proj.tag);
                Check(results, proj.GetComponent<Projectile>() != null, "Projetil_Componente", "presente");
                var pcoll = proj.GetComponent<Collider2D>();
                Check(results, pcoll != null && pcoll.isTrigger, "Projetil_Trigger",
                    pcoll != null ? $"isTrigger={pcoll.isTrigger}" : "sem collider");
                Check(results, proj.GetComponent<Rigidbody2D>() != null, "Projetil_Rigidbody", "presente");
                var psr = proj.GetComponent<SpriteRenderer>();
                bool arrowArt = psr != null && psr.sprite != null && psr.sprite.name.Contains("Arrow");
                Check(results, arrowArt, "Projetil_SpriteFlecha",
                    psr != null && psr.sprite != null ? psr.sprite.name : "sem SpriteRenderer/sprite");
            }
        }
        finally
        {
            spawner.ClearRuntimePrefabs();
        }
    }

    private static void ForceNullIfInvalid(EnemySpawner spawner, string fieldName, System.Type required)
    {
        var field = typeof(EnemySpawner).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return;
        var value = field.GetValue(spawner) as GameObject;
        if (value != null && value.GetComponent(required) == null) field.SetValue(spawner, null);
    }

    private static void Check(List<string> results, bool ok, string name, string details)
    {
        string line = ok ? $"[PASS] {name}: {details}" : $"[FAIL] {name}: {details}";
        results.Add(line);
        if (!ok) Debug.LogError(line);
    }
}
