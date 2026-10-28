using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ObstacleMover : MonoBehaviour
{
    [SerializeField] private Vector3 clearedLocalPosition;
    [SerializeField] private Vector3 clearedLocalEulerAngles;
    [SerializeField] private float moveDurationSeconds = 1.5f;
    [SerializeField] private UnityEvent onCleared;

    private Coroutine moveRoutine;

    public void Clear()
    {
        if (moveRoutine != null)
        {
            return;
        }

        moveRoutine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        Vector3 startPosition = transform.localPosition;
        Quaternion startRotation = transform.localRotation;
        Quaternion endRotation = Quaternion.Euler(clearedLocalEulerAngles);
        float elapsed = 0f;

        while (elapsed < moveDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDurationSeconds;
            transform.localPosition = Vector3.Lerp(startPosition, clearedLocalPosition, t);
            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        transform.localPosition = clearedLocalPosition;
        transform.localRotation = endRotation;
        moveRoutine = null;
        onCleared?.Invoke();
    }
}
