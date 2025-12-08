using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

public class DesertTempleBossMoveController_smoke : MonoBehaviour
{
    [Header("移動の設定")]
    [SerializeField]
    private float leftBound;

    [SerializeField]
    private float rightBound;

    [SerializeField]
    private float initialPositionY;

    [Tooltip("縦幅の半径")]
    [SerializeField]
    private float heightRadius = 1.5f;

    [Tooltip("1周にかかる時間 (秒)")]
    [SerializeField]
    private float cyclePeriod = 4.0f;

    [Header("基本コンポーネント")]
    [SerializeField]
    private GameObject leftArmObject = null;

    [SerializeField]
    private GameObject rightArmObject = null;

    [Header("設定項目")]
    [SerializeField]
    private Transform playerTransform = null;

    [Header("その他の設定")]
    [Tooltip("腕が上下する幅")]
    [SerializeField]
    private float armFloatAmplitude = 1f; 

    [Tooltip("腕の上下振動にかかる時間")]
    [SerializeField]
    private float armFloatDuration = 1.5f;
    private float widthRadius = 3.0f; //横幅の半径
    private float currentMoveTime; // 移動経過時間
    private int lastPhaseIndex = 0; // πの倍数を通過したかを判定するためのインデックス
    private bool rightFlag = false;
    private Vector2 centerPosition; // 中心位置

    private enum MovementPattern
    {
        Standard, // 基本の楕円
        Figure8, // 8の字
        EasedHover, // イージング(片側は膨らみ、もう片側は凹むような、歪んだ楕円)
        Astroid, // 星型（アストロイド）
    }

    private MovementPattern movementPattern = MovementPattern.Standard; // 動きのパターン

    private SpriteRenderer spriteRenderer;
    private List<SpriteRenderer> allRenderers = new List<SpriteRenderer>(); // 子オブジェクトの位置反転用

    // Tween管理用変数
    private Tweener leftArmTween;
    private Tweener rightArmTween;
    private float leftArmDefaultY;
    private float rightArmDefaultY;

    private void Awake()
    {
        if (leftArmObject == null || rightArmObject == null)
        {
            Debug.LogError($"{this.name}の基本コンポーネントが設定されていません。");
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        allRenderers.Add(spriteRenderer);

        // パーツのレンダラーを登録
        void RegisterPart(GameObject obj)
        {
            if (obj == null)
                return;

            // レンダラー登録
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
                allRenderers.Add(sr);
        }

        RegisterPart(leftArmObject);
        RegisterPart(rightArmObject);

        //腕の初期ローカルY座標を保存しておく（基準点にするため）
        leftArmDefaultY = leftArmObject.transform.localPosition.y;
        rightArmDefaultY = rightArmObject.transform.localPosition.y;
    }

    private void Start()
    {
        ResetState();
    }

    public void ResetState()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject
                .FindGameObjectWithTag(GameConstants.PlayerTagName)
                ?.transform;
            if (playerTransform == null)
            {
                Debug.LogError($"{this.name}はPlayerTransformを見つけられませんでした");
                return;
            }
        }
        //中心座標の決定
        centerPosition = new Vector2((leftBound + rightBound) / 2, initialPositionY);

        currentMoveTime = 0f;
        lastPhaseIndex = 0;
        movementPattern = MovementPattern.Standard;
        rightFlag = IsTargetToRight();
        UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新

