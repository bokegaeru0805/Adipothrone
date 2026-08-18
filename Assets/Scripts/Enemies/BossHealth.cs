using System;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// ボスのHPと死亡処理を管理するクラス。CharacterHealthを継承します。
/// 元のboss_HPの全ての機能を持ち、ボスHPバーとの連携や撃破後イベントなどを担当します。
/// </summary>
public class BossHealth : CharacterHealth
{
    [InfoBox(
        "BossHealthスクリプトを使用する場合、決してInitializeBossSpecifics()関数呼び出すことを忘れないでください！"
    )]
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
        DustDevilBoss = 40,
        DesertTempleBossSmoke = 50,
        DesertTempleBoss = 60,
        Apothecary = 70,
    }

    /// <summary>
    /// コンポーネントが有効になった際の初期化処理。
    /// </summary>
    protected override void Awake()
    {
        // 基本クラスのAwake処理（SpriteRendererの取得など）を実行
        base.Awake();

        if (bossname == BossName.None)
        {
            if (
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                != GameConstants.SCENE_NAME_DEBUG
            )
            {
                Debug.LogError($"{this.gameObject.name}のボス名が設定されていません", this);
            }
        }

        if (AfterDeathGameObject != null)
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
                    $"{AfterDeathGameObject.name}にBossAfterDeathスクリプトがアタッチされていません。撃破後イベントが正しく動作しません。",
                    AfterDeathGameObject
                );
            }

            AfterDeathGameObject.SetActive(false); //撃破後のゲームオブジェクトを非表示
        }
        else
        {
            Debug.LogWarning(
                $"{this.gameObject.name}のAfterDeathGameObjectが設定されていません。",
                this
            );
        }

        if (enemyData == null)
        {
            Debug.LogError($"{this.gameObject.name}のEnemyDataが設定されていません", this);
            return; // EnemyDataがないとHPを初期化できないので、以降の処理を中断
        }

        // EnemyDataから最大HPを取得
        MaxHP = enemyData.enemyHP;

        //以下の動作は、他の関数からStart関数でこのスクリプトが無効化される可能性があるため、Start関数ではなくここで行う
        IsDefeated = false;
        CurrentHP = MaxHP;
    }

    /// <summary>
    /// ボス固有の初期化（UI表示、フラグ設定など）をまとめて行います。
    /// </summary>
    public void InitializeBossSpecifics()
    {
        // --- UIの初期化 ---
        if (GameUIManager.instance != null)
        {
            GameUIManager.instance.SetGameUIBossData(this.gameObject);
            InvokeHPChangedEvent(); // HPバーを満タン表示
        }
        else
        {
            Debug.LogError(
                "GameUIManagerのインスタンスが見つかりません！ボスHPバーを初期化できません。",
                this
            );
        }

        // --- ボス固有フラグの初期化 ---
        if (FlagManager.instance != null)
        {
            switch (bossname)
            {
                case BossName.FirstBoss:
                    break;
                case BossName.SlimeBoss:
                    break;
                // 他のボスもここに追加
            }
        }
        else
        {
            Debug.LogError(
                "FlagManagerのインスタンスが見つかりません！ボス固有フラグを設定できません。",
                this
            );
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
