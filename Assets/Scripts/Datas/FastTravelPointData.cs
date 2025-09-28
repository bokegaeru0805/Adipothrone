using UnityEngine;

[CreateAssetMenu(fileName = "NewFastTravelPoint", menuName = "Fast Travel/Fast Travel Point")]
public class FastTravelPointData : ScriptableObject
{
    public enum SceneNameEnum
    {
        None = 0,
        TutorialStart = 10,
        Chapter1 = 20,
        // 追加可能
    }

    [Tooltip("ファストトラベル地点のID")]
    public FastTravelName fastTravelId;

    [Tooltip("ファストトラベル地点の名前（UI表示用）")]
    public string fastTravelName;

    [Tooltip("移動先のシーン（Enumで選択）")]
    public SceneNameEnum sceneEnum = SceneNameEnum.None;

    [Tooltip("移動先のワールド座標")]
    public Vector2 targetPosition;

    /// <summary>
    /// Enumに対応するシーン名の文字列を取得する
    /// </summary>
    public string GetSceneName()
    {
        switch (sceneEnum)
        {
            case SceneNameEnum.TutorialStart:
                return GameConstants.SceneName_TutorialStart;
            case SceneNameEnum.Chapter1:
                return GameConstants.SceneName_Chapter1;
            case SceneNameEnum.None:
            default:
                return string.Empty;
        }
    }
}