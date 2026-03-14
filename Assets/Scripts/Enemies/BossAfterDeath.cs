using System.Collections;
using System.Collections.Generic;
using MyGame.CameraControl;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

public class BossAfterDeath : MonoBehaviour
{
    #region Constants

    // パーティクルの出現範囲のオフセット
    private const float PARTICLE_RADIUS_OFFSET = 1f;

    // 撃破時のフラッシュと明滅を繰り返す回数
    private const int DEFEAT_FLASH_COUNT = 3;

    #endregion

    #region Inspector Settings & References

    [Header("Fungus設定")]
    public Fungus.Flowchart flowchart = null;

    [Header("エフェクト設定")]
    [SerializeField]
    private float particleoffsetY;

    [SerializeField]
    private GameObject FlashPanel; // 撃破時のフラッシュパネル

    [SerializeField]
    private GameObject BossDefeatParticle; // 撃破時のパーティクル

    [Header("ボス情報")]
    [SerializeField]
    private bool isBossNamePreSet = false; //予めボスの名前を設定するか

    [SerializeField, ShowIf(nameof(isBossNamePreSet))]
    private BossHealth.BossName bossName = BossHealth.BossName.None; // ボスの名前

    [SerializeField, ShowIf(nameof(bossName), BossHealth.BossName.DustDevilBoss)]
    private GameObject sandSmokeEffect; // 砂嵐のエフェクト(砂嵐のボス用)

    [Header("ボスの複数スプライト設定")]
    [SerializeField]
    [Tooltip("複数のSpriteRendererを同時に演出対象にするかどうか")]
    private bool useMultipleSprites = false;

    [SerializeField, ShowIf(nameof(useMultipleSprites))]
    [Tooltip("子オブジェクトに含まれるSpriteRendererを自動的に追加するかどうか")]
    private bool autoRegisterChildSprites = false;

    [SerializeField, ShowIf(nameof(useMultipleSprites))]
    [Tooltip("自動追加以外に、手動で追加したいSpriteRendererがあれば登録します")]
    private List<SpriteRenderer> targetSpriteRenderers = new List<SpriteRenderer>();
    #endregion

    #region Private Fields
    private bool shouldHideAfterDefeat = true; //撃破後に非表示にするか
    private FlagManager flagManager;
    private SpriteRenderer spriteRenderer;
    ParticleSystem[] psList = null;
    private List<SpriteRenderer> allSpriteRenderers = new List<SpriteRenderer>(); // 演出対象となる全てのスプライト
    // --- ここまで追加 --
    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        if (flowchart == null)
        {
            Debug.LogWarning($"{this.gameObject.name}にはFlowChartが設定されていません");
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            allSpriteRenderers.Add(spriteRenderer); // 自身をリストに追加
        }
        else
        {
            Debug.LogError(
                "SpriteRendererが見つかりません。ボス撃破後のスプライトが正しく設定されていない可能性があります。"
            );
        }

