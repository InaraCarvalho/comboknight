using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 6.5f;
    [SerializeField] private float baseJumpForce = 13.5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [SerializeField] private BoxCollider2D swordCollider;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform weaponTransform;
    [SerializeField] private Sprite broadswordSprite;
    [SerializeField] private Sprite daggerSprite;
    [SerializeField] private Sprite broadswordIdleSprite;
    [SerializeField] private Sprite daggerIdleSprite;
    [SerializeField] private float attackDuration = 0.16f;

    [SerializeField] private int maxHearts = 5;
    private int currentHearts;
    public int CurrentHearts => currentHearts;
    private bool isInvulnerable;
    public bool IsInvulnerable => isInvulnerable;
    private float knockbackTimer;
    private float attackTimer;
    public bool IsAttacking => attackTimer > 0f;

    [SerializeField] private Vector3 baseScale = new Vector3(2.53f, 2.53f, 1f);
    [SerializeField] private float autoAttackRangeBroadsword = 1.9f;
    [SerializeField] private float autoAttackRangeDagger = 1.5f;
    [SerializeField] private float autoAttackCooldownBroadsword = 0.28f;
    [SerializeField] private float autoAttackCooldownDagger = 0.18f;
    private float autoAttackTimer;
    private readonly Collider2D[] autoAttackHits = new Collider2D[8];

    public enum WeaponType { Broadsword, Dagger }
    public WeaponType CurrentWeapon { get; private set; } = WeaponType.Broadsword;

    private Rigidbody2D rb;
    private float moveInput;
    private float externalMoveInput;
    private bool isGrounded;
    private int facingDirection = 1;
    private HitFlash hitFlash;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hitFlash = GetComponent<HitFlash>();
        var col = GetComponent<Collider2D>();
        if (col != null && col.sharedMaterial == null)
        {
            var noFriction = new PhysicsMaterial2D("PlayerFrictionless") { friction = 0f, bounciness = 0f };
            col.sharedMaterial = noFriction;
        }
        currentHearts = maxHearts;
        if (swordCollider != null) swordCollider.enabled = false;
        baseScale = new Vector3(2.53f, 2.53f, 1f);
        ResetScale();
        ApplyWeaponStats();
    }

    private void Update()
    {
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f) EndAttack();
        }

        if (autoAttackTimer > 0f)
        {
            autoAttackTimer -= Time.deltaTime;
        }
        else if (!IsAttacking && GameManager.Instance != null && GameManager.Instance.IsGameActive)
        {
            CheckAutoAttack();
        }

        float keyboardInput = ReadKeyboardInput();
        moveInput = Mathf.Abs(keyboardInput) > 0.05f ? keyboardInput : externalMoveInput;

        if (!IsAttacking)
        {
            if (moveInput > 0.05f && facingDirection != 1) Flip(1);
            else if (moveInput < -0.05f && facingDirection != -1) Flip(-1);
        }
    }

    private void CheckAutoAttack()
    {
        float range = (CurrentWeapon == WeaponType.Broadsword) ? autoAttackRangeBroadsword : autoAttackRangeDagger;
        float maxDy = (CurrentWeapon == WeaponType.Broadsword) ? 1.9f : 1.1f;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, range, autoAttackHits);
        for (int i = 0; i < count; i++)
        {
            var col = autoAttackHits[i];
            if (col != null && col.CompareTag("Enemy"))
            {
                float dx = col.transform.position.x - transform.position.x;
                float dy = Mathf.Abs(col.transform.position.y - transform.position.y);
                if (dy <= maxDy)
                {
                    bool inFrontOrClose = (dx * facingDirection >= -0.35f) || Mathf.Abs(dx) <= 0.8f;
                    if (inFrontOrClose)
                    {
                        Attack();
                        break;
                    }
                }
            }
        }
    }

    private float ReadKeyboardInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                Jump();

            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftShiftKey.wasPressedThisFrame)
                SwapWeapon();

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) return -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) return 1f;
            return 0f;
        }

        try
        {
            if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space))
                Jump();

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftShift))
                SwapWeapon();

            return Input.GetAxisRaw("Horizontal");
        }
        catch
        {
            return 0f;
        }
    }

    private void FixedUpdate()
    {
        bool wasGrounded = isGrounded;
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }
        else
        {
            isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.05f;
        }

        if (!wasGrounded && isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            transform.DOKill();
            ResetScale();
            transform.DOPunchScale(new Vector3(0.2f * baseScale.x, -0.2f * baseScale.y, 0f), 0.16f, 6, 0.5f).OnComplete(ResetScale);
            VFXManager.Instance?.SpawnLandingDust(transform.position);
        }

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
        }
        else
        {
            float speedMultiplier = (CurrentWeapon == WeaponType.Dagger) ? 1.25f : 1.0f;
            rb.linearVelocity = new Vector2(moveInput * baseSpeed * speedMultiplier, rb.linearVelocity.y);
        }
    }

    public void SetMoveInput(float dir)
    {
        externalMoveInput = dir;
        moveInput = dir;
    }

    public void Jump()
    {
        if (isGrounded)
        {
            float jumpMultiplier = (CurrentWeapon == WeaponType.Dagger) ? 1.15f : 1.0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, baseJumpForce * jumpMultiplier);
            transform.DOKill();
            ResetScale();
            transform.DOPunchScale(new Vector3(-0.15f * baseScale.x, 0.2f * baseScale.y, 0f), 0.25f, 5, 0.5f).OnComplete(ResetScale);
            GameManager.Instance?.PlaySound(GameManager.SoundType.Jump);
            VFXManager.Instance?.SpawnJumpDust(transform.position);
        }
    }

    private void Flip(int dir)
    {
        if (IsAttacking) return;
        facingDirection = dir;
        ResetScale();
    }

    private void ResetScale()
    {
        transform.localScale = new Vector3(baseScale.x * facingDirection, baseScale.y, baseScale.z);
    }

    public void SwapWeapon()
    {
        CurrentWeapon = (CurrentWeapon == WeaponType.Broadsword) ? WeaponType.Dagger : WeaponType.Broadsword;
        ApplyWeaponStats();
        
        transform.DOKill();
        ResetScale();
        transform.DOPunchScale(new Vector3(0.15f * baseScale.x, 0.15f * baseScale.y, 0f), 0.18f, 6, 0.5f).OnComplete(ResetScale);

        GameManager.Instance?.PlaySound(GameManager.SoundType.Swap);
        GameManager.Instance?.UpdateWeaponHUD(CurrentWeapon);
    }

    private void ApplyWeaponStats()
    {
        if (swordCollider != null)
        {
            if (CurrentWeapon == WeaponType.Broadsword)
            {
                swordCollider.size = new Vector2(0.55f, 0.75f);
                swordCollider.offset = new Vector2(0.42f, 0.20f);
            }
            else
            {
                swordCollider.size = new Vector2(0.45f, 0.25f);
                swordCollider.offset = new Vector2(0.32f, -0.05f);
            }
            swordCollider.enabled = IsAttacking;
        }

        UpdatePlayerSprite();
    }

    private void UpdatePlayerSprite()
    {
        if (bodyRenderer == null) return;
        if (IsAttacking)
        {
            Sprite attackSpr = (CurrentWeapon == WeaponType.Broadsword) ? broadswordSprite : daggerSprite;
            if (attackSpr != null) bodyRenderer.sprite = attackSpr;
        }
        else
        {
            Sprite idleSpr = (CurrentWeapon == WeaponType.Broadsword) ? broadswordIdleSprite : daggerIdleSprite;
            if (idleSpr != null) bodyRenderer.sprite = idleSpr;
            else
            {
                Sprite fallback = (CurrentWeapon == WeaponType.Broadsword) ? broadswordSprite : daggerSprite;
                if (fallback != null) bodyRenderer.sprite = fallback;
            }
        }
    }

    public void Attack()
    {
        attackTimer = attackDuration;
        autoAttackTimer = (CurrentWeapon == WeaponType.Broadsword) ? autoAttackCooldownBroadsword : autoAttackCooldownDagger;
        if (swordCollider != null) swordCollider.enabled = true;
        UpdatePlayerSprite();
        AnimateSlashThrust();
        GameManager.Instance?.PlaySound(GameManager.SoundType.Slash);
    }

    private void EndAttack()
    {
        attackTimer = 0f;
        if (swordCollider != null) swordCollider.enabled = false;
        UpdatePlayerSprite();
        ResetScale();
    }

    public void AnimateSlashThrust()
    {
        transform.DOKill();
        ResetScale();
        if (CurrentWeapon == WeaponType.Broadsword)
        {
            transform.DOPunchPosition(new Vector3(0.18f * facingDirection, -0.03f, 0f), 0.14f, 6, 0.5f);
        }
        else
        {
            transform.DOPunchPosition(new Vector3(0.24f * facingDirection, 0f, 0f), 0.09f, 8, 0.6f);
        }
        VFXManager.Instance?.SpawnSlashArc(transform.position, CurrentWeapon == WeaponType.Broadsword, facingDirection);
    }

    public void TakeDamage(int amount, Vector2 enemyPosition)
    {
        if (isInvulnerable || currentHearts <= 0) return;

        currentHearts = Mathf.Max(0, currentHearts - amount);
        GameManager.Instance?.UpdateHeartsHUD(currentHearts);
        GameManager.Instance?.ResetCombo();
        GameManager.Instance?.PlaySound(GameManager.SoundType.Hurt);
        
        GameManager.Instance?.TriggerScreenShake(0.3f, 0.35f);
        transform.DOKill();
        ResetScale();
        transform.DOPunchScale(new Vector3(0.25f * baseScale.x, -0.2f * baseScale.y, 0f), 0.25f, 8, 0.5f).OnComplete(ResetScale);

        hitFlash?.Flash(0.14f);

        knockbackTimer = 0.18f;
        Vector2 knockbackDir = (transform.position.x > enemyPosition.x) ? Vector2.right : Vector2.left;
        rb.linearVelocity = new Vector2(knockbackDir.x * 5f, 5.5f);

        if (currentHearts <= 0) GameManager.Instance?.GameOver();
        else StartCoroutine(InvulnerabilityRoutine());
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(0.14f);
        float elapsed = 0.14f;
        Color normalColor = Color.white;
        Color ghostColor = new Color(1f, 1f, 1f, 0.35f);
        bool isGhost = true;
        while (elapsed < 1.2f)
        {
            if (bodyRenderer != null) bodyRenderer.color = isGhost ? ghostColor : normalColor;
            isGhost = !isGhost;
            yield return new WaitForSeconds(0.08f);
            elapsed += 0.08f;
        }
        if (bodyRenderer != null)
        {
            bodyRenderer.color = normalColor;
            bodyRenderer.enabled = true;
        }
        isInvulnerable = false;
    }

    public int GetCoinBonus() => (CurrentWeapon == WeaponType.Dagger) ? 1 : 0;

    public void ResetHealth()
    {
        transform.DOKill();
        ResetScale();
        currentHearts = maxHearts;
        isInvulnerable = false;
        if (bodyRenderer != null)
        {
            bodyRenderer.color = Color.white;
            bodyRenderer.enabled = true;
        }
    }

    private void OnDestroy() => transform.DOKill();
}
