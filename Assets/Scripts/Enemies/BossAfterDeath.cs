using System.Collections;
using System.Collections.Generic;
using MyGame.CameraControl;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ボス撃破時に再生する演出と、ボス固有の進行処理をまとめて扱うコンポーネント。
/// 新しいボスを追加する場合は、BossHealth.BossName に列挙を追加し、
/// ConfigureBossSpecificSettings() と ApplyBossDefeatResult() の switch に対応を追加する。
/// </summary>
public class BossAfterDeath : MonoBehaviour
{
    #region 定数

    private const float PARTICLE_RADIUS_OFFSET = 1f;
    private const int DEFEAT_FLASH_COUNT = 3;
    private const int DEFEAT_FADE_STEPS = 10;
    private const float DEFEAT_FLASH_INTERVAL = 0.1f;
    private const float DEFEAT_FADE_INTERVAL = 0.3f;

    #endregion

    #region Inspector設定

    [Header("Fungus設定")]
    public Fungus.Flowchart flowchart = null;

    [Header("エフェクト設定")]
    [SerializeField]
    private float particleoffsetY;

    [SerializeField]
    private GameObject FlashPanel;

    [SerializeField]
    private GameObject BossDefeatParticle;

    [Header("ボス情報")]
    [SerializeField]
    private bool isBossNamePreSet = false;

    [SerializeField, ShowIf(nameof(isBossNamePreSet))]
    private BossHealth.BossName bossName = BossHealth.BossName.None;

    [SerializeField, ShowIf(nameof(bossName), BossHealth.BossName.DustDevilBoss)]
    private GameObject sandSmokeEffect;

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

    #region 実行時キャッシュ

    private bool shouldHideAfterDefeat = true;
    private FlagManager flagManager;
    private SpriteRenderer spriteRenderer;
    private ParticleSystem[] psList = null;
    private List<SpriteRenderer> allSpriteRenderers = new List<SpriteRenderer>();

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        if (flowchart == null)
        {
            Debug.LogWarning($"{this.gameObject.name}にはFlowChartが設定されていません");
        }

