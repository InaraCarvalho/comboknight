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

// ETAPA 5 - QA em modo play: sobe o jogo real, mata slimes e morcegos e
// confere se o texto numérico X / Y XP (-Z) e o preenchimento da barra de XP
// atualizam a cada abate e resetam ao subir de nível.
public class XpTextPlayQADriver : MonoBehaviour
{
    private const string ReportName = "QA_XPTEXT_PLAYREPORT.txt";
    private const float GlobalTimeout = 150f;

    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private float startTime;
    private bool finished;

    private GameManager gm;
    private PlayerController player;
    private EnemySpawner spawner;
    private TextMeshProUGUI xpText;
    private Image xpFill;

    private void Start()
    {
        startTime = Time.realtimeSinceStartup;
        DisableLegacy<RuntimeQARunner>();
        DisableLegacy<EnemyPlayQADriver>();
        DisableLegacy<MerchantShopQADriver>();
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
        Record(gm.IsGameActive && gm.PlayerLevel == 1 && gm.XP == 0, "Setup",
            $"active={gm.IsGameActive} level={gm.PlayerLevel} xp={gm.XP}");

        // ===== Estrutura e estilo do texto sobre a barra de XP =====
        var xpBar = GameObject.Find("XpBar");
        xpText = null;
        xpFill = null;
        if (xpBar != null)
        {
            var t = xpBar.transform.Find("XpNumericText");
            if (t != null) xpText = t.GetComponent<TextMeshProUGUI>();
            var f = xpBar.transform.Find("XpFill");
            if (f != null) xpFill = f.GetComponent<Image>();
        }
        Record(xpText != null && xpFill != null, "XpText_Existe",
            $"text={xpText != null} fill={xpFill != null}");

        if (xpText != null)
        {
            Color32 oc = xpText.outlineColor;
            bool outlineBlack = oc.r <= 8 && oc.g <= 8 && oc.b <= 8 && oc.a >= 128;
            bool style = Mathf.Approximately(xpText.fontSize, 11f)
                && xpText.color == Color.white
                && (xpText.fontStyle & FontStyles.Bold) != 0
                && outlineBlack
                && xpText.outlineWidth > 0f
                && xpText.transform.parent != null
                && xpText.transform.parent.name == "XpBar";
            Record(style, "XpText_Estilo",
                $"font={xpText.fontSize} cor={xpText.color} bold={(xpText.fontStyle & FontStyles.Bold) != 0} " +
                $"outline={xpText.outlineWidth:F2}/{oc} " +
                $"pai={xpText.transform.parent?.name}");
        }
        else Record(false, "XpText_Estilo", "texto nao encontrado");
        Diag("hud");

        // ===== Estado inicial: 0 / 35 XP (-35), barra vazia =====
        CheckState("XpText_Inicial", 0, 35, 1);

        // ===== Abate 1 de slime: atualiza na hora (12 / 35) =====
        yield return KillNextSlime("Slime1");
        if (failed > 0 && TimedOut()) { Finish(); yield break; }
        CheckState("Abate_Slime1_AtualizaNaHora", 12, 35, 1);

        // ===== Abate 2 de slime: acumula (24 / 35) =====
        yield return KillNextSlime("Slime2");
        if (TimedOut()) { Finish(); yield break; }
        CheckState("Abate_Slime2_Acumula", 24, 35, 1);

        // ===== Abate 3 de slime: sobe de nivel e RESETA (36 -> nv2, 1 / 60) =====
        yield return KillNextSlime("Slime3");
        if (TimedOut()) { Finish(); yield break; }
        CheckState("Abate_Slime3_SobeNivel_Reseta", 1, 60, 2);

        // ===== Level up direto para o nv3 (zera a barra de novo) =====
        LevelUpTo(3);
        yield return null;
        CheckState("SobeNv3_ResetaBarra", 0, 85, 3);

        // ===== Abate de morcego no nv3: +18 XP atualiza na hora =====
        spawner.enabled = true;
        float t0 = Time.realtimeSinceStartup;
        while (Count<BatEnemy>() == 0 && Time.realtimeSinceStartup - t0 < 10f)
        {
            PinPlayer();
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }
        var bat = FindAnyObjectByType<BatEnemy>();
        if (bat == null)
        {
            Record(false, "Abate_Morcego_AtualizaNaHora", "nenhum morcego spawnou");
        }
        else
        {
            IsolateEnemies(bat.gameObject);
            int xpBeforeBat = gm.XP;
            int levelBeforeBat = gm.PlayerLevel;
            bat.Die();
            yield return WaitXpApplied(xpBeforeBat, levelBeforeBat, 3f);
            CheckState("Abate_Morcego_AtualizaNaHora", 18, 85, 3);
        }

        player.SetSwordActive(true);
        Time.timeScale = 1f;
        Diag("fim");
        Finish();
    }

