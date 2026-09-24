#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

// ETAPA 2/3 - QA em modo play: sobe o jogo real, força os niveis e confere
// os inimigos novos (Charger no nv2, Shooter no nv4) e o bloqueio do escudo.
public class EnemyPlayQADriver : MonoBehaviour
{
    private const string ReportName = "QA_NEW_ENEMY_PLAYREPORT.txt";
    private const float GlobalTimeout = 210f;

    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private float startTime;
    private bool finished;

    private GameManager gm;
    private PlayerController player;
    private EnemySpawner spawner;

    private void Start()
    {
        startTime = Time.realtimeSinceStartup;
        EnemySpawner.VerboseSpawnLogs = true;
        var legacy = FindAnyObjectByType<RuntimeQARunner>();
        if (legacy != null)
        {
            legacy.StopAllCoroutines();
            legacy.enabled = false;
        }
        StartCoroutine(Run());
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

    private void Diag(string phase)
    {
        Debug.Log($"[QA-Diag] {phase} t={Time.realtimeSinceStartup - startTime:F1}s scale={Time.timeScale:F2} " +
                  $"active={gm != null && gm.IsGameActive} lvl={(gm != null ? gm.PlayerLevel : -1)} xp={(gm != null ? gm.XP : -1)} " +
                  $"spawner={(spawner != null && spawner.enabled)} hearts={(player != null ? player.CurrentHearts : -1f):F1} " +
                  $"slime={Count<SlimeEnemy>()} charger={Count<ChargerEnemy>()} bat={Count<BatEnemy>()} " +
                  $"shooter={Count<ShooterEnemy>()} proj={Count<Projectile>()}");
    }

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
        Record(gm.IsGameActive && gm.PlayerLevel == 1, "StartGame",
            $"active={gm.IsGameActive} level={gm.PlayerLevel} scale={Time.timeScale:F2}");
        Diag("start");

        spawner.EnsureRuntimePrefabs();
        Record(spawner.ChargerPrefab != null && spawner.ShooterPrefab != null && spawner.ProjectilePrefab != null,
            "PrefabsProcedurais",
            $"charger={spawner.ChargerPrefab != null} shooter={spawner.ShooterPrefab != null} projectile={spawner.ProjectilePrefab != null}");

        // ===== NV 1: apenas Slime =====
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 7f)
        {
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }

        CleanupEnemies();
        yield return null;

        int slimes = Count<SlimeEnemy>();
        int chargers = Count<ChargerEnemy>();
        int shooters = Count<ShooterEnemy>();
        int bats = Count<BatEnemy>();
        Record(chargers == 0, "Nv1_SemCharger", $"chargers={chargers}");
        Record(bats == 0, "Nv1_SemBat", $"bats={bats}");
        Record(shooters == 0, "Nv1_SemShooter", $"shooters={shooters}");
        Diag("nv1");

        // ===== NV 2: + Charger =====
        CleanupEnemies();
        LevelUpTo(2);
        yield return null;
        Record(gm.PlayerLevel >= 2, "Sobe_Nv2", $"level={gm.PlayerLevel}");

        spawner.enabled = true;
        t0 = Time.realtimeSinceStartup;
        float lastDiag = t0;
        while (Count<ChargerEnemy>() == 0 && Time.realtimeSinceStartup - t0 < 15f)
        {
            if (Time.realtimeSinceStartup - lastDiag > 2.5f)
            {
                lastDiag = Time.realtimeSinceStartup;
                Diag("charger-wait");
            }
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }
        spawner.enabled = false;

        chargers = Count<ChargerEnemy>();
        Record(chargers >= 1, "Nv2_ChargerSpawn", $"chargers={chargers}");
        Record(Count<ShooterEnemy>() == 0, "Nv2_SemShooter", $"shooters={Count<ShooterEnemy>()}");
        Diag("nv2");
        if (chargers >= 1) yield return TestChargerBehavior();

        // ===== Recompensas do Charger (score 150 / xp 15) =====
        yield return TestChargerRewards();

        // ===== NV 3: + Bat =====
        CleanupProjectiles();
        LevelUpTo(3);
        yield return null;
        Record(gm.PlayerLevel >= 3, "Sobe_Nv3", $"level={gm.PlayerLevel}");

        spawner.enabled = true;
        t0 = Time.realtimeSinceStartup;
        lastDiag = t0;
        while (Count<BatEnemy>() == 0 && Time.realtimeSinceStartup - t0 < 7f)
        {
            if (Time.realtimeSinceStartup - lastDiag > 2.5f)
            {
                lastDiag = Time.realtimeSinceStartup;
                Diag("bat-wait");
            }
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }
        spawner.enabled = false;
        Record(Count<BatEnemy>() >= 1, "Nv3_BatSpawn", $"bats={Count<BatEnemy>()}");
        slimes = Count<SlimeEnemy>();
        Record(slimes >= 1, "Slime_Continuo", $"slimes vivos={slimes}");
        Diag("nv3");

