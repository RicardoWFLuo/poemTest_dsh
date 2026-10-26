using UnityEngine;
using UnityEngine.EventSystems;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance;

    [Header("摇杆")]
    public RectTransform joystickBackground;
    public RectTransform joystickHandle;
    public float joystickRadius = 80f;

    [Header("按钮")]
    public GameObject interactButton;   // F
    public GameObject attackButton;     // J
    public GameObject shootButton;      // K
    public GameObject audioButton;      // Z
    public GameObject switchButton;     // V
    public GameObject statsButton;      // Q
    public GameObject skillButton;      // E
    public GameObject item1Button;      // 1（售卖/使用）
    public GameObject item2Button;      // 2（售卖/使用）
    public GameObject item3Button;      // 3（售卖/使用）
    public GameObject item4Button;      // 4（扔下）
    public GameObject item5Button;      // 5（扔下）
    public GameObject item6Button;      // 6（扔下）

    // 摇杆
    private Vector2 moveInput;
    private bool isJoystickDragging;
    private Vector2 joystickStartPos;

    // 按钮状态
    private bool interactPressed;
    private bool attackPressed;
    private bool shootPressed;
    private bool audioPressed;
    private bool switchPressed;
    private bool statsPressed;
    private bool skillPressed;
    private bool item1Pressed;
    private bool item2Pressed;
    private bool item3Pressed;
    private bool item4Pressed;
    private bool item5Pressed;
    private bool item6Pressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

#if UNITY_STANDALONE || UNITY_EDITOR
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.enabled = false;
#else
    if (joystickHandle != null)
        joystickStartPos = joystickHandle.position;

    SetupButton(interactButton, () => interactPressed = true, () => interactPressed = false);
    SetupButton(attackButton,   () => attackPressed = true,   () => attackPressed = false);
    SetupButton(shootButton,    () => shootPressed = true,    () => shootPressed = false);
    SetupButton(audioButton,    () => audioPressed = true,    () => audioPressed = false);
    SetupButton(switchButton,   () => switchPressed = true,   () => switchPressed = false);
    SetupButton(statsButton,    () => statsPressed = true,    () => statsPressed = false);
    SetupButton(skillButton,    () => skillPressed = true,    () => skillPressed = false);
    SetupButton(item1Button,    () => item1Pressed = true,    () => item1Pressed = false);
    SetupButton(item2Button,    () => item2Pressed = true,    () => item2Pressed = false);
    SetupButton(item3Button,    () => item3Pressed = true,    () => item3Pressed = false);
    SetupButton(item4Button,    () => item4Pressed = true,    () => item4Pressed = false);
    SetupButton(item5Button,    () => item5Pressed = true,    () => item5Pressed = false);
    SetupButton(item6Button,    () => item6Pressed = true,    () => item6Pressed = false);
#endif
    }

    private void SetupButton(GameObject button, System.Action onDown, System.Action onUp)
    {
        if (button == null) return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.AddComponent<EventTrigger>();

        EventTrigger.Entry downEntry = new EventTrigger.Entry();
        downEntry.eventID = EventTriggerType.PointerDown;
        downEntry.callback.AddListener((data) => onDown());
        trigger.triggers.Add(downEntry);

        EventTrigger.Entry upEntry = new EventTrigger.Entry();
        upEntry.eventID = EventTriggerType.PointerUp;
        upEntry.callback.AddListener((data) => onUp());
        trigger.triggers.Add(upEntry);
    }

    private void Update()
    {
        HandleJoystick();
    }

    private void HandleJoystick()
    {
        if (joystickBackground == null || joystickHandle == null)
            return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (RectTransformUtility.RectangleContainsScreenPoint(joystickBackground, touch.position))
                    {
                        isJoystickDragging = true;
                        joystickStartPos = touch.position;
                    }
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (isJoystickDragging) UpdateJoystickPosition(touch.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (isJoystickDragging)
                    {
                        ReleaseJoystick();
                    }
                    break;
            }
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(joystickBackground, Input.mousePosition))
            {
                isJoystickDragging = true;
                joystickStartPos = Input.mousePosition;
            }
        }
        if (isJoystickDragging && Input.GetMouseButton(0))
            UpdateJoystickPosition(Input.mousePosition);
        if (Input.GetMouseButtonUp(0) && isJoystickDragging)
        {
            ReleaseJoystick();
        }
