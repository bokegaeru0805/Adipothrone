using UnityEngine;

public class MoveLimitedZone : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Color fillColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // 半透明のグレー
        Color borderColor = Color.black; // 枠線は黒

        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        Gizmos.matrix = Matrix4x4.TRS(
            transform.position + (Vector3)box2D.offset,
            transform.rotation,
            transform.lossyScale
        );
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, (Vector3)box2D.size);
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(Vector3.zero, (Vector3)box2D.size);
    }
}