        StartArmMovingAnimation(); //腕の上下振動アニメーション開始
    }

    private void FixedUpdate()
    {
        bool isTargetCurrentlyRight = IsTargetToRight();
        if (rightFlag != isTargetCurrentlyRight)
        {
            rightFlag = isTargetCurrentlyRight;
            UpdateFacingDirection(rightFlag); //すべてのパーツの向きを更新
        }
        UpdateMovement(); // 移動の更新
    }

    /// <summary>
    /// 移動に関する計算と座標更新を行う
    /// </summary>
    private void UpdateMovement()
    {
        // 時間を経過させる
        currentMoveTime += Time.deltaTime;

        // シータ（角度）を計算: 0 から 2π の範囲で変化
        float theta = (2.0f * Mathf.PI * currentMoveTime) / cyclePeriod;

        // 現在の theta が π の何倍の区間にいるかを計算 (0, 1, 2, ...)
        int currentPhaseIndex = Mathf.FloorToInt(theta / Mathf.PI);

        // 前回のフレームと区間が異なれば（つまり 0, π, 2π... を跨いだら）
        if (currentPhaseIndex > lastPhaseIndex)
        {
            ChangeToRandomPattern();
            lastPhaseIndex = currentPhaseIndex;
        }

        // パターンに応じたオフセット（ズレ）を計算
        Vector2 offset = CalculateOffset(theta, movementPattern);

        // 座標を更新 (中心点 + オフセット)
        transform.position = centerPosition + new Vector2(offset.x, offset.y);
    }

    // <summary>
    /// 指定された角度(theta)とパターンに基づいて位置オフセットを計算する
    /// </summary>
    private Vector2 CalculateOffset(float theta, MovementPattern pattern)
    {
        float x = 0f;
        float y = 0f;

        // 基本のX座標計算 (x = a * cosθ)
        float basicX = widthRadius * Mathf.Cos(theta);

        //　引数の pattern を使用して分岐
        switch (pattern)
        {
            case MovementPattern.Standard:
                x = basicX;
                y = heightRadius * Mathf.Sin(theta);
                break;

            case MovementPattern.Figure8:
                x = basicX;
                // y = b * sin(2θ)
                y = heightRadius * Mathf.Sin(2.0f * theta);
                break;

            case MovementPattern.EasedHover:
                x = basicX;
                // y = b * sin^3θ
                y = heightRadius * Mathf.Pow(Mathf.Sin(theta), 3.0f);
                break;

            case MovementPattern.Astroid:
                // x = a * cos^3θ, y = b * sin^3θ
                x = widthRadius * Mathf.Pow(Mathf.Cos(theta), 3.0f);
                y = heightRadius * Mathf.Pow(Mathf.Sin(theta), 3.0f);
                break;
            default:
                x = basicX;
                y = heightRadius * Mathf.Sin(theta);
                break;
        }

        return new Vector2(x, y);
    }

    /// <summary>
    /// 定義されているMovementPatternの中からランダムに一つ選んで設定する
    /// </summary>
    private void ChangeToRandomPattern()
    {
        // Enumの値を配列として全て取得
        var values = System.Enum.GetValues(typeof(MovementPattern));

        // ランダムなインデックス決定
        int randomIndex = Random.Range(0, values.Length);

        // 新しいパターンを適用
        movementPattern = (MovementPattern)values.GetValue(randomIndex);

        //Debug.Log("移動パターン変更: " + movementPattern.ToString());
    }

    /// <summary>
    /// 腕の上下動アニメーションを開始します
    /// </summary>
    private void StartArmMovingAnimation()
    {
        Debug.Log("腕の上下動アニメーション開始");
        // 既存のTweenがあれば一度破棄してリセット
        leftArmTween?.Kill();
        rightArmTween?.Kill();

        // 位置を初期位置に戻す
        leftArmObject.transform.localPosition = new Vector3(
            leftArmObject.transform.localPosition.x,
            leftArmDefaultY,
            leftArmObject.transform.localPosition.z
        );
        rightArmObject.transform.localPosition = new Vector3(
            rightArmObject.transform.localPosition.x,
            rightArmDefaultY,
            rightArmObject.transform.localPosition.z
        );

        // 左腕のアニメーション
        // 現在のY座標から +amplitude 分だけ移動し、Yoyoで戻ってくる動きを無限ループ(-1)させる
        leftArmTween = leftArmObject
            .transform.DOLocalMoveY(leftArmDefaultY + armFloatAmplitude, armFloatDuration)
            .SetEase(Ease.InOutSine) // 生き物らしい滑らかな動き
            .SetLoops(-1, LoopType.Yoyo) // 往復ループ
            .SetLink(leftArmObject); // オブジェクトが消えたらTweenも自動消滅させる安全策

        // 右腕のアニメーション
        rightArmTween = rightArmObject
            .transform.DOLocalMoveY(rightArmDefaultY + armFloatAmplitude, armFloatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(armFloatDuration / 4) // 左腕とずらして動かす
            .SetLink(rightArmObject);
    }

    /// <summary>
    /// 自身の位置からプレイヤーへのベクトルを取得します
    /// </summary>
    private Vector2 GetVectorToPlayer()
    {
        if (playerTransform != null)
        {
            return (Vector2)playerTransform.position - (Vector2)transform.position;
        }
        return Vector2.zero;
    }

    /// <summary>
    /// 対象が自分より右側にいるか判定します
    /// </summary>
    /// <param name="dir">対象への方向ベクトル</param>
    /// <returns>右側にいるならtrue、左側ならfalse</returns>
    private bool IsTargetToRight()
    {
        Vector2 dir = GetVectorToPlayer();
        return dir.x > 0;
    }

    /// <summary>
    /// 全てのパーツの向き（flipX）を一括で更新します。
    /// Pivot調整済みのため、位置の反転処理は不要です。
    /// </summary>
    /// <param name="isFacingRight">右を向いているか</param>
    private void UpdateFacingDirection(bool isFacingRight)
    {
        // 右向きなら flipX=true 左向きなら flipX=false

        foreach (var sr in allRenderers)
        {
            if (sr != null)
            {
                sr.flipX = isFacingRight;
            }
        }
    }

    private void OnDestroy()
    {
        leftArmTween?.Kill();
        rightArmTween?.Kill();
    }

    private void OnDrawGizmosSelected()
    {
        // 実行中以外でも中心位置を計算して表示できるようにする
        Vector2 drawCenter = Application.isPlaying
            ? centerPosition
            : new Vector2((leftBound + rightBound) / 2, initialPositionY);

        if (!Application.isPlaying)
        {
            widthRadius = (rightBound - leftBound) / 2;
        }

        float step = 0.1f; // 描画の細かさ

        // Enumのすべてのパターンをループして描画
        foreach (MovementPattern pattern in System.Enum.GetValues(typeof(MovementPattern)))
        {
            // パターンごとに色を変える
            switch (pattern)
            {
                case MovementPattern.Standard:
                    Gizmos.color = Color.white;
                    break;
                case MovementPattern.Figure8:
                    Gizmos.color = Color.yellow;
                    break;
                case MovementPattern.EasedHover:
                    Gizmos.color = Color.cyan;
                    break;
                case MovementPattern.Astroid:
                    Gizmos.color = Color.magenta;
                    break;
                default:
                    Gizmos.color = Color.gray;
                    break;
            }

            // 現在選択されているパターンは強調（不透明）、それ以外は半透明にする
            if (pattern != movementPattern)
            {
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            }

            Vector2 prevPos = drawCenter + CalculateOffset(0, pattern);

            // 0 から 2π まで線を描画
            for (float t = step; t <= 2.0f * Mathf.PI + step; t += step)
            {
                Vector2 nextPos = drawCenter + CalculateOffset(t, pattern);
                Gizmos.DrawLine(prevPos, nextPos);
                prevPos = nextPos;
            }
        }
    }
}
