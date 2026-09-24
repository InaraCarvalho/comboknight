using UnityEngine;
using DG.Tweening;

public class ChargerEnemy : MonoBehaviour
{
    public const float DetectRange = 3.5f;
    public const float TelegraphDuration = 0.6f;
    public const float ChargeSpeed = 12f;
    private const float RecoverDuration = 0.4f;
    private const float PostChargeCooldown = 1.2f;

    [SerializeField] private float walkSpeed = 2.2f;
    [SerializeField] private int scoreValue = 150;
    [SerializeField] private int xpValue = 15;

    // Arte do Charger: idle quando caminha/recupera, run durante a investida.
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite runSprite;

    public enum ChargerState { Walk, Telegraph, Charge, Recover }
    public ChargerState State { get; private set; } = ChargerState.Walk;

    private Transform player;
    private SpriteRenderer sr;
    private HitFlash hitFlash;
    private Color baseColor;
    private Vector3 baseScale;
    private Vector3 startPos;
    private int direction = 1;
    private float stateTimer;
    private float cooldownTimer;
    private bool isDead;
    private Tween blinkTween;

    public void SetSprites(Sprite idle, Sprite run)
    {
        idleSprite = idle;
        runSprite = run;
    }

    public bool HasArtSprites => idleSprite != null && runSprite != null;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        sr = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();
        baseColor = (sr != null) ? sr.color : Color.white;
        baseScale = transform.localScale;
        startPos = transform.position;

        if (sr != null) sr.sprite = idleSprite;

        var col = GetComponent<Collider2D>();
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            foreach (var pc in playerGO.GetComponents<Collider2D>())
            {
                if (pc != null && !pc.isTrigger && col != null) Physics2D.IgnoreCollision(pc, col, true);
            }
        }
    }

    private void Update()
    {
        if (isDead) return;

        switch (State)
        {
            case ChargerState.Walk:
                WalkTick();
                break;
            case ChargerState.Telegraph:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) EnterCharge();
                break;
            case ChargerState.Charge:
                transform.position += new Vector3(direction * ChargeSpeed * Time.deltaTime, 0f, 0f);
                transform.position = new Vector3(transform.position.x, startPos.y, transform.position.z);
                if (Mathf.Abs(transform.position.x) > 13f) EndCharge();
                break;
            case ChargerState.Recover:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    State = ChargerState.Walk;
                    cooldownTimer = PostChargeCooldown;
                }
                break;
        }
    }

    private void WalkTick()
    {
        if (player != null)
        {
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f)
            {
                direction = dx > 0f ? 1 : -1;
                Vector3 s = baseScale;
                transform.localScale = new Vector3(Mathf.Abs(s.x) * direction, s.y, s.z);
            }
        }

        float speed = walkSpeed * GetSpeedScale();
        transform.position += new Vector3(direction * speed * Time.deltaTime, 0f, 0f);
        transform.position = new Vector3(transform.position.x, startPos.y, transform.position.z);

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        else if (player != null && Mathf.Abs(player.position.x - transform.position.x) <= DetectRange) EnterTelegraph();
    }

    private float GetSpeedScale()
    {
        float diff = (GameManager.Instance != null) ? GameManager.Instance.DifficultyMultiplier : 1f;
        return Mathf.Min(2.5f, 1f + (diff - 1f) * 0.55f);
    }

    private void EnterTelegraph()
    {
        State = ChargerState.Telegraph;
        stateTimer = TelegraphDuration;

        int loops = Mathf.Max(2, Mathf.RoundToInt(TelegraphDuration / 0.12f));
        blinkTween = sr.DOColor(new Color(1f, 0.25f, 0.25f, baseColor.a), 0.12f)
            .SetLoops(loops, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
        transform.DOPunchScale(new Vector3(-0.2f * baseScale.x, 0.2f * baseScale.y, 0f), TelegraphDuration, 6, 0.5f);
        GameManager.Instance?.PlaySound(GameManager.SoundType.Combo, 0.9f);
    }

    private void EnterCharge()
    {
        blinkTween?.Kill();
        if (sr != null)
        {
            sr.DOKill();
            sr.color = baseColor;
            if (runSprite != null) sr.sprite = runSprite;
        }
        transform.DOKill();
        transform.localScale = baseScale;
        State = ChargerState.Charge;
    }

    private void EndCharge()
    {
        blinkTween?.Kill();
        if (sr != null)
        {
            sr.DOKill();
            sr.color = baseColor;
            if (idleSprite != null) sr.sprite = idleSprite;
        }
        transform.DOKill();
        transform.localScale = baseScale;
        State = ChargerState.Recover;
        stateTimer = RecoverDuration;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;
        if (collision.CompareTag("Sword")) Die();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (State == ChargerState.Charge &&
            (collision.gameObject.CompareTag("Ground") ||
             collision.gameObject.name.StartsWith("Wall")))
        {
            EndCharge();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        blinkTween?.Kill();
        transform.DOKill();
        if (sr != null) sr.DOKill();

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        VFXManager.Instance?.HitStop(0.045f);
        VFXManager.Instance?.SpawnHitSpark(transform.position);
        VFXManager.Instance?.SpawnSlimeSplat(transform.position);

        hitFlash?.Flash(0.08f);

        Vector3 ds = transform.localScale;
        transform.DOScale(new Vector3(ds.x * 1.35f, ds.y * 0.2f, 1f), 0.08f).OnComplete(() =>
        {
            GameManager.Instance?.RegisterKill(scoreValue, transform.position, isBat: false);
            GameManager.Instance?.AddXp(xpValue);
            Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        blinkTween?.Kill();
        transform.DOKill();
        if (sr != null) sr.DOKill();
    }
}
