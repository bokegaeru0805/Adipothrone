using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class CutsceneHook : MonoBehaviour
{
    private PlayableDirector director;

    private void Awake()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void OnEnable()
    {
        if (director != null)
        {
            // 1. イベント登録
            director.played += OnPlay;
            director.stopped += OnStop;

            // 2. 【安全策】すでに再生中なら、手動で登録処理を走らせる
            // (Play On Awakeや、Disable/Enableでの復帰対策)
            if (director.state == PlayState.Playing)
            {
                OnPlay(director);
            }
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnPlay;
            director.stopped -= OnStop;
            
            // 無効化されたら、再生中であっても登録解除しておく
            OnStop(director);
        }
    }

    // 再生開始時に呼ばれる
    private void OnPlay(PlayableDirector pd)
    {
        Debug.Log($"Cutscene Started: {pd.name}");
        if (TimelineSkipManager.instance != null)
        {
            TimelineSkipManager.instance.RegisterDirector(pd);
        }
        else
        {
            Debug.LogWarning("TimelineSkipManagerが見つかりません。",this);
        }
    }

    // 停止時に呼ばれる
    private void OnStop(PlayableDirector pd)
    {
        if (TimelineSkipManager.instance != null)
        {
            TimelineSkipManager.instance.UnregisterDirector(pd);
        }
    }
}