    // Espera o proximo abate espalhar o XP: o Die() dos inimigos so chama
    // AddXp no OnComplete de um tween de 0.08s (escala do tempo), entao o
    // texto so muda ~0.13s depois do golpe — nao e imediato no mesmo frame.
    private IEnumerator WaitXpApplied(int xpBefore, int levelBefore, float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (gm.XP == xpBefore && gm.PlayerLevel == levelBefore &&
               Time.realtimeSinceStartup - t0 < timeout)
        {
            yield return null;
        }
    }

    // Mata o proximo slime espalhado: spawna, isola dos demais (sem deixar a
    // espada matar outros e baguncar o XP) e chama Die() manualmente.
    private IEnumerator KillNextSlime(string tag)
    {
        spawner.enabled = true;
        float t0 = Time.realtimeSinceStartup;
        while (Count<SlimeEnemy>() == 0 && Time.realtimeSinceStartup - t0 < 10f)
        {
            PinPlayer();
            if (TimedOut()) yield break;
            yield return null;
        }
        var slime = FindAnyObjectByType<SlimeEnemy>();
        if (slime == null)
        {
            Record(false, $"Abate_{tag}", "nenhum slime spawnou");
            yield break;
        }
        // Isolar ANTES de matar: os slimes estao vivos (sem XP pendente) e a
        // espada esta desligada, entao destrui-los nao perde recompensa nenhuma.
        IsolateEnemies(slime.gameObject);
        int xpBefore = gm.XP;
        int levelBefore = gm.PlayerLevel;
        slime.Die();
        yield return WaitXpApplied(xpBefore, levelBefore, 3f);
    }

    private void CheckState(string name, int expectedXp, int expectedTotal, int expectedLevel)
    {
        int remaining = Mathf.Max(0, expectedTotal - expectedXp);
        string expected = $"{expectedXp} / {expectedTotal} XP  (-{remaining})";
        bool textOk = xpText != null && xpText.text == expected;
        bool fillOk = xpFill != null && Mathf.Abs(xpFill.fillAmount - (float)expectedXp / expectedTotal) < 0.0005f;
        bool levelOk = gm.PlayerLevel == expectedLevel && gm.XP == expectedXp && gm.XpToNextLevel == expectedTotal;
        Record(textOk && fillOk && levelOk, name,
            $"texto='{(xpText != null ? xpText.text : "?")}' esperado='{expected}' " +
            $"fill={(xpFill != null ? xpFill.fillAmount.ToString("F3") : "?")} " +
            $"esperadoFill={((float)expectedXp / expectedTotal):F3} " +
            $"level={gm.PlayerLevel} xp={gm.XP} total={gm.XpToNextLevel}");
    }

    private void IsolateEnemies(GameObject keep)
    {
        spawner.StopAllCoroutines();
        spawner.enabled = false;
        foreach (var s in FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None))
            if (s.gameObject != keep) Destroy(s.gameObject);
        foreach (var b in FindObjectsByType<BatEnemy>(FindObjectsSortMode.None))
            if (b.gameObject != keep) Destroy(b.gameObject);
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

    private void PinPlayer()
    {
        var p = player.transform.position;
        player.transform.position = new Vector3(0f, p.y, p.z);
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        player.ResetHealth();
        // Sem espada os inimigos nao morrem sozinhos; sem isso o dano deles
        // bagunca a deterministic apenas do XP contado pelos Die() manuais.
        player.SetSwordActive(false);
    }

    private static int Count<T>() where T : Component =>
        FindObjectsByType<T>(FindObjectsSortMode.None).Length;

    private void Diag(string phase)
    {
        Debug.Log($"[QA-Xp-Diag] {phase} t={Time.realtimeSinceStartup - startTime:F1}s scale={Time.timeScale:F2} " +
                  $"active={(gm != null && gm.IsGameActive)} lvl={(gm != null ? gm.PlayerLevel : -1)} " +
                  $"xp={(gm != null ? gm.XP : -1)} total={(gm != null ? gm.XpToNextLevel : -1)} " +
                  $"texto='{(xpText != null ? xpText.text : "?")}' " +
                  $"slime={Count<SlimeEnemy>()} bat={Count<BatEnemy>()}");
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        if (player != null) player.SetSwordActive(true);

        var sb = new StringBuilder();
        sb.AppendLine("QA XP NUMERIC TEXT - PLAY MODE (ETAPA 5)");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"SUMMARY: PASS={passed} FAIL={failed} TOTAL={passed + failed}");

        string report = Application.dataPath + "/" + ReportName;
        File.WriteAllText(report, sb.ToString());
        if (failed == 0) Debug.Log($"QA XPTEXT OK: PASS={passed} FAIL={failed}\n{report}");
        else Debug.LogError($"QA XPTEXT FALHOU: PASS={passed} FAIL={failed}\n{report}");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
#endif
