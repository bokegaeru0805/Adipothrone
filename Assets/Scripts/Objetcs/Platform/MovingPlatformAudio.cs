using CriWare;
using UnityEngine;

/// <summary>
/// リフトの移動音を管理するコンポーネント。
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class MovingPlatformAudio : MonoBehaviour
{
    public enum LiftType
    {
        None = 0,
        Wood = 1,
        DesertTemple = 2,
        SnowMountain = 3,
        // 必要に応じて追加
    }

    [Header("サウンド設定")]
    [Tooltip("リフトの材質タイプ（再生するキュー名を決定）")]
    [SerializeField]
    private LiftType liftType = LiftType.None;

    [Tooltip("SEをループ再生するかどうか")]
    [SerializeField]
    private bool loopSE = true;

    private CriWare.Assets.CriAtomSePlayer sePlayer;

    void Awake()
    {
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
    }

    /// <summary>
    /// 移動音を再生する
    /// </summary>
    public void PlayMoveSound()
    {
        if (sePlayer == null) return;
        
        // 既に再生中なら何もしない（ループ再生対応）
        if (loopSE && sePlayer.status == CriAtomSource.Status.Playing) return;

        switch (liftType)
        {
            case LiftType.Wood:
                sePlayer.Play(SE_Field.LiftMove_Wood);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 移動音を停止する
    /// </summary>
    public void StopMoveSound()
    {
        if (sePlayer != null && sePlayer.status == CriAtomSource.Status.Playing)
        {
            sePlayer.Stop();
        }
    }
}