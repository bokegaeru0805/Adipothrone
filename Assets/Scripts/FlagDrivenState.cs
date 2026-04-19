using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// フラグの状態に基づいて、オブジェクトの状態を多機能に制御する汎用コンポーネント。
/// アクティブ状態、スプライト、位置、アニメーション、コライダー、サウンド、カスタムイベントの実行に対応。
/// 「コントローラー」として常にアクティブなGameObjectにアタッチし、「モデル」となるオブジェクトを制御することを想定しています。
/// </summary>
public class FlagDrivenStatePro : MonoBehaviour
{
    // --- Inspector設定項目 ---

    [Header("制御対象")]
    [Tooltip("このGameObjectを制御します。未設定の場合は自分自身を制御します。")]
    [SerializeField]
    private GameObject controlledObject;

    [Header("状態定義")]
    [Tooltip("どの条件にも一致しない場合に適用されるデフォルトの状態。")]
    [SerializeField]
    private StatePro defaultState;

    [InfoBox("時系列が後の条件（進行度が高いもの）を下に配置してください。")]
    [Tooltip(
        "条件と、それが満たされたときに適用される状態のリスト。下から順（逆順）に評価され、最初に一致した条件が適用されます。"
    )]
    [SerializeField]
    private List<StateConditionPro> stateConditions = new();

    // --- コンポーネントキャッシュ ---

    private GameObject targetObject;
    private SpriteRenderer targetSpriteRenderer;
    private Animator targetAnimator;
    private Collider2D targetCollider;

    // --- 状態変数 ---
    private bool isPositionChangePending = false;
    private Vector3 pendingPosition;
    private bool isActiveStateChangePending = false;
    private bool pendingActiveState;
    private bool isSpriteChangePending = false;
    private Sprite pendingSprite;
    private bool pendingFlipX;
    private bool isInitialStateApplied = false; // 初回状態適用が完了したかどうかのフラグ

    // --- Unityライフサイクル ---

    private void Awake()
    {
        // 制御対象を決定します (未設定なら自分自身)。
        targetObject = controlledObject != null ? controlledObject : this.gameObject;

        // 制御対象の各コンポーネントを一度だけ取得し、キャッシュしておきます。
        if (targetObject != null)
        {
            targetSpriteRenderer = targetObject.GetComponent<SpriteRenderer>();
            targetAnimator = targetObject.GetComponent<Animator>();
            targetCollider = targetObject.GetComponent<Collider2D>();
        }
    }

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 最初のフレームの描画が終わるまで待つ
        // これにより、全てのシングルトンが確実に初期化されている状態になる
        yield return new WaitForEndOfFrame();

        // コンポーネントが有効になるたび、初回フラグをリセット
        isInitialStateApplied = false;

        // FlagManagerが存在する場合のみ、イベント購読を開始します。
        if (FlagManager.instance != null)
        {
            // boolとint、両方のイベントを購読します。
            FlagManager.OnBoolFlagChanged += OnAnyFlagChanged;
            FlagManager.OnIntFlagChanged += OnAnyFlagChanged;
        }
        else
        {
            Debug.Log("FlagManagerが見つかりません。FlagDrivenStateProは機能しません。");
        }

        // CameraMoveAreaからの退出イベントを購読
        CameraMoveArea.OnPlayerExitedArea += HandlePlayerExitedCameraArea;

