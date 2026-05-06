using UnityEngine;

/// <summary>
/// ゲーム全体で使用する定数や計算式、文字列定義を管理する静的クラス
/// </summary>
public static class GameConstants
{
    #region カメラ設定
    public static readonly Vector3 PLAYER_CAMERA_FOLLOW_OFFSET = new Vector3(0f, 4.5f, -10f); // プレイヤーカメラの追従オフセット
    public const float DEFAULT_CAMERA_ORTHO_SIZE = 10.0f; // カメラのデフォルトOrthoSize
    public const float DEFAULT_CAMERA_NEAR_CLIP = 0.3f; // カメラのデフォルトNearClipPlane
    public const float CAMERA_FOLLOW_DAMPING_X = 0f; // カメラのX軸追従ダンピング値
    public const float CAMERA_FOLLOW_DAMPING_Y = 20f; // カメラのY軸追従ダンピング値
    public static readonly int PIXELS_PER_UNIT = 16;
    #endregion

    #region セーブデータ設定
    public static readonly int MaxSaveLoadFiles = 20; // 最大セーブデータ数
    public const int AUTO_SAVE_FILE_NUMBER = 0; // オートセーブ用のファイル番号
    public const int MAX_AUTOSAVE_FOLDERS = 1; //オートセーブ用のフォルダの個数
    public const int NEW_GAME_FILE_NUMBER = 10000; // 新規ゲーム用のファイル番号
    public const float AUTO_SAVE_INTERVAL = 300f; // オートセーブを実行する間隔（秒）
    #endregion

    #region プレイヤーレベル・経験値設定
    public const int PLAYER_MAX_LEVEL = 99; // プレイヤーの最大レベル（これ以上はレベルアップできない）

    /// <summary>
    /// 現在のレベルから次のレベルに上がるために必要な「区間経験値」を計算します。
    /// 計算式: 20 × (現在のレベル)^2
    /// </summary>
    /// <param name="currentLevel">現在のレベル</param>
    /// <returns>次のレベルへ上がるために必要な経験値</returns>
    public static int GetRequiredExpForNextLevel(int currentLevel)
    {
        return 20 * currentLevel * currentLevel;
    }

    /// <summary>
    /// 指定したレベルに到達するために必要な「累計経験値」を計算します。
    /// レベル1から目標レベルの1つ前までの区間経験値を全て足し合わせます。
    /// </summary>
    /// <param name="targetLevel">目標レベル</param>
    /// <returns>目標レベルに到達するための累計経験値</returns>
    public static int GetTotalExpForLevel(int targetLevel)
    {
        if (targetLevel <= 1)
            return 0;

        int totalExp = 0;
        for (int i = 1; i < targetLevel; i++)
        {
            totalExp += GetRequiredExpForNextLevel(i);
        }
        return totalExp;
    }

    #endregion

    #region プレイヤーステータス計算式
    /*/// <summary>
    /// レベルに応じた最大HPを計算する
    /// </summary>
    public static int GetMaxHP(int level)
    {
        return Mathf.RoundToInt(80 + 25f * level * Mathf.Log10(level + 9));
    }*/

    /// <summary>
    /// レベルに応じた最大WPを計算する
    /// </summary>
    public static int GetMaxWP(int level)
    {
        if (level <= 5)
            return 15;
        else if (level <= 10)
            return 20;
        else
            return 15;
    }

    #region 体形状態・WP閾値設定
    public const int BODY_STATE_NORMAL = 0; //通常状態
    public const int BODY_STATE_ARMED_1 = 1; //体形変化状態1
    public const int BODY_STATE_ARMED_2 = 2; //体形変化状態2
    public const int BODY_STATE_ARMED_3 = 3; //体形変化状態3
    public const int BODY_STATE_IMMOBILE = 3; //動けない状態(現在はBodyState_Armed3と同じ)

