using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "TipsDatabase", menuName = "Game/TipsDatabase")]
public class TipsInfoDatabase : ScriptableObject
{
    public List<TipsInfoData> tips;

    private Dictionary<TipsName, TipsInfoData> _lookup;

    private void Initialize()
    {
        _lookup = tips.ToDictionary(t => t.tipsName, t => t);
    }

    /// <summary>
    /// 指定された進行ログID（int値）から、対応する TipsInfoData を取得します。
    /// IDは TipsName にキャストされ、対応するログが存在しない場合は null を返します。
    /// 必要に応じて初期化処理（Initialize）も自動で行います。
    /// </summary>
    public TipsInfoData Get(Enum id)
    {
        if (id is TipsName tipsName)
        {
            if (_lookup == null)
                Initialize();
            return _lookup.TryGetValue(tipsName, out var data) ? data : null;
        }

        return null;
    }
}