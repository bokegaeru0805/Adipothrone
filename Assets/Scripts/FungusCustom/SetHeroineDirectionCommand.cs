using Fungus;
using UnityEngine;

/// <summary>
/// ヒロインの向き（左右）を変更する専用のFungusコマンド。
/// Flowchart上で手軽に向きを制御できるようにします。
/// </summary>
[CommandInfo("Heroine", "Set Heroine Direction", "ヒロインの向き（左右）を変更します。")]
[AddComponentMenu("")]
public class SetHeroinDirectionCommand : Command
{
    public enum FacingDirection
    {
        Left,
        Right
    }

    [Header("Target & Settings")]
    [Tooltip("向きを変更する対象のHeroin_moveコンポーネント。未指定の場合はPlayerManager等から自動的に検索します。")]
    [SerializeField]
    protected Heroin_move targetHeroin;

    [Tooltip("向かせたい方向")]
    [SerializeField]
    protected FacingDirection direction = FacingDirection.Right;

    public override void OnEnter()
    {
        Heroin_move heroin = targetHeroin;

        // 対象がInspectorで直接アタッチされていない場合は自動検索
        if (heroin == null)
        {
            // PlayerManagerが存在する場合はそこから取得
            if (PlayerManager.instance != null && PlayerManager.instance.PlayerGameObject != null)
            {
                heroin = PlayerManager.instance.PlayerGameObject.GetComponent<Heroin_move>();
            }
            
            // それでも見つからない場合はシーン全体から検索
            if (heroin == null)
            {
                heroin = FindObjectOfType<Heroin_move>();
            }
        }

        if (heroin != null)
        {
            // 向きたい方向をbool値に変換
            bool isRight = (direction == FacingDirection.Right);
            
            // Heroin_moveのメソッドを呼び出す
            // ※もしHeroin_move側のSetFacingDirectionが引数にEnumなどを取る仕様の場合は、適宜書き換えてください
            heroin.SetFacingDirection(isRight);
        }
        else
        {
            Debug.LogWarning("SetHeroinDirectionCommand: シーン内にHeroin_moveが見つかりませんでした。");
        }

        // 処理が終わったら次のコマンドへ進む
        Continue();
    }

    public override string GetSummary()
    {
        string targetName = targetHeroin != null ? targetHeroin.gameObject.name : "Heroin (Auto)";
        string dirStr = direction == FacingDirection.Right ? "右 (Right)" : "左 (Left)";
        return $"{targetName} を {dirStr} に向かせる";
    }

    public override Color GetButtonColor()
    {
        return new Color32(160, 180, 250, 255);
    }
}