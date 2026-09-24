using UnityEngine;
using DG.Tweening;

public class ShooterEnemy : MonoBehaviour
{
    public const float ShotInterval = 2.0f;
    public const float AimDuration = 0.5f;

    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private int scoreValue = 200;
    [SerializeField] private int xpValue = 20;

    // Arte do arqueiro: idle normalmente, prepare ao mirar, shoot ao soltar a
    // flecha (volta para idle pouco depois).
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite prepareSprite;
    [SerializeField] private Sprite shootSprite;

    public GameObject ProjectilePrefab => projectilePrefab;
    public bool IsAiming { get; private set; }
    public bool IsDead => isDead;

    private SpriteRenderer sr;
    private HitFlash hitFlash;
    private Color baseColor;
    private float timer;
    private float shootSpriteTimer;
    private bool isDead;
    private Tween aimTween;

    public void SetProjectilePrefab(GameObject prefab)
    {
        projectilePrefab = prefab;
    }

    public void SetSprites(Sprite idle, Sprite prepare, Sprite shoot)
    {
        idleSprite = idle;
        prepareSprite = prepare;
        shootSprite = shoot;
    }

    public bool HasArtSprites => idleSprite != null && prepareSprite != null && shootSprite != null;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();
        baseColor = (sr != null) ? sr.color : Color.white;

        if (sr != null && idleSprite != null) sr.sprite = idleSprite;

        var col = GetComponent<Collider2D>();
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            foreach (var pc in playerGO.GetComponents<Collider2D>())
            {
                if (pc != null && !pc.isTrigger && col != null) Physics2D.IgnoreCollision(pc, col, true);
            }
        }

        timer = 0f;
    }

    private void Update()
    {
        if (isDead) return;

        var player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null && sr != null && !isDead)
        {
            // Vira o sprite para a direcao do jogador (esquerda/direita).
            float dx = player.position.x - transform.position.x;
            sr.flipX = dx < 0f;
        }

        timer += Time.deltaTime;

        if (shootSpriteTimer > 0f)
        {
            shootSpriteTimer -= Time.deltaTime;
            if (shootSpriteTimer <= 0f && !IsAiming && sr != null && idleSprite != null)
                sr.sprite = idleSprite;
        }

        bool aiming = timer >= ShotInterval - AimDuration;
        if (aiming && !IsAiming) EnterAim();
        if (timer >= ShotInterval)
        {
            timer = 0f;
            Fire();
        }
    }

    private void EnterAim()
    {
        IsAiming = true;
        if (sr != null && prepareSprite != null) sr.sprite = prepareSprite;
        aimTween = sr.DOColor(new Color(1f, 0.95f, 0.25f, baseColor.a), 0.12f).SetEase(Ease.OutQuad);
        transform.DOPunchScale(new Vector3(-0.15f * transform.localScale.x, 0.15f * transform.localScale.y, 0f), AimDuration, 4, 0.5f);
    }

    private void Fire()
    {
        if (IsAiming)
        {
            IsAiming = false;
            aimTween?.Kill();
            if (sr != null)
            {
                sr.DOKill();
                sr.color = baseColor;
            }
            transform.DOKill();
        }

        if (projectilePrefab == null) return;

        if (sr != null && shootSprite != null) sr.sprite = shootSprite;
        shootSpriteTimer = 0.18f;

        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        int dir;
        float dx = (player != null) ? player.position.x - transform.position.x : -transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) dir = (transform.position.x >= 0f) ? -1 : 1;
        else dir = dx > 0f ? 1 : -1;

        if (sr != null) sr.flipX = dir < 0f;

        var go = Instantiate(projectilePrefab, transform.position + Vector3.right * dir * 0.45f, Quaternion.identity);
        go.hideFlags = HideFlags.None;
        go.SetActive(true);
        go.GetComponent<Projectile>()?.Launch(dir);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;
        if (collision.CompareTag("Sword")) Die();
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        aimTween?.Kill();
        transform.DOKill();
        if (sr != null) sr.DOKill();

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        VFXManager.Instance?.HitStop(0.045f);
        VFXManager.Instance?.SpawnHitSpark(transform.position);
        VFXManager.Instance?.SpawnBatPoof(transform.position);

        hitFlash?.Flash(0.08f);

        transform.DOScale(Vector3.zero, 0.08f).OnComplete(() =>
        {
            GameManager.Instance?.RegisterKill(scoreValue, transform.position, isBat: false);
            GameManager.Instance?.AddXp(xpValue);
            Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        aimTween?.Kill();
        transform.DOKill();
        if (sr != null) sr.DOKill();
    }
}
