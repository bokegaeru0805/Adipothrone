using UnityEngine;

/// <summary>
/// セーブスロットに依存しないデバッグ機能の設定を保持します。
/// </summary>
[System.Serializable]
public class DebugSettingsSaveData
{
    public bool isShowEventArea = false;
    public bool isMouseDamageEnabled = false;
    public float mouseDamagePercent = 25f;
    public bool isPlayerInvincible = false;
    public float debugTimeScale = 1f;

    /// <summary>
    /// 読み込んだ設定値を有効範囲内へ補正します。
    /// </summary>
    public void Validate()
    {
        mouseDamagePercent = Mathf.Clamp(mouseDamagePercent, 0f, 100f);
        debugTimeScale = Mathf.Clamp(debugTimeScale, 0.1f, 10f);
    }
}
