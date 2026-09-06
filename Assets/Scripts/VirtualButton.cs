using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum ButtonAction { Left, Right, Jump, Swap, Pause }
    [SerializeField] private ButtonAction action;
    public ButtonAction Action => action;

    private PlayerController player;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
    }

    public void Press()
    {
        OnPointerDown(null);
    }

    public void Release()
    {
        OnPointerUp(null);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive && action != ButtonAction.Pause) return;
        if (player == null) player = FindAnyObjectByType<PlayerController>();

        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(new Vector3(-0.14f, -0.14f, 0f), 0.14f, 6, 0.5f);

        switch (action)
        {
            case ButtonAction.Left: player?.SetMoveInput(-1f); break;
            case ButtonAction.Right: player?.SetMoveInput(1f); break;
            case ButtonAction.Jump: player?.Jump(); break;
            case ButtonAction.Swap: player?.SwapWeapon(); break;
            case ButtonAction.Pause: GameManager.Instance?.TogglePause(); break;
        }
    }

    public void OnPointerUp(PointerEventData eventData) => ResetMovement();

    public void OnPointerExit(PointerEventData eventData) => ResetMovement();

    private void OnDisable() => ResetMovement();

    private void ResetMovement()
    {
        if (action == ButtonAction.Left || action == ButtonAction.Right)
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            player?.SetMoveInput(0f);
        }
    }

    private void OnDestroy() => transform.DOKill();
}
