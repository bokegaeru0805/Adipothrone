using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// このスクリプトをゲームUIのルートオブジェクトにアタッチしてください。
public class GameUIRefs : MonoBehaviour
{
    [Header("プレイヤーHPのUI")]
    [SerializeField]
    private Image _playerHPHealthBarImage;
    public Image PlayerHPHealthBarImage => _playerHPHealthBarImage;

    [SerializeField]
    private TextMeshProUGUI _playerHPText;
    public TextMeshProUGUI PlayerHPText => _playerHPText;

    [SerializeField]
    private TextMeshProUGUI _playerMaxHPText;
    public TextMeshProUGUI PlayerMaxHPText => _playerMaxHPText;

    [Header("プレイヤーWPのUI")]
    [SerializeField]
    private Image _playerWPHealthBarImage;
    public Image PlayerWPHealthBarImage => _playerWPHealthBarImage;

    [SerializeField]
    private TextMeshProUGUI _playerWPText;
    public TextMeshProUGUI PlayerWPText => _playerWPText;

    [SerializeField]
    private TextMeshProUGUI _playerMaxWPText;
    public TextMeshProUGUI PlayerMaxWPText => _playerMaxWPText;

    [Header("ボスHPのUI")]
    [SerializeField]
    private Image _bossHealthBarImage;
    public Image BossHealthBarImage => _bossHealthBarImage;

    [SerializeField]
    private GameObject _bossHealthBarBack;
    public GameObject BossHealthBarBack => _bossHealthBarBack;

    [Header("ボスのLvのUI")]
    [SerializeField]
    private TextMeshProUGUI _bossLevelNumberText;
    public TextMeshProUGUI BossLevelNumberText => _bossLevelNumberText;

    [Header("入手アイテムのログのUI")]
    [SerializeField]
    private List<GameObject> _itemLogSlots = new List<GameObject>();
    public List<GameObject> ItemLogSlots => _itemLogSlots;

    [Header("レベルアップのポップアップのUI")]
    [SerializeField]
    private GameObject _levelUpPopup;
    public GameObject LevelUpPopup => _levelUpPopup;

    [Header("技名表示のUI")]
    [SerializeField]
    private GameObject _skillNameDisplay;
    public GameObject SkillNameDisplay => _skillNameDisplay;

    [SerializeField]
    private TextMeshProUGUI _skillNameText;
    public TextMeshProUGUI SkillNameText => _skillNameText;

    [Header("ファストトラベルのパネルUI")]
    [SerializeField]
    private GameObject _fastTravelPanel;
    public GameObject FastTravelPanel => _fastTravelPanel;
}
