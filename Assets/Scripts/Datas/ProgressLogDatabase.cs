using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgressLogDatabase", menuName = "Game/ProgressLogDatabase")]
public class ProgressLogDatabase : ScriptableObject
{
    public List<ProgressLogInfoData> logs;

    private Dictionary<ProgressLogName, ProgressLogInfoData> _lookup;

    private void Initialize()
    {
        _lookup = logs.ToDictionary(l => l.logName, l => l);
    }

    /// <summary>
    /// 指定された進行ログID（int値）から、対応する ProgressLogInfoData を取得します。
    /// IDは ProgressLogName にキャストされ、対応するログが存在しない場合は null を返します。
    /// 必要に応じて初期化処理（Initialize）も自動で行います。
    /// </summary>
    public ProgressLogInfoData Get(int index)
    {
        ProgressLogName name = (ProgressLogName)index;
        if (_lookup == null)
            Initialize();
        return _lookup.TryGetValue(name, out var data) ? data : null;
    }
}
