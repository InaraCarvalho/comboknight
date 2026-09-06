using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GameplayBot : MonoBehaviour
{
    public static GameplayBot Instance { get; private set; }

    private static bool isBotActive = false;
    public bool IsBotActive => isBotActive;

    private PlayerController player;
    private GameManager gm;
    private VirtualButton btnLeft;
    private VirtualButton btnRight;
    private VirtualButton btnJump;
    private VirtualButton btnSwap;

    private TextMeshProUGUI statusBadgeText;
    private RectTransform statusBadgeRt;

    private int currentMoveDir = 0;
    private float attackCooldown = 0f;
    private float swapCooldown = 0f;
    private float jumpCooldown = 0f;
    private float retryTimer = 0f;

    private void Awake()
    {
#if !UNITY_EDITOR
        Destroy(gameObject);
#else
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
#endif
    }

    private void Start()
    {
        CacheReferences();
        CreateStatusBadge();
        UpdateStatusBadge();
    }

    private void CacheReferences()
    {
        player = FindAnyObjectByType<PlayerController>();
        gm = GameManager.Instance;

        var vbs = FindObjectsByType<VirtualButton>(FindObjectsSortMode.None);
        foreach (var vb in vbs)
        {
            if (vb.Action == VirtualButton.ButtonAction.Left) btnLeft = vb;
            else if (vb.Action == VirtualButton.ButtonAction.Right) btnRight = vb;
            else if (vb.Action == VirtualButton.ButtonAction.Jump) btnJump = vb;
            else if (vb.Action == VirtualButton.ButtonAction.Swap) btnSwap = vb;
        }
    }

    private void CreateStatusBadge()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject badgeObj = new GameObject("BotStatusBadge");
        badgeObj.transform.SetParent(canvas.transform, false);
        statusBadgeRt = badgeObj.AddComponent<RectTransform>();
        statusBadgeRt.anchorMin = new Vector2(0.5f, 1f);
        statusBadgeRt.anchorMax = new Vector2(0.5f, 1f);
        statusBadgeRt.pivot = new Vector2(0.5f, 1f);
        statusBadgeRt.anchoredPosition = new Vector2(0f, -54f);
        statusBadgeRt.sizeDelta = new Vector2(340f, 32f);

        statusBadgeText = badgeObj.AddComponent<TextMeshProUGUI>();
        statusBadgeText.alignment = TextAlignmentOptions.Center;
        statusBadgeText.fontSize = 19f;
        statusBadgeText.characterSpacing = 1.5f;
        statusBadgeText.fontStyle = FontStyles.Bold;
        statusBadgeText.raycastTarget = false;
    }

    private void Update()
    {
        bool f9Pressed = false;
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            f9Pressed = true;
        }
        else
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F9)) f9Pressed = true;
            }
            catch {}
        }

        if (f9Pressed)
        {
            ToggleBot();
        }

        if (!isBotActive) return;

        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;
        if (swapCooldown > 0f) swapCooldown -= Time.deltaTime;
        if (jumpCooldown > 0f) jumpCooldown -= Time.deltaTime;

        HandleModals();

        if (gm == null || !gm.IsGameActive) return;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        ExecuteMasterCombat();
    }

    public void ToggleBot()
    {
        isBotActive = !isBotActive;
        if (!isBotActive)
        {
            ReleaseMovement();
        }
        else
        {
            CacheReferences();
        }

        UpdateStatusBadge();
        gm?.PlaySound(GameManager.SoundType.Swap);

        if (isBotActive && statusBadgeRt != null)
        {
            statusBadgeRt.DOKill();
            statusBadgeRt.localScale = Vector3.one;
            statusBadgeRt.DOPunchScale(new Vector3(0.25f, 0.25f, 0f), 0.25f, 6, 0.5f);
        }
    }

    private void UpdateStatusBadge()
    {
        if (statusBadgeText == null) return;
        statusBadgeText.gameObject.SetActive(isBotActive);
        if (isBotActive)
        {
            statusBadgeText.text = "<color=#22C55E><b>[F9] BOT LIGADO (AUTO-PILOT)</b></color>";
            statusBadgeText.color = Color.white;
        }
    }

    private void HandleModals()
    {
        var startPanel = GameObject.Find("StartPanel");
        if (startPanel != null && startPanel.activeInHierarchy)
        {
            var btn = startPanel.GetComponentInChildren<Button>();
            if (btn != null) btn.onClick.Invoke();
            else gm?.StartGame();
            return;
        }

        var pausePanel = GameObject.Find("PausePanel");
        if (pausePanel != null && pausePanel.activeInHierarchy)
        {
            gm?.TogglePause();
            return;
        }

        var gameOverPanel = GameObject.Find("GameOverPanel");
        if (gameOverPanel != null && gameOverPanel.activeInHierarchy)
        {
            retryTimer += Time.unscaledDeltaTime;
            if (retryTimer >= 1.2f)
            {
                retryTimer = 0f;
                var retryBtn = gameOverPanel.GetComponentInChildren<Button>();
                if (retryBtn != null) retryBtn.onClick.Invoke();
                else gm?.RestartGame();
            }
            return;
        }
        else
        {
            retryTimer = 0f;
        }
    }

    private void ExecuteMasterCombat()
    {
        Vector3 playerPos = player.transform.position;

        SlimeEnemy nearestSlime = null;
        float minSlimeDist = float.MaxValue;
        float slimeDx = 0f;
        var slimes = FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None);
        int slimesLeft = 0;
        int slimesRight = 0;

        foreach (var s in slimes)
        {
            if (s == null) continue;
            float dx = s.transform.position.x - playerPos.x;
            float dist = Mathf.Abs(dx);
            if (dx < 0 && dist < 3.5f) slimesLeft++;
            else if (dx > 0 && dist < 3.5f) slimesRight++;

            if (dist < minSlimeDist)
            {
                minSlimeDist = dist;
                slimeDx = dx;
                nearestSlime = s;
            }
        }

        BatEnemy nearestBat = null;
        float minBatDist = float.MaxValue;
        float batDx = 0f;
        float batDy = 0f;
        var bats = FindObjectsByType<BatEnemy>(FindObjectsSortMode.None);

        foreach (var b in bats)
        {
            if (b == null) continue;
            float dx = b.transform.position.x - playerPos.x;
            float dy = b.transform.position.y - playerPos.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < minBatDist)
            {
                minBatDist = dist;
                batDx = dx;
                batDy = dy;
                nearestBat = b;
            }
        }

        Coin nearestCoin = null;
        float minCoinDist = float.MaxValue;
        var coins = FindObjectsByType<Coin>(FindObjectsSortMode.None);
        foreach (var c in coins)
        {
            if (c == null) continue;
            float dist = Vector2.Distance(playerPos, c.transform.position);
            if (dist < minCoinDist)
            {
                minCoinDist = dist;
                nearestCoin = c;
            }
        }

        if (player.CurrentWeapon != PlayerController.WeaponType.Broadsword && swapCooldown <= 0f)
        {
            TriggerSwap();
            swapCooldown = 0.8f;
        }

        bool pincerTrap = (slimesLeft > 0 && slimesRight > 0 && minSlimeDist < 2.0f);
        bool nearWallTrap = (Mathf.Abs(playerPos.x) > 2.3f && ((playerPos.x > 0 && slimeDx < 0) || (playerPos.x < 0 && slimeDx > 0)) && minSlimeDist < 2.2f);

        if (pincerTrap || nearWallTrap)
        {
            int centerDir = (playerPos.x > 0) ? -1 : 1;
            ApplyMove(centerDir);
            TriggerJump();
            TriggerAttack();
            return;
        }

        if (nearestBat != null && minBatDist < 3.8f)
        {
            float horizontalDist = Mathf.Abs(batDx);
            int faceBatDir = (batDx > 0) ? 1 : -1;

            if (horizontalDist > 0.6f)
            {
                ApplyMove(faceBatDir);
            }
            else
            {
                ApplyMove(0);
            }

            if (batDy > 0.45f && horizontalDist < 2.2f)
            {
                TriggerJump();
            }

            if (horizontalDist < 1.7f && Mathf.Abs(batDy) < 1.5f && attackCooldown <= 0f)
            {
                TriggerAttack();
                attackCooldown = 0.16f;
            }

            return;
        }

        if (nearestSlime != null)
        {
            int faceSlimeDir = (slimeDx > 0) ? 1 : -1;

            if (minSlimeDist > 1.25f)
            {
                ApplyMove(faceSlimeDir);
            }
            else if (minSlimeDist < 0.75f)
            {
                ApplyMove(-faceSlimeDir);
            }
            else
            {
                ApplyMove(0);
            }

            if (minSlimeDist <= 1.55f && attackCooldown <= 0f)
            {
                TriggerAttack();
                attackCooldown = 0.16f;
            }

            return;
        }

        if (nearestCoin != null && minCoinDist < 4.0f)
        {
            float cdx = nearestCoin.transform.position.x - playerPos.x;
            int coinDir = (cdx > 0.15f) ? 1 : ((cdx < -0.15f) ? -1 : 0);
            ApplyMove(coinDir);

            if (nearestCoin.transform.position.y > playerPos.y + 0.8f && Mathf.Abs(cdx) < 1.0f)
            {
                TriggerJump();
            }
            return;
        }

        if (Mathf.Abs(playerPos.x) > 0.5f)
        {
            int centerDir = (playerPos.x > 0) ? -1 : 1;
            ApplyMove(centerDir);
        }
        else
        {
            ApplyMove(0);
        }
    }

    private void ApplyMove(int dir)
    {
        if (dir == currentMoveDir) return;

        if (currentMoveDir == -1)
        {
            if (btnLeft != null) btnLeft.Release();
            else player.SetMoveInput(0f);
        }
        else if (currentMoveDir == 1)
        {
            if (btnRight != null) btnRight.Release();
            else player.SetMoveInput(0f);
        }

        currentMoveDir = dir;

        if (dir == -1)
        {
            if (btnLeft != null) btnLeft.Press();
            else player.SetMoveInput(-1f);
        }
        else if (dir == 1)
        {
            if (btnRight != null) btnRight.Press();
            else player.SetMoveInput(1f);
        }
        else
        {
            player.SetMoveInput(0f);
        }
    }

    private void ReleaseMovement()
    {
        if (btnLeft != null) btnLeft.Release();
        if (btnRight != null) btnRight.Release();
        if (player != null) player.SetMoveInput(0f);
        currentMoveDir = 0;
    }

    private void TriggerJump()
    {
        if (jumpCooldown > 0f) return;
        jumpCooldown = 0.35f;

        if (btnJump != null)
        {
            btnJump.Press();
            btnJump.Release();
        }
        else
        {
            player.Jump();
        }
    }

    private void TriggerSwap()
    {
        if (btnSwap != null)
        {
            btnSwap.Press();
            btnSwap.Release();
        }
        else
        {
            player.SwapWeapon();
        }
    }

    private void TriggerAttack()
    {
        player?.Attack();
    }

    private void OnDestroy()
    {
        ReleaseMovement();
    }
}
