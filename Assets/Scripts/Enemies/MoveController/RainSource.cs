using System.Collections;
using UnityEngine;

public class RainSource : MonoBehaviour
{
    [Header("雨のダメージ設定")]
    [SerializeField]
    private int rainDamage = 0; // 雨がプレイヤーに与えるダメージ量

    [Header("プレイヤーのオブジェクト設定")]
    [SerializeField]
    private GameObject PlayerObject;

    [Header("雨の種類")]
    [SerializeField]
    private RainType raintype;

    [Header("雨の発生範囲設定")]
    [SerializeField]
    private Vector2 rainCornerA; // 雨が発生する範囲の左下端

    [SerializeField]
    private Vector2 rainCornerB; // 雨が発生する範囲の右上端

    [Header("降雨の間隔設定")]
    [SerializeField]
    private float IntervalMin; // 降雨の間隔の最小値

    [SerializeField]
    private float IntervalMax; // 降雨の間隔の最大値

    [Header("雨の存在範囲設定")]
    [SerializeField]
    private float ExistBottom; // 雨が存在できる一番下の座標

    [SerializeField]
    private float ExistLeft; // 雨が存在できる一番左の座標

    [SerializeField]
    private float ExistRight; // 雨が存在できる一番右の座標

    [Header("プレイヤーの感知範囲設定")]
    [SerializeField]
    private float DetectTop; //プレイヤーを感知する一番上の座標

    [SerializeField]
    private float DetectBottom; //プレイヤーを感知する一番下の座標

    [SerializeField]
    private float DetectLeft; //プレイヤーを感知する一番左の座標

    [SerializeField]
    private float DetectRight; //プレイヤーを感知する一番右の座標

    [Header("雨の落下時間設定")]
    [SerializeField]
    private float FallTimeMin = 1; //雨が地面に到達するまでの最小時間

    [SerializeField]
    private float FallTimeMax = 1; // 雨が地面に到達するまでの最大時間

    [Header("雨のプレハブ")]
    [SerializeField]
    private GameObject rain_prefab; // 雨のプレハブ
    private bool isEnable; //存在しているかどうかのフラグ
    private Vector3 PlayerPosition;

    private enum RainType
    {
        none = 0, // 雨の種類を指定しない
        normal = 10, // 垂直に降る雨
        parabola = 20, // 放物線を描いて降る雨
    }

    private void Awake()
    {
        if (rainDamage <= 0)
        {
            Debug.LogError("RainSourceの雨のダメージ量が設定されていません。");
        }

        if (rain_prefab == null)
        {
            Debug.LogError("RainSourceに雨のプレハブが設定されていません。");
        }

        if (raintype == RainType.none)
        {
            Debug.LogError("RainSourceの雨の種類が設定されていません。");
        }
    }

