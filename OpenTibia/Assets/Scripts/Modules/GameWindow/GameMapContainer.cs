﻿using OpenTibiaUnity.Core.Appearances;
using OpenTibiaUnity.Core.Components;
using OpenTibiaUnity.Core.Creatures;
using OpenTibiaUnity.Core.Game;
using OpenTibiaUnity.Core.Input.GameAction;
using UnityEngine;

using PlayerAction = OpenTibiaUnity.Protobuf.Shared.PlayerAction;

namespace OpenTibiaUnity.Modules.GameWindow
{
    [ExecuteInEditMode]
    public class GameMapContainer : GamePanelContainer, IMoveWidget, IUseWidget, IWidgetContainerWidget
    {
        [SerializeField] private GameWorldMap _gameWorldMap = null;
        [SerializeField] private TMPro.TextMeshProUGUI _framecounterText = null;
        
        private Rect _cachedScreenRect = Rect.zero;
        private bool _screenRectCached = false;
        private bool _mouseCursorOverRenderer = false;

        private int _lastScreenWidth = 0;
        private int _lastScreenHeight = 0;
        private int _lastFramerate = 0;
        private int _lastPing = 9999;

        private ObjectDragImpl<GameMapContainer> _dragHandler;
        
        private RectTransform worldMapRectTransform {
            get => _gameWorldMap.rectTransform;
        }

        protected override void Start() {
            base.Start();

            _dragHandler = new ObjectDragImpl<GameMapContainer>(this);
            ObjectMultiUseHandler.RegisterContainer(this);

            _gameWorldMap.onPointerEnter.AddListener(OnWorldMapPointerEnter);
            _gameWorldMap.onPointerExit.AddListener(OnWorldMapPointerExit);

            if (OpenTibiaUnity.InputHandler != null)
                OpenTibiaUnity.InputHandler.AddMouseUpListener(Core.Utils.EventImplPriority.Default, OnMouseUp);
        }

        protected void Update() {
            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height) {
                InvalidateScreenRect();

                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
            }
        }

        protected void OnGUI() {
            if (Event.current.type == EventType.Repaint)
                RenderWorldMap();
        }

        protected void OnWorldMapPointerEnter() {
            _mouseCursorOverRenderer = true;
        }

        protected void OnWorldMapPointerExit() {
            _mouseCursorOverRenderer = false;
            OpenTibiaUnity.WorldMapRenderer.HighlightTile = null;
            OpenTibiaUnity.WorldMapRenderer.HighlightObject = OpenTibiaUnity.CreatureStorage.Aim;
            OpenTibiaUnity.GameManager.CursorController.SetCursorState(CursorState.Default, CursorPriority.Medium);
        }

        protected void RenderWorldMap() {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
                return;
#endif

            if (!_screenRectCached)
                CacheScreenRect();

            var gameManager = OpenTibiaUnity.GameManager;
            var worldMapStorage = OpenTibiaUnity.WorldMapStorage;
            var worldMapRenderer = OpenTibiaUnity.WorldMapRenderer;
            var protocolGame = OpenTibiaUnity.ProtocolGame;

            if (gameManager != null && worldMapStorage != null && gameManager.IsGameRunning && worldMapStorage.Valid) {
                if (_mouseCursorOverRenderer && gameManager.GameCanvas.gameObject.activeSelf && !gameManager.GamePanelBlocker.gameObject.activeSelf) {
                    publicStartMouseAction(Input.mousePosition, MouseButton.None, false, true, true);
                } else {
                    worldMapRenderer.HighlightTile = null;
                    worldMapRenderer.HighlightObject = null;
                }

                if (ContextMenuBase.CurrentContextMenu != null || ObjectDragImpl.AnyDraggingObject)
                    worldMapRenderer.HighlightTile = null;

                gameManager.WorldMapRenderingTexture.Release();
                RenderTexture.active = gameManager.WorldMapRenderingTexture;
                worldMapRenderer.Render(worldMapRectTransform.rect);
                RenderTexture.active = null;

                // setting the clip area
                _gameWorldMap.rawImage.uvRect = worldMapRenderer.CalculateClipRect();

                if (worldMapRenderer.Framerate != _lastFramerate) {
                    _lastFramerate = worldMapRenderer.Framerate;
                    _lastPing = protocolGame.Ping;
                    _framecounterText.text = string.Format("FPS: <color=#{0:X6}>{1}</color>\nPing:{2}", GetFramerateColor(_lastFramerate), _lastFramerate, _lastPing);
                }
            } else {
                _framecounterText.text = "";
            }
        }

