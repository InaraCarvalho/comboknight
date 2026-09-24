using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ETAPA 2 - Validacao das novas armas.
// Troca entre as 5 armas na cena real e confere:
//  * velocidades do conceito (base 5.0 / adaga 6.6) e a efetiva de cada arma;
//  * hitbox e alcance (curto < padrao < longo, tamanhos do conceito);
//  * duracao do combo (4.5s na Arma Ritmica, 3.2s nas demais);
//  * texto do HUD de cada arma;
//  * bloqueio frontal de projeteis da Espada & Escudo (e ausencia nas demais);
//  * ciclo de troca das 5 armas (SwapWeapon + GetNextWeapon).
// Campos lidos por reflection: sao privados e, em modo edicao, o
// SerializedObject le a copia nativa que nem sempre reflete escritas diretas.
public static class WeaponStatsValidator
{
    private const float Epsilon = 0.005f;
    private const string ScenePath = "Assets/Scenes/MainScene.unity";

    private struct WeaponCase
    {
        public PlayerController.WeaponType weapon;
        public string label;
        public Vector2 hitboxSize;
        public float frontReach;
        public float comboDuration;
        public bool usesDaggerSpeed;
        public string hudText;

        public WeaponCase(PlayerController.WeaponType weapon, string label, Vector2 hitboxSize,
            float frontReach, float comboDuration, bool usesDaggerSpeed, string hudText)
        {
            this.weapon = weapon;
            this.label = label;
            this.hitboxSize = hitboxSize;
            this.frontReach = frontReach;
            this.comboDuration = comboDuration;
            this.usesDaggerSpeed = usesDaggerSpeed;
            this.hudText = hudText;
        }
    }

    // Ordem exata do ciclo (E/Q) e valores do conceito (ETAPA 2).
    private static readonly WeaponCase[] Cases =
    {
        new WeaponCase(PlayerController.WeaponType.Standard, "Espada Padrao",
            new Vector2(0.50f, 0.60f), 0.650f, GameManager.DefaultComboDuration, false, "ESPADA PADRÃO"),
        new WeaponCase(PlayerController.WeaponType.Dagger, "Adaga Veloz",
            new Vector2(0.35f, 0.25f), 0.495f, GameManager.DefaultComboDuration, true, "ADAGA VELOZ"),
        new WeaponCase(PlayerController.WeaponType.Broadsword, "Lamina Real",
            new Vector2(0.70f, 0.85f), 0.830f, GameManager.DefaultComboDuration, false, "LÂMINA REAL"),
        new WeaponCase(PlayerController.WeaponType.Rhythmic, "Arma Ritmica",
            new Vector2(0.50f, 0.60f), 0.650f, GameManager.ExtendedComboDuration, false, "ARMA RÍTMICA"),
        new WeaponCase(PlayerController.WeaponType.ShieldSword, "Espada e Escudo",
            new Vector2(0.50f, 0.60f), 0.650f, GameManager.DefaultComboDuration, false, "ESPADA E ESCUDO"),
    };

    [MenuItem("Tools/Validate Weapon Stats")]
    public static void RunFromMenu()
    {
        Run();
    }

    // Entrada para batch mode:
    // Unity.exe -batchmode -projectPath <proj> -executeMethod WeaponStatsValidator.RunFromCommandLine -logFile <log>
    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Run()
    {
        var results = new List<string>();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[WeaponQA] Nao foi possivel abrir " + ScenePath);
            return false;
        }

        var player = Object.FindAnyObjectByType<PlayerController>();
        var gm = Object.FindAnyObjectByType<GameManager>();
        var weaponTmp = GameObject.Find("WeaponText")?.GetComponent<TextMeshProUGUI>();
        var swordCol = GetObject<BoxCollider2D>(player, "swordCollider");

        if (player == null || gm == null || weaponTmp == null || swordCol == null)
        {
            Debug.LogError("[WeaponQA] Cena incompleta: player=" + (player != null) +
                           " gm=" + (gm != null) + " hud=" + (weaponTmp != null) +
                           " sword=" + (swordCol != null));
            return false;
        }

