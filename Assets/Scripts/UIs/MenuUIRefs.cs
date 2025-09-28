using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuUIRefs : MonoBehaviour
{
    [Header("メニュー画面のキャンバス")]
    [SerializeField]
    private GameObject _menuCanvas;
    public GameObject MenuCanvas => _menuCanvas;

    [Header("メニュー画面のパネル")]
    [SerializeField]
    private GameObject _menuPanel;
    public GameObject MenuPanel => _menuPanel;

    [Header("メニュー画面のセーブボタン")]
    [SerializeField]
    private Button _saveButton;
    public Button SaveButton => _saveButton;

    [Header("メニュー画面のログ表示パネル")]
    [SerializeField]
    private GameObject _progressLogPanel;
    public GameObject ProgressLogPanel => _progressLogPanel;

    [Header("メニュー画面のレベル表示のテキスト")]
    [SerializeField]
    private TextMeshProUGUI _lvNumberText;
    public TextMeshProUGUI LvNumberText => _lvNumberText;

    [Header("メニュー画面のコイン表示のテキスト")]
    [SerializeField]
    private TextMeshProUGUI _coinNumberText;
    public TextMeshProUGUI CoinNumberText => _coinNumberText;
}
