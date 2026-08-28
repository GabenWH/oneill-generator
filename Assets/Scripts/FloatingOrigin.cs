using UnityEngine;

public class FloatingOrigin : MonoBehaviour
{
    public Transform player;

    public float threshold = 2000f;

    public Vector3 accumulatedOffset;

    private void LateUpdate()
    {
        if (player == null)
            return;

        if (player.position.sqrMagnitude <
            threshold * threshold)
            return;

        Vector3 offset =
            player.position;

        GameObject[] roots =
            gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root.transform == player)
                continue;

            root.transform.position -= offset;
        }

        player.position -= offset;

        accumulatedOffset += offset;
    }

    public Vector3 GetAbsolutePosition()
    {
        if (player == null)
            return accumulatedOffset;

        return accumulatedOffset +
               player.position;
    }
}