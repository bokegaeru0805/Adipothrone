using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敵（CharacterHealth）のHPバーUIを制御します。
/// このスクリプトは、HPバーのルートオブジェクト（Canvasなど）にアタッチし、
/// 親オブジェクトに CharacterHealth があることを前提とします。
/// </summary>
public class CharacterHealthUIController : MonoBehaviour
{
    [Header("UIコンポーネント")]
    [SerializeField]
    [Tooltip("HPバー全体のUIオブジェクト（表示/非表示の切り替え用）")]
    private GameObject hpBarRootObject;

    [SerializeField]
    [Tooltip("HPの量を表すImageコンポーネント（Fill Amount用）")]
    private Image fillImage;

    private float fillTweenDuration = 0.2f; //HPバーが変化する際のアニメーション時間（秒）
    private float hideDelay = 2.5f; //HP変更後、UIを非表示にするまでの待機時間（秒）

    // 親から取得する CharacterHealth の参照
    private CharacterHealth characterHealth;

    // 実行中のDOTweenアニメーションの参照
    private Tween fillTween;

    /// 実行中の「非表示タイマー」コルーチンの参照
    private Coroutine hideCoroutine = null;

    private void Awake()
    {
        // 親オブジェクトから CharacterHealth コンポーネントを取得
        characterHealth = GetComponentInParent<CharacterHealth>();

        // --- コンポーネントのNullチェック ---
        if (characterHealth == null)
        {
            Debug.LogError(
                $"親オブジェクトに CharacterHealth が見つかりませんでした。HPバーは機能しません。",
                this
            );
            // エラーの場合はHPバー自体を非表示にする
            if (hpBarRootObject != null)
            {
                hpBarRootObject.SetActive(false);
            }
            return;
        }

        if (hpBarRootObject == null)
        {
            Debug.LogError("hpBarRootObject がインスペクターで設定されていません。", this);
        }

        if (fillImage == null)
        {
            Debug.LogError("fillImage がインスペクターで設定されていません。", this);
        }

        if (hpBarRootObject != null)
        {
            hpBarRootObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // CharacterHealth が正常に見つかっている場合のみ実行
        if (characterHealth != null)
        {
            // 1. HP変動イベントを購読（登録）
            characterHealth.OnHPChanged += HandleHPChanged;

            // 2. 初期HPをFillAmountに（アニメーションなしで）設定
            UpdateFillAmount(1f, 0f);
        }
    }

    private void OnDisable()
    {
        // CharacterHealth が存在する場合のみ
        if (characterHealth != null)
        {
            //オブジェクトが無効化される際に、イベント購読を解除（必須）
            characterHealth.OnHPChanged -= HandleHPChanged;
        }

        // 実行中のDOTweenアニメーションが残っている場合は、安全のために停止
        if (fillTween != null && fillTween.IsActive())
        {
            fillTween.Kill();
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        // 親オブジェクトが非アクティブになった（エリアを出た）際、
        // HPバーの表示状態も強制的にオフにリセットする。
        // これにより、次にエリアに入って敵が再表示された際、HPバーは隠れた状態からスタートする。
        if (hpBarRootObject != null)
        {
            hpBarRootObject.SetActive(false);
        }
    }

    /// <summary>
    /// CharacterHealth.OnHPChanged イベントから呼び出されるメソッド
    /// </summary>
    /// <param name="newHP">（CharacterHealthから渡されるが、このスクリプトでは使わない）</param>
    private void HandleHPChanged(int newHP)
    {
        if (characterHealth == null)
            return;

        //以前の「非表示タイマー」が動いていたら、停止する
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        // HPバー本体を表示する
        if (hpBarRootObject != null && !hpBarRootObject.activeSelf)
        {
            hpBarRootObject.SetActive(true);
        }

        //  HPバーの FillAmount をDOTweenで更新する
        float newNormalizedHP = characterHealth.NormalizedHP;
        UpdateFillAmount(newNormalizedHP, fillTweenDuration);

        // HPが0より大きい場合（生きている場合）のみ、新しい「非表示タイマー」を開始する
        if (newNormalizedHP > 0f)
        {
            hideCoroutine = StartCoroutine(HideBarAfterDelay());
        }
        else
        {
            // もしHPが0になった（倒された）場合は、即座に非表示にする
            hpBarRootObject.SetActive(false);
        }
    }

    /// <summary>
    /// HPバーのFillAmountのみを更新します (表示/非表示は制御しません)
    /// </summary>
    private void UpdateFillAmount(float targetAmount, float duration)
    {
        if (fillImage != null)
        {
            if (fillTween != null && fillTween.IsActive())
            {
                fillTween.Kill();
            }

            fillTween = fillImage.DOFillAmount(targetAmount, duration).SetEase(Ease.OutQuart);
        }
    }

    /// <summary>
    /// 指定した hideDelay 時間後にHPバーを非表示にするコルーチン
    /// </summary>
    private IEnumerator HideBarAfterDelay()
    {
        // Time.timeScale の影響を受ける
        yield return new WaitForSeconds(hideDelay);

        // 待機後、HPバーを非表示にする
        if (hpBarRootObject != null)
        {
            hpBarRootObject.SetActive(false);
        }

        // タイマーが完了したことを記録
        hideCoroutine = null;
    }
}