    //変更したら、SetBodyState_Fungus.csのBodyStateEnumも変更すること
    public enum BodyStateEnum
    {
        BodyState_Normal = 0,
        BodyState_Armed1 = 10,
        BodyState_Armed2 = 20,
        BodyState_Armed3 = 30,
        BodyState_Immobile = 40,
    }; // 体形状態の列挙型

    public const int ANIM_BODY_STATE_NORMAL = 1; //通常状態のアニメーション
    public const int ANIM_BODY_STATE_ARMED_1 = 2; //体形変化状態1のアニメーション
    public const int ANIM_BODY_STATE_ARMED_2 = 3; //体形変化状態2のアニメーション
    public const int ANIM_BODY_STATE_ARMED_3 = 4; //体形変化状態3のアニメーション
    public const int ANIM_BODY_STATE_IMMOBILE = 4; //動けない状態のアニメーション

    public const int WP_THRESHOLD_ARMED_1 = 15; // 体形変化状態1になるWP
    public const int WP_THRESHOLD_ARMED_2 = 50; // 体形変化状態2になるWP
    public const int WP_THRESHOLD_ARMED_3 = 200; // 体形変化状態3になるWP
    public const int WP_THRESHOLD_IMMOBILE = 200; // 動けない状態になるWP
    public static readonly int[] WpThresholds =
    {
        0,
        WP_THRESHOLD_ARMED_1,
        WP_THRESHOLD_ARMED_2,
        WP_THRESHOLD_ARMED_3,
        WP_THRESHOLD_IMMOBILE,
    }; // 各体形状態のWP閾値
    #endregion

    #region ステータス上昇値・バフ・倍率設定
    // レベルアップ・ステータスレベルによる上昇ボーナス
    // 計算式: 現在レベル * (基礎増加値 + 最大レベル * ボーナス係数)

    // HP計算用の定数
    public const int STATUS_HP_INITIAL_BASE = 80; // レベル1でも保証される最低HP
    public const float STATUS_HP_BASE_INCREASE = 15.0f; // HPの基礎増加値
    public const float STATUS_HP_MAX_LEVEL_BONUS = 10.0f; // HPの最大レベルボーナス係数

    // 攻撃力計算用の定数
    public const float STATUS_ATTACK_BASE_INCREASE = 1.0f; // 攻撃力の基礎増加値
    public const float STATUS_ATTACK_MAX_LEVEL_BONUS = 1.0f; // 攻撃力の最大レベルボーナス係数
    public const float PLAYER_ATTACK_TO_MULTIPLIER_RATE = 100f; // プレイヤー基礎攻撃力を倍率に変換する際の割る数

    // 防御力計算用の定数
    public const float STATUS_DEFENSE_BASE_INCREASE = 1.5f; // 防御力の基礎増加値
    public const float STATUS_DEFENSE_MAX_LEVEL_BONUS = 1.5f; // 防御力の最大レベルボーナス係数

    // 素早さ計算用の定数
    public const float STATUS_SPEED_BASE_INCREASE = 0.01f; // 素早さの基礎増加値 (Lv100で3倍にするための調整)
    public const float STATUS_SPEED_MAX_LEVEL_BONUS = 0.01f; // 素早さの最大レベルボーナス係数

    // 幸運計算用の定数
    public const float STATUS_LUCK_BASE_INCREASE = 1.0f; // 幸運の基礎増加値
    public const float STATUS_LUCK_MAX_LEVEL_BONUS = 1.0f; // 幸運の最大レベルボーナス係数

    public const float MIN_ATTACK_POWER_MULTIPLIER = 0.01f; // 攻撃力の倍率が0以下にならないようにする最小値

