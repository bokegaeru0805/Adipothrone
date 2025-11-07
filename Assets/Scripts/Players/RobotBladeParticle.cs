using UnityEngine;

public class RobotBladeParticle : MonoBehaviour
{
    [SerializeField]
    private GameObject BladeObject;

    [SerializeField]
    private TrailRenderer trail;
    private float myangle = 0;

    [HideInInspector]
    public float BladeLenght = 0;
    private Robot_move robotMoveScript;

    private void Start()
    {
        trail.emitting = false;
        trail.startWidth = 0.6f;
        trail.endWidth = 0.0f;

        if (BladeObject != null)
        {
            // Robot_blade_move は Robot_move の子である想定
            robotMoveScript = BladeObject.GetComponentInParent<Robot_move>();
        }

        if (robotMoveScript == null)
        {
            Debug.LogError("Robot_moveスクリプトが親階層に見つかりません。", this);
        }
        else
        {
            // Robot_move のイベントを購読
            robotMoveScript.OnBladeSwingingChanged += HandleBladeSwingingChanged;
        }
    }

    private void OnEnable()
    {
        if (robotMoveScript != null)
        {
            // Robot_move のイベントを購読
            robotMoveScript.OnBladeSwingingChanged += HandleBladeSwingingChanged;
            
            // 起動時の初期状態を同期
            HandleBladeSwingingChanged(robotMoveScript.isBladeSwinging);
        }
    }

    private void OnDisable()
    {
        if (robotMoveScript != null)
        {
            // 購読解除
            robotMoveScript.OnBladeSwingingChanged -= HandleBladeSwingingChanged;
        }
    }

    /// <summary>
    /// 剣の振り状態が変わったときに Robot_move から呼ばれる
    /// </summary>
    private void HandleBladeSwingingChanged(bool isSwinging)
    {
        // 状態が変わった瞬間に Trail の emitting を切り替える
        trail.emitting = isSwinging;
    }

    private void FixedUpdate()
    {

        // isBladeAttackの監視を削除し、trail.emitting が true の場合（＝攻撃中）のみ処理する
        if (trail.emitting)
        {
            float Bladeangle = BladeObject.transform.eulerAngles.z;
            if (270 <= Bladeangle)
                Bladeangle = Bladeangle - 360;
            myangle = Bladeangle <= 90 ? Bladeangle * (4f / 3f) : (4f / 3f) * Bladeangle - 60;
            Vector3 offset = new Vector3(
                BladeLenght * Mathf.Cos(myangle * Mathf.Deg2Rad),
                BladeLenght * Mathf.Sin(myangle * Mathf.Deg2Rad),
                0
            );
            this.transform.position = BladeObject.transform.position + offset;
        }
    }
}
