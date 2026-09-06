using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Coin : MonoBehaviour
{
    [SerializeField] private float magnetDistance = 2.2f;
    [SerializeField] private float magnetSpeed = 9f;

    private Transform playerTransform;
    private Rigidbody2D rb;
    private bool isCollected;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        float vx = Random.Range(-1.8f, 1.8f);
        float vy = Random.Range(3.5f, 6f);
        rb.linearVelocity = new Vector2(vx, vy);

        transform.DOScaleX(0.1f, 0.25f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    private void Update()
    {
        if (isCollected || playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist < magnetDistance)
        {
            rb.gravityScale = 0f;
            transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, magnetSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        if (collision.CompareTag("Player"))
        {
            isCollected = true;
            transform.DOKill();
            VFXManager.Instance?.SpawnCoinShine(transform.position);
            
            transform.DOScale(Vector3.zero, 0.15f).OnComplete(() =>
            {
                GameManager.Instance?.AddCoin(1);
                Destroy(gameObject);
            });
        }
    }

    private void OnDestroy() => transform.DOKill();
}