    // バフレベル上限
    public const int DEFAULT_ATTACK_BUFF_LIMIT_LEVEL = 10; // 攻撃力バフのデフォルト上限レベル
    public const int DEFAULT_DEFENSE_BUFF_LIMIT_LEVEL = 10; // 防御力バフのデフォルト上限レベル
    public const int DEFAULT_SPEED_BUFF_LIMIT_LEVEL = 10; // スピードバフのデフォルト上限レベル
    public const int DEFAULT_LUCK_BUFF_LIMIT_LEVEL = 10; // 運バフのデフォルト上限レベル

    // バフレベルごとの増加量
    public const float ATTACK_BUFF_VALUE_PER_LEVEL = 1f; // 攻撃力バフのレベルごとの増加量
    public const float DEFENSE_BUFF_VALUE_PER_LEVEL = 1f; // 防御力バフのレベルごとの増加量
    public const float SPEED_BUFF_VALUE_PER_LEVEL = 1f; // スピードバフのレベルごとの増加量
    public const float LUCK_BUFF_VALUE_PER_LEVEL = 1f; // 運バフのレベルごとの増加量

    // プレイヤーへのエフェクト倍率
    public const float PLAYER_ATTACK_EFFECT_MULTIPLIER = 0.005f; // プレイヤーの攻撃力バフの倍率
    public const float PLAYER_DEFENSE_EFFECT_MULTIPLIER = 0.5f; // プレイヤーの防御力バフの倍率
    public const float PLAYER_MOVE_SPEED_EFFECT_MULTIPLIER = 0.002f; // プレイヤーの移動速度バフの倍率
    public const float PLAYER_WEAPON_SPEED_EFFECT_MULTIPLIER = 0.01f; // プレイヤーの武器速度バフの倍率

    // WPによるステータス影響倍率
    public const float PLAYER_ATTACK_WP_MULTIPLIER = 0.1f / 50; // プレイヤーの攻撃力WP倍率
    public const float PLAYER_DEFENSE_WP_MULTIPLIER = 0.1f / 50; // プレイヤーの防御力WP倍率
    public const float PLAYER_WEAPON_SPEED_WP_MULTIPLIER = 0.005f; // プレイヤーの武器速度WP倍率

    // その他の効果閾値
    public const float GUTS_EFFECT_THRESHOLD = 0.9f; // 「耐える」効果が発動するHP割合の閾値
    #endregion

    #region プレイヤー物理・アクション設定
    public const float PLAYER_MOVE_MAX_SPEED = 50.0f; // プレイヤーの最大移動速度
    public const float PLAYER_BLADE_MIN_SPEED = 0.05f; // プレイヤーの剣の最小速度
    public const float PLAYER_DAMAGE_DEFAULT_KNOCKBACK_FORCE = 3.0f; // プレイヤーの被ダメージ時のデフォルトノックバック力

    public const float PLAYER_BASE_HEIGHT = 3.0f; // プレイヤーの基準高さ
    public const float PLAYER_JUMP_HEIGHT = 3.5f; // プレイヤーのジャンプ高さ
    public const float PLAYER_JUMP_PEAK_HEIGHT = PLAYER_BASE_HEIGHT + PLAYER_JUMP_HEIGHT; // プレイヤーのジャンプ頂点高さ

    public const float ROBOT_BASE_HEIGHT = 2.0f; // ロボットの基準高さ
    public const float ROBOT_JUMP_PEAK_HEIGHT = ROBOT_BASE_HEIGHT + PLAYER_JUMP_HEIGHT; // ロボットのジャンプ頂点高さ

    public const float PLAYER_GRAVITY_SCALE = 2.0f; // プレイヤーの重力スケール
    #endregion

    #region システム・UI設定
#if UNITY_EDITOR
    public const float INVINCIBLE_DURATION_ON_LOAD = 0.0f; // エディタプレイ時は無敵時間なし（テストしやすくするため）
#else
    public const float INVINCIBLE_DURATION_ON_LOAD = 3.0f; // 通常の実機ビルド用の値
#endif
    public const string UI_COLOR_TAG_GOLD = "<color=#C6A34C>{0}</color>"; // ゴールド色のUIテキストタグのフォーマット文字列

