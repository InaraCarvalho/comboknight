using UnityEngine;
using DG.Tweening;

public class BatEnemy : MonoBehaviour
{
    [SerializeField] private float speed = 3.2f;
    [SerializeField] private float waveAmplitude = 0.9f;
    [SerializeField] private float waveFrequency = 3.5f;
    [SerializeField] private int scoreValue = 160;
    [SerializeField] private Sprite flyUpSprite;
    [SerializeField] private Sprite flyDownSprite;

    private float baseY;
    private float waveTimer;
    private int direction = 1;
    private bool isDead;
    private SpriteRenderer sr;
    private HitFlash hitFlash;

    private void Start()
    {
        baseY = transform.position.y;
        waveTimer = Random.Range(0f, Mathf.PI * 2);
        direction = (transform.position.x > 0) ? -1 : 1;
        sr = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();

        var col = GetComponent<Collider2D>();
        var wl = GameObject.Find("WallLeft")?.GetComponent<Collider2D>();
        var wr = GameObject.Find("WallRight")?.GetComponent<Collider2D>();
        if (col != null && wl != null) Physics2D.IgnoreCollision(col, wl, true);
        if (col != null && wr != null) Physics2D.IgnoreCollision(col, wr, true);
        
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * direction;
        transform.localScale = s;

        transform.DOScaleY(s.y * 0.7f, 0.15f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.Linear);
    }

    private void Update()
    {
        if (isDead) return;

        transform.position += new Vector3(direction * speed * Time.deltaTime, 0f, 0f);
        waveTimer += Time.deltaTime * waveFrequency;
        transform.position = new Vector3(transform.position.x, baseY + Mathf.Sin(waveTimer) * waveAmplitude, transform.position.z);

        if (sr != null && flyUpSprite != null && flyDownSprite != null)
        {
            sr.sprite = (Mathf.Cos(waveTimer) > 0f) ? flyUpSprite : flyDownSprite;
        }

        if (transform.position.x < -4.6f) { direction = 1; InvertScale(); }
        else if (transform.position.x > 4.6f) { direction = -1; InvertScale(); }
    }

    private void InvertScale()
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * direction;
        transform.localScale = s;
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

        VFXManager.Instance?.HitStop(0.045f);
        VFXManager.Instance?.SpawnHitSpark(transform.position);
        VFXManager.Instance?.SpawnBatPoof(transform.position);

        hitFlash?.Flash(0.08f);
        
        transform.DOScale(Vector3.zero, 0.08f).OnComplete(() =>
        {
            GameManager.Instance?.RegisterKill(scoreValue, transform.position, isBat: true);
            Destroy(gameObject);
        });
    }

    private void OnDestroy() => transform.DOKill();
}
