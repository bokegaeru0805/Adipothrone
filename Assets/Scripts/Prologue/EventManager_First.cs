using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using DG.Tweening;
using Shapes2D;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EventManager_First : MonoBehaviour
{
    [SerializeField]
    private Vector2 PlayerFirstPosition;

    [SerializeField]
    private float PlayerSecondPosition_x;

    [SerializeField]
    private float womanAppearPosition_x;

    [SerializeField]
    private float womanSecondPosition_x;

    [SerializeField]
    private float womanThirdPosition_x;

    [SerializeField]
    private float beforeAppearCameraOffsetX;

    [SerializeField]
    private Sprite FrontPlayer;

    [SerializeField]
    private Sprite BackPlayer;

    [SerializeField]
    private Sprite FleshPlayer;

    [SerializeField]
    private Tilemap targetTilemap; // 消す対象のTilemap

    [SerializeField]
    private Vector2Int startPosition; // 削除範囲の開始位置

    [SerializeField]
    private Vector2Int endPosition; // 削除範囲の終了位置
    private float PlayerWalkSpeed;
    private bool isHeroinFall = false;
    private GameObject PlayerObject;
    private GameObject Player;
    private GameObject Robot;
    private GameObject Woman;
    private Animator PlayerAnimator;
    private Animator WomanAnimator;

    private void Start()
    {
        Player = transform.GetChild(0).gameObject;
        Robot = transform.GetChild(1).gameObject;
        Woman = transform.GetChild(2).gameObject;
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
        yield return new WaitForSecondsRealtime(1.0f);
        PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName); //Playerオブジェクトを取得
        PlayerWalkSpeed = PlayerObject.GetComponent<Heroin_move>().m_defaultSpeed; //Playerの歩行速度を取得
        PlayerObject.SetActive(false); //操作可能なPlayerオブジェクトを非表示化

        Player.SetActive(true); //操作不可能なPlayerオブジェクトを表示
        Player.transform.position = new Vector3(
            PlayerFirstPosition.x,
            PlayerFirstPosition.y,
            Player.transform.position.z
        );
        PlayerAnimator.SetInteger("BodyState", GameConstants.AnimBodyState_Armed2); //playerの体形Armed2に設定

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
        Woman.transform.position = new Vector3(
            womanSecondPosition_x,
            PlayerFirstPosition.y + 2,
            Player.transform.position.z
        );

        yield return Camera
            .main.transform.DOLocalMoveX(
                (PlayerSecondPosition_x + womanSecondPosition_x) / 2,
                (womanAppearPosition_x - womanSecondPosition_x) / PlayerWalkSpeed
            )
            .WaitForCompletion();
        WomanAnimator.SetBool("Run", false);
    }

    public IEnumerator womanDash()
    {
        WomanAnimator.SetBool("Dash", true);
        if (SEManager.instance != null)
            SEManager.instance.PlayEnemyActionSE(SE_EnemyAction.FastMove1);

        yield return Woman
            .transform.DOLocalMoveX(PlayerSecondPosition_x + 0.5f, 1f)
            .SetEase(Ease.OutCubic)
            .WaitForCompletion();
        WomanAnimator.SetBool("Dash", false);
    }

    public void HeroinInflation()
    {
        PlayerAnimator.enabled = true;
        PlayerAnimator.SetInteger("BodyState", GameConstants.AnimBodyState_Immobile); //playerの体形をImmobileに設定
        SEManager.instance?.PlayPlayerActionSE(SE_PlayerAction.Bound1);

        Woman.transform.position = new Vector3(
            womanThirdPosition_x,
            Woman.transform.position.y,
            Player.transform.position.z
        );
    }

    public void HeroinFall()
    {
        if (SEManager.instance != null)
            SEManager.instance.PlayFieldSE(SE_Field.Collapse1);

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
        FadeCanvas.instance.FadeOut(2.0f); //画面を明転させる
    }

    public void ToSecondPrologue()
    {
        TimeManager.instance.RequestPause(); // 時間を停止
        FadeCanvas.instance.FadeOut(Mathf.Epsilon); //画面を明転させる
        Player.SetActive(false);
        Robot.SetActive(false);
        Woman.SetActive(false);

        PlayerObject.SetActive(true);
        PlayerObject.transform.position = new Vector3(-110, 0, PlayerObject.transform.position.z);
        GameManager.instance.savedata.PlayerStatus.isRobotmove = false;

        Camera.main.transform.position = new Vector3(-110, 6, Camera.main.transform.position.z);
        Camera.main.GetComponent<CinemachineBrain>().enabled = true; //カメラの任意移動を可能にする

        SEManager.instance.StopAllSE();

        TimeManager.instance.ReleasePause(); // 時間を元に戻す
    }
}