        protected override void OnDestroy() => base.OnDestroy();

        private static uint GetFramerateColor(int framerate) {
            if (framerate < 10)
                return 0xFFFF00;
            else if (framerate < 30)
                return 0xF55E5E;
            else if (framerate < 58)
                return 0xFE6500;

            return 0x00EB00;
        }
        
        public void CacheScreenRect() {
            if (!_screenRectCached) {
                Vector2 size = Vector2.Scale(worldMapRectTransform.rect.size, worldMapRectTransform.lossyScale);
                _cachedScreenRect = new Rect(worldMapRectTransform.position.x, Screen.height - worldMapRectTransform.position.y, size.x, size.y);
                _cachedScreenRect.x -= worldMapRectTransform.pivot.x * size.x;
                _cachedScreenRect.y -= (1.0f - worldMapRectTransform.pivot.y) * size.y;

                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
                _screenRectCached = true;
            }
        }

        public void InvalidateScreenRect() => _screenRectCached = false;

        public void OnMouseUp(Event e, MouseButton mouseButton, bool repeat) {
            if (publicStartMouseAction(e.mousePosition, mouseButton, true, false, false))
                e.Use();
        }

        private bool publicStartMouseAction(Vector3 mousePosition, MouseButton mouseButton, bool applyAction = false, bool updateCursor = false, bool updateHighlight = false) {
            var gameManager = OpenTibiaUnity.GameManager;
            if (!_mouseCursorOverRenderer || !gameManager.GameCanvas.gameObject.activeSelf || gameManager.GamePanelBlocker.gameObject.activeSelf)
                return false;
            
            var point = RawMousePositionToLocalMapPosition(mousePosition);
            var eventModifiers = OpenTibiaUnity.InputHandler.GetRawEventModifiers();
            var action = DetermineAction(mousePosition, mouseButton, eventModifiers, point, applyAction, updateCursor, updateHighlight);
            return action != AppearanceActions.None;
        }
        
