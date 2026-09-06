using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class MobileTouchController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private float swipeThreshold = 50f;
    [SerializeField] private float doubleTapMaxTime = 0.35f;
    [SerializeField] private float doubleTapMaxDist = 90f;

    private bool isPointerHeld;
    private Vector2 touchStartPos;
    private float lastTapTime;
    private Vector2 lastTapPos;
    private bool hasSwipedUp;

    private void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive)
        {
            OnPointerUp();
            return;
        }

        HandlePointerInput();
    }

    private void OnDisable() => OnPointerUp();

    private void HandlePointerInput()
    {
        var pointer = Pointer.current;
        if (pointer != null)
        {
            Vector2 screenPos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                OnPointerDown(screenPos);
            }
            else if (pointer.press.isPressed && isPointerHeld)
            {
                OnPointerDrag(screenPos);
            }
            else if (isPointerHeld)
            {
                OnPointerUp();
            }
        }
        else
        {
            HandleLegacyFallback();
        }
    }

    private void HandleLegacyFallback()
    {
        try
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                OnPointerDown(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && isPointerHeld)
            {
                OnPointerDrag(Input.mousePosition);
            }
            else if (isPointerHeld)
            {
                OnPointerUp();
            }
        }
        catch {}
    }

    private void OnPointerDown(Vector2 screenPos)
    {
        float timeSinceLastTap = Time.unscaledTime - lastTapTime;
        float distSinceLastTap = Vector2.Distance(screenPos, lastTapPos);

        if (timeSinceLastTap <= doubleTapMaxTime && distSinceLastTap <= doubleTapMaxDist)
        {
            player.Attack();
            lastTapTime = 0f;
        }
        else
        {
            lastTapTime = Time.unscaledTime;
            lastTapPos = screenPos;
        }

        isPointerHeld = true;
        touchStartPos = screenPos;
        hasSwipedUp = false;
        ApplyDirectionByPosition(screenPos.x);
    }

    private void OnPointerDrag(Vector2 screenPos)
    {
        if (!isPointerHeld) return;
        float deltaY = screenPos.y - touchStartPos.y;
        float deltaX = Mathf.Abs(screenPos.x - touchStartPos.x);

        if (!hasSwipedUp && deltaY > swipeThreshold && deltaY > deltaX)
        {
            player.Jump();
            hasSwipedUp = true;
        }

        ApplyDirectionByPosition(screenPos.x);
    }

    private void OnPointerUp()
    {
        if (!isPointerHeld) return;
        isPointerHeld = false;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        player?.SetMoveInput(0f);
    }

    private void ApplyDirectionByPosition(float screenX)
    {
        float dir = (screenX >= Screen.width * 0.5f) ? 1f : -1f;
        player.SetMoveInput(dir);
    }

    public void SimulatePointerPress(Vector2 screenPos) => OnPointerDown(screenPos);
    public void SimulatePointerDrag(Vector2 screenPos) => OnPointerDrag(screenPos);
    public void SimulatePointerRelease() => OnPointerUp();
}