#endif
    }

    private void ReleaseJoystick()
    {
        isJoystickDragging = false;
        moveInput = Vector2.zero;

        if (joystickHandle != null)
            joystickHandle.anchoredPosition = Vector2.zero;
    }

    private void UpdateJoystickPosition(Vector2 inputPos)
    {
        if (joystickHandle == null || joystickRadius <= 0f)
            return;

        Vector2 direction = inputPos - joystickStartPos;
        float distance = direction.magnitude;
        if (distance > joystickRadius)
            direction = direction.normalized * joystickRadius;

        // 摇杆手柄是 UI 元素，必须写 anchoredPosition。
        // 历史版本写的是 world position，并与屏幕坐标的触摸点混用，导致摇杆位置飘移。
        joystickHandle.anchoredPosition = direction;
        moveInput = direction / joystickRadius;
    }

    //移动
    public Vector2 GetMoveInput()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        // 归一化，避免斜向移动比直线移动更快
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
        return Vector2.ClampMagnitude(moveInput, 1f);
#endif
    }

    // 「本帧交互键已被消费」的帧号。
    // Input.GetKeyDown 在同一帧对所有调用者都返回 true，
    // 而诗碑上同时挂着 PoemExplain 与 PoemImageGN，两者都在 Update 里轮询交互键，
    // 结果按一次 F 会同时打开「诗词释义」和「诗词配图」。
    // 这里保证一次按键只被一个系统消费。
    private int interactConsumedFrame = -1;

    public bool GetInteractDown()
    {
        if (interactConsumedFrame == Time.frameCount)
            return false;

        bool down = MobilePressed(ref interactPressed) || Input.GetKeyDown(KeyCode.F);

        if (down)
            interactConsumedFrame = Time.frameCount;

        return down;
    }

    public bool GetAttackDown() => MobilePressed(ref attackPressed) || Input.GetKeyDown(KeyCode.J);
    public bool GetShootDown() => MobilePressed(ref shootPressed) || Input.GetKeyDown(KeyCode.K);
    public bool GetAudioDown() => MobilePressed(ref audioPressed) || Input.GetKeyDown(KeyCode.Z);
    public bool GetSwitchDown() => MobilePressed(ref switchPressed) || Input.GetKeyDown(KeyCode.V);
    public bool GetStatsDown() => MobilePressed(ref statsPressed) || Input.GetKeyDown(KeyCode.Q);
    public bool GetSkillDown() => MobilePressed(ref skillPressed) || Input.GetKeyDown(KeyCode.E);
    public bool GetItem1Down() => MobilePressed(ref item1Pressed) || Input.GetKeyDown(KeyCode.Alpha1);
    public bool GetItem2Down() => MobilePressed(ref item2Pressed) || Input.GetKeyDown(KeyCode.Alpha2);
    public bool GetItem3Down() => MobilePressed(ref item3Pressed) || Input.GetKeyDown(KeyCode.Alpha3);
    public bool GetItem4Down() => MobilePressed(ref item4Pressed) || Input.GetKeyDown(KeyCode.Alpha4);
    public bool GetItem5Down() => MobilePressed(ref item5Pressed) || Input.GetKeyDown(KeyCode.Alpha5);
    public bool GetItem6Down() => MobilePressed(ref item6Pressed) || Input.GetKeyDown(KeyCode.Alpha6);

    private bool MobilePressed(ref bool pressed)
    {
        if (pressed)
        {
            pressed = false;
            return true;
        }
        return false;
    }
}
