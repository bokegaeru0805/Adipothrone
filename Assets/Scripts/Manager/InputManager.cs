using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager instance { get; private set; }

    [Header("入力設定アセット")]
    public InputSettings inputSettings;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject); // ゲーム中ずっと使える
            if (inputSettings == null)
            {
                Debug.LogError("InputSettingsが設定されていません。");
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// ロード中なら入力を無効化する共通判定
    /// </summary>
    private bool InputAllowed()
    {
        return !SaveLoadManager.IsLoading;
    }

    public bool GetKey(KeyCode key)
    {
        return InputAllowed() && Input.GetKey(key);
    }

    public bool GetPlayerMoveRight()
    {
        return InputAllowed() && Input.GetKey(inputSettings.PlayerMoveRight);
    }

    public bool GetPlayerMoveLeft()
    {
        return InputAllowed() && Input.GetKey(inputSettings.PlayerMoveLeft);
    }

    public bool GetPlayerDash()
    {
        return InputAllowed() && Input.GetKey(inputSettings.PlayerDash);
    }

    public bool GetPlayerChange()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.PlayerChange);
    }

    public bool GetPlayerJump()
    {
        return InputAllowed() && Input.GetKey(inputSettings.PlayerJump);
    }

    public bool GetRobotAttack()
    {
        return InputAllowed() && Input.GetKey(inputSettings.RobotAttack);
    }

    public bool GetInteract()
    {
        return InputAllowed() && Input.GetKey(inputSettings.Interact);
    }

    public bool MenuUIOpen()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.MenuUIOpen);
    }

    public bool UIConfirm()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UIConfirm);
    }

    public bool UIMoveRight()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UIMoveRight);
    }

    public bool UIMoveRightHold()
    {
        return InputAllowed() && Input.GetKey(inputSettings.UIMoveRight);
    }

    public bool UIMoveUp()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UIMoveUp);
    }

    public bool UIMoveDown()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UIMoveDown);
    }

    public bool UIMoveLeft()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UIMoveLeft);
    }

    public bool UIMoveLeftHold()
    {
        return InputAllowed() && Input.GetKey(inputSettings.UIMoveLeft);
    }

    public bool UISelectYes()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UISelectYes);
    }

    public bool UIClose()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UIClose);
    }

    public bool UISelectNo()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.UISelectNo);
    }

    // public bool GetSkipHold()
    // {
    //     return InputAllowed() && Input.GetKey(inputSettings.Skip);
    // }

    public bool SkipDialogHold()
    {
        return InputAllowed() && Input.GetKey(inputSettings.Skip);
    }

    public bool GetQuickItemRight()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.QuickItemRight);
    }

    public bool GetQuickItemLeft()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.QuickItemLeft);
    }

    public bool GetQuickItemUpDown()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.QuickItemUpDown);
    }

    public bool GetQuickItemSelect()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.QuickItemSelect);
    }

    public bool GetTabRight()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.TabRight);
    }

    public bool GetTabLeft()
    {
        return InputAllowed() && Input.GetKeyDown(inputSettings.TabLeft);
    }
}
