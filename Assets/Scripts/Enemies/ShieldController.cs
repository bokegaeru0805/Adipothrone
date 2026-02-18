using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

[System.Serializable]
public class ShieldData
{
    [Tooltip("このシールドによるダメージ軽減率（0.1 = 10%軽減）。加算方式で計算されます。")]
    [Range(0f, 1f)]
    public float reductionPercentage = 0.2f;

    [Tooltip(
        "一対一対応モードの場合にリンクするオブジェクト（敵、スイッチ、破壊可能オブジェクトなど）"
    )]
    public GameObject linkedObject;
}

/// <summary>
/// シールドによるダメージ軽減、破壊ロジック、および視覚効果（パーティクル）を管理するクラス。
/// </summary>
public class ShieldController : MonoBehaviour
{
    #region Settings

    [Header("シールド設定")]
    [Tooltip("シールド機能を有効にするか")]
    [SerializeField]
    private bool isShieldActive = true;

    [Tooltip("True: 特定の敵とシールドをリンクさせる\nFalse: 単純にリストの末尾から消費する")]
    [SerializeField]
    private bool useOneToOneLinking = false;

    [Tooltip("現在のシールドリスト")]
    [SerializeField]
    private List<ShieldData> shieldLayers = new List<ShieldData>();

    [Header("演出設定")]
    [Tooltip("シールド演出用のパーティクル")]
    [SerializeField]
    private ParticleSystem shieldParticle;

    [Tooltip("シールド最大時の回転速度（Y軸）")]
    [SerializeField]
    private float maxRotationSpeed = 10.0f;

    [Tooltip("シールド最小時（残り1枚）の回転速度（Y軸）")]
    [SerializeField]
    private float minRotationSpeed = 2.0f;

    [Header("表示設定")]
    [Tooltip("シールドのエフェクトを表示するかどうか（Falseでもダメージ軽減は機能します）")]
    [SerializeField]
    private bool showVisuals = true;

    #endregion

    #region Internal Variables

    // 開始時の枚数（比率計算用）
    private float maxShieldCount;

    // パーティクルの初期色
    private Color baseParticleColor;

    #endregion

    #region Events

    /// <summary>
    /// シールドが1枚破壊されたときに発行されるイベント。
    /// </summary>
    public event Action OnShieldBroken;

    /// <summary>
    /// 全てのシールドが破壊された（枚数が0になった）瞬間に発行されるイベント。
    /// </summary>
    public event Action OnAllShieldsBroken;

    #endregion

    #region Public Accessors

    /// <summary>
    /// 現在のシールド枚数を取得します。
    /// </summary>
    public int CurrentShieldCount => shieldLayers.Count;

    /// <summary>
    /// 現在のシールドによる合計ダメージ軽減率（0.0f ～ 1.0f）を取得します。
    /// </summary>
    public float CurrentReductionRate
    {
        get
        {
            if (!isShieldActive || shieldLayers.Count == 0)
                return 0f;

            float total = 0f;
            foreach (var shield in shieldLayers)
            {
                total += shield.reductionPercentage;
            }
            return Mathf.Clamp01(total);
        }
    }

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        if (shieldParticle != null)
        {
            // パーティクルの初期色を保存
            baseParticleColor = shieldParticle.main.startColor.color;
        }

        // 開始時の枚数を最大値として記録（0の場合は1にして除算エラー回避）
        maxShieldCount = Mathf.Max(1, shieldLayers.Count);

        // 一対一モードのチェック
        if (useOneToOneLinking)
        {
            ValidateLinkedEnemies();
        }

