using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private float maxLifetime = 6f;

    private int direction = 1;
    private float lifetime;
    private bool resolved;

    public void Launch(int dir)
    {
        direction = dir;
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x) * dir, s.y, s.z);
    }

    private void Update()
    {
        if (resolved) return;

        lifetime += Time.deltaTime;
        if (lifetime >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += Vector3.right * direction * speed * Time.deltaTime;

        if (Mathf.Abs(transform.position.x) > 15f) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        Resolve(other.gameObject, other.bounds.center);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;
        Resolve(collision.gameObject, collision.GetContact(0).point);
    }

    private void Resolve(GameObject other, Vector2 point)
    {
        if (resolved) return;

        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc == null) return;
            if (pc.TryBlockProjectile((Vector2)transform.position))
            {
                resolved = true;
                VFXManager.Instance?.SpawnHitSpark(transform.position);
                GameManager.Instance?.FloatText(transform.position + Vector3.up * 0.4f, "BLOQUEADO", new Color(0.6f, 0.88f, 1f), 3.8f);
                GameManager.Instance?.TriggerScreenShake(0.15f, 0.18f);
                GameManager.Instance?.PlaySound(GameManager.SoundType.Coin, 1.3f);
                Destroy(gameObject);
                return;
            }

            resolved = true;
            pc.TakeDamage(1, transform.position);
            VFXManager.Instance?.SpawnHitSpark(transform.position);
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Ground") || other.name.StartsWith("Wall"))
        {
            resolved = true;
            VFXManager.Instance?.SpawnHitSpark(point);
            Destroy(gameObject);
        }
    }
}