        // このコンポーネントが有効になった際、現在のフラグに基づいて初期状態を正しく適用します。
        EvaluateAndApplyState();
    }

    private void OnDisable()
    {
        // このコンポーネントが無効になる際、必ずイベントの購読を解除します。
        if (FlagManager.instance != null)
        {
            FlagManager.OnBoolFlagChanged -= OnAnyFlagChanged;
            FlagManager.OnIntFlagChanged -= OnAnyFlagChanged;
        }

        // CameraMoveAreaからの退出イベントの購読を解除
        CameraMoveArea.OnPlayerExitedArea -= HandlePlayerExitedCameraArea;
    }

    // --- イベントハンドラ ---

    // boolとintの変更を同じメソッドで受け取り、再評価を促す
    private void OnAnyFlagChanged(Enum flag, bool newValue) => EvaluateAndApplyState();

    private void OnAnyFlagChanged(Enum flag, int newValue) => EvaluateAndApplyState();

    // CameraMoveAreaから退出したときに呼び出されるメソッド
    private void HandlePlayerExitedCameraArea(CameraMoveArea _exitedArea)
    {
        // アクティブ状態変更が保留されている場合
        if (isActiveStateChangePending)
        {
            if (targetObject.activeSelf != pendingActiveState)
            {
                targetObject.SetActive(pendingActiveState);
            }
            isActiveStateChangePending = false; // 保留状態を解除
        }

        // スプライト変更が保留されている場合
        if (isSpriteChangePending && targetSpriteRenderer != null)
        {
            if (pendingSprite != null)
            {
                targetSpriteRenderer.sprite = pendingSprite;
            }
            targetSpriteRenderer.flipX = pendingFlipX;
            isSpriteChangePending = false; // 保留状態を解除
        }

        // 位置変更が保留されている場合のみ実行
        if (isPositionChangePending && targetObject != null)
        {
            targetObject.transform.position = pendingPosition;
            isPositionChangePending = false; // 保留状態を解除
        }
    }

    // --- コアロジック ---

    /// <summary>
    /// 全ての条件を評価し、最初に見つかった一致する状態を適用します。
    /// </summary>
    private void EvaluateAndApplyState()
    {
        //Debug.Log("[FlagDrivenStatePro] Evaluating state based on flags.", this);

        // 条件リストを下から（＝新しい/進行度が高い条件から）順（逆順）にチェックします。
        for (int i = stateConditions.Count - 1; i >= 0; i--)
        {
            var condition = stateConditions[i];

            if (condition.AreAllFlagsMet())
            {
                // 条件に一致するものが見つかったら、その状態を適用して処理を終了します。
                ApplyState(condition.stateToApply);
                return;
            }
        }

        // どの条件にも一致しなかった場合は、デフォルト状態を適用します。
        ApplyState(defaultState);
    }

    /// <summary>
    /// 指定された状態をターゲットオブジェクトに適用します。
    /// </summary>
    private void ApplyState(StatePro state)
    {
        if (targetObject == null)
            return;

        // 【アクティブ状態の変更】
        if (state.changeActiveState)
        {
            // 遅延条件：delayフラグがtrue かつ 初回実行が完了している場合
            if (state.delayActiveStateUntilAreaExit && isInitialStateApplied)
            {
                pendingActiveState = state.isActive;
                isActiveStateChangePending = true;
            }
            else
            {
                // 即時実行
                if (targetObject.activeSelf != state.isActive)
                {
                    targetObject.SetActive(state.isActive);
                }
                isActiveStateChangePending = false; // 保留キャンセル
            }
        }

        // 【スプライトの変更】
        if (state.changeSprite && targetSpriteRenderer != null)
        {
            // 遅延条件
            if (state.delaySpriteUntilAreaExit && isInitialStateApplied)
            {
                pendingSprite = state.sprite;
                pendingFlipX = state.flipX;
                isSpriteChangePending = true;
            }
            else
            {
                // 即時実行
                if (state.sprite != null)
                {
                    targetSpriteRenderer.sprite = state.sprite;
                }
                targetSpriteRenderer.flipX = state.flipX;
                isSpriteChangePending = false; // 保留キャンセル
            }
        }

        // 【位置の変更】ロジック
        if (state.changePosition)
        {
            // 遅延条件：delayフラグがtrue かつ 初回実行が完了している場合
            if (state.delayPositionUntilAreaExit && isInitialStateApplied)
            {
                // 遅延実行する場合：目標位置を保存し、保留フラグを立てる
                pendingPosition = state.position;
                isPositionChangePending = true;
            }
            else
            {
                // 即時実行する場合（初回適用時 または delayフラグがfalseの時）
                targetObject.transform.position = state.position;
                // もし保留中の移動があった場合は、それをキャンセルする
                isPositionChangePending = false;
            }
        }

        // 【アニメーションステートの再生】
        // changeAnimationフラグがtrueの場合のみ、アニメーション関連の処理を行う
        if (state.changeAnimation)
        {
            // アニメーションステート名が指定されている場合のみ、チェックと再生処理に進む
            if (!string.IsNullOrEmpty(state.animationStateName))
            {
                //エラーチェック1: Animatorコンポーネントの存在を確認
                if (targetAnimator == null)
                {
                    // Animatorがないのにアニメーションを再生しようとした場合は、エラーを出す
                    Debug.LogError(
                        $"アニメーションステート '{state.animationStateName}' を再生しようとしましたが、"
                            + $"ターゲットオブジェクト '{targetObject.name}' にAnimatorコンポーネントがアタッチされていません。",
                        targetObject
                    );
                }
                else
                {
                    //エラーチェック2: 指定されたアニメーションステートがAnimator内に存在するかを確認
                    // Animator.HasStateはパフォーマンスのために文字列ではなくハッシュ値で比較するため、文字列をハッシュ値に変換する
                    int stateHash = Animator.StringToHash(state.animationStateName);

                    // HasState(レイヤー番号, ステートのハッシュ値) で存在をチェック
                    if (targetAnimator.HasState(0, stateHash))
                    {
                        // 全てのチェックを通過した場合：アニメーションを再生
                        if (state.randomizeAnimationStart)
                        {
                            // 0.0(0%) から 1.0(100%) の間でランダムな開始時間を生成
                            float randomNormalizedTime = UnityEngine.Random.Range(0f, 1f);

                            // Play(ステートのハッシュ, レイヤー番号, 正規化された開始時間) を使って再生
                            targetAnimator.Play(stateHash, 0, randomNormalizedTime);
                        }
                        else
                        {
                            // ランダム化しない場合は通常通り最初から再生
                            targetAnimator.Play(stateHash); // 文字列よりハッシュで渡す方がわずかに高速です
                        }
                    }
                    else
                    {
                        // Animator内に指定された名前のステートが存在しない場合は、エラーを出す
                        Debug.LogError(
                            $"Animator Controllerに '{state.animationStateName}' という名前のアニメーションステートが見つかりません。"
                                + $"ターゲットオブジェクト '{targetObject.name}' のAnimator設定を確認してください。",
                            targetObject
                        );
                    }
                }
            }
            // animationStateNameが空の場合は、再生するものがないので何もしない（これはエラーではない）
        }

        // // 【コライダーの状態】
        // if (state.changeColliderState && targetCollider != null && targetCollider.enabled != state.isColliderEnabled)
        // {
        //     targetCollider.enabled = state.isColliderEnabled;
        // }

        // // 【サウンド再生】
        // if (state.playSound && audioSource != null && state.soundToPlay != null)
        // {
        //     audioSource.PlayOneShot(state.soundToPlay);
        // }

        // 【UnityEventの実行】
        if (state.invokeUnityEvent)
        {
            state.onStateApply?.Invoke();
        }

        // 最初の状態適用が完了したことを記録する
        isInitialStateApplied = true;
    }
}
