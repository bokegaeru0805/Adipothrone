// using UnityEngine;
// using UnityEngine.SceneManagement;

// public class SceneLoader : MonoBehaviour
// {
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     static void LoadStartScene()
//     {
//         string firstSceneName = GameConstants.SceneName_Title; // 最初にロードしたいシーン名を設定
//         if (SceneManager.GetActiveScene().name != firstSceneName) // すでにロードされていない場合
//         {
//             SceneManager.LoadScene(firstSceneName);
//         }
//     }
// }