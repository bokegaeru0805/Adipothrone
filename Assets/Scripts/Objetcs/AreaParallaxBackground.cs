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

        [Tooltip("Y軸追従が有効な際、カメラの中心から上下にどれくらいズラすか（0でカメラ中央）")]
        public float manualOffsetY = 0f;

        [HideInInspector]
        public List<Transform> _instances = new List<Transform>();

        [HideInInspector]
        public float _spriteWidth;
    }

    [SerializeField]
    [Tooltip("有効にすると、すべての背景レイヤーがカメラのY座標に追従します")]
    private bool _followCameraY = true;

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
    private float _lastCameraPosY;
    private bool _needsArrangement = false;
    private bool _enteredFromRight = false;

    private void Awake()
    {
        _camera = Camera.main;

        if (_targetCameraArea != null)
        {
            _targetCollider = _targetCameraArea.GetComponent<Collider2D>();
        }

        InitializeLayers();
    }

    private void OnEnable()
    {
        CameraMoveArea.OnPlayerEnteredArea += OnPlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea += OnPlayerExitedArea;

        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        CameraMoveArea.OnPlayerEnteredArea -= OnPlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea -= OnPlayerExitedArea;

        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    /// <summary>
    /// ゲーム開始時に必要な枚数を計算し、事前に複製してプールします。
    /// </summary>
    private void InitializeLayers()
    {
        if (_camera == null)
            return;

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

            int requiredCount = Mathf.CeilToInt((cameraHalfWidth * 2f) / layer._spriteWidth) + 2;

            layer.baseBackground.SetActive(false);

            for (int i = 0; i < requiredCount; i++)
            {
                GameObject instance = Instantiate(layer.baseBackground, transform);
                instance.SetActive(false);
                layer._instances.Add(instance.transform);
            }
        }
    }

    /// <summary>
    /// プレイヤーがいずれかのエリアに入ったときに通知されるイベントハンドラー
    /// </summary>
    private void OnPlayerEnteredArea(CameraMoveArea area)
    {
        if (area != _targetCameraArea)
            return;
        if (_isPlayerInArea)
            return;

        getPlayerTransform();
        _isPlayerInArea = true;
        _lastCameraPosX = _camera.transform.position.x;
        _lastCameraPosY = _camera.transform.position.y;

        bool enteredFromRight = false;
        if (_playerTransform != null && _targetCollider != null)
        {
            enteredFromRight = _playerTransform.position.x > _targetCollider.bounds.center.x;
        }

        ArrangeBackgrounds(enteredFromRight);

        // ここで即座に配置せず、カメラ座標が確定するのを待機するフラグを立てる
        _needsArrangement = true;
    }

    /// <summary>
    /// プレイヤーがいずれかのエリアから出たときに通知されるイベントハンドラー
    /// </summary>
    private void OnPlayerExitedArea(CameraMoveArea area)
    {
        if (area != _targetCameraArea)
            return;

        _isPlayerInArea = false;

        foreach (var layer in _parallaxLayers)
        {
            foreach (var inst in layer._instances)
            {
                inst.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 指定された仕様に則ってプレイヤーのTransformを取得・キャッシュします。
    /// </summary>
    private void getPlayerTransform()
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
            float targetY = _followCameraY
                ? _camera.transform.position.y + layer.manualOffsetY
                : layer.baseBackground.transform.position.y;

            if (enteredFromRight)
            {
                startX = camX + cameraHalfWidth;
                for (int i = layer._instances.Count - 1; i >= 0; i--)
                {
                    int reverseIndex = (layer._instances.Count - 1) - i;
                    float posX = startX - (reverseIndex * layer._spriteWidth);
                    layer._instances[i].position = new Vector3(
                        posX,
                        targetY,
                        layer.baseBackground.transform.position.z
                    );
                    layer._instances[i].gameObject.SetActive(true);
                }
            }
            else
            {
                startX = camX - cameraHalfWidth;
                for (int i = 0; i < layer._instances.Count; i++)
                {
                    float posX = startX + (i * layer._spriteWidth);
                    layer._instances[i].position = new Vector3(
                        posX,
                        targetY,
                        layer.baseBackground.transform.position.z
                    );
                    layer._instances[i].gameObject.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Cinemachineの全処理が終わった確定座標で背景を更新します。
    /// </summary>
    private void OnCameraUpdated(CinemachineBrain brain)
    {
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
        float camY = _camera.transform.position.y;

        // エリア進入直後、カメラの座標が確定した最初のフレームで初期配置を行う
        if (_needsArrangement)
        {
            _lastCameraPosX = camX;
            _lastCameraPosY = camY;
            ArrangeBackgrounds(_enteredFromRight);
            _needsArrangement = false;
            return; // 初回は正しい位置への配置のみ行い、スクロール計算はスキップする
        }

        float deltaX = camX - _lastCameraPosX;
        float deltaY = camY - _lastCameraPosY;

        if (Mathf.Abs(deltaX) < 0.001f)
            deltaX = 0f;
        if (Mathf.Abs(deltaY) < 0.001f)
            deltaY = 0f;

        if (deltaX == 0f && deltaY == 0f)
            return;

        foreach (var layer in _parallaxLayers)
        {
            if (layer._instances.Count == 0)
                continue;

            float moveX = deltaX * layer.scrollFactor;
            float targetY = _followCameraY
                ? _camera.transform.position.y + layer.manualOffsetY
                : layer.baseBackground.transform.position.y;

            foreach (var inst in layer._instances)
            {
                float newX = inst.position.x + moveX;
                inst.position = new Vector3(newX, targetY, inst.position.z);
            }

            float cameraHalfWidth = _camera.orthographicSize * _camera.aspect;
            float leftBound = camX - cameraHalfWidth - layer._spriteWidth;
            float rightBound = camX + cameraHalfWidth + layer._spriteWidth;

            Transform first = layer._instances[0];
            Transform last = layer._instances[layer._instances.Count - 1];

            if (deltaX > 0f && first.position.x < leftBound)
            {
                first.position = new Vector3(
                    last.position.x + layer._spriteWidth,
                    targetY,
                    first.position.z
                );
                layer._instances.RemoveAt(0);
                layer._instances.Add(first);
            }
            else if (deltaX < 0f && last.position.x > rightBound)
            {
                last.position = new Vector3(
                    first.position.x - layer._spriteWidth,
                    targetY,
                    last.position.z
                );
                layer._instances.RemoveAt(layer._instances.Count - 1);
                layer._instances.Insert(0, last);
            }
        }

        _lastCameraPosX = camX;
        _lastCameraPosY = camY;
    }
}