        // Guarda os valores originais para devolver a cena ao estado inicial.
        float originalBaseSpeed = GetFloat(player, "baseSpeed");
        float originalDaggerSpeed = GetFloat(player, "daggerSpeed");
        bool originalLocked = GetBool(player, "weaponLocked");
        var originalWeapon = player.CurrentWeapon;
        float originalComboDuration = GetFloat(gm, "comboDuration");
        string originalHudText = weaponTmp.text;
        Vector3 originalHudScale = weaponTmp.transform.localScale;
        Vector3 originalPlayerScale = player.transform.localScale;
        Vector2 originalSwordSize = swordCol.size;
        Vector2 originalSwordOffset = swordCol.offset;

        int passed = 0;
        int failed = 0;

        Check(results, ref passed, ref failed, "geral", "Velocidades do conceito",
            Mathf.Abs(originalBaseSpeed - 5.0f) < Epsilon &&
            Mathf.Abs(originalDaggerSpeed - 6.6f) < Epsilon,
            $"baseSpeed={originalBaseSpeed:F2} (exige 5.00), daggerSpeed={originalDaggerSpeed:F2} (exige 6.60)");

        float standardReach = 0f;
        float daggerReach = 0f;
        float broadswordReach = 0f;

        foreach (var c in Cases)
        {
            player.SetWeapon(c.weapon);

            float expectedSpeed = c.usesDaggerSpeed ? originalDaggerSpeed : originalBaseSpeed;
            float weaponSpeed = GetFloat(player, "weaponSpeed");
            Check(results, ref passed, ref failed, c.label, "Velocidade",
                Mathf.Abs(weaponSpeed - expectedSpeed) < Epsilon,
                $"weaponSpeed={weaponSpeed:F2} esperado={expectedSpeed:F2}");

            Vector2 size = swordCol.size;
            float reach = swordCol.offset.x + size.x * 0.5f;
            Check(results, ref passed, ref failed, c.label, "Hitbox e alcance",
                Mathf.Abs(size.x - c.hitboxSize.x) < Epsilon &&
                Mathf.Abs(size.y - c.hitboxSize.y) < Epsilon &&
                Mathf.Abs(reach - c.frontReach) < Epsilon,
                $"hitbox=({size.x:F2} x {size.y:F2}) esperado=({c.hitboxSize.x:F2} x {c.hitboxSize.y:F2}), " +
                $"frente em {reach:F3} (exige {c.frontReach:F3})");

            float comboDuration = GetFloat(gm, "comboDuration");
            Check(results, ref passed, ref failed, c.label, "Duracao do combo",
                Mathf.Abs(comboDuration - c.comboDuration) < Epsilon,
                $"comboDuration={comboDuration:F2}s esperado={c.comboDuration:F2}s" +
                (c.comboDuration > GameManager.DefaultComboDuration ? " (estendida pela Arma Ritmica)" : ""));

            gm.UpdateWeaponHUD(c.weapon);
            Check(results, ref passed, ref failed, c.label, "Texto do HUD",
                weaponTmp.text == c.hudText,
                $"texto=\"{weaponTmp.text}\" esperado=\"{c.hudText}\"");

            // Escudo: exatamente UM dos lados (o da frente) e bloqueado;
            // nas demais armas nenhum lado e bloqueado.
            Vector2 pos = player.transform.position;
            bool blockedRight = player.TryBlockProjectile(pos + Vector2.right);
            bool blockedLeft = player.TryBlockProjectile(pos + Vector2.left);
            bool shieldActive = (c.weapon == PlayerController.WeaponType.ShieldSword);
            bool shieldOk = shieldActive ? (blockedRight != blockedLeft)
                                         : (!blockedRight && !blockedLeft);
            Check(results, ref passed, ref failed, c.label, "Projeto frontal bloqueado",
                shieldOk,
                $"escudo={shieldActive}, bloqueia direita={blockedRight}, esquerda={blockedLeft}");

            if (c.weapon == PlayerController.WeaponType.Standard) standardReach = reach;
            else if (c.weapon == PlayerController.WeaponType.Dagger) daggerReach = reach;
            else if (c.weapon == PlayerController.WeaponType.Broadsword) broadswordReach = reach;
        }

