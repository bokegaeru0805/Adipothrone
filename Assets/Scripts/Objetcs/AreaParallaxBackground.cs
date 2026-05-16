using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// 指定されたCameraMoveAreaと連動し、プレイヤーの移動やカメラの動きに合わせて
/// 複数の背景オブジェクトをパララックス（視差）スクロールさせるコンポーネント。
/// </summary>
public class AreaParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        [Tooltip("コピー元となる背景オブジェクト（スプライトが含まれている必要があります）")]
        public GameObject baseBackground;

        [Tooltip(
            "スクロール速度の係数\n0: 通常の背景と同じ（ワールド座標に固定）\n1: カメラに完全追従（画面に固定されて動かない）\n0〜1の間で設定すると遠景の視差効果になります"
        )]
        public float scrollFactor = 0.5f;

        [HideInInspector]
        public List<Transform> _instances = new List<Transform>();

        [HideInInspector]
        public float _spriteWidth;
    }

    [SerializeField]
    [Tooltip("連動させる対象のCameraMoveAreaを設定します")]
    private CameraMoveArea _targetCameraArea;

    [SerializeField]
    private List<ParallaxLayer> _parallaxLayers = new List<ParallaxLayer>();

    private Camera _camera;
    private Transform _playerTransform;
    private Collider2D _targetCollider;
    private bool _isPlayerInArea = false;
    private float _lastCameraPosX;

    private void Awake()
    {
        _camera = Camera.main;

        // 連動対象のエリアからコライダーコンポーネントを取得しておく
        if (_targetCameraArea != null)
        {
            _targetCollider = _targetCameraArea.GetComponent<Collider2D>();
        }

        InitializeLayers();
    }

    private void OnEnable()
    {
        // CameraMoveAreaの静的イベントに自身のメソッドを登録
        CameraMoveArea.OnPlayerEnteredArea += OnPlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea += OnPlayerExitedArea;

        // Cinemachineのカメラ計算が完了したタイミングのイベントを購読（UnityEventのためAddListenerを使用）
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        // 破棄・無効化時にイベントの登録を解除
        CameraMoveArea.OnPlayerEnteredArea -= OnPlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea -= OnPlayerExitedArea;

        // UnityEventの登録解除のためRemoveListenerを使用
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    /// <summary>
    /// ゲーム開始時に必要な枚数を計算し、事前に複製してプールします。
    /// </summary>
    private void InitializeLayers()
    {
        if (_camera == null)
            return;

        // カメラの半幅を計算
        float cameraHalfWidth = _camera.orthographicSize * _camera.aspect;

        foreach (var layer in _parallaxLayers)
        {
            if (layer.baseBackground == null)
                continue;

            SpriteRenderer sr = layer.baseBackground.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError("背景オブジェクトにSpriteRendererがありません。", this);
                continue;
            }

            layer._spriteWidth = sr.bounds.size.x;

            // カメラ幅をカバーするのに必要な枚数 + 両端のバッファ2枚
            int requiredCount = Mathf.CeilToInt((cameraHalfWidth * 2f) / layer._spriteWidth) + 2;

            // オリジナルのオブジェクトは非表示にする
            layer.baseBackground.SetActive(false);

            for (int i = 0; i < requiredCount; i++)
            {
                GameObject instance = Instantiate(layer.baseBackground, transform);
                instance.SetActive(false); // 初期状態では非表示
                layer._instances.Add(instance.transform);
            }
        }
    }

    /// <summary>
    /// プレイヤーがいずれかのエリアに入ったときに通知されるイベントハンドラー
    /// </summary>
    private void OnPlayerEnteredArea(CameraMoveArea area)
    {
        // 進入されたエリアが、インスペクターで指定したターゲットでなければ処理しない
        if (area != _targetCameraArea)
            return;
        if (_isPlayerInArea)
            return;

        GetPlayerTransform();
        _isPlayerInArea = true;
        _lastCameraPosX = _camera.transform.position.x;

        // プレイヤーの進入方向（右側からかどうか）を判定
        bool enteredFromRight = false;
        if (_playerTransform != null && _targetCollider != null)
        {
            enteredFromRight = _playerTransform.position.x > _targetCollider.bounds.center.x;
        }

        ArrangeBackgrounds(enteredFromRight);
    }

    /// <summary>
    /// プレイヤーがいずれかのエリアから出たときに通知されるイベントハンドラー
    /// </summary>
    private void OnPlayerExitedArea(CameraMoveArea area)
    {
        // 退出されたエリアが、インスペクターで指定したターゲットでなければ処理しない
        if (area != _targetCameraArea)
            return;

        _isPlayerInArea = false;

        // エリアから出たら全インスタンスを非表示にして描画負荷を下げる
        foreach (var layer in _parallaxLayers)
        {
            foreach (var inst in layer._instances)
            {
                inst.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 指定された要件コードに則ってプレイヤーのTransformを取得・キャッシュします。
    /// </summary>
    private void GetPlayerTransform()
    {
        if (_playerTransform == null)
        {
            if (PlayerManager.instance != null && PlayerManager.instance.PlayerGameObject != null)
            {
                _playerTransform = PlayerManager.instance.PlayerGameObject.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(
                    GameConstants.PLAYER_TAG_NAME
                );
                if (playerObj != null)
                    _playerTransform = playerObj.transform;
            }
        }
    }

    /// <summary>
    /// 進入した方向に基づいて、背景オブジェクトを現在のカメラ枠内に隙間なく再配置します。
    /// </summary>
    private void ArrangeBackgrounds(bool enteredFromRight)
    {
        float camX = _camera.transform.position.x;
        float cameraHalfWidth = _camera.orthographicSize * _camera.aspect;

        foreach (var layer in _parallaxLayers)
        {
            if (layer._instances.Count == 0)
                continue;

            float startX;

            if (enteredFromRight)
            {
                // 右端を基準に左へ向かって敷き詰める
                startX = camX + cameraHalfWidth;
                for (int i = layer._instances.Count - 1; i >= 0; i--)
                {
                    int reverseIndex = (layer._instances.Count - 1) - i;
                    float posX = startX - (reverseIndex * layer._spriteWidth);
                    layer._instances[i].position = new Vector3(
                        posX,
                        layer.baseBackground.transform.position.y,
                        layer.baseBackground.transform.position.z
                    );
                    layer._instances[i].gameObject.SetActive(true);
                }
            }
            else
            {
                // 左端を基準に右へ向かって敷き詰める
                startX = camX - cameraHalfWidth;
                for (int i = 0; i < layer._instances.Count; i++)
                {
                    float posX = startX + (i * layer._spriteWidth);
                    layer._instances[i].position = new Vector3(
                        posX,
                        layer.baseBackground.transform.position.y,
                        layer.baseBackground.transform.position.z
                    );
                    layer._instances[i].gameObject.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Cinemachineの全処理（Confinerの壁押し戻し等）が終わった確定座標で背景を更新します。
    /// これにより端でのあらぶり（ジッター）を防ぎます。
    /// </summary>
    private void OnCameraUpdated(CinemachineBrain brain)
    {
        // メインカメラ以外の処理は無視
        if (brain.OutputCamera != _camera)
            return;

        UpdateParallax();
    }

    /// <summary>
    /// 背景のスクロールとループ処理を行います。
    /// </summary>
    private void UpdateParallax()
    {
        if (!_isPlayerInArea)
            return;

        float camX = _camera.transform.position.x;
        float deltaX = camX - _lastCameraPosX;

        // 微小な計算誤差やブレを無視する（デッドゾーン）
        if (Mathf.Abs(deltaX) < 0.001f)
        {
            deltaX = 0f;
        }

        // カメラが動いていなければ処理をスキップ
        if (deltaX == 0f)
            return;

        foreach (var layer in _parallaxLayers)
        {
            if (layer._instances.Count == 0)
                continue;

            // 背景のスクロール移動
            float moveX = deltaX * layer.scrollFactor;
            foreach (var inst in layer._instances)
            {
                inst.position += new Vector3(moveX, 0, 0);
            }

            // ループ用の境界値を計算
            float cameraHalfWidth = _camera.orthographicSize * _camera.aspect;
            float leftBound = camX - cameraHalfWidth - layer._spriteWidth;
            float rightBound = camX + cameraHalfWidth + layer._spriteWidth;

            Transform first = layer._instances[0];
            Transform last = layer._instances[layer._instances.Count - 1];

            // カメラの移動方向に応じて、片側だけワープ判定を行う
            if (deltaX > 0f && first.position.x < leftBound)
            {
                // カメラが右へ移動：左端の画像を右へワープ
                first.position = new Vector3(
                    last.position.x + layer._spriteWidth,
                    first.position.y,
                    first.position.z
                );
                layer._instances.RemoveAt(0);
                layer._instances.Add(first);
            }
            else if (deltaX < 0f && last.position.x > rightBound)
            {
                // カメラが左へ移動：右端の画像を左へワープ
                last.position = new Vector3(
                    first.position.x - layer._spriteWidth,
                    last.position.y,
                    last.position.z
                );
                layer._instances.RemoveAt(layer._instances.Count - 1);
                layer._instances.Insert(0, last);
            }
        }

        _lastCameraPosX = camX;
    }
}
