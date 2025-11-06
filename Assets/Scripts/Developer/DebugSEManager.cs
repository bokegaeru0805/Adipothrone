using CriWare;
using CriWare.Assets;
using UnityEngine;

/// <summary>
/// CRIWARE (ADX2) を使用してSEを管理するクラス。
/// </summary>
public class DebugSEManager : MonoBehaviour
{
    [Header("SEのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset seAcbAsset;

    public static DebugSEManager instance { get; private set; }
    private CriAtomExPlayer player;

    private void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }
    }

    private void Start()
    {
        // プレイヤーは最初に一度だけ生成し、使い回す
        player = new CriAtomExPlayer();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            Stop();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            Play("BGM");
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            Play("HitTarget");
        }
    }

    /// <summary>
    /// 指定したSEを再生します。
    /// </summary>
    public void Play(string seName)
    {
        // SE名が空の場合は何もしない
        if (string.IsNullOrEmpty(seName))
        {
            return;
        }

        player.SetCue(seAcbAsset.Handle, seName);
        player.Start();
    }

    /// <summary>
    /// 指定したSEを再生します。
    /// </summary>
    public void Stop()
    {
        player.Stop(false);
    }
}