        // 複数スプライト対応の初期化処理
        if (useMultipleSprites)
        {
            // 1. 子オブジェクトからの自動取得
            if (autoRegisterChildSprites)
            {
                SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>();
                foreach (var sr in childRenderers)
                {
                    if (sr != null && !allSpriteRenderers.Contains(sr))
                    {
                        allSpriteRenderers.Add(sr);
                    }
                }
            }

            // 2. 手動登録分の追加
            if (targetSpriteRenderers != null)
            {
                foreach (var sr in targetSpriteRenderers)
                {
                    if (sr != null && !allSpriteRenderers.Contains(sr))
                    {
                        allSpriteRenderers.Add(sr);
                    }
                }
            }
        }
    }

    public void Start()
    {
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogError("FlagManagerが見つかりません。ボス撃破イベントが正しく動作しません。");
            return;
        }

        switch (bossName)
        {
            case BossHealth.BossName.DustDevilBoss:
                if (sandSmokeEffect != null)
                {
                    psList = sandSmokeEffect.GetComponentsInChildren<ParticleSystem>();
                }
                else
                {
                    Debug.LogWarning(
                        "砂嵐のボスの撃破後エフェクトが設定されていません。砂嵐エフェクトが正しく動作しません。"
                    );
                }
                break;
            case BossHealth.BossName.DesertTempleBossSmoke:
                shouldHideAfterDefeat = false;
                break;
            case BossHealth.BossName.DesertTempleBoss:
                shouldHideAfterDefeat = false;
                break;
        }
        StartCoroutine(DefeatBoss());
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// この撃破後イベントが処理するボスの名前を設定します。
    /// </summary>
    /// <param name="newBossName">設定したいボスの名前</param>
    public void SetBossName(BossHealth.BossName newBossName)
    {
        this.bossName = newBossName;
    }

    #endregion

    #region Core Logic (Coroutine)

    private IEnumerator DefeatBoss()
    {
        // --- 初期設定とフラッシュ演出 ---

        // 各SpriteRendererの初期色とHSV値を保存しておく辞書を作成
        Dictionary<SpriteRenderer, Color> initialColors = new Dictionary<SpriteRenderer, Color>();
        Dictionary<SpriteRenderer, Vector3> initialHSVs = new Dictionary<SpriteRenderer, Vector3>();

        foreach (var sr in allSpriteRenderers)
        {
            Color col = sr.color;
            initialColors[sr] = col;
            Color.RGBToHSV(col, out float h, out float s, out float v);
            initialHSVs[sr] = new Vector3(h, s, v); // X = Hue, Y = Saturation, Z = Value
        }

        FlashPanel.SetActive(true); // FlashPanelを表示する

        // 仮
        if (bossName == BossHealth.BossName.DesertTempleBoss)
        {
            FadeCanvas.instance?.FadeOut(3.0f); // フェードアウトする
            BGMManager.instance?.FadeOut(3.0f); // BGMをフェードアウトする
        }

        for (int i = 0; i < DEFEAT_FLASH_COUNT; i++)
        {
            SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Impact1); // 衝撃音を鳴らす

            for (int j = 0; j < 10; j++)
            {
                // 全てのSpriteRendererの色を更新
                foreach (var sr in allSpriteRenderers)
                {
                    Vector3 hsv = initialHSVs[sr];
                    sr.color = Color.HSVToRGB(hsv.x, hsv.y, (j + 1) * (hsv.z / 10f));
                }

                FlashPanel.GetComponent<Image>().color = new Color(
                    1,
                    1,
                    1,
                    0.8f * (1f - (j + 1) / 10f)
                );
                yield return new WaitForSeconds(0.1f); // 0.1秒待つ
            }
        }
        FlashPanel.SetActive(false); // FlashPanelを非表示にする

        if (shouldHideAfterDefeat)
        {
            // --- パーティクル生成 ---

            // SpriteRendererのboundsからワールド空間での実際の横幅を取得
            float bossWidth = 1f;
            if (spriteRenderer != null)
            {
                Bounds bossBounds = spriteRenderer.bounds;
                bossWidth = bossBounds.size.x;
            }

            Vector3 newPos = this.transform.position; // 自分の座標を取得
            GameObject newGameObject = Instantiate(BossDefeatParticle); // Particleを出現させる
            newGameObject.transform.position = new Vector2(newPos.x, newPos.y + particleoffsetY); // Particleの座標を設定

            ParticleSystem particleSystem = newGameObject.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                // Shapeモジュールを取得
                var shapeModule = particleSystem.shape;

                // Shapeのスケール（出現範囲の大きさ）をボスの横幅に合わせる
                // YとZのスケールは元の値を維持する
                shapeModule.radius = bossWidth * 0.25f + PARTICLE_RADIUS_OFFSET; // ボスの横幅の半分を設定
            }

            // --- 演出と消滅 ---

            BGMManager.instance?.FadeOut(3.0f); // BGMをフェードアウトする
            CameraManager.instance?.PlayCustomShake(3.0f, 0.5f, 0.3f * 10); // カメラシェイクを再生

            for (int i = 0; i < 10; i++)
            {
                float ratio = 1f - (i + 1) / 10f; // 現在の不透明度を計算

                // 全てのBossの透明度を徐々に下げていく
                foreach (var sr in allSpriteRenderers)
                {
                    Color origCol = initialColors[sr];
                    sr.color = new Color(origCol.r, origCol.g, origCol.b, ratio);
                }

                switch (bossName)
                {
                    case BossHealth.BossName.DustDevilBoss:
                        if (psList != null)
                        {
                            foreach (var ps in psList)
                            {
                                var emission = ps.emission;
                                emission.rateOverTimeMultiplier = ratio;
                            }
                        }
                        break;
                }
                yield return new WaitForSeconds(0.3f); // 0.3秒待つ
                if (i % 2 == 0)
                {
                    SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Vanish1); // 消滅音を鳴らす
                }
            }
        }

        // --- 撃破後処理（フラグ更新・ログ登録） ---

        switch (bossName)
        {
            case BossHealth.BossName.FirstBoss:
                flagManager.SetBoolFlag(PrologueTriggeredEvent.DefeatFirstBoss, true);
                FungusHelper.ExecuteBlock(flowchart, "FirstBossDefeat");
                BGMManager.instance.Play(BGMCategory.Field_Quiet);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatFirstBoss
                ); // 進行ログを登録
                break;

            case BossHealth.BossName.SlimeBoss:
                flagManager.SetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated, true);
                FungusHelper.ExecuteBlock(flowchart, "RiverBossDefeat");
                BGMManager.instance.Play(BGMCategory.Env_Water_Stream1);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatRiverBoss
                ); // 進行ログを登録
                break;

            case BossHealth.BossName.StoneGolemBoss:
                flagManager.SetBoolFlag(Chapter1TriggeredEvent.CaveBossDefeated, true);
                FungusHelper.ExecuteBlock(flowchart, "CaveBossDefeat");
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatHouseCaveBoss
                ); // 進行ログを登録
                break;

            case BossHealth.BossName.DustDevilBoss:
                //FlagはFlowchart側で立てるため、ここでは立てない
                //flagManager.SetBoolFlag(Chapter2TriggeredEvent.DustDevilBossDefeated, true);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DustDevilBossDefeat
                ); // 進行ログを登録
                PlayerManager.instance.SetPlayerBoolStatus(
                    PlayerStatusBoolName.isCanUseShield,
                    true
                ); // シールド機能を解放する
                GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.Shield); // Tipsを登録する
                FungusHelper.ExecuteBlock(flowchart, "DustDevilBossDefeat");
                break;
            case BossHealth.BossName.DesertTempleBossSmoke:
                BGMManager.instance?.FadeOut(3.0f); // BGMをフェードアウトする
                // フラグはFlowchart側で立てるため、ここでは立てない
                // flagManager.SetBoolFlag(Chapter2TriggeredEvent.TempleBossSmokeDefeated, true);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.TempleBossSmokeDefeat
                ); // 進行ログを登録
                FungusHelper.ExecuteBlock(flowchart, "TempleBossSmokeDefeat");
                break;
            case BossHealth.BossName.DesertTempleBoss:
                FungusHelper.ExecuteBlock(flowchart, "TempleBossDefeat");
                break;

            case BossHealth.BossName.None:
                Debug.LogWarning(
                    "BossNameがNoneに設定されています。撃破イベントを処理できません。"
                );
                break;
        }

        if (shouldHideAfterDefeat)
        {
            Destroy(this.gameObject); // 自分を消す
        }
    }

    #endregion
}
