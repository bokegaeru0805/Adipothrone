using System;
using System.Collections;
using MyGame.CameraControl;
using NaughtyAttributes;
using UnityEditor.EditorTools;
using UnityEngine;

/// <summary>
/// HPを持ち、ダメージを受けることができる全てのキャラクターの基本となる抽象クラス。
/// HPの増減、被弾時の共通エフェクトやサウンド、死亡判定の基本フローなど、
/// 敵とボスで完全に共通する機能のみを定義します。
/// </summary>
public abstract class CharacterHealth : PoolableObject, IDamageable, IDroppable, IDefeatable
{
    // --- シェーダープロパティ・キーワード名 ---
    private const string SHADER_PROP_FLASH_AMOUNT = "_FlashAmount";
    private const string SHADER_PROP_OVERLAY_ON = "_OverlayOn";
    private const string SHADER_KEYWORD_OVERLAY_ON = "_OVERLAY_ON";

    // --- プロパティ（継承先クラスから読み書き可能） ---
    public int MaxHP { get; protected set; }
    public int CurrentHP { get; protected set; }
    public bool IsDefeated { get; protected set; }
    public float EncounterStartTime { get; private set; }

    [Header("シールド連携設定")]
    [Tooltip("【受信側】シールド機能を有効にするか")]
    [SerializeField]
    private bool enableShield = false;

    [Tooltip("【受信側】自分自身のシールドを管理するコントローラー（ボスなどが設定）")]
    [SerializeField, ShowIf(nameof(enableShield))]
    protected ShieldController myShieldController;

    [Tooltip("【発信側】死亡時にシールド破壊を通知するか（雑魚敵などが設定）")]
    [SerializeField]
    protected bool linkToShieldController = false;

    [Tooltip("【発信側】破壊通知を送る対象のシールドコントローラー")]
    [SerializeField, ShowIf(nameof(linkToShieldController))]
    protected ShieldController targetShieldController;

    /// <summary>
    /// HPが変動した際にUIなどに通知するためのイベント。
    /// </summary>
    public event Action<int> OnHPChanged;

    /// <summary>
    /// 派生クラスから安全にOnHPChangedイベントを発火させるためのメソッド。
    /// </summary>
    protected void InvokeHPChangedEvent()
    {
        OnHPChanged?.Invoke(CurrentHP);
    }

    /// <summary>
    /// このキャラクターのレベルを取得します。EnemyDataから参照されます。
    /// </summary>
    public int Level => enemyData != null ? enemyData.requiredLevel : 0;

    // --- Inspector設定（継承先クラスで利用） ---
    [Tooltip("キャラクターの基本データを設定します")]
    [SerializeField]
    protected EnemyData enemyData;

    [Tooltip("このキャラクターがオーバーレイテクスチャ効果を使用するかどうか")]
    [SerializeField]
    private bool enableOverlayTexture = false;

    // --- 内部参照（継承先クラスで利用） ---
    protected SpriteRenderer spriteRenderer;
    protected Material material;
    protected Color col;
    protected Animator animator;

    // --- 被弾エフェクト設定 ---
    //この面積（ピクセル単位）を超えたら大きいと判断し、フラッシュを弱くします
    private float largeSpriteAreaThreshold = 50000f; // 例: 約223x223ピクセル

    private float normalFlashAmount = 0.5f; //通常時のフラッシュの明るさ
    private float reducedFlashAmount = 0.25f; //大きいスプライト用の、抑えめのフラッシュの明るさ

    private bool isLargeSprite = false; // Awakeでサイズを判定して設定するフラグ

