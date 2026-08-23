using System.Collections.Generic;
using Fungus;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DirectionalMoveLimitArea : MonoBehaviour
{
    public enum BlockedDirection
    {
        Left = 0,
        Right = 1,
    }

    [Header("進行制限")]
    [SerializeField]
    private BlockedDirection blockedDirection = BlockedDirection.Right;

    [SerializeField]
    [Tooltip("Areaの禁止方向側半分を塞ぐMoveLimitAreaを指定します。")]
    private MoveLimitedArea moveLimitArea;

    [Header("会話")]
    [SerializeField]
    private Flowchart localFlowchart;

    [SerializeField]
    private string blockName;

    [Header("発動条件")]
    [SerializeField]
    [Tooltip("空中で会話が始まり落下することを防ぐため、通常は有効にします。")]
    private bool requireGrounded = true;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private Heroin_move playerMove;
    private bool isWaitingForInputRelease;

    private void Awake()
    {
        BoxCollider2D areaCollider = GetComponent<BoxCollider2D>();
        areaCollider.isTrigger = true;
    }

    private void Update()
    {
        if (playerMove == null)
            return;

        bool isBlockedDirectionPressed = IsBlockedDirectionPressed(
            playerMove.HorizontalMoveIntent
        );

        if (!isBlockedDirectionPressed)
        {
            isWaitingForInputRelease = false;
            return;
        }

        if (
            isWaitingForInputRelease
            || GameManager.instance == null
            || GameManager.instance.IsTalking
            || (requireGrounded && !playerMove.IsGrounded)
        )
            return;

        if (localFlowchart == null || string.IsNullOrWhiteSpace(blockName))
        {
            Debug.LogWarning("会話用のLocal FlowchartまたはBlock名が設定されていません。", this);
            isWaitingForInputRelease = true;
            return;
        }

        if (!localFlowchart.HasBlock(blockName))
        {
            Debug.LogWarning(
                $"Fungus Block '{blockName}' が Flowchart '{localFlowchart.name}' に見つかりません。",
                this
            );
            isWaitingForInputRelease = true;
            return;
        }

        isWaitingForInputRelease = true;
        FungusHelper.ExecuteBlock(localFlowchart, blockName);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Heroin_move enteredPlayer = other.GetComponentInParent<Heroin_move>();
        if (enteredPlayer == null)
            return;

        playerColliders.Add(other);
        playerMove = enteredPlayer;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count > 0)
            return;

        playerMove = null;
        isWaitingForInputRelease = false;
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        playerMove = null;
        isWaitingForInputRelease = false;
    }

    private bool IsBlockedDirectionPressed(float horizontalMoveIntent)
    {
        return blockedDirection == BlockedDirection.Right
            ? horizontalMoveIntent > 0f
            : horizontalMoveIntent < 0f;
    }

    private void OnValidate()
    {
        BoxCollider2D areaCollider = GetComponent<BoxCollider2D>();
        if (areaCollider == null)
            return;

        areaCollider.isTrigger = true;

        if (moveLimitArea == null)
            return;

        BoxCollider2D limitCollider = moveLimitArea.GetComponent<BoxCollider2D>();
        if (limitCollider == null)
            return;

        Transform limitTransform = moveLimitArea.transform;
        float direction = blockedDirection == BlockedDirection.Right ? 1f : -1f;
        Vector2 areaSize = areaCollider.size;
        Vector2 areaOffset = areaCollider.offset;

        limitTransform.localRotation = Quaternion.identity;
        limitTransform.localScale = Vector3.one;
        limitTransform.localPosition = new Vector3(
            areaOffset.x + direction * areaSize.x * 0.25f,
            areaOffset.y,
            0f
        );

        limitCollider.offset = Vector2.zero;
        limitCollider.size = new Vector2(areaSize.x * 0.5f, areaSize.y);
        limitCollider.isTrigger = false;
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D areaCollider = GetComponent<BoxCollider2D>();
        if (areaCollider == null)
            return;

        float direction = blockedDirection == BlockedDirection.Right ? 1f : -1f;
        Vector2 areaSize = areaCollider.size;
        Vector2 areaOffset = areaCollider.offset;
        Vector2 blockedCenter = areaOffset + Vector2.right * direction * areaSize.x * 0.25f;
        Vector2 blockedSize = new Vector2(areaSize.x * 0.5f, areaSize.y);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.75f, 0f, 0.9f);
        Gizmos.DrawWireCube(areaOffset, areaSize);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.25f);
        Gizmos.DrawCube(blockedCenter, blockedSize);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(blockedCenter, blockedSize);

        float arrowLength = Mathf.Max(areaSize.x * 0.2f, 0.5f);
        float arrowHeadSize = Mathf.Min(arrowLength * 0.35f, areaSize.y * 0.15f);
        Vector3 arrowStart = new Vector3(areaOffset.x, areaOffset.y, 0f);
        Vector3 arrowEnd = arrowStart + Vector3.right * direction * arrowLength;
        Gizmos.DrawLine(arrowStart, arrowEnd);
        Gizmos.DrawLine(
            arrowEnd,
            arrowEnd + new Vector3(-direction * arrowHeadSize, arrowHeadSize, 0f)
        );
        Gizmos.DrawLine(
            arrowEnd,
            arrowEnd + new Vector3(-direction * arrowHeadSize, -arrowHeadSize, 0f)
        );
    }
}