        public AppearanceActions DetermineAction(Vector3 mousePosition, MouseButton mouseButton, EventModifiers eventModifiers, Vector2 point, bool applyAction = false, bool updateCursor = false, bool updateHighlight = false) {
            if (updateCursor)
                updateCursor = OpenTibiaUnity.GameManager.ClientVersion >= 1100;

            if (updateHighlight)
                updateHighlight = OpenTibiaUnity.GameManager.ClientVersion >= 1100;

            var inputHandler = OpenTibiaUnity.InputHandler;
            if (inputHandler.IsMouseButtonDragged(MouseButton.Left) || inputHandler.IsMouseButtonDragged(MouseButton.Right))
                return AppearanceActions.None;

            var gameManager = OpenTibiaUnity.GameManager;
            var worldMapRenderer = OpenTibiaUnity.WorldMapRenderer;
            var worldMapStorage = OpenTibiaUnity.WorldMapStorage;
            var creatureStorage = OpenTibiaUnity.CreatureStorage;
            var mapPosition = worldMapRenderer.PointToMap(point);

            if (gameManager.ClientVersion >= 1100)
                worldMapRenderer.HighlightTile = mapPosition;

            if (!mapPosition.HasValue)
                return AppearanceActions.None;
            
            var player = OpenTibiaUnity.Player;
            var action = AppearanceActions.None;
            Creature creature = null;
            ObjectInstance topLookObject = null;
            ObjectInstance topUseObject = null;
            int topLookObjectStackPos = -1;
            int topUseObjectStackPos = -1;

            var optionStorage = OpenTibiaUnity.OptionStorage;
            var absolutePosition = worldMapStorage.ToAbsolute(mapPosition.Value);
            
            if (optionStorage.MousePreset == MousePresets.LeftSmartClick) {
                bool forceLook = eventModifiers == EventModifiers.Shift;

                if (mouseButton == MouseButton.Right) {
                    var field = worldMapStorage.GetField(mapPosition.Value);
                    topLookObjectStackPos = field.GetTopLookObject(out topLookObject);
                    topUseObjectStackPos = field.GetTopUseObject(out topUseObject);
                    if (!!topLookObject && topLookObject.IsCreature)
                        creature = creatureStorage.GetCreature(topLookObject.Data);

                    if (!!topUseObject || !!topLookObject)
                        action = AppearanceActions.ContextMenu;
                } else if (mouseButton == MouseButton.Left) {
                    if (eventModifiers == EventModifiers.None) {
                        if (mapPosition.Value.z != worldMapStorage.PlayerZPlane) {
                            var field = worldMapStorage.GetField(mapPosition.Value);
                            var creatureObjectStackPos = field.GetTopCreatureObject(out ObjectInstance creatureObject);

                            action = AppearanceActions.SmartClick;
                            if (!!creatureObject && !!(creature = creatureStorage.GetCreature(creatureObject.Data))) {
                                if (creature.Id == player.Id || forceLook) {
                                    topLookObjectStackPos = creatureObjectStackPos;
                                    topLookObject = creatureObject;
                                    action = AppearanceActions.Look;
                                } else if (creature.IsNPC) {
                                    action = AppearanceActions.Talk;
                                } else {
                                    action = AppearanceActions.Attack;
                                }
                            } else if ((topUseObjectStackPos = field.GetTopUseObject(out topUseObject)) != -1 && !!topUseObject && topUseObject.Type.IsUsable) {
                                action = AppearanceActions.Use;
                            } else if ((topLookObjectStackPos = field.GetTopLookObject(out topLookObject)) != -1 && !!topLookObject) {
                                action = AppearanceActions.Look;
                            } else {
                                // TODO (default action)
                            }
                        } else {
                            action = AppearanceActions.AutoWalk;
                        }
                    } else if (eventModifiers == EventModifiers.Shift) {
                        topLookObjectStackPos = worldMapStorage.GetTopLookObject(mapPosition.Value, out topLookObject);
                        if (!!topLookObject)
                            action = AppearanceActions.Look;
                    } else if (eventModifiers == EventModifiers.Control) {
                        action = AppearanceActions.AutoWalk;
                    } else if (eventModifiers == EventModifiers.Alt) {
                        creature = worldMapRenderer.PointToCreature(point, true);
                        if (!!creature && creature.Id != player.Id && (!creature.IsNPC || gameManager.ClientVersion < 1000))
                            action = AppearanceActions.Attack;

                        }
                }
            } else if (optionStorage.MousePreset == MousePresets.Classic) {
                if (eventModifiers == EventModifiers.Alt) {
                    if (mouseButton == MouseButton.Left || mouseButton == MouseButton.None) {
                        creature = worldMapRenderer.PointToCreature(point, true);
                        if (!!creature && creature.Id != player.Id && (!creature.IsNPC || gameManager.ClientVersion < 1000))
                            action = AppearanceActions.Attack;
                    }
                } else if (eventModifiers == EventModifiers.Control) {
                    if (mouseButton != MouseButton.Both && mouseButton != MouseButton.Middle) {
                        topLookObjectStackPos = worldMapStorage.GetTopLookObject(mapPosition.Value, out topLookObject);
                        topUseObjectStackPos = worldMapStorage.GetTopUseObject(mapPosition.Value, out topUseObject);
                        if (!!topLookObject && topLookObject.IsCreature)
                            creature = creatureStorage.GetCreature(topLookObject.Data);

                        if (!!topUseObject || !!topLookObject)
                            action = AppearanceActions.ContextMenu;
                    }
                } else if (mouseButton == MouseButton.Left || mouseButton == MouseButton.None) {
                    if (eventModifiers == EventModifiers.None) {
                        topLookObjectStackPos = worldMapStorage.GetTopLookObject(mapPosition.Value, out topLookObject);
                        if (!!topLookObject) {
                            if (optionStorage.MouseLootPreset == MouseLootPresets.Left && topLookObject.Type.IsCorpse) {
                                topUseObject = topLookObject;
                                topUseObjectStackPos = topLookObjectStackPos;
                                action = AppearanceActions.Loot;
                            } else if (topLookObject.Type.DefaultAction == PlayerAction.AutowalkHighlight) {
                                action = AppearanceActions.AutoWalkHighlight;
                            } else {
                                action = AppearanceActions.AutoWalk;
                            }
                        }
                            
                    } else if (eventModifiers == EventModifiers.Shift) {
                        topLookObjectStackPos = worldMapStorage.GetTopLookObject(mapPosition.Value, out topLookObject);
                        if (!!topLookObject)
                            action = AppearanceActions.Look;
                    }
                } else if (mouseButton == MouseButton.Right) {
                    if (eventModifiers == EventModifiers.None) {
                        creature = worldMapRenderer.PointToCreature(point, true);
                        if (!!creature && creature.Id != player.Id && (!creature.IsNPC || gameManager.ClientVersion < 1000)) {
                            action = AppearanceActions.Attack;
                        } else {
                            topUseObjectStackPos = worldMapStorage.GetTopUseObject(mapPosition.Value, out topUseObject);
                            if (!!topUseObject) {
                                if (optionStorage.MouseLootPreset == MouseLootPresets.Right && topUseObject.Type.IsCorpse)
                                    action = AppearanceActions.Loot;
                                else if (topUseObject.Type.IsContainer)
                                    action = AppearanceActions.Open;
                                else
                                    action = AppearanceActions.Use;
                            }
                        }
                    } else if (eventModifiers == EventModifiers.Shift && optionStorage.MouseLootPreset == MouseLootPresets.ShiftPlusRight) {
                        topUseObjectStackPos = worldMapStorage.GetTopUseObject(mapPosition.Value, out topUseObject);
                        if (!!topUseObject && topUseObject.Type.IsCorpse)
                            action = AppearanceActions.Loot;
                    }
                } else if (mouseButton == MouseButton.Both) {
                    if (eventModifiers == EventModifiers.None) {
                        topLookObjectStackPos = worldMapStorage.GetTopLookObject(mapPosition.Value, out topLookObject);
                        if (!!topLookObject)
                            action = AppearanceActions.Look;
                    }
                }

            } else if (optionStorage.MousePreset == MousePresets.Regular) {
                // TODO
            }

            if (updateCursor)
                OpenTibiaUnity.GameManager.CursorController.SetCursorState(action, CursorPriority.Medium);

            if (updateHighlight && !OpenTibiaUnity.GameManager.ActiveBlocker.gameObject.activeSelf) {
                switch (action) {
                    case AppearanceActions.Talk:
                    case AppearanceActions.Attack:
                        worldMapRenderer.HighlightObject = creature;
                        break;

                    case AppearanceActions.Look:
                    case AppearanceActions.AutoWalkHighlight:
                        worldMapRenderer.HighlightObject = topLookObject;
                        break;
                        
                    case AppearanceActions.Use:
                    case AppearanceActions.Open:
                    case AppearanceActions.Loot:
                        worldMapRenderer.HighlightObject = topUseObject;
                        break;
                        
                    default:
                        worldMapRenderer.HighlightObject = null;
                        break;
                }
            } else if (updateHighlight) {
                worldMapRenderer.HighlightObject = null;
            }
            
            if (applyAction) {
                switch (action) {
                    case AppearanceActions.None: break;
                    case AppearanceActions.Attack:
                        if (!!creature && creature.Id != player.Id)
                            OpenTibiaUnity.CreatureStorage.ToggleAttackTarget(creature, true);
                        break;
                    case AppearanceActions.AutoWalk:
                    case AppearanceActions.AutoWalkHighlight:
                        player.StartAutowalk(worldMapStorage.ToAbsolute(mapPosition.Value), false, true);
                        break;
                    case AppearanceActions.ContextMenu:
                        OpenTibiaUnity.CreateObjectContextMenu(absolutePosition, topLookObject, topLookObjectStackPos, topUseObject, topUseObjectStackPos, creature)
                            .Display(mousePosition);
                        break;
                    case AppearanceActions.Look:
                        new LookActionImpl(absolutePosition, topLookObject, topLookObjectStackPos).Perform();
                        break;
                    case AppearanceActions.Use:
                        if (topUseObject.Type.IsMultiUse)
                            ObjectMultiUseHandler.Activate(absolutePosition, topUseObject, topUseObjectStackPos);
                        else
                            GameActionFactory.CreateUseAction(absolutePosition, topUseObject.Type, topUseObjectStackPos, Vector3Int.zero, null, 0, UseActionTarget.Auto).Perform();
                        break;
                    case AppearanceActions.Open:
                        GameActionFactory.CreateUseAction(absolutePosition, topUseObject, topUseObjectStackPos, Vector3Int.zero, null, 0, UseActionTarget.Auto).Perform();
                        break;
                    case AppearanceActions.Talk:
                        GameActionFactory.CreateGreetAction(creature).Perform();
                        break;
                    case AppearanceActions.Loot:
                        // TODO: Loot action
                        break;
                    case AppearanceActions.Unset:
                        break;
                }
            }

            return action;
        }

