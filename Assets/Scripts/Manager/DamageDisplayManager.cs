using DamageNumbersPro;
using UnityEngine;

/// <summary>
/// DamageDisplayスキル装備中のダメージ数値表示を管理します。
/// </summary>
public class DamageDisplayManager : MonoBehaviour
{
    private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthProperty = Shader.PropertyToID("_OutlineWidth");

    [Header("Damage Numbers Pro")]
    [Tooltip("ダメージ表示に使用するDamageNumberMeshのプリセットPrefabを設定します")]
    [SerializeField]
    private DamageNumberMesh _damageNumberPrefab;

    [Tooltip("連続狩猟の表示に使用するDamageNumberMeshのプリセットPrefabを設定します")]
    [SerializeField]
    private DamageNumberMesh _consecutiveHuntPrefab;

    [Tooltip("生成するダメージ数値の表示倍率")]
    [SerializeField]
    [Min(0.1f)]
    private float _damageNumberScale = 1.75f;

    [Tooltip("生成するダメージ数値の基本色")]
    [SerializeField]
    private Color _damageNumberColor = new Color(1f, 0.65f, 0.1f, 1f);

    [Tooltip("ダメージ数値のアウトライン色")]
    [SerializeField]
    private Color _outlineColor = new Color(0.1f, 0.05f, 0f, 1f);

    [Tooltip("ダメージ数値のアウトライン幅")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _outlineWidth = 0.2f;

    public static DamageDisplayManager instance { get; private set; }

    public float DamageNumberScale => _damageNumberScale;
    public Color DamageNumberColor => _damageNumberColor;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// DamageDisplayスキルが装備されている場合、指定位置へダメージ値を表示します。
    /// </summary>
    public void ShowDamage(Vector3 position, int damage, Transform followedTarget)
    {
        if (_damageNumberPrefab == null)
            return;

        if (
            SkillManager.instance == null
            || !SkillManager.instance.IsSkillActive(SkillName.DamageDisplay)
        )
        {
            return;
        }

        var spawnedDamageNumber =
            followedTarget != null
                ? _damageNumberPrefab.Spawn(position, damage, followedTarget)
                : _damageNumberPrefab.Spawn(position, damage);
        spawnedDamageNumber.SetScale(_damageNumberScale);
        spawnedDamageNumber.SetColor(_damageNumberColor);
        ApplyOutline(spawnedDamageNumber);
    }

    private void ApplyOutline(DamageNumber damageNumber)
    {
        foreach (Material material in damageNumber.GetMaterials())
        {
            if (material == null)
                continue;

            if (material.HasProperty(OutlineColorProperty))
                material.SetColor(OutlineColorProperty, _outlineColor);

            if (material.HasProperty(OutlineWidthProperty))
                material.SetFloat(OutlineWidthProperty, _outlineWidth);

            material.EnableKeyword("OUTLINE_ON");
        }
    }

    /// <summary>
    /// 連続狩猟の現在の連続数とドロップ率補正を表示します。
    /// </summary>
    public void ShowConsecutiveHunt(Vector3 position, ConsecutiveHuntResult result)
    {
        if (_consecutiveHuntPrefab == null || !result.IsActive)
            return;

        string maxLabel = result.IsMax ? "（上限到達）" : string.Empty;
        string displayText = $"連続撃破 ×{result.ConsecutiveKills}{maxLabel}";

        DamageNumber popup = _consecutiveHuntPrefab.Spawn(position, displayText);
        popup.SetColor(_damageNumberColor);
        ApplyOutline(popup);

        if (result.IsBonusIncreased || result.IsMax)
            popup.SetScale(_damageNumberScale * 1.15f);
        else
            popup.SetScale(_damageNumberScale);
    }
}
