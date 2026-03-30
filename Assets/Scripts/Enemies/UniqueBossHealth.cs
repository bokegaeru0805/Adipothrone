using System;
using System.Collections;
using System.Linq;
using UnityEngine;

#region ユニークボスHP管理クラス
/// <summary>
/// 特定のイベントで戦闘が開始される、ユニークなボスのHPを管理するクラス。
/// ActivateBattle()メソッドが呼ばれるまで、ダメージを受け付けないのが特徴です。
/// 死亡時や戦闘開始時の「演出（アニメーションやBGM）」は、別クラス（UniqueBossPresentation）に委譲しています。
/// </summary>
public class UniqueBossHealth : CharacterHealth, IEnemyResettable
{
    #region フィールド・プロパティ
    // --- 内部コンポーネント参照 ---
    private Rigidbody2D rbody;
    private Transform dropParent;

    // Rigidbodyの制御を行うかどうか
    private bool shouldControlRigidbody = true;

    /// <summary>
    /// ボス戦が開始され、ダメージを受け付けられる状態かどうかを管理するフラグ。
    /// ActivateBattle()が呼ばれるとtrueになります。
    /// </summary>
    private bool isBattleActive = false;
    #endregion

    #region イベント定義

    public event Action OnBattleActivated; //ボス戦が開始された瞬間に発行されるイベント。
    public event Action OnReset; //ボスの状態がリセットされた瞬間に発行されるイベント。
    #endregion

    #region 初期化・ライフサイクル
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
    #endregion

    #region 戦闘制御ロジック
    /// <summary>
    /// ボスの状態を戦闘開始前の初期状態に戻します。
    /// オブジェクトプールなどで再利用する際に使用します。
    /// </summary>
    public void ResetState()
    {
        IsDefeated = false;
        CurrentHP = MaxHP;
        ResetColor(); // 色と透明度を完全に戻す

        // Rigidbodyの制御が有効な場合のみ、物理挙動を再び有効化
        if (shouldControlRigidbody && rbody != null)
        {
            rbody.isKinematic = false; // 物理挙動を再び有効化
        }

        // 戦闘状態フラグをリセット
        isBattleActive = false;

        // リセットされたことを他のスクリプト（演出クラスなど）に通知
        OnReset?.Invoke();
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
        GameUIManager.instance?.SetGameUIBossData(this.gameObject);
        InvokeHPChangedEvent(); // HPバーを満タン表示にする

        // 戦闘開始を他のスクリプト（演出クラスなど）に通知
        OnBattleActivated?.Invoke();
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
    /// ユニークボス固有の死亡処理。
    /// 演出関連の処理は省き、純粋なシステム上の「死亡状態の確定」のみを行います。
    /// </summary>
    protected override void OnDeath()
    {
        // Rigidbodyの制御が有効な場合のみ、物理挙動を停止
        if (shouldControlRigidbody && rbody != null)
        {
            rbody.velocity = Vector2.zero;
            rbody.isKinematic = true;
        }

        // 死亡時に色を元に戻す
        ResetColor();
    }

    /// <summary>
    /// Rigidbody2Dの物理挙動をこのスクリプトで制御するかどうかを設定します。
    /// </summary>
    /// <param name="shouldControl">制御する場合はtrue、しない場合はfalse</param>
    public void SetRigidbodyControl(bool shouldControl)
    {
        this.shouldControlRigidbody = shouldControl;
    }
    #endregion

    /// <summary>
    /// 親クラス（CharacterHealth）が自動で非アクティブ化するのを防ぐための空のオーバーライド。
    /// 実際の非アクティブ化は、演出クラス（UniqueBossPresentation）がアニメーション終了時に行います。
    /// </summary>
    protected override IEnumerator DeactivateAfterTime(float time)
    {
        // 親クラスの処理を完全に無視し、何もしない（ここで勝手に消えるのを防ぐ）
        yield break;
    }
}
#endregion
