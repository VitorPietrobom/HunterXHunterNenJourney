using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterAwakening
{
    /// <summary>
    /// All key bindings live here (GDD §12.5). Gameplay scripts only read intent properties.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        InputAction move, look, sprint, jump, dodge, light, heavy, block, interact,
            ten, ren, zetsu, gyo, ko, hatsu, pause, dashboard, previous, next, drillPrimary, save, load, help;
        InputAction[] all;

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool SprintHeld => sprint.IsPressed();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool DodgePressed => dodge.WasPressedThisFrame();
        public bool LightPressed => light.WasPressedThisFrame();
        public bool HeavyPressed => heavy.WasPressedThisFrame();
        public bool BlockHeld => block.IsPressed();
        public bool BlockPressed => block.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool TenPressed => ten.WasPressedThisFrame();
        public bool RenHeld => ren.IsPressed();
        public bool ZetsuPressed => zetsu.WasPressedThisFrame();
        public bool GyoHeld => gyo.IsPressed();
        public bool KoPressed => ko.WasPressedThisFrame();
        public bool HatsuPressed => hatsu.WasPressedThisFrame();
        public bool HatsuHeld => hatsu.IsPressed();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool DashboardPressed => dashboard.WasPressedThisFrame();
        public bool PreviousPressed => previous.WasPressedThisFrame();
        public bool NextPressed => next.WasPressedThisFrame();
        public bool DrillPrimaryPressed => drillPrimary.WasPressedThisFrame();
        public bool DrillPrimaryHeld => drillPrimary.IsPressed();
        public bool SavePressed => save.WasPressedThisFrame();
        public bool LoadPressed => load.WasPressedThisFrame();
        public bool HelpPressed => help.WasPressedThisFrame();

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");

            sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            dodge = Button("Dodge", "<Keyboard>/leftCtrl", "<Keyboard>/c", "<Gamepad>/buttonEast");
            light = Button("LightAttack", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            heavy = Button("HeavyAttack", "<Mouse>/rightButton", "<Gamepad>/buttonNorth");
            block = Button("Block", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/dpad/up");
            ten = Button("Ten", "<Keyboard>/t", "<Gamepad>/dpad/left");
            ren = Button("Ren", "<Keyboard>/r", "<Gamepad>/rightTrigger");
            zetsu = Button("Zetsu", "<Keyboard>/z", "<Gamepad>/rightStickPress");
            gyo = Button("Gyo", "<Keyboard>/g", "<Gamepad>/leftTrigger");
            ko = Button("Ko", "<Keyboard>/f", "<Gamepad>/rightShoulder");
            hatsu = Button("Hatsu", "<Keyboard>/v", "<Gamepad>/leftStickPress");
            pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            dashboard = Button("Dashboard", "<Keyboard>/tab", "<Gamepad>/select");
            previous = Button("Previous", "<Keyboard>/leftBracket", "<Keyboard>/minus", "<Gamepad>/dpad/down");
            next = Button("Next", "<Keyboard>/rightBracket", "<Keyboard>/equals", "<Gamepad>/dpad/right");
            drillPrimary = Button("DrillPrimary", "<Keyboard>/space", "<Mouse>/leftButton", "<Gamepad>/buttonSouth");
            save = Button("Save", "<Keyboard>/f5");
            load = Button("Load", "<Keyboard>/f9");
            help = Button("Help", "<Keyboard>/f1");

            all = new[] { move, look, sprint, jump, dodge, light, heavy, block, interact, ten, ren, zetsu, gyo, ko, hatsu, pause,
                          dashboard, previous, next, drillPrimary, save, load, help };
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            return action;
        }

        void OnEnable() { foreach (var a in all) a.Enable(); }
        void OnDisable() { foreach (var a in all) a.Disable(); }
        void OnDestroy() { foreach (var a in all) a.Dispose(); }
    }
}
