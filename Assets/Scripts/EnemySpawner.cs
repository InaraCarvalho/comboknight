using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject slimePrefab;
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private GameObject chargerPrefab;
    [SerializeField] private GameObject shooterPrefab;
    [SerializeField] private GameObject projectilePrefab;
    // Centro de spawn dos inimigos de chao: cada um nasce com os pes no topo
    // do chao (groundY = -4.5) -> slime e charger na mesma altura do cavaleiro.
    // Caiu do antigo groundSpawnY=-3.95, que deixava o slime flutuando e o
    // charger afundado no chao por causa das alturas de sprite diferentes.
    [SerializeField] private float slimeSpawnY = -4.07f;
    [SerializeField] private float chargerSpawnY = -3.70f;
    [SerializeField] private float minSpawnX = -6.2f;
    [SerializeField] private float maxSpawnX = 6.2f;
    [SerializeField] private float minBatY = -2.2f;
    [SerializeField] private float maxBatY = 1.6f;
    [SerializeField] private float minShooterY = -3.6f;
    [SerializeField] private float maxShooterY = -2.9f;
    [SerializeField] private float minDistanceFromPlayer = 2.4f;
    [SerializeField] private float warningDuration = 0.9f;

    // Arte real dos novos inimigos (wireada na cena). Fallback procedural
    // continua disponivel se algum campo ficar null.
    [SerializeField] private Sprite chargerIdleSprite;
    [SerializeField] private Sprite chargerRunSprite;
    [SerializeField] private Sprite shooterIdleSprite;
    [SerializeField] private Sprite shooterPrepareSprite;
    [SerializeField] private Sprite shooterShootSprite;
    [SerializeField] private Sprite projectileSprite;

    public enum EnemyKind { Slime, Charger, Bat, Shooter }

    public static bool VerboseSpawnLogs;

    public static readonly Color ChargerBodyColor = new Color(1f, 0.55f, 0.08f);
    public static readonly Color ShooterBodyColor = new Color(0.62f, 0.25f, 0.92f);
    public static readonly Color ProjectileBodyColor = new Color(1f, 0.92f, 0.25f);

    public GameObject ChargerPrefab => chargerPrefab;
    public GameObject ShooterPrefab => shooterPrefab;
    public GameObject ProjectilePrefab => projectilePrefab;
    public bool SlimePrefabWired => slimePrefab != null;
    public bool BatPrefabWired => batPrefab != null;

    private float slimeTimer = 1.2f;
    private float batTimer = 1.8f;
    private float chargerTimer;
    private float shooterTimer;
    private GameObject sceneSlimePrefab;
    private GameObject sceneBatPrefab;

    private static Sprite cachedWarningSprite;
    private static Sprite cachedCircleSprite;
    private static Sprite cachedRectSprite;

    // Tipos liberados por nivel do jogador: Slime (nv1), Charger (nv2),
    // Bat (nv3) e Shooter (nv4). Os demais nivels mantem todos liberados.
    public static List<EnemyKind> GetUnlockedKinds(int level)
    {
        var kinds = new List<EnemyKind> { EnemyKind.Slime };
        if (level >= 2) kinds.Add(EnemyKind.Charger);
        if (level >= 3) kinds.Add(EnemyKind.Bat);
        if (level >= 4) kinds.Add(EnemyKind.Shooter);
        return kinds;
    }

    // Limites da arena enviados pela ResponsiveCamera: monstros nascem sempre
    // DENTRO do campo de visao, com folga nas laterais em qualquer proporcao.
    public void SetSpawnBounds(float halfWidth)
    {
        minSpawnX = -halfWidth + 0.8f;
        maxSpawnX = halfWidth - 0.8f;
    }

    private void Awake()
    {
        EnsureRuntimePrefabs();
        CacheScenePrefabs();
    }

    // Guarda uma copia dos prefabs WIRADOS da cena para restaurar os casos em
    // que um asset invalido foi zerado em runtime (o QATestHarness exige os dois).
    private void CacheScenePrefabs()
    {
        if (sceneSlimePrefab == null && slimePrefab != null && slimePrefab.GetComponent<SlimeEnemy>() != null)
            sceneSlimePrefab = slimePrefab;
        if (sceneBatPrefab == null && batPrefab != null && batPrefab.GetComponent<BatEnemy>() != null)
            sceneBatPrefab = batPrefab;
    }

    // Prefabs novos (Charger/Shooter/Projectile) sao criados em tempo de
    // execucao quando a cena nao os traz wired: sprites procedurais laranja,
    // roxo e amarelo, sem depender de assets. Chamado tambem pela validacao.
    public void EnsureRuntimePrefabs()
    {
        CacheScenePrefabs();
        if (slimePrefab == null) slimePrefab = sceneSlimePrefab != null ? sceneSlimePrefab : BuildSlimeTemplate();
        if (batPrefab == null) batPrefab = sceneBatPrefab != null ? sceneBatPrefab : BuildBatTemplate();
        if (chargerPrefab == null) chargerPrefab = BuildChargerTemplate();
        if (projectilePrefab == null) projectilePrefab = BuildProjectileTemplate();
        if (shooterPrefab == null) shooterPrefab = BuildShooterTemplate();

        var shooter = shooterPrefab != null ? shooterPrefab.GetComponent<ShooterEnemy>() : null;
        if (shooter != null && shooter.ProjectilePrefab == null && projectilePrefab != null)
            shooter.SetProjectilePrefab(projectilePrefab);
    }

    // Destroi apenas os templates criados em runtime (chamado pela validacao
    // para restaurar a cena sem sujar a pasta de Assets).
    public void ClearRuntimePrefabs()
    {
        ClearIfTemplate(ref slimePrefab);
        ClearIfTemplate(ref batPrefab);
        ClearIfTemplate(ref chargerPrefab);
        ClearIfTemplate(ref shooterPrefab);
        ClearIfTemplate(ref projectilePrefab);
    }

    private static void ClearIfTemplate(ref GameObject prefab)
    {
        if (prefab == null) return;
        if ((prefab.hideFlags & HideFlags.DontSave) == 0) return;
        DestroyImmediate(prefab);
        prefab = null;
    }

    private GameObject BuildChargerTemplate()
    {
        var go = new GameObject("ChargerPrefabTemplate");
        go.tag = "Enemy";
        go.SetActive(false);
        go.hideFlags = HideFlags.DontSave;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = chargerIdleSprite != null ? chargerIdleSprite : GetRectSprite();
        // Arte real: mantem a cor inalterada. Sem arte: laranja procedural.
        sr.color = chargerIdleSprite != null ? Color.white : ChargerBodyColor;
        sr.sortingOrder = 4;
        go.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.55f, 0.7f);
        box.offset = new Vector2(0f, 0f);
        var charger = go.AddComponent<ChargerEnemy>();
        charger.SetSprites(chargerIdleSprite, chargerRunSprite);
        go.AddComponent<HitFlash>();
        return go;
    }

    private GameObject BuildShooterTemplate()
    {
        var go = new GameObject("ShooterPrefabTemplate");
        go.tag = "Enemy";
        go.SetActive(false);
        go.hideFlags = HideFlags.DontSave;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = shooterIdleSprite != null ? shooterIdleSprite : GetCircleSprite();
        sr.color = shooterIdleSprite != null ? Color.white : ShooterBodyColor;
        sr.sortingOrder = 4;
        go.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.35f;
        var shooter = go.AddComponent<ShooterEnemy>();
        shooter.SetSprites(shooterIdleSprite, shooterPrepareSprite, shooterShootSprite);
        go.AddComponent<HitFlash>();
        return go;
    }

    private GameObject BuildProjectileTemplate()
    {
        var go = new GameObject("ProjectilePrefabTemplate");
        go.tag = "Projectile";
        go.SetActive(false);
        go.hideFlags = HideFlags.DontSave;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = projectileSprite != null ? projectileSprite : GetCircleSprite();
        sr.color = projectileSprite != null ? Color.white : ProjectileBodyColor;
        sr.sortingOrder = 6;
        go.transform.localScale = new Vector3(1.0f, 1.0f, 1f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.15f;
        circle.isTrigger = true;
        go.AddComponent<Projectile>();
        return go;
    }

    private GameObject BuildSlimeTemplate()
    {
        var go = new GameObject("SlimePrefabTemplate");
        go.tag = "Enemy";
        go.SetActive(false);
        go.hideFlags = HideFlags.DontSave;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = new Color(0.35f, 0.85f, 0.35f);
        sr.sortingOrder = 4;
        go.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.18f;
        go.AddComponent<SlimeEnemy>();
        go.AddComponent<HitFlash>();
        return go;
    }

    private GameObject BuildBatTemplate()
    {
        var go = new GameObject("BatPrefabTemplate");
        go.tag = "Enemy";
        go.SetActive(false);
        go.hideFlags = HideFlags.DontSave;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = new Color(0.55f, 0.3f, 0.75f);
        sr.sortingOrder = 5;
        go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.2f;
        go.AddComponent<BatEnemy>();
        go.AddComponent<HitFlash>();
        return go;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) return;

        EnsureRuntimePrefabs();

        float diff = GameManager.Instance.DifficultyMultiplier;
        var kinds = GetUnlockedKinds(GameManager.Instance.PlayerLevel);

        // Slimes: spawn CONTINUO (sem ondas), com intervalo que diminui com o nivel.
        slimeTimer += Time.deltaTime;
        float slimeInterval = Mathf.Max(1.1f, 3.4f / diff);
        if (kinds.Contains(EnemyKind.Slime) && slimeTimer >= slimeInterval)
        {
            slimeTimer = 0f;
            if (slimePrefab != null) StartCoroutine(WarnThenSpawn(slimePrefab, PickSpot(minSpawnX, maxSpawnX, slimeSpawnY, slimeSpawnY), false));
        }

        // Chargers (nv2+): raros, mais lentos que os slimes.
        if (kinds.Contains(EnemyKind.Charger))
        {
            chargerTimer += Time.deltaTime;
            float chargerInterval = Mathf.Max(3.6f, 7.0f / diff);
            if (chargerTimer >= chargerInterval)
            {
                chargerTimer = 0f;
                if (VerboseSpawnLogs)
                    Debug.Log($"[QA-Spawn] charger attempt prefab={(chargerPrefab != null)} lvl={GameManager.Instance.PlayerLevel} enabled={isActiveAndEnabled} scale={Time.timeScale:F2}");
                if (chargerPrefab != null) StartCoroutine(WarnThenSpawn(chargerPrefab, PickSpot(minSpawnX, maxSpawnX, chargerSpawnY, chargerSpawnY), false));
            }
        }
        else chargerTimer = 0f;

        // Bats (nv3+): voam baixo, mesmo aviso aereo de antes.
        if (kinds.Contains(EnemyKind.Bat))
        {
            batTimer += Time.deltaTime;
            float batInterval = Mathf.Max(1.7f, 4.6f / diff);
            if (batTimer >= batInterval)
            {
                batTimer = 0f;
                if (batPrefab != null) StartCoroutine(WarnThenSpawn(batPrefab, PickSpot(minSpawnX, maxSpawnX, Random.Range(minBatY, maxBatY), Random.Range(minBatY, maxBatY)), true));
            }
        }

        // Shooters (nv4+): estao nas laterais da arena, na altura do cavaleiro,
        // para que o tiro horizontal tenha chance de acertar (ou ser bloqueado).
        if (kinds.Contains(EnemyKind.Shooter))
        {
            shooterTimer += Time.deltaTime;
            float shooterInterval = Mathf.Max(4.2f, 8.5f / diff);
            if (shooterTimer >= shooterInterval)
            {
                shooterTimer = 0f;
                if (VerboseSpawnLogs)
                    Debug.Log($"[QA-Spawn] shooter attempt prefab={(shooterPrefab != null)} lvl={GameManager.Instance.PlayerLevel} enabled={isActiveAndEnabled} scale={Time.timeScale:F2}");
                if (shooterPrefab != null)
                {
                    float sideX = (Random.value < 0.5f) ? minSpawnX : maxSpawnX;
                    Vector2 spot = PickSpot(sideX - 0.01f, sideX + 0.01f, minShooterY, maxShooterY);
                    StartCoroutine(WarnThenSpawn(shooterPrefab, spot, true));
                }
            }
        }
        else shooterTimer = 0f;
    }

    // Escolhe um ponto aleatorio do cenario, evitando cair em cima do jogador.
    private Vector2 PickSpot(float minX, float maxX, float minY, float maxY)
    {
        Transform player = null;
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            if (player == null) return new Vector2(x, y);
            float dist = Mathf.Abs(x - player.position.x);
            if (dist > minDistanceFromPlayer) return new Vector2(x, y);
        }

        float fallbackX = (player.position.x >= 0f) ? minX : maxX;
        if (minX >= maxX) fallbackX = minX;
        return new Vector2(fallbackX, Random.Range(minY, maxY));
    }

    // Sinaliza o local antes de o inimigo aparecer, para o jogador nao levar
    // dano de surpresa. O aviso pisca no ponto e entao o inimigo nasce la.
    private IEnumerator WarnThenSpawn(GameObject prefab, Vector2 spot, bool isBat)
    {
        if (VerboseSpawnLogs) Debug.Log($"[QA-Spawn] warn {prefab.name} at {spot}");
        GameObject warning = CreateWarning(spot, isBat);
        float t = warningDuration;
        while (t > 0f)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) break;
            t -= Time.deltaTime;
            yield return null;
        }

        if (warning != null)
        {
            warning.transform.DOKill();
            Destroy(warning);
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameActive)
        {
            var instance = Instantiate(prefab, spot, Quaternion.identity);
            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);
            if (VerboseSpawnLogs)
                Debug.Log($"[QA-Spawn] spawned {instance.name} active={instance.activeSelf} pos={instance.transform.position}");

            // Prefab asset do Shooter pode ter chegado sem o projetil ligado:
            // reforca o link no CLONE (nunca no asset da cena).
            var shooter = instance.GetComponent<ShooterEnemy>();
            if (shooter != null && projectilePrefab != null)
                shooter.SetProjectilePrefab(projectilePrefab);
        }
        else if (VerboseSpawnLogs)
        {
            Debug.Log($"[QA-Spawn] ABORT spawn {prefab.name}: game inactive during warning");
        }
    }

    private GameObject CreateWarning(Vector3 spot, bool isBat)
    {
        var go = new GameObject("SpawnWarning");
        go.transform.position = spot;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWarningSprite();
        sr.color = isBat ? new Color(1f, 0.25f, 0.25f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.9f);
        sr.sortingOrder = 20;

        go.transform.localScale = isBat ? new Vector3(1.0f, 1.0f, 1f) : new Vector3(0.9f, 0.28f, 1f);
        go.transform.DOScale(go.transform.localScale * 1.25f, warningDuration)
            .SetEase(Ease.OutQuad);
        sr.DOFade(0.15f, warningDuration * 0.5f).SetLoops(2, LoopType.Yoyo);

        // Pequeno "!" de alerta flutuando sobre o marcador.
        var mark = new GameObject("AlertMark");
        mark.transform.SetParent(go.transform, false);
        mark.transform.localPosition = new Vector3(0f, isBat ? 0.95f : 0.7f, 0f);
        var markSr = mark.AddComponent<SpriteRenderer>();
        markSr.sprite = GetWarningSprite();
        markSr.transform.localScale = new Vector3(isBat ? 0.28f : 0.45f, isBat ? 0.28f : 0.45f, 1f);
        markSr.sortingOrder = 20;
        markSr.DOFade(0.15f, warningDuration * 0.5f).SetLoops(2, LoopType.Yoyo);

        return go;
    }

    // Circulo branco gerado em tempo de execucao (morte ao custo de nenhum asset).
    private static Sprite GetWarningSprite()
    {
        if (cachedWarningSprite != null) return cachedWarningSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float radius = size * 0.5f - 1f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                px[y * size + x] = (d <= radius) ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        cachedWarningSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedWarningSprite;
    }

    private static Sprite GetCircleSprite() => GetRadialSprite(64, circular: true);

    private static Sprite GetRectSprite() => GetRadialSprite(64, circular: false);

    private static Sprite GetRadialSprite(int size, bool circular)
    {
        if (circular && cachedCircleSprite != null) return cachedCircleSprite;
        if (!circular && cachedRectSprite != null) return cachedRectSprite;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        if (circular)
        {
            float radius = size * 0.5f - 1f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = (Vector2.Distance(new Vector2(x, y), center) <= radius) ? Color.white : Color.clear;
        }
        else
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = Color.white;
        }
        tex.SetPixels(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        if (circular) cachedCircleSprite = sprite;
        else cachedRectSprite = sprite;
        return sprite;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
