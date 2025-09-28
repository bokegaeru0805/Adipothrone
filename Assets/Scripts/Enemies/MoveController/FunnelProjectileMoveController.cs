using System.Collections;
using AIE2D;
using UnityEngine;

/// <summary>
/// 発射されたファンネルの移動と画面外検知を管理する
/// </summary>
public class FunnelProjectile : MonoBehaviour
{
    private float preparationTime = 0.3f;
    private float recoilDistance = 0.5f;

    // === 内部パラメータ ===
    private Vector2 moveDirection;
    private float moveSpeed;
    private bool isLaunched = false;
    private NightBorneMoveController ownerController; // 自分を制御するコントローラー
    private Coroutine launchCoroutine; // 実行中のコルーチンを管理
    private StaticAfterImageEffect2DPlayer afterImage; //残像エフェクト

    private void Awake()
    {
        afterImage = gameObject.GetComponent<StaticAfterImageEffect2DPlayer>();
        if (afterImage == null)
        {
            Debug.LogWarning(
                $"{this.name} に StaticAfterImageEffect2DPlayer コンポーネントが見つかりませんでした。"
            );
        }
        else
        {
            afterImage.SetActive(false); //初期状態では残像を無効化
        }

        this.tag = GameConstants.ImmuneEnemyTagName;

        // 最初は無効化しておく
        this.enabled = false;
    }

    /// <summary>
    /// 発射前のパラメータを設定する
    /// </summary>
    /// <param name="prepTime">予備動作の時間</param>
    /// <param name="recoilDist">後退する距離</param>
    public void Setup(float prepTime, float recoilDist)
    {
        this.preparationTime = prepTime;
        this.recoilDistance = recoilDist;
    }

    /// <summary>
    /// 発射命令を受け、予備動作と移動を開始する
    /// </summary>
    public void Launch(NightBorneMoveController controller, Vector2 direction, float speed)
    {
        // すでに実行中の場合は何もしない
        if (launchCoroutine != null)
        {
            return;
        }

        this.ownerController = controller;
        this.moveDirection = direction.normalized; // 方向を正規化
        this.moveSpeed = speed;
        this.tag = GameConstants.DamageableEnemyTagName;

        // スクリプトを有効化し、コルーチンを開始させる
        this.enabled = true;

        afterImage?.SetActive(true); //残像を有効化

        launchCoroutine = StartCoroutine(LaunchSequence());
    }

    /// <summary>
    /// 予備動作から発射までの一連の流れを管理するコルーチン
    /// </summary>
    private IEnumerator LaunchSequence()
    {
        // --- 1. 予備動作フェーズ ---

        // (1) 移動する方向を向く
        if (moveDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.eulerAngles = new Vector3(0, 0, angle);
        }

        // (2) 指定時間かけて、発射方向とは逆向きに少し後退する
        Vector3 startPosition = transform.position;
        Vector3 recoilEndPosition = startPosition - (Vector3)moveDirection * recoilDistance;

        float elapsedTime = 0f;
        while (elapsedTime < preparationTime)
        {
            // Lerpを使って滑らかに後退させる
            transform.position = Vector3.Lerp(
                startPosition,
                recoilEndPosition,
                elapsedTime / preparationTime
            );
            elapsedTime += Time.deltaTime;
            yield return null; // 1フレーム待機
        }
        // 誤差をなくすために最終位置を確定
        transform.position = recoilEndPosition;

        // --- 2. 発射フェーズ ---
        isLaunched = true;
        SEManager.instance.PlayEnemyActionSEPitch(
            SE_EnemyAction.SwordThrow1,
            Random.Range(1.0f, 1.2f)
        ); // 発射音を再生
    }

    private void Update()
    {
        // isLaunchedがtrueになるまで（＝予備動作が終わるまで）移動しない
        if (!isLaunched)
            return;

        // 発射後は、指定された方向へ直進する
        transform.position += (Vector3)moveDirection * moveSpeed * Time.deltaTime;

        // 画面外に出たかどうかを毎フレーム自分でチェックする
        CheckIfOutOfBounds();
    }

    /// <summary>
    /// 現在アクティブなCameraMoveAreaの範囲外に出たかどうかを判定し、
    /// 範囲外であれば自身を初期化する処理を呼び出します。
    /// </summary>
    private void CheckIfOutOfBounds()
    {
        // 1. 現在のカメラ境界を取得
        Bounds? areaBounds = CameraMoveArea.ActiveAreaBounds;

        // 2. 境界が取得できない（=アクティブなエリアがない）場合は、何もしない
        if (!areaBounds.HasValue)
        {
            return;
        }

        // 3. 自分の座標が、取得した境界の内側に含まれているかチェック
        //    Bounds.Contains()は点が境界の内側にあればtrueを返す
        //    ! (not) をつけて、外側に出た場合を判定する
        if (!areaBounds.Value.Contains(transform.position))
        {
            // 4. 範囲外に出たので、元々OnBecameInvisibleにあった処理を実行
            this.tag = GameConstants.ImmuneEnemyTagName;

            if (ownerController != null)
            {
                ownerController.OnFunnelOffScreen(this.gameObject);
            }

            ResetState();
        }
    }

    /// <summary>
    /// ファンネルの状態を初期化する
    /// </summary>
    private void ResetState()
    {
        // 実行中のコルーチンがあれば停止する
        if (launchCoroutine != null)
        {
            StopCoroutine(launchCoroutine);
            launchCoroutine = null;
        }

        isLaunched = false;
        this.enabled = false;
        afterImage?.SetActive(false); //残像を無効化
    }
}