    /// <summary>
    /// コンポーネントが有効になった際の初期化処理。
    /// 派生クラスで必要なコンポーネントをキャッシュする土台となります。
    /// virtual: 派生クラスでこの処理を上書き（拡張）できます。
    /// </summary>
    protected virtual void Awake()
    {
        // 描画用のコンポーネントを取得し、初期色を保存
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"{this.gameObject.name}にSpriteRendererがアタッチされていません");
            return;
        }
        else
        {
            col = spriteRenderer.color;
            material = spriteRenderer.material;
        }

        if (enableShield && myShieldController == null)
        {
            myShieldController = GetComponent<ShieldController>();
            if (myShieldController == null)
            {
                Debug.LogError(
                    $"[{this.gameObject.name}] シールド機能が有効ですが、同じオブジェクトにShieldControllerが見つかりませんでした。"
                );
            }
        }

        animator = GetComponent<Animator>();

        // オーバーレイテクスチャ効果の初期設定
        SetOverlayEnabled(enableOverlayTexture);

        // スプライトの画面上でのサイズを計算し、大きいかどうかを判定する
        CalculateSpriteScreenSize();
    }

    protected virtual void OnEnable()
    {
        EncounterStartTime = Time.time; // 有効化された時間を記録
    }

    /// <summary>
    /// ダメージ処理の全体の流れを定義するテンプレートメソッド。
    /// </summary>
    public virtual void Damage(int damage)
    {
        // 処理実行前の共通ガード節
        if (IsDefeated || Time.timeScale <= 0)
            return;

        // --- Step 1: ダメージ適用前の共通処理 ---
        TimeManager.instance.TriggerHitStop(); //ヒットストップを行う
        CameraManager.instance.PlayHitShake(); //カメラ揺れを行う

        // シールドによるダメージ軽減計算
        if (myShieldController != null)
        {
            // シールドコントローラーに計算を依頼し、軽減後のダメージを受け取る
            damage = myShieldController.CalculateDamageAfterShield(damage);

            // （オプション）シールドで0ダメージになった場合の演出分岐などをここに書いても良い
        }
        //

        // --- Step 2: HPの減算 ---
        CurrentHP -= damage;

        // HPが変動したことを外部に通知する
        OnHPChanged?.Invoke(CurrentHP);

        // --- Step 3: ダメージ適用"後"の、派生クラス独自の処理を呼び出すフック ---
        OnDamageApplied();

        // --- Step 4: 共通の被弾エフェクト ---
        StartCoroutine(FlashOnDamage());

        // --- Step 5: 死亡判定の、派生クラス独自の処理を呼び出すフック ---
        CheckForDeath();
    }

    /// <summary>
    /// [フック] ダメージが適用された直後に呼ばれる仮想メソッド。
    /// 派生クラスはこれを上書きして、HPバーの更新など固有の処理を追加できます。
    /// </summary>
    protected virtual void OnDamageApplied()
    {
        // 基本クラスでは何もしない
    }

    /// <summary>
    /// [フック] 死亡判定を行うための仮想メソッド。
    /// 派生クラスはこれを上書きして、特別な死亡条件を追加できます。
    /// </summary>
    protected virtual void CheckForDeath()
    {
        // 基本的な死亡判定
        if (CurrentHP <= 0)
        {
            HandleDeathFlow();
        }
    }

    /// <summary>
    /// 死亡時の共通フローを管理します。HPが0になった際にDamageメソッドから呼び出されます。
    /// </summary>
    protected void HandleDeathFlow()
    {
        // 多重実行を防ぐため、一度だけ実行
        if (IsDefeated)
            return;
        IsDefeated = true;

        // 自分がシールドとリンクしている場合、対象のシールドを破壊する
        if (linkToShieldController && targetShieldController != null)
        {
            // 自分自身(this)を渡して、対応するシールドを割ってもらう
            targetShieldController.BreakSpecificShield(this.gameObject);
        }

        //討伐記録をセーブデータに反映する処理を呼び出す
        RecordDefeat();

        // 共通の死亡時処理
        this.tag = GameConstants.UNTAGGED_TAG_NAME; // 敵として認識されなくなるようタグを変更
        DropOnDeathHandler.Drop(this); // アイテムドロップ処理を呼び出す

        // 固有の死亡演出を呼び出す（中身は継承先クラスで実装）
        OnDeath();
    }

    /// <summary>
    /// この敵が討伐されたことをセーブデータに記録します。
    /// </summary>
    private void RecordDefeat()
    {
        // GameManagerと、この敵のEnemyDataが正しく設定されているかを確認
        if (GameManager.instance != null && enemyData != null)
        {
            // GameManager経由でセーブデータにアクセスし、討伐数を1加算する
            GameManager.instance.savedata.EnemyRecordData.AddKillCount(enemyData.enemyID);
        }
        else
        {
            Debug.LogWarning(
                "GameManagerまたはEnemyDataが見つからないため、討伐数を記録できませんでした。"
            );
        }
    }

    /// <summary>
    /// 派生クラスで固有の死亡演出を実装するための抽象メソッド。
    /// abstract: このクラスを継承するクラスは、必ずこのメソッドを実装しなければなりません。
    /// </summary>
    protected abstract void OnDeath();

    /// <summary>
    /// 被弾時にキャラクターを点滅させる共通のコルーチン。
    /// 色の明度（V値）に応じて、白く光るか半透明になるかの演出を切り替えます。
    /// </summary>
    protected IEnumerator FlashOnDamage()
    {
        if (spriteRenderer == null)
            yield break;

        Material mat = spriteRenderer.material;

        if (mat.HasProperty(SHADER_PROP_FLASH_AMOUNT))
        {
            // isLargeSpriteフラグに応じて、使用するフラッシュの明るさを決定
            float flashAmountToUse = isLargeSprite ? reducedFlashAmount : normalFlashAmount;

            try
            {
                // 決定した明るさでフラッシュさせる
                mat.SetFloat(SHADER_PROP_FLASH_AMOUNT, flashAmountToUse);
                yield return new WaitForSeconds(0.1f);
            }
            finally
            {
                mat.SetFloat(SHADER_PROP_FLASH_AMOUNT, 0.0f);
            }
        }
        else
        {
            Debug.LogWarning("マテリアルに '_FlashAmount' プロパティが存在しません。", this);
        }
    }

    /// <summary>
    /// スプライトの画面上での実際のサイズを計算し、isLargeSpriteフラグを設定します。
    /// </summary>
    private void CalculateSpriteScreenSize()
    {
        // カメラとスプライトがなければ計算不可
        if (Camera.main == null || spriteRenderer.sprite == null)
            return;

        // Orthographicカメラ（2Dで一般的）を前提として計算
        if (Camera.main.orthographic)
        {
            // 1ワールド単位あたりのピクセル数を計算
            float pixelsPerUnit = Screen.height / (Camera.main.orthographicSize * 2);

            // スプライトのワールド座標でのサイズを取得（transform.scaleも考慮される）
            Vector2 spriteWorldSize = spriteRenderer.bounds.size;

            // ピクセル単位でのサイズに変換
            float spriteWidthPixels = spriteWorldSize.x * pixelsPerUnit;
            float spriteHeightPixels = spriteWorldSize.y * pixelsPerUnit;

            // ピクセル単位での面積を計算
            float spriteArea = spriteWidthPixels * spriteHeightPixels;

            // 閾値と比較してフラグを設定
            isLargeSprite = spriteArea > largeSpriteAreaThreshold;

            // // デバッグ用に計算結果を出力
            // Debug.Log(
            //     $"[{this.gameObject.name}] Sprite Area: {spriteArea:F0} pixels. Is large? -> {isLargeSprite}",
            //     this
            // );
        }
        else
        {
            // Perspectiveカメラの場合の計算はより複雑になるため、ここでは警告を出す
            Debug.LogWarning(
                $"[{this.gameObject.name}] はPerspectiveカメラを使用しています。スプライトサイズの計算が不正確になる可能性があります。"
            );
        }
    }

    //元の点滅処理（参考用）
    // protected IEnumerator FlashOnDamage()
    // {
    //     if (spriteRenderer == null) yield break;

    //     // --- 1. 現在の色をHSVに変換し、V値（明度）を取得 ---
    //     Color.RGBToHSV(spriteRenderer.color, out float h, out float s, out float v);

    //     // 元の不透明度を保存しておく
    //     float originalAlpha = spriteRenderer.color.a;
    //     // 元の色（HSV）を保存しておく
    //     Color originalColor = spriteRenderer.color;

    //     // --- 2. V値（明度）が最大かどうかで処理を分岐 ---
    //     // わずかな誤差を許容するため、0.99fより小さいかで判定
    //     if (v < 0.99f)
    //     {
    //         // 【V値が最大でない場合】-> 一瞬、白く光らせる（V値を最大にする）

    //         // a. V値を最大(1.0f)にした色を計算
    //         Color flashColor = Color.HSVToRGB(h, s, 1.0f);
    //         flashColor.a = originalAlpha; // 不透明度は維持

    //         // b. 一瞬だけ色を差し替え
    //         spriteRenderer.color = flashColor;
    //         yield return new WaitForSeconds(0.1f);

    //         // c. 元の色に戻す
    //         spriteRenderer.color = originalColor;
    //         yield return new WaitForSeconds(0.1f);
    //     }
    //     else
    //     {
    //         // 【V値がすでに最大に近い場合（白など）】-> 従来通り、半透明にする

    //         // a. 一瞬暗く（半透明に）する
    //         Color transparentColor = originalColor;
    //         transparentColor.a = originalAlpha * 0.2f; // 80%カット
    //         spriteRenderer.color = transparentColor;
    //         yield return new WaitForSeconds(0.1f);

    //         // b. 元の不透明度に戻す
    //         spriteRenderer.color = originalColor;
    //         yield return new WaitForSeconds(0.1f);
    //     }
    // }

    // --- ヘルパーメソッド ---
    /// <summary>
    /// 指定時間後にこのゲームオブジェクトを非アクティブ化します。
    /// </summary>
    /// <param name="time">非アクティブ化までの待機時間</param>
    protected virtual IEnumerator DeactivateAfterTime(float time)
    {
        yield return new WaitForSeconds(time);

        // プールタグが設定されていればプールに戻す
        if (!string.IsNullOrEmpty(myPoolTag))
        {
            ReturnToPool(); // PoolableObjectのメソッドを呼び出す
        }
        else
        {
            this.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Animatorに指定したパラメーターが存在するかを確認します。
    /// </summary>
    /// <param name="paramName">確認したいパラメーター名</param>
    /// <returns>存在する場合はtrue、存在しない場合はfalseを返します。</returns>
    protected bool HasParameter(string paramName)
    {
        if (animator == null)
            return false;
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 現在のHPの割合を0.0f～1.0fの範囲で取得します。
    /// UIの更新やAIの条件分岐などに使用します。
    /// </summary>
    public float NormalizedHP
    {
        get
        {
            // MaxHPが0以下の場合に、ゼロ除算エラーを防ぐためのチェック
            if (MaxHP <= 0)
            {
                return 0f;
            }

            // CurrentHPとMaxHPはどちらも整数(int)なため、
            // そのまま割り算すると小数点以下が切り捨てられてしまいます。(例: 50 / 100 = 0)
            // (float)とキャスト（型変換）することで、正しい小数点の割合(0.5)を算出します。
            return (float)CurrentHP / MaxHP;
        }
    }

    /// <summary>
    /// シェーダーのオーバーレイ機能を有効または無効にします。
    /// このメソッドを呼び出すと、このオブジェクトに割り当てられたマテリアルが複製され、設定が独立します。
    /// </summary>
    /// <param name="isEnabled">trueでオーバーレイを有効化、falseで無効化します。</param>
    public void SetOverlayEnabled(bool isEnabled)
    {
        if (material == null)
        {
            Debug.LogError("マテリアルが見つかりません。");
            return;
        }

        if (material.HasProperty(SHADER_PROP_OVERLAY_ON))
        {
            if (isEnabled)
            {
                // プロパティの数値を1（On）にする
                if (material.HasProperty(SHADER_PROP_OVERLAY_ON))
                {
                    material.SetFloat(SHADER_PROP_OVERLAY_ON, 1.0f);
                }
                // シェーダーのキーワードを有効化する（これをしないと描画ロジックが動かない）
                material.EnableKeyword(SHADER_KEYWORD_OVERLAY_ON);
            }
            else
            {
                // プロパティの数値を0（Off）にする
                if (material.HasProperty(SHADER_PROP_OVERLAY_ON))
                {
                    material.SetFloat(SHADER_PROP_OVERLAY_ON, 0.0f);
                }
                // シェーダーのキーワードを無効化する
                material.DisableKeyword(SHADER_KEYWORD_OVERLAY_ON);
            }
        }
        else
        {
            if (isEnabled)
            {
                Debug.LogWarning(
                    "マテリアルに '"
                        + SHADER_PROP_OVERLAY_ON
                        + "' プロパティが存在しません。シェーダーを確認してください。",
                    this
                );
            }
        }
    }

    #region Interface Implementations
    // --- インターフェースの共通実装 ---
    public EnemyData GetEnemyData() => enemyData;

    public Vector3 GetDropPosition() => transform.position;

    public virtual Transform GetDropParent() => transform.parent;
    #endregion
}
