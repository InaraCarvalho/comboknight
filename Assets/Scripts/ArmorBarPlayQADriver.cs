#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ETAPA 6 - QA em modo play: sobe o jogo real, compra armadura no mercador,
// toma dano de um inimigo de verdade e confere que a barra azul de armadura
// absorve o golpe (popup "ESCUDO -25") antes de mexer na barra verde de HP.
public class ArmorBarPlayQADriver : MonoBehaviour
{
    private const string ReportName = "QA_ARMOR_PLAYREPORT.txt";
    private const float GlobalTimeout = 180f;

    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private float startTime;
    private bool finished;

    private GameManager gm;
    private PlayerController player;
    private EnemySpawner spawner;
    private GameObject armorBarRoot;
    private Image armorFill;
    private TextMeshProUGUI armorLabel;
    private Image hpFill;

    private static readonly Color ArmorBlue = new Color(0x4d / 255f, 0xa6 / 255f, 0xff / 255f);

    private void Start()
    {
        startTime = Time.realtimeSinceStartup;
        DisableLegacy<RuntimeQARunner>();
        DisableLegacy<EnemyPlayQADriver>();
        DisableLegacy<MerchantShopQADriver>();
        DisableLegacy<XpTextPlayQADriver>();
        StartCoroutine(Run());
    }

    private void DisableLegacy<T>() where T : MonoBehaviour
    {
        var legacy = FindAnyObjectByType<T>();
        if (legacy == null) return;
        legacy.StopAllCoroutines();
        legacy.enabled = false;
    }

    private void Update()
    {
        if (finished) return;
        if (Time.realtimeSinceStartup - startTime > GlobalTimeout)
        {
            Record(false, "Watchdog", $"suite nao terminou em {GlobalTimeout}s");
            Finish();
        }
    }

    private void Record(bool ok, string name, string details)
    {
        if (ok) passed++;
        else failed++;
        string line = ok ? $"[PASS] {name}: {details}" : $"[FAIL] {name}: {details}";
        results.Add(line);
        if (ok) Debug.Log(line);
        else Debug.LogError(line);
    }

    private bool TimedOut() => Time.realtimeSinceStartup - startTime > GlobalTimeout;

