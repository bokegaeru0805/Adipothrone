using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 特定のイベントで戦闘が開始される、ユニークなボスのHPを管理するクラス。
/// ActivateBattle()メソッドが呼ばれるまで、ダメージを受け付けないのが特徴です。
/// </summary>
public class UniqueBossHealth : CharacterHealth, IEnemyResettable
{
    // --- 内部コンポーネント参照 ---
    private Rigidbody2D rbody;
    private Transform dropParent;
    private float deathAnimationLength = 1.0f; // 死亡アニメーションの長さ（秒）
    private float crossFadeTime = 1.0f; // BGMのクロスフェード時間（秒）
    private float returnMusicTime = 2.0f; // 戦闘終了後にBGMを戻すまでの時間（秒）
    private const string deathAnimParam = "death"; // 死亡アニメーションのパラメータ名
    private bool shouldControlRigidbody = true; // Rigidbodyの制御を行うかどうか

    /// <summary>
    /// ボス戦が開始され、ダメージを受け付けられる状態かどうかを管理するフラグ。
    /// ActivateBattle()が呼ばれるとtrueになります。
    /// </summary>
    private bool isBattleActive = false;

    /// <summary>
    /// このボスが倒された瞬間に発行されるイベント。
    /// </summary>
    public event Action OnDefeated;

    /// <summary>
    /// 基本クラスのAwakeを拡張し、コンポーネントの取得や設定を行います。
    /// </summary>
    protected override void Awake()
    {
        // まず基本クラスのAwake処理（SpriteRendererの取得など）を実行
        base.Awake();

        // このボスに必要なコンポーネントをキャッシュ
        rbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>(); // Animatorもここで取得
        dropParent = this.transform.parent;

        if (dropParent == null)
        {
            Debug.LogWarning($"{this.gameObject.name}の親オブジェクトが設定されていません。");
        }

        if (enemyData == null)
        {
            Debug.LogError($"{this.gameObject.name}のEnemyDataが設定されていません");
        }
        else
        {
            // EnemyDataから最大HPを取得
            MaxHP = enemyData.enemyHP;
        }
    }

    private void Start()
    {
        // ゲーム開始時は必ずリセットされた状態から始める
        ResetState();
    }

    /// <summary>
    /// ボスの状態を戦闘開始前の初期状態に戻します。
    /// オブジェクトプールなどで再利用する際に使用します。
    /// </summary>
    public void ResetState()
    {
        IsDefeated = false;
        CurrentHP = MaxHP;
        col.a = 1;
        spriteRenderer.color = col;

        if (animator != null && HasParameter(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, false);
        }

        // Rigidbodyの制御が有効な場合のみ、物理挙動を再び有効化
        if (shouldControlRigidbody && rbody != null)
        {
            rbody.isKinematic = false; // 物理挙動を再び有効化
        }

        // 戦闘状態フラグをリセット
        isBattleActive = false;
    }

    /// <summary>
    /// ボス戦を開始します。
    /// このメソッドが呼ばれると、HPバーが表示され、ボスがダメージを受けるようになります。
    /// </summary>
    public void ActivateBattle()
    {
        // 戦闘状態フラグを立てる
        isBattleActive = true;

        // ボスHPバーを表示させ、初期HPを通知
        GameUIManager.instance.SetGameUIBossData(this.gameObject);
        InvokeHPChangedEvent(); // HPバーを満タン表示にする

        BGMManager.instance?.Crossfade(BGMCategory.Boss_Unique, crossFadeTime);
    }

    /// <summary>
    /// ダメージ処理を上書き（override）します。
    /// 戦闘がアクティブな場合のみ、基本クラスのダメージ処理を呼び出します。
    /// </summary>
    public override void Damage(int damage)
    {
        // 戦闘が開始されていなければ、ダメージを受け付けずに処理を中断
        if (!isBattleActive)
        {
            return;
        }

        // 戦闘が開始されている場合のみ、基本クラス（CharacterHealth）のDamage処理を実行
        base.Damage(damage);
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
    /// ユニークボス固有の死亡処理。
    /// </summary>
    protected override void OnDeath()
    {
        // 自分が倒されたことを、購読している他のスクリプトに通知する
        OnDefeated?.Invoke();

        // Rigidbodyの制御が有効な場合のみ、物理挙動を停止
        if (shouldControlRigidbody && rbody != null)
        {
            rbody.velocity = Vector2.zero;
            rbody.isKinematic = true;
        }

        // 死亡時に色を元に戻す
        col.a = 1;
        spriteRenderer.color = col;

        // 死亡アニメーションの長さを自動で取得
        if (animator != null && animator.runtimeAnimatorController != null && enemyData != null)
        {
            string clipNameToFind = $"{enemyData.name}_death";
            AnimationClip deathClip =
                animator.runtimeAnimatorController.animationClips.FirstOrDefault(clip =>
                    clip.name == clipNameToFind
                );

            if (deathClip != null)
            {
                deathAnimationLength = deathClip.length;
            }
            else
            {
                Debug.LogWarning(
                    $"アニメーションクリップ '{clipNameToFind}' が見つかりませんでした。"
                );
            }
        }

        // 死亡アニメーションを再生
        if (animator != null && HasParameter(deathAnimParam))
        {
            animator.SetBool(deathAnimParam, true);
        }

        // アニメーションの長さだけ待ってからオブジェクトを非アクティブ化
        StartCoroutine(DeactivateAfterTime(deathAnimationLength));
    }

    protected override IEnumerator DeactivateAfterTime(float time)
    {
        // 1. 死亡アニメーションに合わせて現在のBGMをフェードアウト開始
        BGMManager.instance?.FadeOut(time);

        // 2. 指定された時間だけ待機する（元の親クラスの処理をここに持ってくる）
        yield return new WaitForSeconds(time);

        // 3. オブジェクトが消える「前」に、次のエリアBGMを再生する
        CameraMoveArea.PlayCurrentAreaBgm(returnMusicTime);

        // 4. 最後に自身を非アクティブ化する
        this.gameObject.SetActive(false);
    }

    /// <summary>
    /// Rigidbody2Dの物理挙動をこのスクリプトで制御するかどうかを設定します。
    /// </summary>
    /// <param name="shouldControl">制御する場合はtrue、しない場合はfalse</param>
    public void SetRigidbodyControl(bool shouldControl)
    {
        this.shouldControlRigidbody = shouldControl;
    }

    /// <summary>
    /// オブジェクトが非表示になる際のクリーンアップ処理。
    /// </summary>
    private void OnDisable()
    {
        // 戦闘状態フラグをリセット
        isBattleActive = false;
        // ボスHPバーを非表示にするようUIマネージャーに依頼
        GameUIManager.instance?.RemoveUIBossData(this.gameObject);
    }
}
