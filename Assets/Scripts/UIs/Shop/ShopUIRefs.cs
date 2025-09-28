using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// このスクリプトをショップUIのルートオブジェクトにアタッチしてください。
public class ShopUIRefs : MonoBehaviour
{
    [Header("基本UI")]
    [SerializeField] private GameObject _shopUIPanel; // 店のUIパネル
    public GameObject ShopUIPanel => _shopUIPanel;

    [Header("店のボタンリスト")]
    [SerializeField] private List<Button> _shopButtons; // 店のボタンリスト
    public List<Button> ShopButtons => _shopButtons;

    [SerializeField] private Button _topButton; // 一番上のボタン
    public Button TopButton => _topButton;

    [SerializeField] private Button _bottomButton; // 一番下のボタン
    public Button BottomButton => _bottomButton;

    [Header("選択されているアイテムの所持数を表示するUI")]
    [SerializeField] private TextMeshProUGUI _selectedItemAmountText;
    public TextMeshProUGUI SelectedItemAmountText => _selectedItemAmountText;

    [Header("アイテム詳細パネルUI")]
    [SerializeField] private GameObject _itemDetailPanel; // アイテム詳細パネル
    public GameObject ItemDetailPanel => _itemDetailPanel;

    [Header("武器詳細パネルUI")]
    [SerializeField] private GameObject _weaponDetailPanel; // 武器詳細パネル
    public GameObject WeaponDetailPanel => _weaponDetailPanel;

    [Header("購入確認パネル")]
    [SerializeField] private GameObject _purchasePromptPanel; // 購入確認パネル
    public GameObject PurchasePromptPanel => _purchasePromptPanel;

    [SerializeField] private Button _purchaseYesButton; // 購入確認パネルのYesボタン
    public Button PurchaseYesButton => _purchaseYesButton;

    [SerializeField] private Button _purchaseNoButton; // 購入確認パネルのNoボタン
    public Button PurchaseNoButton => _purchaseNoButton;

    [Header("現在の所持金を表示するUI")]
    [SerializeField] private TextMeshProUGUI _currentMoneyText;
    public TextMeshProUGUI CurrentMoneyText => _currentMoneyText;

    [Header("タブの上部選択UIのSprite")]
    [SerializeField] private Sprite _selectedTabImage;
    public Sprite SelectedTabImage => _selectedTabImage;

    [SerializeField] private Sprite _unselectedTabImage;
    public Sprite UnselectedTabImage => _unselectedTabImage;
}