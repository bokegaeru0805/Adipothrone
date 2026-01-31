using System.Collections;
using CriWare;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// プレイヤーを感知して伸縮するレーザービームを制御するクラス。
/// SpriteRendererのDraw Modeが"Tiled"になっていることを前提としています。
/// </summary>
public class LaserBeamController : MonoBehaviour, IEnemyResettable
{
    [Header("コンポーネント参照")]
    [Tooltip("ビームの当たり判定用コライダー（ContactDamageController用）")]
    [SerializeField]
    private BoxCollider2D beamCollider;

    [Tooltip("プレイヤー感知用のエリアコライダー（Trigger）")]
    [SerializeField]
    private Collider2D detectionAreaCollider;

    [Header("動作設定")]
    [Tooltip("プレイヤー感知から発射までの遅延時間（秒）")]
    [SerializeField]
    private float delayBeforeFire = 0.5f;

    [Tooltip("ビームが最大まで伸びるのにかかる時間（秒）")]
    [SerializeField]
    private float expansionDuration = 0.2f;

    [Tooltip("ビームの最大長")]
    [SerializeField]
    private float maxBeamLength = 10.0f;

    [Tooltip("ビームの初期長（待機時の長さ）")]
    [SerializeField]
    private float initialBeamLength = 0.0f;

    [Header("初期状態設定")]
    [Tooltip(
        "Trueの場合、ゲーム開始時（およびリセット時）に最初からビームが伸びた状態で始まります"
    )]
    [SerializeField]
    private bool startActive = false;

    [Header("ループ設定")]
    [Tooltip(
        "Trueの場合、一定時間後に縮み、再びプレイヤーを待ちます。\nFalseの場合、一度伸びたら伸びっぱなしになります。"
    )]
    [SerializeField]
    private bool isLooping = true;

    [Tooltip("伸びきった状態で維持する時間（秒）")]
    [SerializeField, ShowIf(nameof(isLooping))]
    private float activeDuration = 2.0f;

    [Tooltip("ビームが縮むのにかかる時間（秒）")]
    [SerializeField, ShowIf(nameof(isLooping))]
    private float shrinkDuration = 0.5f;

    // 内部ステート
    private enum BeamState
    {
        Idle, // 待機中（感知待ち）
        Delaying, // 感知後、発射待ち
        Expanding, // 伸長中
        Active, // 照射中（伸びきった状態）
        Shrinking // 収束中
        ,
    }

    // --- 内部変数 ---
    private BeamState currentState = BeamState.Idle;
    private Coroutine beamCoroutine;
    private float defaultHeight; // スプライトの元の高さ（太さ）
    private CriAtomExPlayback expandSePlayback; // 伸縮音制御用のPlaybackハンドル

    // --- コンポーネント参照 ---
    private SpriteRenderer beamSpriteRenderer; //ビームの描画用スプライト
    private CriWare.Assets.CriAtomSePlayer _sePlayer;

    private void Awake()
    {
        if (beamCollider == null)
            beamCollider = GetComponent<BoxCollider2D>();

        beamSpriteRenderer = GetComponent<SpriteRenderer>();
        _sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        // スプライトの高さ（太さ）は初期値を維持する
        if (beamSpriteRenderer != null)
        {
            defaultHeight = beamSpriteRenderer.size.y;
        }
    }

    private void Start()
    {
        // 初期状態にリセット
        ResetState();
    }

    /// <summary>
    /// 初期状態にリセットします
    /// </summary>
    public void ResetState()
    {
        // 実行中のコルーチンがあれば停止
        if (beamCoroutine != null)
        {
            StopCoroutine(beamCoroutine);
            beamCoroutine = null;
        }

        //リセット時に音が残っていたら消す
        StopExpandSound();

        if (startActive)
        {
            // 最初からアクティブ設定の場合
            // ビームを最大長に設定
            UpdateBeamSize(maxBeamLength);

            // いきなり照射状態（維持フェーズ）からシーケンスを開始する
            // (ループ設定などの挙動を統一して管理するためコルーチンを通す)
            beamCoroutine = StartCoroutine(FireSequence(true));
        }
        else
        {
            // 通常待機設定の場合
            currentState = BeamState.Idle;
            // ビームの長さを初期値に戻す
            UpdateBeamSize(initialBeamLength);

            // 感知エリアを有効化
            if (detectionAreaCollider != null)
            {
                detectionAreaCollider.enabled = true;
            }
        }
    }

    /// <summary>
    /// エリア侵入判定（Trigger）
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // アイドル状態でないなら何もしない
        if (currentState != BeamState.Idle)
            return;