        public int GetMoveObjectUnderPoint(Vector3 mousePosition, out ObjectInstance @object) {
            if (!_mouseCursorOverRenderer) {
                @object = null;
                return - 1;
            }

            var mapPosition = MousePositionToMapPosition(mousePosition);
            if (!mapPosition.HasValue) {
                @object = null;
                return -1;
            }

            return OpenTibiaUnity.WorldMapStorage.GetTopMoveObject(mapPosition.Value, out @object);
        }

        public int GetTopObjectUnderPoint(Vector3 mousePosition, out ObjectInstance @object) {
            if (!_mouseCursorOverRenderer) {
                @object = null;
                return -1;
            }

            var mapPosition = MousePositionToMapPosition(mousePosition);
            if (!mapPosition.HasValue) {
                @object = null;
                return -1;
            }

            return OpenTibiaUnity.WorldMapStorage.GetTopLookObject(mapPosition.Value, out @object);
        }

        public int GetUseObjectUnderPoint(Vector3 mousePosition, out ObjectInstance @object) {
            if (!_mouseCursorOverRenderer) {
                @object = null;
                return -1;
            }

            var mapPosition = MousePositionToMapPosition(mousePosition);
            if (!mapPosition.HasValue) {
                @object = null;
                return -1;
            }

            return OpenTibiaUnity.WorldMapStorage.GetTopUseObject(mapPosition.Value, out @object);
        }

        public int GetMultiUseObjectUnderPoint(Vector3 mousePosition, out ObjectInstance @object) {
            if (!_mouseCursorOverRenderer) {
                @object = null;
                return -1;
            }

            var mapPosition = MousePositionToMapPosition(mousePosition);
            if (!mapPosition.HasValue) {
                @object = null;
                return -1;
            }

            return OpenTibiaUnity.WorldMapStorage.GetTopMultiUseObject(mapPosition.Value, out @object);
        }

        public Vector3Int? MousePositionToMapPosition(Vector3 mousePosition) {
            return OpenTibiaUnity.GameManager.WorldMapRenderer.PointToMap(RawMousePositionToLocalMapPosition(mousePosition));
        }

        public Vector3Int? MousePositionToAbsolutePosition(Vector3 mousePosition) {
            return OpenTibiaUnity.GameManager.WorldMapRenderer.PointToAbsolute(RawMousePositionToLocalMapPosition(mousePosition));
        }

        private Vector2 RawMousePositionToLocalMapPosition(Vector3 mousePosition) {
            CacheScreenRect();

            var mousePoint = new Vector2(Input.mousePosition.x, _lastScreenHeight - Input.mousePosition.y);
            return mousePoint - _cachedScreenRect.position;
        }
    }
}
