using System.Collections;
using UnityEngine;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private Material flashMaterial;
    private SpriteRenderer sr;
    private Material defaultMaterial;
    private Coroutine flashRoutine;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) defaultMaterial = sr.sharedMaterial;
        if (flashMaterial == null)
        {
            var shader = Shader.Find("GUI/Text Shader");
            if (shader != null) flashMaterial = new Material(shader) { color = Color.white };
        }
    }

    public void SetFlashMaterial(Material mat)
    {
        flashMaterial = mat;
    }

    public void Flash(float duration = 0.08f)
    {
        if (sr == null || flashMaterial == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(DoFlash(duration));
    }

    private IEnumerator DoFlash(float duration)
    {
        sr.sharedMaterial = flashMaterial;
        yield return new WaitForSeconds(duration);
        if (sr != null && defaultMaterial != null) sr.sharedMaterial = defaultMaterial;
        flashRoutine = null;
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        if (sr != null && defaultMaterial != null)
        {
            sr.sharedMaterial = defaultMaterial;
        }
    }
}
