using System.Collections;
using UnityEngine;

public class BridgeController : MonoBehaviour
{
    [SerializeField] private float risenYPosition;
    [SerializeField] private float loweredYOffset = -5f;
    [SerializeField] private float openDurationSeconds = 1.5f;

    private Coroutine openRoutine;

    private void Awake()
    {
        risenYPosition = transform.localPosition.y;

        Vector3 position = transform.localPosition;
        position.y = risenYPosition + loweredYOffset;
        transform.localPosition = position;
    }

    public void Open()
    {
        if (openRoutine != null)
        {
            return;
        }

        openRoutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        float startY = transform.localPosition.y;
        float elapsed = 0f;
        Vector3 position = transform.localPosition;

        while (elapsed < openDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / openDurationSeconds;
            position.y = Mathf.Lerp(startY, risenYPosition, t);
            transform.localPosition = position;
            yield return null;
        }

        position.y = risenYPosition;
        transform.localPosition = position;
        openRoutine = null;
    }
}