    //string coloredText = string.Format(GameConstants.UI_COLOR_TAG_GOLD, "攻撃力");のようにして使用

    public const float GAUGE_SMOOTH_TIME = 0.15f; // ゲージのスムーズな更新にかかる時間
    public const float CHARGE_EFFECT_DEFAULT_DURATION = 300f / 60f; // チャージエフェクトの基本持続時間
    public const int BUY_MAX_QUANTITY = 99; // 購入時の最大個数
    public const string UI_NAME_FAST_TRAVEL_PANEL = "FastTravelPanel"; // ファストトラベルパネルのUI名
    public const string DEFAULT_NPC_DIALOGUE_BLOCK_NAME = "DefaultGreeting"; // NPCのデフォルトの会話ブロック名
    #endregion

    #region 文字列定数 (オブジェクト名・タグ・レイヤー・シーンなど)
    // --- オブジェクト名 ---
    public const string PLAYER_OBJECT_NAME = "Noeri"; // プレイヤーのオブジェクト名
    public const string ROBOT_OBJECT_NAME = "Fabo"; // ロボットのオブジェクト名

    // --- タグ名 ---
    public const string UNTAGGED_TAG_NAME = "Untagged"; // タグ無しの名前
    public const string PLAYER_TAG_NAME = "Player"; // プレイヤーのタグ名
    public const string PLAYER_ATTACK_TAG_NAME = "PlayerAttack"; // プレイヤーの攻撃タグ名
    public const string DAMAGEABLE_ENEMY_TAG_NAME = "DamageableEnemy"; // ダメージを受ける敵のタグ名
    public const string IMMUNE_ENEMY_TAG_NAME = "ImmuneEnemy"; // ダメージを受けない敵のタグ名
    public const string INTERACTABLE_OBJECT_TAG_NAME = "InteractableObject"; // インタラクト可能なオブジェクトのタグ名
    public const string AREA_TRANSITION_TAG_NAME = "AreaTransition"; // エリア遷移のタグ名
    public const string PHYSICS_OBJECT_TAG_NAME = "PhysicsObject"; // オブジェクトの地面判定タグ名
    public const string MAIN_GLOBAL_VOLUME_TAG_NAME = "MainGlobalVolume"; // メインのGlobal Volumeのタグ名

    // --- 物理レイヤー名 ---
    public const string PHYSICS_LAYER_NAME_GROUND = "GroundLayer"; // 当たり判定(Layer)用の名前
    public const string PHYSICS_LAYER_NAME_OBJECT_GROUND = "ObjectGround"; // オブジェクトの地面判定用の名前

    // --- 描画順(Sorting Layer)レイヤー名 ---
    public const string SORTING_LAYER_NAME_GROUND = "Ground"; // 描画順(Sorting Layer)用の名前
    public const string SORTING_LAYER_NAME_CHARACTER = "Character"; // 描画順(Sorting Layer)用の名前

    // --- シーン名 ---
    public const string SCENE_NAME_TITLE = "TitleScene"; // タイトルシーンの名前
    public const string SCENE_NAME_TUTORIAL_START = "TutorialStartScene"; // チュートリアルシーンの名前
    public const string SCENE_NAME_CHAPTER_1 = "Chapter1Scene"; // 第1章のシーン名
    public const string SCENE_NAME_DESERT = "DesertScene"; // 砂漠エリアのシーン名
    public const string SCENE_NAME_ROYAL_CAPITAL = "RoyalCapitalScene"; // 王都のシーンの名前
    public const string SCENE_NAME_DEBUG = "DebugScene"; // デバッグ用シーンの名前

    // --- プールタグ名 ---
    public const string EFFECT_ENEMY_SPAWN_POOLTAG = "Effect_Enemy_Spawn";
    public const string DROP_ITEM_POOLTAG = "DropItem";
    #endregion

    #endregion
}
