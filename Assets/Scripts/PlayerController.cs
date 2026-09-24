using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 5.0f;
    // Adaga Veloz e a unica arma acima da velocidade base (ETAPA 2).
    [SerializeField] private float daggerSpeed = 6.6f;
    // Velocidade efetiva da arma atual (aplicada no movimento), calculada em
    // ApplyWeaponStats.
    private float weaponSpeed;
    [SerializeField] private float baseJumpForce = 13.5f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float coyoteTime = 0.12f;
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
    private float currentHearts;
    public float CurrentHearts => currentHearts;
    public int MaxHearts => maxHearts;
    private bool isInvulnerable;
    public bool IsInvulnerable => isInvulnerable;
    private float knockbackTimer;
    private float attackTimer;
    public bool IsAttacking => attackTimer > 0f;
    // Armadura comprada no mercador (ETAPA 6): barra de sobrevida azul de
    // 100 pontos que absorve golpes (-25 por hit) ANTES de tocar no HP;
    // nao regenera sozinha; so recarrega comprando de novo no mercador.
    public float MaxArmor { get; private set; } = 100f;
    public float CurrentArmor { get; private set; } = 0f;
    public bool HasArmor => CurrentArmor > 0f;
    // Apos comprar uma arma no mercador, o jogador fica preso nela (nao troca).
    private bool weaponLocked;

    [SerializeField] private Vector3 baseScale = new Vector3(2.53f, 2.53f, 1f);
    [SerializeField] private float autoAttackRangeBroadsword = 1.9f;
    [SerializeField] private float autoAttackRangeDagger = 1.5f;
    [SerializeField] private float autoAttackCooldownBroadsword = 0.28f;
    [SerializeField] private float autoAttackCooldownDagger = 0.18f;
    // (Removido) deteccao de alvo + cooldown do auto-ataque: a espada agora e
    // ESTATICA e mata por contato, nao precisa mais mirar nem atacar de novo.
    // private float autoAttackTimer;
    // private readonly Collider2D[] autoAttackHits = new Collider2D[8];
    // private Collider2D lockedAutoTarget;

    // Armas do conceito (ETAPA 2), na ordem do ciclo de troca (E/Q): Espada
    // Padrao (arma inicial) -> Adaga Veloz -> Lamina Real -> Arma Ritmica ->
    // Espada & Escudo -> volta para a Padrao.
    public enum WeaponType { Standard, Dagger, Broadsword, Rhythmic, ShieldSword }
    public WeaponType CurrentWeapon { get; private set; } = WeaponType.Standard;

    private Rigidbody2D rb;
    private float moveInput;
    private float externalMoveInput;
    private bool isGrounded;
    // Contato lateral com as paredes da arena (auto-run para de verdade ao
    // encostar, sem ficar "empurrando" o collider a cada frame).
    private bool contactWallLeft;
    private bool contactWallRight;
    private float jumpBufferTimer;
    private float coyoteTimer;
    private int facingDirection = 1;
    public int FacingDirection => facingDirection;
    // Movimento contínuo automático com direção controlada pelo jogador: o
    // cavaleiro anda sozinho na última direção escolhida e PARA ao encostar na
    // parede. Não vira sozinho — a direção só muda quando o jogador aperta o
    // botão/tecla do lado contrário.
    [SerializeField] private float autoTurnX = 6.5f;
    private int autoDirection = 1;
    // Auto-run fica desligado quando o auto-pilot (F9) assume o controle, pois
    // ele precisa de input e parada exatos para as rotinas de QA.
    private bool autoRunEnabled = true;
    private HitFlash hitFlash;
    // Throttle do feedback visual do bloqueio melee frontal (evita spam do
    // texto "BLOQUEADO" a cada frame enquanto o inimigo permanece em contato).
    private float lastBlockFeedback;

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
        if (swordCollider != null) swordCollider.enabled = true;

        var hurtBoxCollider = gameObject.AddComponent<CircleCollider2D>();
        hurtBoxCollider.isTrigger = true;
        hurtBoxCollider.radius = 0.35f;

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

        // (Removido) Auto-ataque com cooldown + re-selecao de alvo fazia o
        // cavaleiro soltar 2-3 golpes antes de acertar. Agora a espada fica
        // SEMPRE ativa (estatica) e qualquer inimigo que encosta nela morre.
        // if (autoAttackTimer > 0f)
        // {
        //     autoAttackTimer -= Time.deltaTime;
        // }
        // else if (!IsAttacking && GameManager.Instance != null && GameManager.Instance.IsGameActive)
        // {
        //     CheckAutoAttack();
        // }

        float keyboardInput = ReadKeyboardInput();
        if (Mathf.Abs(keyboardInput) > 0.05f)
        {
            moveInput = keyboardInput;
            if (Mathf.Abs(moveInput) > 0.05f) autoDirection = (moveInput > 0f) ? 1 : -1;
        }
        else if (Mathf.Abs(externalMoveInput) > 0.05f)
        {
            moveInput = externalMoveInput;
            if (Mathf.Abs(moveInput) > 0.05f) autoDirection = (moveInput > 0f) ? 1 : -1;
        }
        else
        {
            moveInput = autoRunEnabled ? autoDirection : 0f;
        }

        if (!IsAttacking)
        {
            if (moveInput > 0.05f && facingDirection != 1) Flip(1);
            else if (moveInput < -0.05f && facingDirection != -1) Flip(-1);
        }
    }

    // Arma ESTATICA com dano por contato: o cavaleiro so recebe dano quando o
    // inimigo toca o CORPO (hurtbox) e NAO a espada. Inimigo encostando na
    // espada morre (OnTriggerEnter no proprio inimigo, tag "Sword") e nunca
    // fere nem empurra o cavaleiro.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (swordCollider != null && swordCollider.IsTouching(other)) return;
        if (TryBlockMelee(other.transform.position)) return;
        TakeDamage(1, other.transform.position);
    }

    // ===== Protecao frontal (ETAPA 2): ataque corpo a corpo pela frente =====
    // O cavaleiro ENFRENTA os inimigos quando anda; golpe vindo do lado em que
    // ele olha (frente) nunca o fere em nenhuma arma. Por tras continua ferindo
    // normal. Com a Espada & Escudo o bloqueio ganha feedback visual ("BLOQUEADO");
    // nas demais armas o golpe apenas e anulado, sem spam por frame.
    private bool TryBlockMelee(Vector2 attackerPos)
    {
        float dx = attackerPos.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.001f) return true; // frontal puro
        if ((dx > 0f) != (facingDirection > 0)) return false;

        if (HasShield && Time.time - lastBlockFeedback > 0.4f)
        {
            lastBlockFeedback = Time.time;
            VFXManager.Instance?.SpawnHitSpark(transform.position + Vector3.up * 0.5f);
            GameManager.Instance?.FloatText(transform.position + Vector3.up * 1.2f,
                "BLOQUEADO", new Color(0.6f, 0.88f, 1f), 3.8f);
            GameManager.Instance?.TriggerScreenShake(0.15f, 0.18f);
            GameManager.Instance?.PlaySound(GameManager.SoundType.Coin, 1.3f);
        }
        return true;
    }

    // ===== CODIGO ANTIGO COMENTADO: auto-ataque com trava de alvo =====
    // private void CheckAutoAttack()
    // {
    //     float range = (CurrentWeapon == WeaponType.Broadsword) ? autoAttackRangeBroadsword : autoAttackRangeDagger;
    //     float maxDy = (CurrentWeapon == WeaponType.Broadsword) ? 1.9f : 1.1f;
    //     int count = Physics2D.OverlapCircleNonAlloc(transform.position, range, autoAttackHits);
    //     if (count == 0)
    //     {
    //         lockedAutoTarget = null;
    //         return;
    //     }
    //
    //     Collider2D nearest = null;
    //     float nearestDist = float.MaxValue;
    //     bool targetStillInRange = false;
    //
    //     for (int i = 0; i < count; i++)
    //     {
    //         var col = autoAttackHits[i];
    //         if (col == null || !col.CompareTag("Enemy")) continue;
    //         float dx = col.transform.position.x - transform.position.x;
    //         float dy = Mathf.Abs(col.transform.position.y - transform.position.y);
    //         if (dy > maxDy) continue;
    //         bool inFrontOrClose = (dx * facingDirection >= -0.35f) || Mathf.Abs(dx) <= 0.8f;
    //         if (!inFrontOrClose) continue;
    //
    //         if (lockedAutoTarget != null && col == lockedAutoTarget) targetStillInRange = true;
    //
    //         float dist = dx * dx + dy * dy;
    //         if (dist < nearestDist)
    //         {
    //             nearestDist = dist;
    //             nearest = col;
    //         }
    //     }
    //
    //     if (targetStillInRange) return;
    //
    //     if (nearest != null)
    //     {
    //         lockedAutoTarget = nearest;
    //         Attack();
    //     }
    //     else
    //     {
    //         lockedAutoTarget = null;
    //     }
    // }
    //
    // private void CheckAutoAttackOld()
    // {
    //     float range = (CurrentWeapon == WeaponType.Broadsword) ? autoAttackRangeBroadsword : autoAttackRangeDagger;
    //     float maxDy = (CurrentWeapon == WeaponType.Broadsword) ? 1.9f : 1.1f;
    //     int count = Physics2D.OverlapCircleNonAlloc(transform.position, range, autoAttackHits);
    //     for (int i = 0; i < count; i++)
    //     {
    //         var col = autoAttackHits[i];
    //         if (col != null && col.CompareTag("Enemy"))
    //         {
    //             float dx = col.transform.position.x - transform.position.x;
    //             float dy = Mathf.Abs(col.transform.position.y - transform.position.y);
    //             if (dy <= maxDy)
    //             {
    //                 bool inFrontOrClose = (dx * facingDirection >= -0.35f) || Mathf.Abs(dx) <= 0.8f;
    //                 if (inFrontOrClose)
    //                 {
    //                     Attack();
    //                     break;
    //                 }
    //             }
    //         }
    //     }
    // }

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

        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);

        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.fixedDeltaTime;
            if (coyoteTimer > 0f) DoJump();
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
            float vx = moveInput * weaponSpeed;
            if ((moveInput > 0.05f && contactWallRight) || (moveInput < -0.05f && contactWallLeft))
                vx = 0f;
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        }
    }

    public void SetMoveInput(float dir)
    {
        externalMoveInput = dir;
        moveInput = dir;
    }

    // Auto-run (player anda sozinho na ultima direcao sem segurar o botao)
    // fica desligado enquanto o auto-pilot de QA toma o controle.
    public void SetAutoRun(bool enabled)
    {
        autoRunEnabled = enabled;
        if (!enabled) externalMoveInput = 0f;
    }

    // Limites da arena enviados pela ResponsiveCamera: campo mantido por
    // compatibilidade com o ResponsiveLayoutValidator (que confere se o valor
    // acompanha a meia-largura visivel da tela). O cavaleiro NAO usa mais este
    // valor para virar sozinho — a direcao agora e exclusiva do jogador.
    public void SetArenaBounds(float halfWidth)
    {
        autoTurnX = halfWidth - 0.6f;
    }

    // ===== Espada & Escudo: anula projeteis frontais (ETAPA 2) =====
    public bool HasShield => CurrentWeapon == WeaponType.ShieldSword;

    // Projeto que chega pela FRENTE do cavaleiro (mesmo lado em que ele olha)
    // e anulado enquanto a Espada & Escudo estiver ativa; por tras nao ha
    // protecao. Chamado pelo script do projetil antes de aplicar dano, ou ja
    // tratado aqui na hurtbox pelo callback com a tag "Projectile".
    public bool TryBlockProjectile(Vector2 projectilePosition)
    {
        if (!HasShield) return false;
        float dx = projectilePosition.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.001f) return true; // frontal puro
        return (dx > 0f) == (facingDirection > 0);
    }

    // Hurtbox (trigger do corpo): o script Projectile e quem resolve dano e
    // bloqueio (texto "BLOQUEADO" + VFX unicos). Aqui fica apenas o fallback
    // para projetis sem o script, caso aparecam no futuro.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Projectile")) return;
        if (other.GetComponent<Projectile>() != null) return;
        if (!TryBlockProjectile(other.bounds.center)) return;
        Destroy(other.gameObject);
    }

    // Caminho para projeteis com colisor solido (nao-trigger), mesma regra.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D other = collision.collider;
        if (other == null) return;
        UpdateWallContact(other, true);
        if (!other.CompareTag("Projectile")) return;
        if (other.GetComponent<Projectile>() != null) return;
        if (!TryBlockProjectile(other.bounds.center)) return;
        Destroy(other.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider != null) UpdateWallContact(collision.collider, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider != null) UpdateWallContact(collision.collider, false);
    }

    // Paredes da arena sao colisores solidos nomeados WallLeft/WallRight.
    // Como o collider do corpo bate nelas, usamos a posicao relativa para
    // marcar qual lado esta encostado e entao zerar a velocidade (auto-run
    // nao empurra a parede). Colisao com o chao/inimigos nao conta.
    private void UpdateWallContact(Collider2D other, bool active)
    {
        if (other.transform == null) return;
        string n = other.name;
        bool isLeftWall = n == "WallLeft" || n.StartsWith("WallLeft", System.StringComparison.Ordinal);
        bool isRightWall = n == "WallRight" || n.StartsWith("WallRight", System.StringComparison.Ordinal);
        if (isRightWall) contactWallRight = active;
        else if (isLeftWall) contactWallLeft = active;
    }

    public void SetSwordActive(bool active)
    {
        if (swordCollider != null) swordCollider.enabled = active;
    }

    public void Jump()
    {
        jumpBufferTimer = jumpBufferTime;
        if (isGrounded || coyoteTimer > 0f) DoJump();
    }

    private void DoJump()
    {
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        float jumpMultiplier = (CurrentWeapon == WeaponType.Dagger) ? 1.15f : 1.0f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, baseJumpForce * jumpMultiplier);
        transform.DOKill();
        ResetScale();
        transform.DOPunchScale(new Vector3(-0.15f * baseScale.x, 0.2f * baseScale.y, 0f), 0.25f, 5, 0.5f).OnComplete(ResetScale);
        GameManager.Instance?.PlaySound(GameManager.SoundType.Jump);
        VFXManager.Instance?.SpawnJumpDust(transform.position);
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
        // (Mercador) depois de comprar uma arma o jogador fica com ela travada.
        if (weaponLocked) return;
        CurrentWeapon = GetNextWeapon(CurrentWeapon);
        ApplyWeaponStats();
        // lockedAutoTarget = null; // (removido junto com o auto-ataque)

        transform.DOKill();
        ResetScale();
        transform.DOPunchScale(new Vector3(0.15f * baseScale.x, 0.15f * baseScale.y, 0f), 0.18f, 6, 0.5f).OnComplete(ResetScale);

        GameManager.Instance?.PlaySound(GameManager.SoundType.Swap);
        GameManager.Instance?.UpdateWeaponHUD(CurrentWeapon);
    }

    // Ciclo das 5 armas (ETAPA 2): Padrao -> Adaga -> Lamina Real -> Ritmica
    // -> Escudo -> Padrao ...
    public static WeaponType GetNextWeapon(WeaponType current)
    {
        switch (current)
        {
            case WeaponType.Standard: return WeaponType.Dagger;
            case WeaponType.Dagger: return WeaponType.Broadsword;
            case WeaponType.Broadsword: return WeaponType.Rhythmic;
            case WeaponType.Rhythmic: return WeaponType.ShieldSword;
            default: return WeaponType.Standard;
        }
    }

    private void ApplyWeaponStats()
    {
        // Velocidade (ETAPA 2): base 5.0 para Standard, Lamina Real, Ritmica e
        // Escudo; 6.6 para a Adaga Veloz.
        weaponSpeed = (CurrentWeapon == WeaponType.Dagger) ? daggerSpeed : baseSpeed;

        if (swordCollider != null)
        {
            switch (CurrentWeapon)
            {
                case WeaponType.Dagger: // Adaga Veloz: hitbox curta (0.35 x 0.25)
                    swordCollider.size = new Vector2(0.35f, 0.25f);
                    swordCollider.offset = new Vector2(0.32f, -0.05f);
                    break;
                case WeaponType.Broadsword: // Lamina Real: hitbox longa (0.70 x 0.85)
                    swordCollider.size = new Vector2(0.70f, 0.85f);
                    swordCollider.offset = new Vector2(0.48f, 0.25f);
                    break;
                default: // Padrao, Ritmica e Escudo: hitbox padrao (0.50 x 0.60)
                    swordCollider.size = new Vector2(0.50f, 0.60f);
                    swordCollider.offset = new Vector2(0.40f, 0.15f);
                    break;
            }
            swordCollider.enabled = true;
        }

        // Arma Ritmica avisa o GameManager para estender a janela de combo
        // (4.5s); as demais voltam ao padrao (3.2s). A protecao da Espada &
        // Escudo nao precisa de estado extra: vale enquanto CurrentWeapon for
        // ShieldSword (ver TryBlockProjectile).
        ApplyComboDurationForCurrentWeapon();

        UpdatePlayerSprite();
    }

    private void ApplyComboDurationForCurrentWeapon()
    {
        // GameManager.Instance so existe em runtime: o fallback por busca
        // mantem a duracao correta tambem quando o PlayerController acorda
        // ANTES do GameManager (ordem de execucao dos Awakes) e na validacao
        // em modo edicao (batch), onde o singleton ainda nao foi criado.
        var gm = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();
        gm?.SetComboDuration(CurrentWeapon == WeaponType.Rhythmic
            ? GameManager.ExtendedComboDuration
            : GameManager.DefaultComboDuration);
    }

    private void UpdatePlayerSprite()
    {
        if (bodyRenderer == null) return;
        // Arma ESTATICA: o cavaleiro fica sempre no sprite com a espada visivel/
        // estendida (independente de atacar). Antes alternava para o sprite de
        // ataque so durante o golpe e na idlea a espada sumia.
        // So a Adaga tem sprite proprio; as outras 4 armas usam a espada
        // (nao ha artes separadas para padrao/ritmica/escudo).
        Sprite weaponSpr = (CurrentWeapon == WeaponType.Dagger) ? daggerSprite : broadswordSprite;
        if (weaponSpr != null) bodyRenderer.sprite = weaponSpr;

        // Antigo (espada visivel somente durante o ataque):
        // if (IsAttacking)
        // {
        //     Sprite attackSpr = (CurrentWeapon == WeaponType.Broadsword) ? broadswordSprite : daggerSprite;
        //     if (attackSpr != null) bodyRenderer.sprite = attackSpr;
        // }
        // else
        // {
        //     Sprite idleSpr = (CurrentWeapon == WeaponType.Broadsword) ? broadswordIdleSprite : daggerIdleSprite;
        //     if (idleSpr != null) bodyRenderer.sprite = idleSpr;
        //     else
        //     {
        //         Sprite fallback = (CurrentWeapon == WeaponType.Broadsword) ? broadswordSprite : daggerSprite;
        //         if (fallback != null) bodyRenderer.sprite = fallback;
        //     }
        // }
    }

    public void Attack()
    {
        attackTimer = attackDuration;
        // (Removido) agendava a proxima verificacao do auto-ataque:
        // autoAttackTimer = (CurrentWeapon == WeaponType.Broadsword) ? autoAttackCooldownBroadsword : autoAttackCooldownDagger;
        VFXManager.Instance?.SpawnSlashArc(transform.position, CurrentWeapon != WeaponType.Dagger, facingDirection);
        GameManager.Instance?.PlaySound(GameManager.SoundType.Slash);
        AnimateSlashThrust();
        UpdatePlayerSprite();
    }

    private void EndAttack()
    {
        attackTimer = 0f;
        // (Removido) espada nao precisa mais ser desligada: fica sempre ativa.
        // if (swordCollider != null) swordCollider.enabled = false;
        UpdatePlayerSprite();
        ResetScale();
    }

    public void AnimateSlashThrust()
    {
        transform.DOKill();
        ResetScale();
        // (Removido) estocada de posicao empurrava o cavaleiro contra os inimigos.
        // agora e apenas um "punch" de escala, sem deslocamento fisico.
        transform.DOPunchScale(new Vector3(0.05f * baseScale.x, -0.05f * baseScale.y, 0f), 0.12f, 6, 0.5f);
        // Antigo (causava o empurrao):
        // if (CurrentWeapon == WeaponType.Broadsword)
        // {
        //     transform.DOPunchPosition(new Vector3(0.18f * facingDirection, -0.03f, 0f), 0.14f, 6, 0.5f);
        // }
        // else
        // {
        //     transform.DOPunchPosition(new Vector3(0.24f * facingDirection, 0f, 0f), 0.09f, 8, 0.6f);
        // }
    }

    public void TakeDamage(int damage, Vector3 attackerPos)
    {
        if (isInvulnerable || currentHearts <= 0) return;

        // Efeitos comuns: o golpe sempre sacode, pisca e quebra o combo, porque
        // a armadura so poupa o HP (nao evita o impacto).
        GameManager.Instance?.ResetCombo();
        GameManager.Instance?.PlaySound(GameManager.SoundType.Hurt);

        GameManager.Instance?.TriggerScreenShake(0.3f, 0.35f);
        GameManager.VibrateOnce();
        transform.DOKill();
        ResetScale();
        transform.DOPunchScale(new Vector3(0.25f * baseScale.x, -0.2f * baseScale.y, 0f), 0.25f, 8, 0.5f).OnComplete(ResetScale);

        hitFlash?.Flash(0.14f);

        knockbackTimer = 0.12f;
        Vector2 knockbackDir = (transform.position.x > attackerPos.x) ? Vector2.right : Vector2.left;
        rb.linearVelocity = new Vector2(knockbackDir.x * 2.8f, 4.0f);

        // Armadura (ETAPA 6): enquanto durar, absorve o golpe INTEIRO com -25
        // de sobrevida azul (popup "ESCUDO -25") e o HP verde nao é tocado.
        if (CurrentArmor > 0f)
        {
            CurrentArmor = Mathf.Max(0f, CurrentArmor - 25f);
            GameManager.Instance?.FloatText(transform.position + Vector3.up * 1.2f,
                "ESCUDO -25", new Color(0.302f, 0.651f, 1f), 4.2f);
            GameManager.Instance?.UpdateArmorHUD(CurrentArmor, MaxArmor);
            StartCoroutine(InvulnerabilityRoutine());
            return;
        }

        currentHearts = Mathf.Max(0, currentHearts - damage);
        GameManager.Instance?.UpdateHeartsHUD(currentHearts);

        if (currentHearts <= 0) GameManager.Instance?.GameOver();
        else StartCoroutine(InvulnerabilityRoutine());
    }

    // Cura por PORCENTAGEM do HP maximo: moedas coletadas (4%-10%) e poco
    // do mercador (50%).
    public void HealFraction(float fraction)
    {
        if (currentHearts <= 0f || fraction <= 0f) return;
        currentHearts = Mathf.Min(maxHearts, currentHearts + maxHearts * fraction);
        GameManager.Instance?.UpdateHeartsHUD(currentHearts);
    }

    // Armadura do mercador: recarrega a barra azul ao maximo (100). Comprar
    // de novo quando ja tem armadura apenas restaura (nao empilha).
    public void GrantArmor()
    {
        CurrentArmor = MaxArmor;
        GameManager.Instance?.UpdateArmorHUD(CurrentArmor, MaxArmor);
    }

    // Arma comprada no mercador: fixa o jogador nela e trava a troca (E/Q).
    public void SetWeapon(WeaponType weapon)
    {
        weaponLocked = true;
        CurrentWeapon = weapon;
        ApplyWeaponStats();
        GameManager.Instance?.UpdateWeaponHUD(CurrentWeapon);
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
        // lockedAutoTarget = null; // (removido junto com o auto-ataque)
        if (bodyRenderer != null)
        {
            bodyRenderer.color = Color.white;
            bodyRenderer.enabled = true;
        }
    }

    private void OnDestroy() => transform.DOKill();
}
