using System;
using UnityEngine;

/// <summary>
/// ボスのHPと死亡処理を管理するクラス。CharacterHealthを継承します。
/// 元のboss_HPの全ての機能を持ち、ボスHPバーとの連携や撃破後イベントなどを担当します。
/// </summary>
public class BossHealth : CharacterHealth
{
    // --- ボス固有のプロパティとイベント ---
    [Header("ボス固有設定")]
    public BossName bossname; // ボスの種類を識別するためのEnum

    [SerializeField]
    private GameObject AfterDeathGameObject; // 撃破後に出現させるオブジェクト

    // ボスの種類を定義するEnum
    public enum BossName
    {
        None = 0,
        FirstBoss = 10,
        SlimeBoss = 20,
        StoneGolemBoss = 30,
    }

    /// <summary>
    /// コンポーネントが有効になった際の初期化処理。
    /// </summary>
    protected override void Awake()
    {
        // 基本クラスのAwake処理（SpriteRendererの取得など）を実行
        base.Awake();

        // --- 元のAwakeにあったエラーチェック ---
        if (bossname == BossName.None)
            Debug.LogError($"{this.gameObject.name}のボス名が設定されていません");
        if (AfterDeathGameObject == null)
        {
            Debug.LogWarning($"{this.gameObject.name}はAfterDeathGameObjectを持っていません");
        }
        else
        {
            AfterDeathGameObject.SetActive(false); //最初は非表示
            
            var BossAfterDeathScript = AfterDeathGameObject.GetComponent<BossAfterDeath>();
            if (BossAfterDeathScript != null)
            {
                BossAfterDeathScript.SetBossName(bossname);
            }
            else
            {
                Debug.LogWarning(
                    $"{AfterDeathGameObject.name}にBossAfterDeathスクリプトがアタッチされていません。撃破後イベントが正しく動作しません。"
                );
            }
        }
        if (enemyData == null)
            Debug.LogError($"{this.gameObject.name}のEnemyDataが設定されていません");

        // EnemyDataから最大HPを取得
        MaxHP = enemyData.enemyHP;
    }

    /// <summary>
    /// ゲーム開始時のボス固有のセットアップ処理。
    /// </summary>
    private void Start()
    {
        IsDefeated = false;
        CurrentHP = MaxHP;

        if (AfterDeathGameObject != null)
        {
            AfterDeathGameObject.SetActive(false); //撃破後のゲームオブジェクトを非表示
        }

        // ボスHPバーを表示させ、初期HPを通知
        GameUIManager.instance.SetGameUIBossData(this.gameObject);

        // base.OnHPChanged は protected にして、InvokeHPChangedEvent() のようなメソッドを作るのがより丁寧ですが、
        // 今回は直接イベントを呼び出します。
        InvokeHPChangedEvent(); // HPバーを満タン表示にする

        // ボスの種類に応じたフラグ管理などの初期設定
        switch (bossname)
        {
            case BossName.FirstBoss:
                FlagManager.instance.SetKeyOpened(KeyID.K4_2, false);
                break;
            case BossName.SlimeBoss:
                break;
        }
    }

    /// <summary>
    /// [フックの上書き] ダメージが適用された直後に、HPバー更新イベントを発行します。
    /// </summary>
    protected override void OnDamageApplied()
    {
        // isDefeatedになる前のHPでイベントを発行
        if (!IsDefeated)
        {
            InvokeHPChangedEvent();
        }
    }

    /// <summary>
    /// ボス固有の死亡処理。撃破後オブジェクトの有効化と自身の破壊を行います。
    /// </summary>
    protected override void OnDeath()
    {
        // 撃破後オブジェクトが設定されていれば、それを有効化する
        if (AfterDeathGameObject != null)
        {
            AfterDeathGameObject.transform.position = this.transform.position;
            // ボスの向きを撃破後オブジェクトに引き継ぐ
            bool shouldFlipX = this.gameObject.GetComponent<SpriteRenderer>().flipX;
            AfterDeathGameObject.GetComponent<SpriteRenderer>().flipX = shouldFlipX;
            AfterDeathGameObject.SetActive(true);
        }

        // ボスは再利用しないので、自身を完全に破壊する
        Destroy(this.gameObject);
    }

    /// <summary>
    /// ボスオブジェクトが破壊される際に、UIの後始末を依頼します。
    /// </summary>
    private void OnDestroy()
    {
        // GameUIManagerが存在する場合のみ、ボスHPバーを削除するよう通知
        GameUIManager.instance?.RemoveUIBossData(this.gameObject);
    }

    // ボスはドロップアイテムを親オブジェクトに生成しないので、nullを返すように上書き
    public override Transform GetDropParent() => null;
}
