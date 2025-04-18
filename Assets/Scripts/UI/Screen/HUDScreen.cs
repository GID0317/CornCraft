using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

using CraftSharp.Event;
using CraftSharp.Protocol;

namespace CraftSharp.UI
{
    [RequireComponent(typeof (CanvasGroup))]
    public class HUDScreen : BaseScreen
    {
        private const float HEALTH_MULTIPLIER = 1F;
        private static readonly Vector3 STAMINA_TARGET_OFFSET = new(0, -0.5F, 0F);

        // UI controls and objects
        [SerializeField] private TMP_Text latencyText, debugText, modeText;
        [SerializeField] private Animator modePanelAnimator, crosshairAnimator, statusPanelAnimator;
        [SerializeField] private Button[] modeButtons = new Button[4];
        [SerializeField] private ValueBar healthBar;
        [SerializeField] private RingValueBar staminaBar;
        [SerializeField] private InteractionPanel interactionPanel;
        [SerializeField] private InventoryHotbar inventoryHotbar;
        [SerializeField] private Animator screenAnimator;

        private Animator staminaBarAnimator;

        private bool isActive = false, debugInfo = false;

        private bool modePanelShown  = false;
        private int selectedMode     = 0;
        private int displayedLatency = 0;

        [SerializeField] private RectTransform chatContentPanel;
        [SerializeField] private GameObject chatMessagePreviewPrefab;
        [SerializeField] [Range(0.1F, 1F)] private float transitionTime = 0.1F;
        private float transitionCooldown = 0F;

        public override bool IsActive
        {
            set {
                isActive = value;
                
                screenAnimator.SetBool(SHOW_HASH, isActive);

                if (isActive)
                {
                    transitionCooldown = transitionTime;
                    // Show 3d items
                    interactionPanel.ShowItemIconsAndTargetHint();
                }
                else
                {
                    // Hide 3d items
                    interactionPanel.HideItemIconsAndTargetHint();
                }
            }

            get => isActive;
        }

        public override bool ReleaseCursor()
        {
            return false;
        }

        public override bool ShouldPauseControllerInput()
        {
            return false;
        }

        protected override void Initialize()
        {
            // Initialize controls...
            staminaBarAnimator = staminaBar.GetComponent<Animator>();

            cameraAimCallback = e => crosshairAnimator.SetBool(SHOW_HASH, e.Aiming);

            gameModeCallback = e =>
            {
                var showStatus = e.GameMode switch {
                    GameMode.Survival   => true,
                    GameMode.Creative   => false,
                    GameMode.Adventure  => true,
                    GameMode.Spectator  => false,

                    _                   => false
                };

                statusPanelAnimator.SetBool(SHOW_HASH, showStatus);
            };

            healthCallback = e =>
            {
                if (e.UpdateMaxHealth)
                    healthBar.MaxValue = e.Health * HEALTH_MULTIPLIER;

                healthBar.CurValue = e.Health * HEALTH_MULTIPLIER;
            };

            staminaCallback = e =>
            {
                staminaBar.CurValue = e.Stamina;

                if (!Mathf.Approximately(staminaBar.MaxValue, e.MaxStamina))
                {
                    staminaBar.MaxValue = e.MaxStamina;
                }

                staminaBarAnimator.SetBool(SHOW_HASH, e.Stamina < e.MaxStamina); // Stamina is full
            };

            // Register callbacks
            chatMessageCallback = e =>
            {
                var styledMessage = TMPConverter.MC2TMP(e.Message);
                var chatMessageObj = Instantiate(chatMessagePreviewPrefab, chatContentPanel);
                
                var chatMessage = chatMessageObj.GetComponent<TMP_Text>();
                chatMessage.text = styledMessage;
            };

            EventManager.Instance.Register(cameraAimCallback);
            EventManager.Instance.Register(gameModeCallback);
            EventManager.Instance.Register(healthCallback);
            EventManager.Instance.Register(staminaCallback);
            EventManager.Instance.Register(chatMessageCallback);

            // Initialize controls
            var game = CornApp.CurrentClient;
            if (game)
            {
                interactionPanel.OnItemCountChange += newCount =>
                {
                    // Disable camera zoom if there are more than 1 interaction options
                    game.SetCameraZoomEnabled(newCount <= 1);
                };

                crosshairAnimator.SetBool(SHOW_HASH, false);
            }
        }

        #nullable enable

        private Action<CameraAimingEvent>?      cameraAimCallback;
        private Action<GameModeUpdateEvent>?    gameModeCallback;
        private Action<HealthUpdateEvent>?      healthCallback;
        private Action<StaminaUpdateEvent>?     staminaCallback;
        private Action<ChatMessageEvent>?       chatMessageCallback;

        #nullable disable

        private void OnDestroy()
        {
            if (cameraAimCallback is not null)
                EventManager.Instance.Unregister(cameraAimCallback);
            
            if (gameModeCallback is not null)
                EventManager.Instance.Unregister(gameModeCallback);
            
            if (healthCallback is not null)
                EventManager.Instance.Unregister(healthCallback);

            if (staminaCallback is not null)
                EventManager.Instance.Unregister(staminaCallback);
            
            if (chatMessageCallback is not null)
                EventManager.Instance.Unregister(chatMessageCallback);
        }

