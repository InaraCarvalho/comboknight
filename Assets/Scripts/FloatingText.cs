using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;

    public void Setup(string text, Color color, float size = 4f)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();
        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = size;

        transform.DOMoveY(transform.position.y + 1.2f, 0.7f).SetEase(Ease.OutQuad).SetLink(gameObject);
        textMesh.DOFade(0f, 0.7f).SetEase(Ease.InQuad).SetLink(gameObject).OnComplete(() => Destroy(gameObject));
        transform.DOPunchScale(Vector3.one * 0.3f, 0.25f).SetLink(gameObject);
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (textMesh != null) textMesh.DOKill();
    }
}
