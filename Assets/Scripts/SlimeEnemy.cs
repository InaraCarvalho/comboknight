using UnityEngine;
using DG.Tweening;

public class SlimeEnemy : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 2.0f;
    [SerializeField] private float hopHeight = 0.45f;
    [SerializeField] private float hopSpeed = 6f;
    [SerializeField] private int scoreValue = 100;
    [SerializeField] private int xpValue = 12;
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite hopSprite;

    private Transform player;
    private SpriteRenderer sr;
    private Vector3 startPos;
    private float hopTimer;
    private int direction = 1;
    private bool isDead;
    private HitFlash hitFlash;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        sr = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();
        startPos = transform.position;
        hopTimer = Random.Range(0f, Mathf.PI);

        var col = GetComponent<Collider2D>();
        var wl = GameObject.Find("WallLeft")?.GetComponent<Collider2D>();
        var wr = GameObject.Find("WallRight")?.GetComponent<Collider2D>();
        if (col != null && wl != null) Physics2D.IgnoreCollision(col, wl, true);
        if (col != null && wr != null) Physics2D.IgnoreCollision(col, wr, true);

        // (Removido empurrao fisico) cavaleiro NAO empurra mais os inimigos:
        // ignora a colisao entre o corpo dele e o corpo deste inimigo.
        // O dano do inimigo no cavaleiro agora vem da hurtbox (OnTriggerStay2D).
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            foreach (var pc in playerGO.GetComponents<Collider2D>())
            {
                if (pc != null && !pc.isTrigger && col != null) Physics2D.IgnoreCollision(pc, col, true);
            }
        }

        Vector3 s = transform.localScale;
        transform.DOScale(new Vector3(s.x * 1.15f, s.y * 0.85f, 1f), 0.35f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutQuad);
    }

    private void Update()
    {
        if (isDead) return;

        if (player != null && Mathf.Abs(player.position.x - transform.position.x) > 1.0f)
        {
            direction = (player.position.x > transform.position.x) ? 1 : -1;
        }

        hopTimer += Time.deltaTime * hopSpeed;
        float hop = Mathf.Abs(Mathf.Sin(hopTimer));

        if (sr != null && idleSprite != null && hopSprite != null)
        {
            sr.sprite = (hop > 0.25f) ? hopSprite : idleSprite;
        }

        transform.position += new Vector3(direction * baseSpeed * (0.5f + hop * 0.8f) * GetSpeedScale() * Time.deltaTime, 0f, 0f);
        transform.position = new Vector3(transform.position.x, startPos.y + hop * hopHeight, transform.position.z);
    }

    // Fica mais rapido conforme a dificuldade (nivel do jogador) aumenta.
    private float GetSpeedScale()
    {
        float diff = (GameManager.Instance != null) ? GameManager.Instance.DifficultyMultiplier : 1f;
        return Mathf.Min(2.5f, 1f + (diff - 1f) * 0.55f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isDead && collision.CompareTag("Sword"))
        {
            Die();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDead && collision.gameObject.CompareTag("Player"))
        {
            collision.gameObject.GetComponent<PlayerController>()?.TakeDamage(1, transform.position);
        }
    }

    private void OnCollisionStay2D(Collision2D collision) => OnCollisionEnter2D(collision);

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        transform.DOKill();

        // (Corrige dano pos-morte) desliga o collider na hora: inimigo morto
        // nao continua tocando a hurtbox e nao prejudica o cavaleiro.
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

    private void OnDestroy() => transform.DOKill();
}
