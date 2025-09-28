using System.Collections.Generic;
using UnityEngine;
using System;


/// <summary>
/// 複数のフラグ操作をまとめて実行するための汎用コンポーネント。
/// UnityEventからExecute()を呼び出して使用する。
/// </summary>
public class FlagAction : MonoBehaviour
{
    [Tooltip("実行したいフラグ操作のリスト")]
    [SerializeField] private List<FlagOperation> operations = new List<FlagOperation>();

    /// <summary>
    /// 設定された全てのフラグ操作を実行します。
    /// </summary>
    public void ApplyFlagOperations()
    {
        if (FlagManager.instance == null)
        {
            Debug.LogError("FlagManagerのインスタンスが見つかりません。");
            return;
        }

        foreach (var op in operations)
        {
            ExecuteOperation(op);
        }
    }

    private void ExecuteOperation(FlagOperation op)
    {
        try
        {
            Type enumType = Type.GetType(op.enumTypeName);
            if (enumType == null) return;
            Enum enumValue = (Enum)Enum.Parse(enumType, op.enumValueName);

            switch (op.operationType)
            {
                case FlagOperation.OperationType.SetBool:
                    FlagManager.instance.SetBoolFlag(enumValue, op.boolValueToSet);
                    break;
                case FlagOperation.OperationType.SetInt:
                    FlagManager.instance.SetIntFlag(enumValue, op.intValueToSet);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"フラグ操作の実行に失敗しました: {op.enumValueName} - {e.Message}", this);
        }
    }
}