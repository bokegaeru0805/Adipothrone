using System.Collections;
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
    #endregion

    #region Private Fields

    private FlagManager flagManager;
    private SpriteRenderer spriteRenderer;
    ParticleSystem[] psList = null;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        if (flowchart == null)
        {
            Debug.LogWarning($"{this.gameObject.name}にはFlowChartが設定されていません");
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError(
                "SpriteRendererが見つかりません。ボス撃破後のスプライトが正しく設定されていない可能性があります。"
            );
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

        Color bossCol = spriteRenderer.color; // 自分の色を取得
        Color.RGBToHSV(bossCol, out float H, out float S, out float V);
        float boss_hue = H;
        float boss_saturation = S;
        float boss_value = V;

        FlashPanel.SetActive(true); // FlashPanelを表示する

        for (int i = 0; i < DEFEAT_FLASH_COUNT; i++)
        {
            SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Impact1); // 衝撃音を鳴らす

            for (int j = 0; j < 10; j++)
            {
                spriteRenderer.color = Color.HSVToRGB(
                    boss_hue,
                    boss_saturation,
                    (j + 1) * (boss_value / 10)
                );
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

        // --- パーティクル生成 ---

        // SpriteRendererのboundsからワールド空間での実際の横幅を取得
        Bounds bossBounds = spriteRenderer.bounds;
        float bossWidth = bossBounds.size.x;

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

            // Bossの透明度を徐々に下げていく
            spriteRenderer.color = new Color(bossCol.r, bossCol.g, bossCol.b, ratio);

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

        // --- 撃破後処理（フラグ更新・ログ登録） ---

        switch (bossName)
        {
            case BossHealth.BossName.FirstBoss:
                flagManager.SetBoolFlag(PrologueTriggeredEvent.DefeatFirstBoss, true);
                FungusHelper.ExecuteBlock(flowchart, "FirstBossDefeat");
                BGMManager.instance.Play(BGMCategory.Field_Quiet); // 指定したBGMを再生
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatFirstBoss
                ); // 初ボス撃破のログを登録
                break;

            case BossHealth.BossName.SlimeBoss:
                flagManager.SetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated, true);
                FungusHelper.ExecuteBlock(flowchart, "RiverBossDefeat");
                BGMManager.instance.Play(BGMCategory.Env_Water_Stream1); // 指定したBGMを再生
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatRiverBoss
                ); // 川のボス撃破のログを登録
                break;

            case BossHealth.BossName.StoneGolemBoss:
                flagManager.SetBoolFlag(Chapter1TriggeredEvent.CaveBossDefeated, true);
                FungusHelper.ExecuteBlock(flowchart, "CaveBossDefeat");
                GameManager.instance.savedata.ProgressLogData.RegisterProgressData(
                    ProgressLogName.DefeatHouseCaveBoss
                ); // 家の洞窟のボス撃破のログを登録
                break;

            case BossHealth.BossName.None:
                Debug.LogWarning(
                    "BossNameがNoneに設定されています。撃破イベントを処理できません。"
                );
                break;
        }

        Destroy(this.gameObject); // このオブジェクトを消す
    }

    #endregion
}
