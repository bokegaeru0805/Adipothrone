using UnityEngine;

public class GameOverUIRefs : MonoBehaviour
{
    [Header("ゲームオーバーパネル")]
    [SerializeField]
    private GameObject _gameOverPanel;
    public GameObject GameOverPanel => _gameOverPanel;

    [Header("ゲームオーバー時の続きから始めるボタン")]
    [SerializeField]
    private GameObject _continueSelectButton;
    public GameObject ContinueSelectButton => _continueSelectButton;
}