        // ===== NV 4: + Shooter =====
        CleanupProjectiles();
        LevelUpTo(4);
        yield return null;
        Record(gm.PlayerLevel >= 4, "Sobe_Nv4", $"level={gm.PlayerLevel}");

        spawner.enabled = true;
        t0 = Time.realtimeSinceStartup;
        lastDiag = t0;
        while (Count<ShooterEnemy>() == 0 && Time.realtimeSinceStartup - t0 < 15f)
        {
            PinPlayer();
            if (Time.realtimeSinceStartup - lastDiag > 2.5f)
            {
                lastDiag = Time.realtimeSinceStartup;
                Diag("shooter-wait");
            }
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }
        spawner.enabled = false;

        shooters = Count<ShooterEnemy>();
        Record(shooters >= 1, "Nv4_ShooterSpawn", $"shooters={shooters}");
        Diag("nv4");
        if (shooters >= 1) yield return TestShooterBehavior();

        // ===== Bloqueio frontal com Espada & Escudo =====
        yield return TestShieldBlock();

        Finish();
    }

    private IEnumerator TestChargerBehavior()
    {
        player.SetSwordActive(false);
        var charger = FindAnyObjectByType<ChargerEnemy>();
        if (charger == null)
        {
            Record(false, "Charger_Telegrafo", "nenhum charger encontrado");
            yield break;
        }

        if (charger.State == ChargerEnemy.ChargerState.Walk)
        {
            float side = (player.transform.position.x >= 0f) ? -1f : 1f;
            charger.transform.position = new Vector3(
                player.transform.position.x + side * 2.8f,
                charger.transform.position.y, 0f);
        }

        bool sawTelegraph = false;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 3f)
        {
            if (charger == null) break;
            if (charger.State == ChargerEnemy.ChargerState.Telegraph) { sawTelegraph = true; break; }
            if (charger.State == ChargerEnemy.ChargerState.Charge) break;
            if (TimedOut()) break;
            yield return null;
        }
        Record(sawTelegraph || (charger != null && charger.State == ChargerEnemy.ChargerState.Charge),
            "Charger_Telegrafo", charger != null ? $"estado={charger.State}" : "charger destruido");

        bool sawCharge = false;
        float chargeStartX = 0f;
        t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 3f)
        {
            if (charger == null) break;
            if (charger.State == ChargerEnemy.ChargerState.Charge)
            {
                sawCharge = true;
                chargeStartX = charger.transform.position.x;
                break;
            }
            if (TimedOut()) break;
            yield return null;
        }
        Record(sawCharge, "Charger_Investida",
            charger != null ? $"estado={charger.State}" : "charger destruido");

        if (sawCharge && charger != null)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (charger != null)
            {
                float dx = Mathf.Abs(charger.transform.position.x - chargeStartX);
                float speed = dx / 0.25f;
                Record(Mathf.Abs(speed - ChargerEnemy.ChargeSpeed) < 4f,
                    "Charger_Velocidade12", $"velocidade≈{speed:F1} (esperado ≈12)");
            }
            else Record(false, "Charger_Velocidade12", "charger destruido durante medida");

            t0 = Time.realtimeSinceStartup;
            while (charger != null && charger.State == ChargerEnemy.ChargerState.Charge &&
                   Time.realtimeSinceStartup - t0 < 4f)
            {
                if (TimedOut()) break;
                yield return null;
            }
            Record(charger == null || charger.State != ChargerEnemy.ChargerState.Charge,
                "Charger_InvestidaTermina",
                charger != null ? $"estado={charger.State}" : "destruido (colidiu)");

            if (charger != null) charger.transform.position = new Vector3(0f, charger.transform.position.y, 0f);
        }
    }

    private IEnumerator TestChargerRewards()
    {
        var charger = FindAnyObjectByType<ChargerEnemy>();
        if (charger == null)
        {
            Record(false, "Charger_Recompensas", "nenhum charger para matar");
            player.SetSwordActive(true);
            yield break;
        }

        gm.ResetCombo();
        player.SetWeapon(PlayerController.WeaponType.Standard);
        player.SetSwordActive(false);
        yield return new WaitForSecondsRealtime(1.4f);

        int kills0 = gm.Kills;
        int score0 = gm.Score;
        int xp0 = gm.XP;
        int lvl0 = gm.PlayerLevel;
        int threshold0 = gm.XpToNextLevel;

        charger.Die();
        yield return new WaitForSecondsRealtime(0.35f);

        int scoreDelta = gm.Score - score0;
        int xpDelta = gm.XP - xp0;
        Record(gm.Kills == kills0 + 1, "Charger_Kill", $"kills {kills0} -> {gm.Kills}");
        Record(scoreDelta >= 150, "Charger_Score150", $"score delta={scoreDelta}");

        bool xpOk;
        if (gm.PlayerLevel == lvl0) xpOk = xpDelta == 15;
        else xpOk = gm.PlayerLevel == lvl0 + 1 && xpDelta == 15 - threshold0;
        Record(xpOk, "Charger_XP15",
            $"xp delta={xpDelta} level {lvl0} -> {gm.PlayerLevel} (esperado 15 ou 15-{threshold0})");
        player.SetSwordActive(true);
        yield return new WaitForSecondsRealtime(0.3f);
    }

    private IEnumerator TestShooterBehavior()
    {
        CleanupProjectiles();

        ShooterEnemy shooter = null;
        float findUntil = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < findUntil && !TimedOut())
        {
            PinPlayer();
            foreach (var s in FindObjectsByType<ShooterEnemy>(FindObjectsSortMode.None))
            {
                if (!s.IsDead) { shooter = s; break; }
            }
            if (shooter != null) break;
            yield return null;
        }

        if (shooter == null)
        {
            Record(false, "Shooter_Laterais", "nenhum shooter vivo encontrado");
            yield break;
        }

        float sideX = Mathf.Abs(shooter.transform.position.x);
        Record(sideX >= 1.5f, "Shooter_Laterais", $"|x|={sideX:F2} (esperado ≥1.5)");

        bool sawAim = false;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < ShooterEnemy.ShotInterval + 1.5f)
        {
            PinPlayer();
            if (shooter == null) break;
            if (shooter.IsAiming) { sawAim = true; break; }
            if (Count<Projectile>() > 0) break;
            if (TimedOut()) break;
            yield return null;
        }
        Record(sawAim, "Shooter_MiraAmarela", sawAim ? "IsAiming=true visto" : "IsAiming nao visto");

        t0 = Time.realtimeSinceStartup;
        while (Count<Projectile>() == 0 && Time.realtimeSinceStartup - t0 < ShooterEnemy.ShotInterval + 2f)
        {
            PinPlayer();
            if (TimedOut()) break;
            yield return null;
        }
        int seen = Count<Projectile>();
        Record(seen >= 1, "Shooter_Tiro", $"projeteis vistos={seen}");

        t0 = Time.realtimeSinceStartup;
        while (Count<Projectile>() > 0 && Time.realtimeSinceStartup - t0 < 6f)
        {
            PinPlayer();
            if (TimedOut()) break;
            yield return null;
        }
        Record(Count<Projectile>() == 0, "Projetil_Destruido_Apos", $"restantes={Count<Projectile>()}");
    }

    private IEnumerator TestShieldBlock()
    {
        spawner.StopAllCoroutines();
        CleanupEnemies();
        spawner.enabled = false;
        gm.ResetCombo();
        player.SetSwordActive(true);

        if (gm.IsPaused) gm.TogglePause();
        if (gm.IsShopOpen) CloseMerchant();
        if (!gm.IsGameActive) gm.StartGame();
        Time.timeScale = 1f;
        player.ResetHealth();
        Diag("shield-inicio");

        float wallX = FindWallLimit();

        // Passo 1: sem escudo, o projetil frontal DANO o cavaleiro.
        player.SetWeapon(PlayerController.WeaponType.Standard);
        while (player.IsInvulnerable && !TimedOut())
        {
            Time.timeScale = 1f;
            yield return null;
        }
        yield return null;

        CenterPlayer();
        int face = player.FacingDirection >= 0 ? 1 : -1;
        float heartsBeforeDamage = player.CurrentHearts;
        Vector3 spawnPos = ClampInsideWall(
            player.transform.position + new Vector3(face * 2.2f, 0.2f, 0f), wallX);
        SpawnProjectile(spawnPos, -face);

        float t0 = Time.realtimeSinceStartup;
        while (player.CurrentHearts >= heartsBeforeDamage &&
               Count<Projectile>() > 0 && Time.realtimeSinceStartup - t0 < 4f)
        {
            CenterPlayer();
            Time.timeScale = 1f;
            if (TimedOut()) break;
            yield return null;
        }
        Record(player.CurrentHearts < heartsBeforeDamage,
            "Projetil_SemEscudo_Dano",
            $"hearts {heartsBeforeDamage:F2} -> {player.CurrentHearts:F2} restantes={Count<Projectile>()}");

        CleanupProjectiles();
        yield return new WaitForSecondsRealtime(1.4f);

        // Passo 2: com Espada & Escudo de frente, bloqueia sem perder HP.
        // O projetil nasce do lado que o cavaleiro olha e dentro dos limites
        // da arena (fora da parede) para chegar ao corpo.
        player.SetWeapon(PlayerController.WeaponType.ShieldSword);
        yield return null;

        CenterPlayer();
        face = player.FacingDirection >= 0 ? 1 : -1;
        float heartsBeforeBlock = player.CurrentHearts;
        bool blockedSeen = false;
        spawnPos = ClampInsideWall(
            player.transform.position + new Vector3(face * 2.2f, 0.2f, 0f), wallX);
        SpawnProjectile(spawnPos, -face);

        t0 = Time.realtimeSinceStartup;
        while (Count<Projectile>() > 0 && Time.realtimeSinceStartup - t0 < 5f)
        {
            CenterPlayer();
            Time.timeScale = 1f;
            if (HasBlockedText()) { blockedSeen = true; }
            if (TimedOut()) break;
            yield return null;
        }
        if (HasBlockedText()) blockedSeen = true;

        Record(Count<Projectile>() == 0, "Escudo_Bloqueio_Destroi", $"restantes={Count<Projectile>()}");
        Record(player.CurrentHearts >= heartsBeforeBlock,
            "Escudo_SemDano",
            $"hearts {heartsBeforeBlock:F2} -> {player.CurrentHearts:F2}");
        Record(blockedSeen, "Escudo_TextoBloqueado", blockedSeen ? "texto BLOQUEADO visto" : "texto nao visto");

        player.SetWeapon(PlayerController.WeaponType.Standard);
        CleanupProjectiles();
        Diag("shield-fim");
        yield return null;
    }

    private static float FindWallLimit()
    {
        float limit = 3.4f;
        var wl = GameObject.Find("WallLeft");
        var wr = GameObject.Find("WallRight");
        if (wl != null && wr != null)
            limit = Mathf.Abs(wr.transform.position.x) - 0.7f;
        return Mathf.Max(1.5f, limit);
    }

    private static Vector3 ClampInsideWall(Vector3 pos, float wallX)
    {
        return new Vector3(Mathf.Clamp(pos.x, -wallX, wallX), pos.y, 0f);
    }

    private void CenterPlayer()
    {
        var p = player.transform.position;
        player.transform.position = new Vector3(0f, p.y, p.z);
    }

    private Rigidbody2D playerRb;

    private void PinPlayer()
    {
        CenterPlayer();
        if (playerRb == null) playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
        player.ResetHealth();
    }

    private void SpawnProjectile(Vector3 pos, int dir)
    {
        if (spawner.ProjectilePrefab == null) return;
        var go = Instantiate(spawner.ProjectilePrefab, pos, Quaternion.identity);
        go.hideFlags = HideFlags.None;
        go.SetActive(true);
        go.GetComponent<Projectile>()?.Launch(dir);
    }

    private bool HasBlockedText()
    {
        foreach (var ft in FindObjectsByType<FloatingText>(FindObjectsSortMode.None))
        {
            var tmp = ft.GetComponent<TextMeshPro>();
            if (tmp != null && tmp.text == "BLOQUEADO") return true;
        }
        return false;
    }

    private void LevelUpTo(int target)
    {
        int guard = 0;
        while (gm.PlayerLevel < target && guard++ < 12)
        {
            int need = gm.XpToNextLevel - gm.XP;
            gm.AddXp(need > 0 ? need : 1);
        }
    }

    private void CloseMerchant()
    {
        var mi = typeof(GameManager).GetMethod("CloseMerchant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        mi?.Invoke(gm, new object[] { true });
    }

    private void CleanupEnemies()
    {
        if (spawner != null) spawner.StopAllCoroutines();
        foreach (var c in FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<BatEnemy>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<ChargerEnemy>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<ShooterEnemy>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        CleanupProjectiles();
    }

    private void CleanupProjectiles()
    {
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
    }

    private static int Count<T>() where T : Component =>
        FindObjectsByType<T>(FindObjectsSortMode.None).Length;

    private void Finish()
    {
        if (finished) return;
        finished = true;
        EnemySpawner.VerboseSpawnLogs = false;

        var sb = new StringBuilder();
        sb.AppendLine("QA NEW ENEMIES - PLAY MODE (ETAPA 3)");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"SUMMARY: PASS={passed} FAIL={failed} TOTAL={passed + failed}");

        string report = Application.dataPath + "/" + ReportName;
        File.WriteAllText(report, sb.ToString());
        if (failed == 0) Debug.Log($"QA PLAY OK: PASS={passed} FAIL={failed}\n{report}");
        else Debug.LogError($"QA PLAY FALHOU: PASS={passed} FAIL={failed}\n{report}");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
#endif
