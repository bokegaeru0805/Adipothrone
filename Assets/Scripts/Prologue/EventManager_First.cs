using System.Collections;
using Cinemachine;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EventManager_First : MonoBehaviour
{
    [Header("プレイヤーの位置設定")]
    [SerializeField]
    [Tooltip("イベント開始時のプレイヤーの初期座標")]
    private Vector2 PlayerFirstPosition;

    [SerializeField]
    [Tooltip("プレイヤーが歩いて移動する目標のX座標")]
    private float PlayerSecondPosition_x;

    [Header("女性（Woman）の位置設定")]
    [SerializeField]
    [Tooltip("カメラ移動時の基準となる女性の出現X座標")]
    private float womanAppearPosition_x;

    [SerializeField]
    [Tooltip("イベント中に女性が最初に配置される初期のX座標")]
    private float womanSecondPosition_x;

    [SerializeField]
    [Tooltip("プレイヤー肥大化後に女性が下がる位置のX座標")]
    private float womanThirdPosition_x;

    [Header("カメラ設定")]
    [SerializeField]
    [Tooltip("女性が出現する前に、プレイヤーの移動に合わせてカメラを先行させるX軸のオフセット値")]
    private float beforeAppearCameraOffsetX;

    [Header("プレイヤーのスプライト設定")]
    [SerializeField]
    [Tooltip("前を向いている（正面）プレイヤーのスプライト")]
    private Sprite FrontPlayer;

    [SerializeField]
    [Tooltip("後ろを向いている（背面）プレイヤーのスプライト")]
    private Sprite BackPlayer;

    [SerializeField]
    [Tooltip("肉体変化時などに使用するプレイヤーのスプライト")]
    private Sprite FleshPlayer;

    [Header("タイルマップ削除設定")]
    [SerializeField]
    [Tooltip("イベント中に床が崩れる演出で消去する対象のTilemap")]
    private Tilemap targetTilemap;

    [SerializeField]
    [Tooltip("タイル削除を実行する矩形範囲の開始座標（左下）")]
    private Vector2Int startPosition;

    [SerializeField]
    [Tooltip("タイル削除を実行する矩形範囲の終了座標（右上）")]
    private Vector2Int endPosition;

    [Header("イベント用演出オブジェクト")]
    [SerializeField]
    [Tooltip("イベント演出用に動かす操作不可能なプレイヤーオブジェクト")]
    private GameObject Player;

    [SerializeField]
    [Tooltip("イベント演出用に動かすロボットオブジェクト")]
    private GameObject Robot;

    [SerializeField]
    [Tooltip("イベント演出用に動かす女性（敵）オブジェクト")]
    private GameObject Woman;

    private float PlayerWalkSpeed;
    private bool isHeroinFall = false;
    private GameObject PlayerObject;
    private Animator PlayerAnimator;
    private Animator WomanAnimator;
    private const string IsArmsCrossedAnimParam = "IsArmsCrossed";

    private void Start()
    {
        // 各オブジェクトが存在していれば非表示にする
        if (Player != null)
            Player.SetActive(false);

        if (Robot != null)
            Robot.SetActive(false);

        if (Woman != null)
            Woman.SetActive(false);

        PlayerAnimator = Player.GetComponent<Animator>();
        WomanAnimator = Woman.GetComponent<Animator>();
    }

    private void Update()
    {
        if (InputManager.instance.SkipDialogHold())
        {
            Robot.transform.DOComplete();
            Woman.transform.DOComplete();
            Camera.main.transform.DOComplete();
            if (!isHeroinFall)
            {
                Player.transform.DOComplete();
            }
        }
    }

    public IEnumerator EventStart()
    {
        FadeCanvas.instance.FadeOut(1.0f); //画面を暗転させる
        BGMManager.instance.Play(BGMCategory.Field_Quiet);
        yield return new WaitForSecondsRealtime(1.0f);
        PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME); //Playerオブジェクトを取得
        PlayerWalkSpeed = PlayerObject.GetComponent<Heroin_move>().m_defaultSpeed; //Playerの歩行速度を取得
        PlayerObject.SetActive(false); //操作可能なPlayerオブジェクトを非表示化

        Player.SetActive(true); //操作不可能なPlayerオブジェクトを表示
        Player.transform.position = new Vector3(
            PlayerFirstPosition.x,
            PlayerFirstPosition.y,
            Player.transform.position.z
        );
        PlayerAnimator.SetInteger("BodyState", GameConstants.ANIM_BODY_STATE_ARMED_2); //playerの体形Armed2に設定

        Robot.SetActive(true); //操作不可能なRobotオブジェクトを表示
        Robot.transform.position = new Vector3(
            PlayerFirstPosition.x - 1.5f,
            PlayerFirstPosition.y + 3.5f,
            Player.transform.position.z
        );
        Camera.main.GetComponent<CinemachineBrain>().enabled = false; //カメラの任意移動を不可能にする
        Camera.main.transform.position = new Vector3(
            PlayerFirstPosition.x,
            PlayerFirstPosition.y + 6,
            Camera.main.transform.position.z
        );
        FadeCanvas.instance.FadeIn(1.0f); //画面を明転させる
        yield return new WaitForSecondsRealtime(1.0f);
    }

    public IEnumerator move()
    {
        PlayerAnimator.SetInteger("AnimState", 1);
        Robot.transform.DOLocalMoveX(
            PlayerSecondPosition_x - 1.5f,
            (PlayerSecondPosition_x - PlayerFirstPosition.x) / PlayerWalkSpeed
        );
        Camera.main.transform.DOLocalMoveX(
            PlayerSecondPosition_x + beforeAppearCameraOffsetX,
            ((PlayerSecondPosition_x + beforeAppearCameraOffsetX) - PlayerFirstPosition.x)
                / PlayerWalkSpeed
        );
        yield return Player
            .transform.DOLocalMoveX(
                PlayerSecondPosition_x,
                (PlayerSecondPosition_x - PlayerFirstPosition.x) / PlayerWalkSpeed
            )
            .SetEase(Ease.Linear)
            .WaitForCompletion();

        PlayerAnimator.SetInteger("AnimState", 0);
        PlayerAnimator.enabled = false;
        Player.GetComponent<SpriteRenderer>().sprite = BackPlayer;
    }

    public IEnumerator womanAppear()
    {
        Player.GetComponent<SpriteRenderer>().sprite = FrontPlayer;

        Woman.SetActive(true);
        WomanAnimator.SetBool(IsArmsCrossedAnimParam, true);
        Woman.transform.position = new Vector3(
            womanSecondPosition_x,
            Player.transform.position.y,
            Player.transform.position.z
        );

        yield return Camera
            .main.transform.DOLocalMoveX(
                222,
                (womanAppearPosition_x - womanSecondPosition_x) / PlayerWalkSpeed
            )
            .WaitForCompletion();
    }

    public IEnumerator womanDash()
    {
        WomanAnimator.SetBool(IsArmsCrossedAnimParam, false);

        // WomanのSpriteRendererとマテリアルを取得
        SpriteRenderer womanRenderer = Woman.GetComponent<SpriteRenderer>();
        Material womanMaterial = womanRenderer != null ? womanRenderer.material : null;

        // 1. ホログラム機能の強度を上げていきながら、徐々に非表示（透明）にする
        if (womanMaterial != null)
        {
            // ホログラムのシェーダーキーワードを有効化する
            womanMaterial.EnableKeyword("_HOLOGRAM_ON");

            // _HologramBlendを1に（ホログラム化）
            womanMaterial.SetFloat("_HologramBlend", 1.0f);

            SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Warp2);

            // DOTweenでマテリアルの色（アルファ値）を1秒かけて0にする
            yield return womanRenderer
                .DOColor(new Color(1, 1, 1, 0), 0.5f)
                .SetEase(Ease.OutCubic)
                .WaitForCompletion();
        }
        else
        {
            // マテリアルが取得できない場合の安全策として1秒待機
            yield return new WaitForSeconds(1.0f);
        }

        // 2. 非表示の状態で目標座標（PlayerSecondPosition_x + 0.5f）へ瞬時に移動
        Woman.transform.position = new Vector3(
            PlayerSecondPosition_x + 0.5f,
            Woman.transform.position.y,
            Woman.transform.position.z
        );

        // 3. 移動完了後、まずはホログラム姿でパッと表示し、そこから実体化（出現）させる
        if (womanMaterial != null)
        {
            // ホログラムを有効にした状態で、スプライトの不透明度を1にしてパッと表示
            womanMaterial.EnableKeyword("_HOLOGRAM_ON");
            womanMaterial.SetFloat("_HologramBlend", 1.0f);
            womanRenderer.color = new Color(1, 1, 1, 1);

            // ほんの一瞬（0.1秒）ホログラム状態をキープ
            yield return new WaitForSeconds(0.1f);

            // ホログラムから実体（通常表示）へじわっと変化させて出現させる（ここでは0.4秒かけて変化）
            yield return womanMaterial.DOFloat(0.0f, "_HologramBlend", 0.4f).WaitForCompletion();

            // 完全にホログラム機能をオフにする
            womanMaterial.DisableKeyword("_HOLOGRAM_ON");
        }

        WomanAnimator.SetBool(IsArmsCrossedAnimParam, true);
    }

    public void HeroinInflation()
    {
        PlayerAnimator.enabled = true;
        PlayerAnimator.SetInteger("BodyState", GameConstants.ANIM_BODY_STATE_IMMOBILE); //playerの体形をImmobileに設定
        //WPの数値を体形に応じて設定し、ステータスを更新
        PlayerBodyManager.instance?.SetWPFromBodyState(
            GameConstants.BodyStateEnum.BodyState_Immobile
        );
        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.Bound1);

        Woman.transform.position = new Vector3(
            womanThirdPosition_x,
            Player.transform.position.y,
            Player.transform.position.z
        );
    }

    public void HeroinFall()
    {
        SEManager.instance?.PlayFieldSE(SE_Field.Collapse1);

        isHeroinFall = true;
        for (int x = startPosition.x; x <= endPosition.x; x++)
        {
            for (int y = startPosition.y; y <= endPosition.y; y++)
            {
                Vector3Int tilePosition = new Vector3Int(x, y, 0);
                targetTilemap.SetTile(tilePosition, null); // 指定位置のタイルを削除
            }
        }

        Player.transform.DOLocalMoveY(-10, 2.0f).SetEase(Ease.InSine);
        FadeCanvas.instance.FadeOut(2.0f); //画面を暗転させる
    }

    public void ToSecondPrologue()
    {
        TimeManager.instance.RequestPause(); // 時間を停止
        FadeCanvas.instance.FadeOut(0); //念のため、画面を即座に暗転させる
        Player.SetActive(false);
        Robot.SetActive(false);
        Woman.SetActive(false);

        PlayerObject.SetActive(true);
        PlayerObject.transform.position = new Vector3(-110, 0, PlayerObject.transform.position.z);
        GameManager.instance.savedata.PlayerStatus.isRobotmove = false;

        Camera.main.transform.position = new Vector3(-110, 6, Camera.main.transform.position.z);
        Camera.main.GetComponent<CinemachineBrain>().enabled = true; //カメラの任意移動を可能にする

        PlayerBodyManager.instance?.SetWPFromBodyState(
            GameConstants.BodyStateEnum.BodyState_Armed2
        );

        SEManager.instance.StopAllSE();

        TimeManager.instance.ReleasePause(); // 時間を元に戻す
    }
}
