using UnityEngine;
using UnityEngine.Playables;

public class HeroineMixerBehaviour : PlayableBehaviour
{
    private Heroin_move boundScript;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private bool isFirstFrame = true;
    private bool originalScriptState = true;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        boundScript = playerData as Heroin_move;

        // バインドがない場合は何もしない
        if (boundScript == null) return;

        // 初回のみコンポーネント取得とスクリプトの制御を行う
        if (isFirstFrame)
        {
            animator = boundScript.GetComponent<Animator>();
            spriteRenderer = boundScript.GetComponent<SpriteRenderer>();
            
            // Heroin_moveのUpdateが走るとアニメーションが上書きされるため、一時的に止める
            if (boundScript.enabled)
            {
                originalScriptState = true;
                boundScript.enabled = false;
                //Debug.Log("[HeroineMixerBehaviour] Disabled Heroin_move script for Timeline control.");
            }
            else
            {
                originalScriptState = false;
            }
            
            isFirstFrame = false;
        }

        int inputCount = playable.GetInputCount();
        float totalWeight = 0f;

        // 適用するパラメータ
        int targetBodyState = (int)HeroineClip.BodyStateType.UseCurrent;
        int targetAnimState = 0;
        HeroineClip.FacingType targetFacing = HeroineClip.FacingType.Keep;

        // 一番ウェイトが高いクリップの設定を採用する
        float maxWeight = -1f;

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            totalWeight += inputWeight;

            if (inputWeight > maxWeight)
            {
                var inputPlayable = (ScriptPlayable<HeroinePlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                targetBodyState = input.bodyState;
                targetAnimState = input.animState;
                targetFacing = input.facing;
                maxWeight = inputWeight;
            }
        }

        // Timelineの影響がある場合のみ適用
        if (totalWeight > 0f)
        {
            if (animator != null)
            {
                // BodyStateの決定ロジック
                int finalBodyState = targetBodyState;

                // 「現在の状態(UseCurrent)」が選択されている場合
                if (finalBodyState == (int)HeroineClip.BodyStateType.UseCurrent)
                {
                    // PlayerBodyManagerから現在の変身状態(AnimBodyState)を取得
                    if (PlayerBodyManager.instance != null)
                    {
                        finalBodyState = PlayerBodyManager.instance.AnimBodyState;
                    }
                    else
                    {
                        // プレビュー等でManagerが取れない場合のフォールバック（通常状態）
                        finalBodyState = GameConstants.ANIM_BODY_STATE_NORMAL;
                    }
                }

                // Animatorパラメータの適用
                animator.SetInteger("BodyState", finalBodyState);
                animator.SetInteger("AnimState", targetAnimState);
                
                // Timeline中は歩行速度などを一定にする
                animator.SetFloat("WalkSpeed", 1.0f);
            }

            // 向きの適用
            if (spriteRenderer != null && targetFacing != HeroineClip.FacingType.Keep)
            {
                // Heroin_moveの仕様： rightFlag=true (flipX=true) が右向き
                bool isRight = (targetFacing == HeroineClip.FacingType.Right);
                spriteRenderer.flipX = isRight;
            }
        }
        else
        {
            // クリップがない区間はスクリプトを復帰させる
            if (boundScript != null && !boundScript.enabled && originalScriptState)
            {
                boundScript.enabled = true;
            }
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        // Timeline終了時にスクリプトを元の状態に戻す
        if (boundScript != null)
        {
            if (originalScriptState)
            {
                boundScript.enabled = true;
                
                // 終了時にアニメーションをIdleに戻しておく（安全策）
                if (animator != null)
                {
                    animator.SetInteger("AnimState", 0);
                }
            }
        }
        isFirstFrame = true;
    }
}