        RegisterSpriteRenderers();
    }

    private void Start()
    {
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogError("FlagManagerが見つかりません。ボス撃破イベントが正しく動作しません。");
            return;
        }

        ConfigureBossSpecificSettings();
        StartCoroutine(DefeatBoss());
    }

    #endregion

    #region 公開API

    /// <summary>
    /// この撃破後イベントが処理するボス名を設定する。
    /// </summary>
    /// <param name="newBossName">設定するボスの種類</param>
    public void SetBossName(BossHealth.BossName newBossName)
    {
        bossName = newBossName;
    }

    #endregion

    #region ボス固有初期化

    /// <summary>
    /// ボス固有の初期設定を行う。
    /// 新しい Boss を追加した場合は、この switch に対応を追加する。
    /// ただし実際の列挙追加は BossHealth.BossName 側で行う。
    /// </summary>
    private void ConfigureBossSpecificSettings()
    {
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
        }
    }

    #endregion

    #region 撃破演出

    /// <summary>
    /// ボス撃破時の演出全体をまとめて管理するコルーチン。
    /// 1. 初期色の記録
    /// 2. フラッシュ演出
    /// 3. 透明化演出
    /// 4. ボス固有の結果反映
    /// の順に処理する。
    /// </summary>
    private IEnumerator DefeatBoss()
    {
        var initialColors = new Dictionary<SpriteRenderer, Color>();
        var initialHSVs = new Dictionary<SpriteRenderer, Vector3>();

        foreach (var sr in allSpriteRenderers)
        {
            Color color = sr.color;
            initialColors[sr] = color;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            initialHSVs[sr] = new Vector3(h, s, v);
        }

        if (FlashPanel != null)
        {
            FlashPanel.SetActive(true);
        }

        if (bossName == BossHealth.BossName.DesertTempleBoss)
        {
            FadeCanvas.instance?.FadeOut(3.0f);
            BGMManager.instance?.FadeOut(3.0f);
        }

        for (int i = 0; i < DEFEAT_FLASH_COUNT; i++)
        {
            SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Impact1);

            for (int j = 0; j < 10; j++)
            {
                foreach (var sr in allSpriteRenderers)
                {
                    Vector3 hsv = initialHSVs[sr];
                    sr.color = Color.HSVToRGB(hsv.x, hsv.y, (j + 1) * (hsv.z / 10f));
                }

                if (FlashPanel != null)
                {
                    FlashPanel.GetComponent<Image>().color = new Color(
                        1f,
                        1f,
                        1f,
                        0.8f * (1f - (j + 1) / 10f)
                    );
                }

                yield return new WaitForSeconds(DEFEAT_FLASH_INTERVAL);
            }
        }

        if (FlashPanel != null)
        {
            FlashPanel.SetActive(false);
        }

        if (shouldHideAfterDefeat)
        {
            SpawnDefeatParticle();
            BGMManager.instance?.FadeOut(3.0f);
            CameraManager.instance?.PlayCustomShake(3.0f, 0.5f, 0.3f * 10);

            for (int i = 0; i < DEFEAT_FADE_STEPS; i++)
            {
                float ratio = 1f - (i + 1) / DEFEAT_FADE_STEPS;
                SetAllSpritesAlpha(initialColors, ratio);
                UpdateBossSpecificDefeatEffect(ratio);

                yield return new WaitForSeconds(DEFEAT_FADE_INTERVAL);

                if (i % 2 == 0)
                {
                    SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Vanish1);
                }
            }
        }

        ApplyBossDefeatResult();

        if (shouldHideAfterDefeat)
        {
            Destroy(this.gameObject);
        }
    }

    #endregion

    #region ボス固有の結果処理

    /// <summary>
    /// ボス撃破後の各種処理をまとめて実行する。
    /// 新しい Boss を追加した場合は、BossHealth.BossName の列挙とこの switch を同時に更新する。
    /// </summary>
    private void ApplyBossDefeatResult()
    {
        switch (bossName)
        {
            case BossHealth.BossName.FirstBoss:
                flagManager.SetBoolFlag(PrologueTriggeredEvent.DefeatFirstBoss, true);
                FungusHelper.ExecuteBlock(flowchart, "FirstBossDefeat");
                BGMManager.instance.Play(BGMCategory.Field_Quiet);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatFirstBoss
                );
                break;

            case BossHealth.BossName.SlimeBoss:
                flagManager.SetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated, true);
                FungusHelper.ExecuteBlock(flowchart, "RiverBossDefeat");
                BGMManager.instance.Play(BGMCategory.Env_Water_Stream1);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatRiverBoss
                );
                break;

            case BossHealth.BossName.StoneGolemBoss:
                flagManager.SetBoolFlag(Chapter1TriggeredEvent.CaveBossDefeated, true);
                FungusHelper.ExecuteBlock(flowchart, "CaveBossDefeat");
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatHouseCaveBoss
                );
                break;

            case BossHealth.BossName.DustDevilBoss:
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DustDevilBossDefeat
                );
                PlayerManager.instance.SetPlayerBoolStatus(
                    PlayerStatusBoolName.isCanUseShield,
                    true
                );
                GameManager.instance.savedata.TipsData.RegisterTipsData(TipsName.Shield);
                FungusHelper.ExecuteBlock(flowchart, "DustDevilBossDefeat");
                break;

            case BossHealth.BossName.DesertTempleBossSmoke:
                BGMManager.instance?.FadeOut(3.0f);
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.TempleBossSmokeDefeat
                );
                GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                    EnemyName.DustDevil_Active
                );
                GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                    EnemyName.Golem_DesertTemple_Float
                );
                GameManager.instance.savedata.EnemyRecordData.RegisterEncounter(
                    EnemyName.Golem_DesertTemple_Walk
                );
                FungusHelper.ExecuteBlock(flowchart, "TempleBossSmokeDefeat");
                break;

            case BossHealth.BossName.DesertTempleBoss:
                flagManager.SetBoolFlag(Chapter2TriggeredEvent.TempleBossDefeated,true);
                FungusHelper.ExecuteBlock(flowchart, "TempleBossDefeat");
                break;

            case BossHealth.BossName.None:
                Debug.LogWarning(
                    "BossNameがNoneに設定されています。撃破イベントを処理できません。"
                );
                break;
        }
    }

    #endregion

    #region 補助処理

    /// <summary>
    /// ボスの SpriteRenderer を演出対象として取得する。
    /// 自身のSpriteRendererと、useMultipleSprites が有効な場合は子要素や手動登録分もまとめて扱う。
    /// </summary>
    private void RegisterSpriteRenderers()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            allSpriteRenderers.Add(spriteRenderer);
        }
        else
        {
            Debug.LogError(
                "SpriteRendererが見つかりません。ボス撃破後のスプライトが正しく設定されていない可能性があります。"
            );
        }

        if (useMultipleSprites)
        {
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

    /// <summary>
    /// ボス破壊時に出現させるパーティクルを生成する。
    /// 生成位置はボスの位置に合わせ、横幅に応じて放射範囲を調整する。
    /// </summary>
    private void SpawnDefeatParticle()
    {
        if (BossDefeatParticle == null)
        {
            return;
        }

        float bossWidth = 1f;
        if (spriteRenderer != null)
        {
            Bounds bossBounds = spriteRenderer.bounds;
            bossWidth = bossBounds.size.x;
        }

        Vector3 newPos = transform.position;
        GameObject newGameObject = Instantiate(BossDefeatParticle);
        newGameObject.transform.position = new Vector2(newPos.x, newPos.y + particleoffsetY);

        ParticleSystem particleSystem = newGameObject.GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            var shapeModule = particleSystem.shape;
            shapeModule.radius = bossWidth * 0.25f + PARTICLE_RADIUS_OFFSET;
        }
    }

    /// <summary>
    /// ボス固有の消滅演出を適用する。
    /// 新しいボスで独自の破壊演出が必要な場合、ここに switch を追加する。
    /// </summary>
    /// <param name="ratio">現在の透明度割合</param>
    private void UpdateBossSpecificDefeatEffect(float ratio)
    {
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
    }

    /// <summary>
    /// 演出対象の全 SpriteRenderer のアルファ値をまとめて更新する。
    /// </summary>
    /// <param name="initialColors">初期のカラー情報</param>
    /// <param name="ratio">現在の透明度</param>
    private void SetAllSpritesAlpha(Dictionary<SpriteRenderer, Color> initialColors, float ratio)
    {
        foreach (var sr in allSpriteRenderers)
        {
            Color origCol = initialColors[sr];
            sr.color = new Color(origCol.r, origCol.g, origCol.b, ratio);
        }
    }

    #endregion
}
