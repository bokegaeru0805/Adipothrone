using System.Collections.Generic;
using UnityEngine;

public static class GameConstants
{
    public static readonly Vector3 PLAYER_CAMERA_FOLLOW_OFFSET = new Vector3(0f, 4.5f, -10f);
    public const float CameraFollowDampingY = 20f; // カメラのY軸追従ダンピング値
    public static readonly int PIXELS_PER_UNIT = 16;
    public static readonly int MaxSaveLoadFiles = 12; // 最大セーブデータ数
    public const int AUTO_SAVE_FILE_NUMBER = 0; // オートセーブ用のファイル番号
    public const int NEW_GAME_FILE_NUMBER = 10000; // 新規ゲーム用のファイル番号
    public const float AUTO_SAVE_INTERVAL = 300f; // オートセーブを実行する間隔（秒）
    public static readonly Dictionary<int, int> LevelExpRequirements = new Dictionary<int, int>
    {
        { 1, 0 }, // Lv1 -> 初期値
        { 2, 15 },
        { 3, 69 },
        { 4, 168 },
        { 5, 317 },
        { 6, 517 },
        { 7, 773 },
        { 8, 1085 },
        { 9, 1455 },
        { 10, 1855 },
        // 必要に応じて追加
        //Mathf.Pow(level, 2.2f) * 15
    };

    public static int GetMaxHP(int level)
    {
        return Mathf.RoundToInt(80 + 25f * level * Mathf.Log10(level + 9));
    }

    public static int GetMaxWP(int level)
    {
        if (level <= 5)
            return 15;
        else if (level <= 10)
            return 20;
        else
            return 15;
    }

    public static int GetDefense(int level)
    {
        return Mathf.RoundToInt(96.8f * Mathf.Sqrt(level) - 94.8f * Mathf.Log(level + 1) - 26.6f);
    }

    public const int PlayerMaxLevel = 10; // 上限レベル
    public const float levelAttackBonus = 0.01f; // レベルアップ時の攻撃力ボーナス
    public const int BodyState_Normal = 0; //通常状態
    public const int BodyState_Armed1 = 1; //体形変化状態1
    public const int BodyState_Armed2 = 2; //体形変化状態2
    public const int BodyState_Armed3 = 3; //体形変化状態3
    public const int BodyState_Immobile = 3; //動けない状態(現在はBodyState_Armed3と同じ)

    //変更したら、SetBodyState_Fungus.csのBodyStateEnumも変更すること
    public enum BodyStateEnum
    {
        BodyState_Normal = 0,
        BodyState_Armed1 = 10,
        BodyState_Armed2 = 20,
        BodyState_Armed3 = 30,
        BodyState_Immobile = 40,
    }; // 体形状態の列挙型