        // プレイヤーかどうか判定
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            // 接触したのが「感知エリア」であるかを確認
            // detectionAreaCollider が設定されており、かつ
            // プレイヤー(other)が感知エリアに触れている場合のみ発射する
            // (これにより、待機中の短いビーム部分に触れただけでは発射しないようにする)
            if (detectionAreaCollider != null && detectionAreaCollider.IsTouching(other))
            {
                beamCoroutine = StartCoroutine(FireSequence(false));
            }
        }
    }

    /// <summary>
    /// ビームの発射シーケンス（遅延 -> 伸長 -> [待機 -> 収束]）
    /// </summary>
    private IEnumerator FireSequence(bool skipIntro = false)
    {
        float timer = 0f;
        if (!skipIntro)
        {
            // 通常の発射シーケンス（遅延 -> 伸長）

            // --- 1. 遅延フェーズ ---
            currentState = BeamState.Delaying;
            yield return new WaitForSeconds(delayBeforeFire);

            // --- 2. 伸長フェーズ ---
            currentState = BeamState.Expanding;
            float startLen = beamSpriteRenderer.size.x;
            _sePlayer?.Play(SE_Field.LaserShoot);

            if (_sePlayer != null)
            {
                expandSePlayback = _sePlayer.Play(SE_Field.LaserExpand);
            }

            while (timer < expansionDuration)
            {
                timer += Time.deltaTime;
                float t = timer / expansionDuration;
                // 滑らかに伸ばす
                float currentLen = Mathf.Lerp(startLen, maxBeamLength, t);
                UpdateBeamSize(currentLen);
                yield return null;
            }

            // 伸長完了したら音を停止
            StopExpandSound();
            // 念のため最終値を適用
            UpdateBeamSize(maxBeamLength);
        }
        else
        {
            // いきなり照射状態から開始
            currentState = BeamState.Active;
            // (長さはResetState等ですでに設定済み前提だが、念のためここでもセット)
            UpdateBeamSize(maxBeamLength);
        }

        currentState = BeamState.Active;

        // --- 3. ループ分岐 ---
        if (!isLooping)
        {
            // ループしない場合はここで終了（伸びたまま）
            yield break;
        }

        // --- 4. 照射維持フェーズ ---
        yield return new WaitForSeconds(activeDuration);

        // --- 5. 収束フェーズ ---
        currentState = BeamState.Shrinking;
        timer = 0f;

        while (timer < shrinkDuration)
        {
            timer += Time.deltaTime;
            float t = timer / shrinkDuration;
            // 滑らかに縮める
            float currentLen = Mathf.Lerp(maxBeamLength, initialBeamLength, t);
            UpdateBeamSize(currentLen);
            yield return null;
        }
        UpdateBeamSize(initialBeamLength);

        // --- 6. 完了（再度待機状態へ） ---
        currentState = BeamState.Idle;
        beamCoroutine = null;
    }

    /// <summary>
    /// ビームの長さ（スプライトとコライダー）を更新する
    /// ピボットが「左（Left）」にあることを前提とした計算です。
    /// </summary>
    /// <param name="length">設定する長さ</param>
    private void UpdateBeamSize(float length)
    {
        if (beamSpriteRenderer != null)
        {
            // Tiled設定のスプライトサイズを変更
            beamSpriteRenderer.size = new Vector2(length, defaultHeight);
        }

        if (beamCollider != null)
        {
            // コライダーのサイズを変更
            beamCollider.size = new Vector2(length, beamCollider.size.y);

            // コライダーのオフセット位置を調整
            // (長さが変わると中心位置が変わるため、左端を基準にするなら 半分だけ右にずらす)
            beamCollider.offset = new Vector2(length / 2f, beamCollider.offset.y);
        }
    }

    /// <summary>
    /// 伸縮音の再生を停止する
    /// </summary>
    private void StopExpandSound()
    {
        var status = expandSePlayback.GetStatus();
        if (status == CriAtomExPlayback.Status.Playing || status == CriAtomExPlayback.Status.Prep)
        {
            expandSePlayback.Stop();
        }
    }

    private void OnDisable()
    {
        StopExpandSound();
        ResetState();
    }

    private void OnDrawGizmos()
    {
        // ビームの最大射程範囲を描画
        // 指定色: 赤色の半透明
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);

        // オブジェクトの回転と位置、スケールを反映させる
        Gizmos.matrix = transform.localToWorldMatrix;

        // ビームの高さを取得（コライダーがあればその高さ、なければスプライト、どちらもなければ1.0）
        float height = 1.0f;
        float yOffset = 0f;

        if (beamCollider != null)
        {
            height = beamCollider.size.y;
            yOffset = beamCollider.offset.y;
        }
        else if (beamSpriteRenderer != null)
        {
            height = beamSpriteRenderer.size.y;
        }

        // ビームはピボットが左（0）にあり、右に向かって伸びる前提
        // Gizmos.DrawCubeの中心座標は、長さの半分(maxBeamLength / 2)の位置になる
        Vector3 center = new Vector3(maxBeamLength / 2f, yOffset, 0f);
        Vector3 size = new Vector3(maxBeamLength, height, 0.1f);

        Gizmos.DrawCube(center, size);

        // 外枠も薄く表示して見やすくする
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireCube(center, size);
    }

    private void OnDrawGizmosSelected()
    {
        // 検知範囲（Area）を描画
        // オブジェクト選択時のみ表示
        if (detectionAreaCollider != null)
        {
            // 指定色: Cyan系
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f); // 半透明のCyan

            // オブジェクトの回転を反映
            // detectionAreaColliderが子オブジェクトにある場合等は
            // detectionAreaCollider.transform.localToWorldMatrix を使う必要がありますが、
            // 今回は同じ階層または位置関係が固定されている前提で親のmatrixを使用します
            Gizmos.matrix = detectionAreaCollider.transform.localToWorldMatrix;

            // BoxCollider2Dの場合
            if (detectionAreaCollider is BoxCollider2D box)
            {
                Gizmos.DrawCube(box.offset, box.size);

                // 枠線
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            // CircleCollider2Dの場合（念のため対応）
            else if (detectionAreaCollider is CircleCollider2D circle)
            {
                Gizmos.DrawSphere(circle.offset, circle.radius);

                // 枠線
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(circle.offset, circle.radius);
            }
        }
    }
}
