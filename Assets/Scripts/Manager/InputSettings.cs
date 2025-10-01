using UnityEngine;

[CreateAssetMenu(fileName = "InputSettings", menuName = "Settings/InputSettings")]
public class InputSettings : ScriptableObject
{
    public KeyCode PlayerMoveLeft = KeyCode.LeftArrow;
    public KeyCode PlayerMoveRight = KeyCode.D;
    public KeyCode PlayerDash = KeyCode.LeftShift;
    public KeyCode PlayerChange = KeyCode.Q;
    public KeyCode PlayerJump = KeyCode.Space;
    // public KeyCode RobotJump = KeyCode.W;
    // public KeyCode RobotDip = KeyCode.S;
    public KeyCode RobotAttack = KeyCode.S;
    public KeyCode Interact = KeyCode.E;
    public KeyCode MenuUIOpen = KeyCode.Escape;
    public KeyCode UIConfirm = KeyCode.Space;
    public KeyCode UIMoveLeft = KeyCode.A;
    public KeyCode UIMoveRight = KeyCode.D;
    public KeyCode UIMoveUp = KeyCode.W;
    public KeyCode UIMoveDown = KeyCode.S;
    public KeyCode UISelectYes = KeyCode.A;
    public KeyCode UISelectNo = KeyCode.D;
    public KeyCode UIClose = KeyCode.Tab;
    public KeyCode Skip = KeyCode.Escape;
    public KeyCode QuickItemLeft = KeyCode.J;
    public KeyCode QuickItemRight = KeyCode.K;
    public KeyCode QuickItemUp = KeyCode.UpArrow;
    public KeyCode QuickItemDown = KeyCode.DownArrow;
    public KeyCode QuickItemSelect = KeyCode.H;
    public KeyCode QuickItemHighlight = KeyCode.LeftControl;
    public KeyCode TabLeft = KeyCode.Q;
    public KeyCode TabRight = KeyCode.E;
}
