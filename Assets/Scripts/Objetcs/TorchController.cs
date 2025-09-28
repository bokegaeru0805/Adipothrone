using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TorchController : MonoBehaviour
{
    private bool isFirstUpdate = true;
    private Light2D torchLight = null;
    private Animator torchAnimator = null;
    private Material torchMaterial = null;

    [SerializeField]
    public TorchState firstState = TorchState.Red; // 初期状態を設定するための変数
    private TorchState currentState = TorchState.None;

    public enum TorchState
    {
        None = 0,
        Off = 10,
        Red = 20,
        Blue = 30,
    }

    [SerializeField]
    private Sprite defaultTorchSprite = null;
    private Color redTorchColor = new Color(1f, 0.35f, 0.052f);
    private Color blueTorchColor = new Color(0.051f, 0.78f, 1f);

    public void TurnOnRed() => SetTorchState(TorchState.Red);

    public void TurnOnBlue() => SetTorchState(TorchState.Blue);

    public void TurnOff() => SetTorchState(TorchState.Off);

    private void Awake()
    {
        torchLight = this.GetComponent<Light2D>();
        torchAnimator = this.GetComponent<Animator>();
        torchMaterial = this.GetComponent<SpriteRenderer>().material;
    }

    private void Start()
    {
        SetTorchState(firstState);
        isFirstUpdate = false;
    }

    public void SetTorchState(TorchState torchState)
    {
        if (torchState == currentState)
        {
            return; // 状態が変わっていない場合は何もしない
        }
        currentState = torchState;

        switch (torchState)
        {
            case TorchState.Off:
                torchLight.enabled = false; // トーチの光をオフにする
                torchAnimator.enabled = false; // アニメーターを無効化
                torchMaterial.DisableKeyword("HSV_ON"); // HSVキーワードを無効化
                torchMaterial.mainTexture = defaultTorchSprite.texture; // デフォルトのスプライト
                if (!isFirstUpdate)
                {
                    SEManager.instance?.PlayFieldSE(SE_Field.FlameOff); // トーチの光が消えるSEを再生
                }
                break;
            case TorchState.Red:
                torchLight.enabled = true; // トーチの光をオンにする
                torchAnimator.enabled = true; // アニメーターを有効化
                torchLight.color = redTorchColor; // 赤色の光を設定
                torchMaterial.EnableKeyword("HSV_ON"); // HSVキーワードを有効化
                torchAnimator.SetTrigger("red"); // 赤色のアニメーションをトリガー
                if (!isFirstUpdate)
                {
                    SEManager.instance?.PlayFieldSE(SE_Field.FlameOn); // トーチの光が点くSEを再生
                }
                break;
            case TorchState.Blue:
                torchLight.enabled = true;
                torchAnimator.enabled = true;
                torchLight.color = blueTorchColor;
                torchMaterial.DisableKeyword("HSV_ON");
                torchAnimator.SetTrigger("blue");
                if (!isFirstUpdate)
                {
                    SEManager.instance?.PlayFieldSE(SE_Field.FlameOn); // トーチの光が点くSEを再生
                }
                break;
        }
    }
}