    private void Start()
    {
        if (PlayerObject == null)
            PlayerObject = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName);
        // プレイヤーオブジェクトを探して格納
        if (PlayerObject == null)
        {
            Debug.LogError(
                "RainSource: PlayerObjectが見つかりません。タグ 'Player' を確認してください。"
            );
            return;
        }
        PlayerPosition = PlayerObject.transform.position;
        isEnable = false;
        switch (raintype)
        {
            case RainType.normal:
                StartCoroutine(VerticalRain());
                break;
            case RainType.parabola:
                StartCoroutine(ParabolicRain());
                break;
        }
    }

    private IEnumerator VerticalRain()
    {
        while (true)
        {
            PlayerPosition = PlayerObject.transform.position;
            bool isInArea =
                DetectLeft < PlayerPosition.x
                && PlayerPosition.x < DetectRight
                && DetectBottom < PlayerPosition.y
                && PlayerPosition.y < DetectTop;

            if (isInArea)
            {
                if (!isEnable)
                {
                    isEnable = true;
                }

                float interval = Random.Range(IntervalMin, IntervalMax);
                yield return new WaitForSeconds(interval);
                float AppearY = Random.Range(rainCornerA.y, rainCornerB.y);
                Vector2 spawnPos = new Vector2(Random.Range(rainCornerA.x, rainCornerB.x), AppearY);
                GameObject rain = Instantiate(rain_prefab, spawnPos, Quaternion.identity); // 雨を生成
                var script = rain.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
                if (script != null)
                {
                    script.SetDamageAmount(rainDamage); // 雨のダメージを設定
                }
                else
                {
                    Debug.LogWarning("Rain prefab does not have ContactDamageController script.");
                }

                rain.transform.SetParent(this.transform); // 雨の親をこのオブジェクトに設定（雲に追従）
                Rigidbody2D newrbody = rain.GetComponent<Rigidbody2D>(); //雨のRigidbody2Dを取得
                float FallTime = Random.Range(FallTimeMin, FallTimeMax);
                float vy = (ExistBottom - AppearY) / FallTime;
                newrbody.AddForce(new Vector2(0, vy), ForceMode2D.Impulse); //雨の速度を設定

                // 雨の動きをコルーチンで制御（地面に到達したら削除）
                StartCoroutine(DestroyRain(rain));
            }
            else
            {
                if (isEnable)
                {
                    isEnable = false;
                    foreach (Transform child in transform)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
            yield return null; // 条件を満たさなくても、必ずフレームを待つ！
        }
    }

    private IEnumerator ParabolicRain()
    {
        while (true)
        {
            PlayerPosition = PlayerObject.transform.position;
            bool isInArea =
                DetectLeft < PlayerPosition.x
                && PlayerPosition.x < DetectRight
                && DetectBottom < PlayerPosition.y
                && PlayerPosition.y < DetectTop;

            if (isInArea)
            {
                if (!isEnable)
                {
                    isEnable = true;
                }

                float interval = Random.Range(IntervalMin, IntervalMax);
                yield return new WaitForSeconds(interval);

                float AppearX = Random.Range(rainCornerA.x, rainCornerB.x);
                float AppearY = Random.Range(rainCornerA.y, rainCornerB.y);
                Vector2 spawnPos = new Vector2(AppearX, AppearY);
                GameObject rain = Instantiate(rain_prefab, spawnPos, Quaternion.identity); // 雨を生成
                var script = rain.GetComponent<ContactDamageController>(); //ダメージに関するスクリプトを取得
                if (script != null)
                {
                    script.SetDamageAmount(rainDamage); // 雨のダメージを設定
                }
                else
                {
                    Debug.LogWarning("Rain prefab does not have ContactDamageController script.");
                }

                Rigidbody2D newrbody = rain.GetComponent<Rigidbody2D>();
                float targetPointX = Random.Range(ExistLeft, ExistRight);
                float FallTime = Random.Range(FallTimeMin, FallTimeMax);
                float vx = (targetPointX - AppearX) / FallTime;
                float vy = (ExistBottom - AppearY) / FallTime;
                newrbody.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse);

                rain.transform.SetParent(this.transform);

                StartCoroutine(DestroyRain(rain));
            }
            else
            {
                if (isEnable)
                {
                    isEnable = false;
                    foreach (Transform child in transform)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            yield return null;
        }
    }

    private IEnumerator DestroyRain(GameObject rain)
    {
        Rigidbody2D rbody = rain.GetComponent<Rigidbody2D>(); //Rigidbody2Dコンポーネントを取得

        while (rain != null)
        {
            Vector3 pos = rain.transform.position;
            if (pos.y < ExistBottom || pos.x < ExistLeft || pos.x > ExistRight)
            {
                Destroy(rain);
                int isDropSoundPlay = Random.Range(1, 6);
                if (isDropSoundPlay == 1 && SEManager.instance != null)
                {
                    SEManager.instance.PlayFieldSE(SE_Field.WaterDrop1);
                }
                yield break;
            }

            float vx = rbody.velocity.x; //速度のx成分を取得
            float vy = rbody.velocity.y; //速度のy成分を取得
            rain.transform.eulerAngles = new Vector3(0, 0, Mathf.Atan2(vy, vx) * Mathf.Rad2Deg); //向きを設定
            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center1 = new Vector3(
            (DetectLeft + DetectRight) / 2f,
            (DetectTop + DetectBottom) / 2f,
            0f
        );
        Vector3 size1 = new Vector3(
            Mathf.Abs(DetectRight - DetectLeft),
            Mathf.Abs(DetectTop - DetectBottom),
            0f
        );
        Gizmos.DrawWireCube(center1, size1);

        Gizmos.color = Color.red;
        Vector3 center2 = new Vector3(
            (ExistLeft + ExistRight) / 2f,
            (DetectTop + ExistBottom) / 2f,
            0f
        );
        Vector3 size2 = new Vector3(
            Mathf.Abs(ExistRight - ExistLeft),
            Mathf.Abs(DetectTop - ExistBottom),
            0f
        );
        Gizmos.DrawWireCube(center2, size2);
    }
}
