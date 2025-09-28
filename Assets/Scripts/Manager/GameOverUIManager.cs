using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameOverUIManager : MonoBehaviour
{
    [Header("UI参照のルート")]
    [SerializeField]
    private GameOverUIRefs uiRefs = null;
    public static GameOverUIManager instance { get; private set; }
    private GameObject firstSelected;
    private GameObject lastSelected; //最後に選ばれていたボタンを保存する変数
    private Stack<GameObject> panelStack = new Stack<GameObject>();

    private void Awake()
    {
        instance = this;

        if (uiRefs == null)
        {
            Debug.LogError("GameOverUIRefsが設定されていません！");
            return;
        }

        if (uiRefs.GameOverPanel == null || uiRefs.ContinueSelectButton == null)
        {
            Debug.LogError("GameOverUIRefsの参照が正しく設定されていません！");
            return;
        }

        firstSelected = uiRefs.ContinueSelectButton; // 最初に選ばれるボタンを設定
    }

    public void StartGameOver()
    {
        Gameover();
    }

    private void Gameover()
    {
        // ゲーム内時間を停止
        TimeManager.instance.RequestPause();
        //SEを全て停止
        SEManager.instance?.StopAllSE();
        //オブジェクトプールをクリア
        ObjectPooler.instance?.ReturnAllToPool();
        
        uiRefs.GameOverPanel.SetActive(true); // GameOverパネルを表示
        panelStack = new Stack<GameObject>(); //一応Stackを初期化
        if (firstSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
        else
        {
            Debug.LogWarning("GameOver画面で、最初に選ばれるボタンが設定されていません");
        }

        BGMManager.instance!.Play(BGMCategory.GameOver);
        //GameOverBGMをループなしで流す

        // フェードイン処理
        foreach (var img in uiRefs.GameOverPanel.GetComponentsInChildren<Image>())
        {
            if (img != null)
            {
                img.color = new Color(img.color.r, img.color.g, img.color.b, 0f); // 最初に透明にする
                img.DOFade(1f, 1f).SetUpdate(true); // 1秒かけてフェードイン
            }
        }
    }

    private void Update()
    {
        if (InputManager.instance.UISelectNo() && !SaveLoadManager.isDataPrompting)
        {
            CloseTopPanel();
        }

        // UI上の選択が消えたら元に戻す
        if (EventSystem.current.currentSelectedGameObject == null && lastSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(lastSelected);
        }
        else
        {
            // 選ばれているオブジェクトを記憶
            lastSelected = EventSystem.current.currentSelectedGameObject;
        }
    }

    public void OpenPanel(GameObject panel, int Stage)
    {
        if (panelStack.Count >= Stage && Stage != -1)
        {
            //同じ階層までパネルを非表示にする
            while (panelStack.Count >= Stage)
            {
                GameObject top = panelStack.Pop();
                top.SetActive(false); //パネルを非表示にする
            }
        }

        if (panel.activeSelf == false)
        {
            panel.SetActive(true); //パネルを表示する
            panel.GetComponent<IPanelActive>().SelectFirstButton(); //最初の選択ボタンを指定する
            panelStack.Push(panel);
        }
    }

    public void CloseTopPanel()
    {
        if (panelStack.Count > 0)
        {
            GameObject top = panelStack.Pop();
            top.SetActive(false); //パネルを非表示にする
            if (panelStack.Count > 0)
            {
                top = panelStack.Peek();
                top.GetComponent<IPanelActive>().SelectFirstButton(); //最初の選択ボタンを指定する
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(firstSelected);
            }
        }
    }
}