        Check(results, ref passed, ref failed, "geral", "Alcance curto < padrao < longo",
            daggerReach < standardReach && standardReach < broadswordReach,
            $"adaga={daggerReach:F3} < padrao={standardReach:F3} < lamina real={broadswordReach:F3}");

        // Ciclo real pelo metodo de troca (E/Q), com a trava do mercador
        // liberada: 5 trocas percorrem as 5 armas e voltam ao inicio.
        player.SetWeapon(PlayerController.WeaponType.Standard);
        SetBool(player, "weaponLocked", false);
        var startWeapon = player.CurrentWeapon;
        bool cycleOk = true;
        var path = new StringBuilder();
        for (int i = 0; i < Cases.Length; i++)
        {
            cycleOk = cycleOk && (player.CurrentWeapon == Cases[i].weapon);
            path.Append(player.CurrentWeapon);
            if (i < Cases.Length - 1) path.Append(" -> ");
            player.SwapWeapon();
        }
        cycleOk = cycleOk && (player.CurrentWeapon == startWeapon);
        path.Append(" -> ").Append(player.CurrentWeapon);
        Check(results, ref passed, ref failed, "geral", "Ciclo das 5 armas (SwapWeapon)",
            cycleOk, path.ToString());

        // Devolve a cena ao estado original (nao salva nada).
        player.SetWeapon(originalWeapon);
        SetBool(player, "weaponLocked", originalLocked);
        gm.SetComboDuration(originalComboDuration);
        swordCol.size = originalSwordSize;
        swordCol.offset = originalSwordOffset;
        weaponTmp.text = originalHudText;
        weaponTmp.transform.localScale = originalHudScale;
        player.transform.localScale = originalPlayerScale;
        weaponTmp.transform.DOKill();
        player.transform.DOKill();

        var sb = new StringBuilder();
        sb.AppendLine("=== COMBO KNIGHT WEAPONS QA (ETAPA 2) ===");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"-------------------------------------------------");
        sb.AppendLine($"SUMMARY: Total={passed + failed} | Passed={passed} | Failed={failed}");
        string report = sb.ToString();

        File.WriteAllText(Application.dataPath + "/QA_WEAPON_REPORT.txt", report);
        if (failed == 0) Debug.Log(report);
        else Debug.LogError(report);

        return failed == 0;
    }

    private static void Check(List<string> results, ref int passed, ref int failed,
        string caseLabel, string testName, bool ok, string details)
    {
        string mark = ok ? "[PASS]" : "[FAIL]";
        results.Add($"{mark} {caseLabel} {testName}: {details}");
        if (ok) passed++;
        else failed++;
    }

    private static FieldInfo FindField(object target, string name)
    {
        if (target == null) return null;
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static float GetFloat(object target, string name)
    {
        FieldInfo fi = FindField(target, name);
        return fi != null && fi.FieldType == typeof(float) ? (float)fi.GetValue(target) : float.NaN;
    }

    private static bool GetBool(object target, string name)
    {
        FieldInfo fi = FindField(target, name);
        return fi != null && fi.FieldType == typeof(bool) && (bool)fi.GetValue(target);
    }

    private static void SetBool(object target, string name, bool value)
    {
        FieldInfo fi = FindField(target, name);
        if (fi != null && fi.FieldType == typeof(bool)) fi.SetValue(target, value);
    }

    private static T GetObject<T>(object target, string name) where T : class
    {
        FieldInfo fi = FindField(target, name);
        return fi != null ? fi.GetValue(target) as T : null;
    }
}
