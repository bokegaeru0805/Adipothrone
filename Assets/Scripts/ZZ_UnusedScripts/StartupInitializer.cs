// using UnityEngine;

// /// <summary>
// /// ゲーム開始(起動)時に一度だけ初期化処理するクラス(エディタ上でも使える)
// /// </summary>
// public class StartupInitializer : MonoBehaviour
// {
//     //初期化処理が完了したか
//     public static bool IsInitialized { get; private set; } = false;

//     //=================================================================================
//     //起動時
//     //=================================================================================

//     //ゲーム開始時(シーン読み込み前、Awake前)に実行される
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     static void Initialize()
//     {
//         new GameObject("StartupInitializer", typeof(StartupInitializer));
//     }

//     //=================================================================================
//     //初期化
//     //=================================================================================

//     private void Start()
//     {
//         //初期化処理
//         SaveLoadManager.instance.DisableSave();

//         //初期化が済んだら自分を消す
//         Destroy(gameObject);
//         IsInitialized = true;
//     }
// }