    private IEnumerator Run()
    {
        IEnumerator inner = RunInner();
        while (true)
        {
            object current;
            try
            {
                if (!inner.MoveNext()) yield break;
                current = inner.Current;
            }
            catch (System.Exception e)
            {
                Record(false, "Exception", $"{e.GetType().Name}: {e.Message}");
                Finish();
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator RunInner()
    {
        gm = GameManager.Instance;
        player = FindAnyObjectByType<PlayerController>();
        spawner = FindAnyObjectByType<EnemySpawner>();

        if (gm == null || player == null || spawner == null)
        {
            Record(false, "Setup", $"gm={gm != null} player={player != null} spawner={spawner != null}");
            Finish();
            yield break;
        }

        gm.StartGame();
        yield return new WaitForSecondsRealtime(0.3f);
        Record(gm.IsGameActive && gm.PlayerLevel == 1, "Setup",
            $"active={gm.IsGameActive} level={gm.PlayerLevel}");
        Diag("inicio");

        // ===== Estrutura: ArmorBar 240x8 abaixo do HpBar, fill azul #4da6ff =====
        armorBarRoot = GetField(gm, "armorBarRoot") as GameObject;
        armorFill = GetField(gm, "armorBarFill") as Image;
        armorLabel = GetField(gm, "armorText") as TextMeshProUGUI;
        var hpGo = GameObject.Find("HpBar");
        hpFill = hpGo != null ? hpGo.transform.Find("HpFill")?.GetComponent<Image>() : null;

        Record(armorBarRoot != null && armorFill != null && armorLabel != null, "ArmorBar_Existe",
            $"root={armorBarRoot != null} fill={armorFill != null} texto={armorLabel != null}");

        if (armorBarRoot != null)
        {
            var rt = (RectTransform)armorBarRoot.transform;
            var hpRt = hpGo != null ? (RectTransform)hpGo.transform : null;
            bool sizeOk = rt.sizeDelta == new Vector2(240f, 8f);
            bool belowHp = hpRt != null && rt.anchoredPosition.y <= hpRt.anchoredPosition.y - hpRt.sizeDelta.y;
            Record(sizeOk && belowHp, "ArmorBar_TamanhoPosicao",
                $"size={rt.sizeDelta} pos={rt.anchoredPosition} " +
                $"hpPos={(hpRt != null ? hpRt.anchoredPosition.ToString() : "?")} hpSize={(hpRt != null ? hpRt.sizeDelta.ToString() : "?")}");
        }
        else Record(false, "ArmorBar_TamanhoPosicao", "ArmorBar nao encontrado");

        if (armorFill != null)
        {
            Color c = armorFill.color;
            bool blue = Mathf.Abs(c.r - ArmorBlue.r) < 0.02f
                && Mathf.Abs(c.g - ArmorBlue.g) < 0.02f
                && Mathf.Abs(c.b - ArmorBlue.b) < 0.02f;
            bool filled = armorFill.type == Image.Type.Filled && armorFill.fillMethod == Image.FillMethod.Horizontal;
            Record(blue && filled, "ArmorBar_FillAzul",
                $"cor={c} esperado=#{ColorUtility.ToHtmlStringRGB(ArmorBlue)} tipo={armorFill.type}/{armorFill.fillMethod}");
        }
        else Record(false, "ArmorBar_FillAzul", "ArmorFill nao encontrado");

        // ===== Inicial: sem armadura -> barra e texto escondidos =====
        CheckArmorState("ArmorBar_Inicial_Oculta", 0f, false);

        // ===== Loja do mercador (nv5): compra a ARMADURA =====
        LevelUpTo(5);
        float t0 = Time.realtimeSinceStartup;
        while (!gm.IsShopOpen && Time.realtimeSinceStartup - t0 < 4f)
        {
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }
        Record(gm.IsShopOpen && gm.PlayerLevel == 5, "Loja_AbreNv5",
            $"open={gm.IsShopOpen} level={gm.PlayerLevel}");

        gm.AddCoin(100);
        var buttons = GetField(gm, "shopButtons") as Button[];
        var prices = GetField(gm, "shopPrices") as int[];
        if (buttons != null && buttons.Length == 4 && prices != null && prices.Length == 4)
        {
            int coinsPre = gm.Coins;
            buttons[2].onClick.Invoke();
            yield return null;
            bool bought = player.CurrentArmor == player.MaxArmor
                && gm.Coins == coinsPre - prices[2]
                && !buttons[2].interactable;
            Record(bought, "Loja_Compra_Armadura",
                $"armor={player.CurrentArmor:F0}/{player.MaxArmor:F0} moedas {coinsPre} -> {gm.Coins} " +
                $"preco={prices[2]} interactable={buttons[2].interactable}");
        }
        else Record(false, "Loja_Compra_Armadura", "shopButtons/shopPrices ausentes");

        CheckArmorState("ArmorBar_ApósCompra", player.MaxArmor, true);

        // ===== Sair e continuar =====
        var shop = GameObject.Find("Merchant");
        var exitBtn = shop != null ? shop.transform.Find("MerchantCard/CloseBtn")?.GetComponent<Button>() : null;
        if (exitBtn != null) exitBtn.onClick.Invoke();
        yield return null;
        Record(!gm.IsShopOpen && Time.timeScale == 1f && gm.IsGameActive, "Loja_SairEContinuar",
            $"open={gm.IsShopOpen} scale={Time.timeScale:F2} active={gm.IsGameActive}");
        Diag("com-armadura");

        // ===== Dano REAL de inimigo: barra azul absorve, HP intocado =====
        float heartsBefore = player.CurrentHearts;
        float hpFillBefore = hpFill != null ? hpFill.fillAmount : -1f;
        spawner.enabled = true;
        player.SetSwordActive(false);
        bool popupSeen = false;
        t0 = Time.realtimeSinceStartup;
        while (player.CurrentArmor >= player.MaxArmor && Time.realtimeSinceStartup - t0 < 20f)
        {
            PinPlayer();
            if (HasShieldPopup()) popupSeen = true;
            if (TimedOut()) break;
            yield return null;
        }
        // O popup nasce no mesmo golpe: olha mais alguns frames apos a queda.
        for (int i = 0; i < 5 && !popupSeen; i++)
        {
            if (HasShieldPopup()) popupSeen = true;
            yield return null;
        }

        if (player.CurrentArmor >= player.MaxArmor)
        {
            Record(false, "DanoInimigo_ArmaduraAbsorve", "nenhum inimigo acertou o jogador em 20s");
        }
        else
        {
            bool absorbed = player.CurrentArmor == 75f
                && player.CurrentHearts == heartsBefore
                && player.HasArmor;
            Record(absorbed, "DanoInimigo_ArmaduraAbsorve",
                $"armor={player.CurrentArmor:F0}/100 hp={player.CurrentHearts:F1} (antes hp={heartsBefore:F1})");
            Record(popupSeen, "DanoInimigo_PopupEscudo25",
                popupSeen ? "popup 'ESCUDO -25' azul visto" : "popup 'ESCUDO -25' nao encontrado");

            bool fillOk = armorFill != null && Mathf.Abs(armorFill.fillAmount - 0.75f) < 0.001f;
            bool hpUntouched = hpFill != null && Mathf.Abs(hpFill.fillAmount - hpFillBefore) < 0.001f;
            Record(fillOk && hpUntouched, "ArmorBar_75_VerdeIntocada",
                $"fill={armorFill?.fillAmount:F3} (esperado 0,750) hpFill={hpFill?.fillAmount:F3} (antes {hpFillBefore:F3})");

            // ===== Absorve ate zerar (golpes controlados esperando os i-frames) =====
            IsolateEnemies();
            float expected = player.CurrentArmor;
            bool drainOk = true;
            string drainPath = "";
            while (expected > 0f && drainOk)
            {
                yield return WaitInvulnerableClear(4f);
                int heartsTrack = Mathf.RoundToInt(player.CurrentHearts);
                player.TakeDamage(1, player.transform.position + Vector3.right);
                expected = Mathf.Max(0f, expected - 25f);
                drainPath += $"{player.CurrentArmor:F0} ";
                drainOk = player.CurrentArmor == expected
                    && Mathf.RoundToInt(player.CurrentHearts) == heartsTrack
                    && armorFill != null && Mathf.Abs(armorFill.fillAmount - expected / player.MaxArmor) < 0.001f;
                if (TimedOut()) drainOk = false;
            }
            Record(drainOk && player.CurrentArmor == 0f, "ArmorBar_AbsorveAteZerar",
                $"caminho={drainPath.TrimEnd()} armorFinal={player.CurrentArmor:F0} hp={player.CurrentHearts:F1}");

            CheckArmorState("ArmorBar_EscondeAoZerar", 0f, false);

            // ===== Sem armadura: o proximo golpe SUBTRAI a barra verde =====
            yield return WaitInvulnerableClear(4f);
            float heartsPreHp = player.CurrentHearts;
            float hpFillPreHp = hpFill != null ? hpFill.fillAmount : -1f;
            player.TakeDamage(1, player.transform.position + Vector3.right);
            yield return null;
            bool hpDropped = player.CurrentHearts < heartsPreHp
                && hpFill != null && hpFill.fillAmount < hpFillPreHp
                && player.CurrentArmor == 0f;
            Record(hpDropped, "HP_SobeDepois_Armadura0",
                $"hp {heartsPreHp:F1} -> {player.CurrentHearts:F1} hpFill {hpFillPreHp:F3} -> {hpFill?.fillAmount:F3} armor={player.CurrentArmor:F0}");

            // ===== Recompra/GrantArmor recarrega a barra =====
            player.GrantArmor();
            yield return null;
            CheckArmorState("GrantArmor_Recarrega", player.MaxArmor, true);
        }

        Time.timeScale = 1f;
        Diag("fim");
        Finish();
    }

    // Confere armadura, visibilidade da barra/texto e o fill de uma vez.
    private void CheckArmorState(string name, float expectedArmor, bool expectVisible)
    {
        float fill = armorFill != null ? armorFill.fillAmount : -1f;
        bool rootActive = armorBarRoot != null && armorBarRoot.activeSelf;
        bool labelActive = armorLabel != null && armorLabel.gameObject.activeInHierarchy;
        float expectedFill = player != null && player.MaxArmor > 0f
            ? Mathf.Clamp01(expectedArmor / player.MaxArmor) : 0f;
        bool armorOk = player != null && Mathf.Approximately(player.CurrentArmor, expectedArmor);
        bool fillOk = armorFill != null && Mathf.Abs(fill - expectedFill) < 0.001f;
        bool visOk = rootActive == expectVisible && labelActive == expectVisible;
        string labelText = armorLabel != null ? armorLabel.text : "?";
        bool textOk = !expectVisible || labelText == $"ARMADURA {Mathf.CeilToInt(expectedArmor)}/{Mathf.CeilToInt(player != null ? player.MaxArmor : 100f)}";
        Record(armorOk && fillOk && visOk && textOk, name,
            $"armor={player?.CurrentArmor:F0}/{player?.MaxArmor:F0} (esperado {expectedArmor:F0}) " +
            $"fill={fill:F3} (esperado {expectedFill:F3}) barra={rootActive} texto={labelActive} " +
            $"label='{labelText}'");
    }

    private bool HasShieldPopup()
    {
        foreach (var ft in FindObjectsByType<FloatingText>(FindObjectsSortMode.None))
        {
            var tmp = ft.GetComponent<TextMeshPro>();
            if (tmp == null || tmp.text != "ESCUDO -25") continue;
            Color c = tmp.color;
            if (c.a > 0.4f && c.b > 0.85f && c.b > c.g && c.g > c.r) return true;
        }
        return false;
    }

    private IEnumerator WaitInvulnerableClear(float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (player.IsInvulnerable && Time.realtimeSinceStartup - t0 < timeout)
            yield return null;
    }

    private void IsolateEnemies()
    {
        spawner.StopAllCoroutines();
        spawner.enabled = false;
        foreach (var s in FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None)) Destroy(s.gameObject);
        foreach (var b in FindObjectsByType<BatEnemy>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        foreach (var c in FindObjectsByType<ChargerEnemy>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        foreach (var s in FindObjectsByType<ShooterEnemy>(FindObjectsSortMode.None)) Destroy(s.gameObject);
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
    }

    private void LevelUpTo(int target)
    {
        int guard = 0;
        while (gm.PlayerLevel < target && guard++ < 16)
        {
            int need = gm.XpToNextLevel - gm.XP;
            gm.AddXp(need > 0 ? need : 1);
        }
    }

    // Centraliza o jogador para o inimigo alcancar a hurtbox, sem matar nada
    // (espada desligada) e sem limpar i-frames (senao o golpe nao "envelhece").
    private void PinPlayer()
    {
        var p = player.transform.position;
        player.transform.position = new Vector3(0f, p.y, p.z);
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        player.SetSwordActive(false);
    }

    private static object GetField(object target, string field)
    {
        FieldInfo fi = typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        return fi?.GetValue(target);
    }

    private void Diag(string phase)
    {
        Debug.Log($"[QA-Armor-Diag] {phase} t={Time.realtimeSinceStartup - startTime:F1}s scale={Time.timeScale:F2} " +
                  $"active={(gm != null && gm.IsGameActive)} shop={(gm != null && gm.IsShopOpen)} " +
                  $"armor={player?.CurrentArmor:F0}/{player?.MaxArmor:F0} hp={player?.CurrentHearts:F1} " +
                  $"barra={(armorBarRoot != null && armorBarRoot.activeSelf)} slime={Count<SlimeEnemy>()}");
    }

    private static int Count<T>() where T : Component =>
        FindObjectsByType<T>(FindObjectsSortMode.None).Length;

    private void Finish()
    {
        if (finished) return;
        finished = true;
        Time.timeScale = 1f;

        var sb = new StringBuilder();
        sb.AppendLine("QA ARMOR BAR - PLAY MODE (ETAPA 6)");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"SUMMARY: PASS={passed} FAIL={failed} TOTAL={passed + failed}");

        string report = Application.dataPath + "/" + ReportName;
        File.WriteAllText(report, sb.ToString());
        if (failed == 0) Debug.Log($"QA ARMOR OK: PASS={passed} FAIL={failed}\n{report}");
        else Debug.LogError($"QA ARMOR FALHOU: PASS={passed} FAIL={failed}\n{report}");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
#endif