    public const int AnimBodyState_Normal = 1; //通常状態のアニメーション
    public const int AnimBodyState_Armed1 = 2; //体形変化状態1のアニメーション
    public const int AnimBodyState_Armed2 = 3; //体形変化状態2のアニメーション
    public const int AnimBodyState_Armed3 = 4; //体形変化状態3のアニメーション
    public const int AnimBodyState_Immobile = 4; //動けない状態のアニメーション
    public static readonly int[] WpThresholds =
    {
        0,
        WpThreshold_Armed1,
        WpThreshold_Armed2,
        WpThreshold_Armed3,
        WpThreshold_Immobile
    }; // 各体形状態のWP閾値
    public const int WpThreshold_Armed1 = 15; // 体形変化状態1になるWP
    public const int WpThreshold_Armed2 = 50; // 体形変化状態2になるWP
    public const int WpThreshold_Armed3 = 200; // 体形変化状態3になるWP
    public const int WpThreshold_Immobile = 200; // 動けない状態になるWP
    public const float PlayerAttackEffectMultiplier = 0.005f; // プレイヤーの攻撃力バフの倍率
    public const float PlayerDefenseEffectMultiplier = 0.5f; // プレイヤーの防御力バフの倍率
    public const float PlayerMoveSpeedEffectMultiplier = 0.002f; // プレイヤーの移動速度バフの倍率
    public const float PlayerWeaponSpeedEffectMultiplier = 0.01f; // プレイヤーの武器速度バフの倍率
    public const float PlayerAttackWpMultiplier = 0.1f / 15; // プレイヤーの攻撃力WP倍率
    public const float PlayerDefenseWpMultiplier = 0.1f / 15; // プレイヤーの防御力WP倍率
    public const float PlayerMoveWpMultiplier = 0.1f / 15; // プレイヤーの移動速度WP倍率
    public const float PlayerWeaponSpeedWpMultiplier = 0.005f; // プレイヤーの武器速度WP倍率
    public const float AttackBuffValuePerLevel = 1f; // 攻撃力バフのレベルごとの増加量
    public const float DefenseBuffValuePerLevel = 1f; // 防御力バフのレベルごとの増加量
    public const float SpeedBuffValuePerLevel = 1f; // スピードバフのレベルごとの増加量
    public const float LuckBuffValuePerLevel = 1f; // 運バフのレベルごとの増加量
    public const int DefaultAttackBuffLimitLevel = 10; // 攻撃力バフのデフォルト上限レベル
    public const int DefaultDefenseBuffLimitLevel = 10; // 防御力バフのデフォルト上限レベル
    public const int DefaultSpeedBuffLimitLevel = 10; // スピードバフのデフォルト上限レベル
    public const int DefaultLuckBuffLimitLevel = 10; // 運バフのデフォルト上限レベル
    public const float MinAttackPowerMultiplier = 0.01f; // 攻撃力の倍率が0以下にならないようにする最小値
    public const float PlayerMoveMaxSpeed = 50.0f; // プレイヤーの最大移動速度
    public const float PlayerBladeMinSpeed = 0.05f; // プレイヤーの剣の最小速度
    public const float GutsEffectThreshold = 0.9f; // 「耐える」効果が発動するHP割合の閾値
    public const float GaugeSmoothTime = 0.15f; // ゲージのスムーズな更新にかかる時間
    public const float PlayerBaseHeight = 3.0f; // プレイヤーの基準高さ
    public const float PlayerJumpPeakHeight = PlayerBaseHeight + PlayerJumpHeight; // プレイヤーのジャンプ頂点高さ
    public const float PlayerJumpHeight = 3.5f; // プレイヤーのジャンプ高さ
    public const float RobotBaseHeight = 2.0f; // ロボットの基準高さ
    public const float RobotJumpPeakHeight = RobotBaseHeight + PlayerJumpHeight; // ロボットのジャンプ頂点高さ
    public const string UIColorTagGold = "<color=#C6A34C>{0}</color>";

    //string coloredText = string.Format(GameConstants.UIColorTagGold, "攻撃力");のようにして使用

    public const string PlayerObjectName = "Noeri"; // プレイヤーのオブジェクト名
    public const string RobotObjectName = "Fabo"; // ロボットのオブジェクト名
    public const string PlayerTagName = "Player"; // プレイヤーのタグ名
    public const string PlayerAttackTagName = "PlayerAttack"; // プレイヤーの攻撃タグ名
    public const string DamageableEnemyTagName = "DamageableEnemy"; // ダメージを受ける敵のタグ名
    public const string ImmuneEnemyTagName = "ImmuneEnemy"; // ダメージを受けない敵のタグ名
    public const string InteractableObjectTagName = "InteractableObject"; // インタラクト可能なオブジェクトのタグ名
    public const string AreaTransitionTagName = "AreaTransition"; // エリア遷移のタグ名
    public const string SceneName_Title = "TitleScene"; // タイトルシーンの名前
    public const string SceneName_TutorialStart = "TutorialStartScene"; // チュートリアルシーンの名前
    public const string SceneName_Chapter1 = "Chapter1Scene"; // 第1章のシーン名
    public const string UIName_FastTravelPanel = "FastTravelPanel"; // ファストトラベルパネルのUI名
    public const string DefaultNpcDialogueBlockName = "DefaultGreeting"; // NPCのデフォルトの会話ブロック名
    public const float ChargeEffectDefaultDuration = 300f / 60f; // チャージエフェクトの基本持続時間
    public const int BuyMaxQuantity = 99; // 購入時の最大個数
}
