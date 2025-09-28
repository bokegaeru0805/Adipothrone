using System.Collections;
using UnityEngine;

public class TitleManager : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(SearchBGM());
    }

    private IEnumerator SearchBGM()
    {
        if (BGMManager.instance != null)
        {
            while (!BGMManager.instance.IsPlayingCategory(BGMCategory.Title))
            {
                BGMManager.instance.Play(BGMCategory.Title);
                yield return null; // BGMが再生されるまで待機
            }
        }
    }

    public void SelectNewStart()
    {
        if (SaveLoadManager.instance != null)
        {
            SaveLoadManager.instance.newLoad();
        }
    }
}