        public override void UpdateScreen()
        {
            if (transitionCooldown > 0F)
            {
                transitionCooldown -= Time.unscaledDeltaTime;
                return;
            }
            
            var game = CornApp.CurrentClient;
            if (!game) return;

            if (Keyboard.current.f3Key.isPressed)
            {
                if (Keyboard.current.f4Key.wasPressedThisFrame)
                {
                    int buttonCount = modeButtons.Length;
                    if (modePanelShown) // Select next gamemode
                    {
                        selectedMode = (selectedMode + 1) % buttonCount;
                        modeText.text = ChatParser.TranslateString($"gameMode.{((GameMode) selectedMode).GetIdentifier()}");
                        modeButtons[selectedMode].Select();
                    }
                    else // Show gamemode switch
                    {
                        selectedMode = (int) game.GameMode;
                        if (selectedMode >= 0 && selectedMode < modeButtons.Length)
                        {
                            modeText.text = ChatParser.TranslateString($"gameMode.{((GameMode) selectedMode).GetIdentifier()}");
                            modePanelAnimator.SetBool(SHOW_HASH, true);
                            modePanelShown = true;
                            modeButtons[selectedMode].Select();
                            // Hide crosshair (if shown)
                            crosshairAnimator.SetBool(SHOW_HASH, false);
                        }
                    }
                }
            }

            if (Keyboard.current.f3Key.wasReleasedThisFrame)
            {
                if (modePanelShown) // Hide gamemode switch
                {
                    modePanelAnimator.SetBool(SHOW_HASH, false);
                    modePanelShown = false;
                    // Show crosshair (if should be shown)
                    if (game.CameraController && game.CameraController.IsAimingOrLocked)
                    {
                        crosshairAnimator.SetBool(SHOW_HASH, true);
                    }

                    if (selectedMode != (int) game.GameMode) // Commit switch request
                    {
                        game.TrySendChat($"/gamemode {((GameMode) selectedMode).GetIdentifier()}");
                    }
                }
                else // Toggle debug info
                {
                    debugInfo = !debugInfo;
                }
            }

            if (Keyboard.current.xKey.wasPressedThisFrame) // Execute interactions
            {
                interactionPanel.RunInteractionOption();
            }

            if (Keyboard.current.pKey.wasPressedThisFrame) // Open packet screen
            {
                game.ScreenControl.PushScreen<PacketScreen>();
            }

            var mouseScroll = Mouse.current.scroll.value.y;
            if (mouseScroll != 0F && !Keyboard.current.shiftKey.IsPressed())
            {
                if (interactionPanel && interactionPanel.ShouldConsumeMouseScroll) // Interaction option selection
                {
                    if (mouseScroll < 0F)
                        interactionPanel.SelectNextOption();
                    else
                        interactionPanel.SelectPrevOption();
                }
                else // Hotbar slot selection
                {
                    if (mouseScroll < 0F)
                        game.ChangeHotbarSlotBy(1);
                    else
                        game.ChangeHotbarSlotBy(-1);
                }
            }
            
            // Hotbar slot selection by key
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(4);
            if (Keyboard.current.digit6Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(5);
            if (Keyboard.current.digit7Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(6);
            if (Keyboard.current.digit8Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(7);
            if (Keyboard.current.digit9Key.wasPressedThisFrame)
                game.ChangeHotbarSlot(8);

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                game.DropItem(Keyboard.current.ctrlKey.isPressed);
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                game.SwapItemOnHands();
            }

            if (Keyboard.current.slashKey.wasPressedThisFrame)
            {
                var chatScreen = game.ScreenControl.PushScreen<ChatScreen>();

                // Input command prefix '/'
                chatScreen.InputCommandPrefix();
            }
            else if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                game.ScreenControl.PushScreen<ChatScreen>();
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                game.ScreenControl.PushScreen<PauseScreen>();
            }
            
            debugText.text = game.GetInfoString(debugInfo);

            var currentLatency = game.GetOwnLatency();

            if (displayedLatency != currentLatency)
            {
                displayedLatency = (int) Mathf.Lerp(displayedLatency, currentLatency, 0.55F);
                
                if (displayedLatency >= 500)
                    latencyText.text =  $"<color=red>{displayedLatency} ms</color>";
                else if (displayedLatency >= 100)
                    latencyText.text =  $"<color=orange>{displayedLatency} ms</color>";
                else
                    latencyText.text =  $"{displayedLatency} ms";
            }
        }

        private void LateUpdate()
        {
            var game = CornApp.CurrentClient;
            if (!game) return;
            
            if (game.CameraController)
            {
                var originOffset = game.WorldOriginOffset;
                var uiCamera = game.UICamera;
                var camControl = game.CameraController;
                
                // Update stamina bar position
                var newPos = uiCamera.ViewportToWorldPoint(camControl.GetTargetViewportPos(STAMINA_TARGET_OFFSET));

                // Don't modify z coordinate
                staminaBar.transform.position = new Vector3(newPos.x, newPos.y, staminaBar.transform.position.z);

                // Update interaction target hint
                interactionPanel.UpdateInteractionTargetHint(originOffset, uiCamera, camControl);
            }
        }
    }
}