        UpdateVisuals();
    }

    #endregion

    #region Logic Methods

    /// <summary>
    /// 設定の検証（一対一モードなら全てのシールドに敵が割り当てられているか確認）
    /// </summary>
    private void ValidateLinkedEnemies()
    {
        for (int i = 0; i < shieldLayers.Count; i++)
        {
            if (shieldLayers[i].linkedObject == null) // linkedEnemy -> linkedObject
            {
                Debug.LogWarning(
                    $"[ShieldController] 一対一モードですが、Index {i} のシールドにオブジェクトが割り当てられていません。",
                    this
                );
            }
        }
    }

    /// <summary>
    /// シールドを考慮したダメージ計算を行う（加算方式）
    /// </summary>
    public int CalculateDamageAfterShield(int rawDamage)
    {
        if (!isShieldActive || shieldLayers.Count == 0)
        {
            return rawDamage;
        }

        // 軽減率を加算方式で計算（例: 0.2 + 0.2 = 0.4 (40%カット)）
        // Accessorを使って現在の軽減率を取得
        float totalReduction = CurrentReductionRate;

        // ダメージ計算: ダメージ * (1 - 軽減率)
        float finalDamageFloat = rawDamage * (1.0f - totalReduction);

        // 最小1ダメージは保証するか、0にするかはゲーム性による（ここでは0も許可）
        return Mathf.FloorToInt(finalDamageFloat);
    }

    /// <summary>
    /// 外部からシールドを1枚破壊する（リストの末尾から削除）
    /// </summary>
    public void BreakShield()
    {
        if (shieldLayers.Count > 0)
        {
            // 末尾を削除
            shieldLayers.RemoveAt(shieldLayers.Count - 1);

            // イベント発行
            OnShieldBroken?.Invoke();

            if (shieldLayers.Count == 0)
            {
                OnAllShieldsBroken?.Invoke();
            }

            UpdateVisuals();
        }
    }

    /// <summary>
    /// 特定の敵にリンクしたシールドを破壊する
    /// </summary>
    public void BreakSpecificShield(GameObject sourceObject)
    {
        if (!useOneToOneLinking)
        {
            // 一対一モードでないなら、通常の破壊処理に回す
            BreakShield();
            return;
        }

        // リンクしている敵と一致するシールドを探して削除
        // 逆順に回すことで削除時のインデックスずれを防ぐ
        bool removed = false;
        for (int i = shieldLayers.Count - 1; i >= 0; i--)
        {
            if (shieldLayers[i].linkedObject == sourceObject)
            {
                shieldLayers.RemoveAt(i);
                removed = true;
                break;
            }
        }

        if (removed)
        {
            // イベント発行
            OnShieldBroken?.Invoke();

            if (shieldLayers.Count == 0)
            {
                OnAllShieldsBroken?.Invoke();
            }

            UpdateVisuals();
        }
    }

    #endregion

    #region Visuals Methods

    /// <summary>
    /// 外部からシールドの表示/非表示を切り替える
    /// </summary>
    public void SetVisualVisibility(bool isVisible)
    {
        if (showVisuals != isVisible)
        {
            showVisuals = isVisible;
            UpdateVisuals(); // 即座に反映
        }
    }

    /// <summary>
    /// シールドの枚数に応じて見た目を更新する
    /// </summary>
    private void UpdateVisuals()
    {
        if (shieldParticle == null)
            return;

        int currentCount = shieldLayers.Count;

        // 枚数が0、または「表示設定がOFF」なら非表示にする
        if (currentCount == 0 || !showVisuals)
        {
            shieldParticle.Stop();
            shieldParticle.gameObject.SetActive(false); // 完全に消す場合
            return;
        }

        if (!shieldParticle.gameObject.activeSelf)
        {
            shieldParticle.gameObject.SetActive(true);
            shieldParticle.Play();
        }

        // 現在の割合 (0.0 ～ 1.0)
        float ratio = (float)currentCount / maxShieldCount;

        // --- 1. 回転速度の変化 ---
        // 多いほど速く、少ないほど遅く
        var rotationModule = shieldParticle.rotationOverLifetime;
        rotationModule.enabled = true;

        // Y軸の回転速度を補間設定
        // Constantで設定されている前提で値を書き換える
        float currentSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, ratio);
        rotationModule.y = new ParticleSystem.MinMaxCurve(currentSpeed * Mathf.Deg2Rad);

        // --- 2. 透明度の変化 ---
        // 初期色から徐々に透明度を下げる（枚数が減るほど薄くなる）
        var mainModule = shieldParticle.main;
        Color newColor = baseParticleColor;
        newColor.a = baseParticleColor.a * ratio; // 割合に応じてアルファ値を乗算
        mainModule.startColor = newColor;
    }

    #endregion

    #region UI Methods

    /// <summary>
    /// 現在のシールド軽減率を計算し、GameUIManager経由で状況メッセージを表示します。
    /// プレイヤーが攻撃した際や、シールドの状態が変化した際に呼び出すことを想定しています。
    /// </summary>
    /// <param name="enemyName">メッセージに埋め込む敵の名前（例: "魔王"）</param>
    public void ShowShieldStatusUI(string enemyName)
    {
        if (GameUIManager.instance == null)
        {
            Debug.LogWarning("GameUIManagerが存在しないため、シールド状況を表示できません。");
            return;
        }

        // 現在の軽減率を計算
        // Accessorを使って取得
        float totalReduction = CurrentReductionRate;

        // メッセージを生成して表示
        string message = GetStatusMessage(enemyName, totalReduction);
        GameUIManager.instance.ShowSkillNameUI(message);
    }

    /// <summary>
    /// 軽減率の数値に応じて、表示するメッセージを決定します。
    /// インスペクターではなく、ここで閾値と文章を一元管理します。
    /// </summary>
    private string GetStatusMessage(string name, float reduction)
    {
        // 100%以上：完全無効化
        if (reduction >= 1.0f)
        {
            return $"{name}は完全な障壁に守られている！";
        }
        // 80%以上：非常に硬い
        else if (reduction >= 0.8f)
        {
            return $"{name}の守りは極めて堅固だ！";
        }
        // 50%以上：そこそこ硬い
        else if (reduction >= 0.5f)
        {
            return $"{name}は防壁を展開している";
        }
        // 0%より大きい：少し軽減
        else if (reduction > 0f)
        {
            return $"{name}の周囲に微弱な魔力が漂っている";
        }
        // 0%以下：シールドなし
        else
        {
            return $"{name}は無防備になった！";
        }
    }

    #endregion
}
