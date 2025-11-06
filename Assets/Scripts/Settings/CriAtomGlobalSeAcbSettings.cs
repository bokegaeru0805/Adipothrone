/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 * (This script has been modified based on the original.)
 *
 ****************************************************************************/

using UnityEngine;

namespace CriWare.Assets
{
    /// <summary>
    /// SE再生コンポーネント(CriAtomSePlayer)で共通して使用するACBアセットを定義する設定ファイルです。
    /// このファイルに変更を加えることで、プロジェクト内の全てのSE再生に影響します。
    /// </summary>
    // UnityのAssetsメニューからこの設定ファイルを作成できるようにするための属性です。
    // Assets > Create > CRIWARE > Create Global SE ACB Settings
    [CreateAssetMenu(fileName = "CriAtomGlobalSeAcbSettings", menuName = "CRIWARE/Create Global SE ACB Settings")]
    public class CriAtomGlobalSeAcbSettings : ScriptableObject
    {
        [Header("全SE再生コンポーネントで共通利用するACBアセット")]
        [Tooltip("ここに設定したACBアセットが、全てのCriAtomSePlayerコンポーネントの再生に使用されます。")]
        public CriAtomAcbAsset commonAcbAssetForSe;
    }
}