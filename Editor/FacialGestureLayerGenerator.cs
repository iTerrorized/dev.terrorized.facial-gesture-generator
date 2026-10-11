using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Terrorized.Editor
{
    public class FacialGestureLayerGenerator : EditorWindow
    {
        // Configuration fields
        private bool writeDefaults = false;
        private HandPriority handPriority = HandPriority.Right;
        private bool useGestureWeightForFist = false;
        private bool toggleForGestures = false;
        private bool disableDuringFaceTracking = false;

        // Avatar Dynamics
        private bool enableHeadpat = false;
        private bool enableBoop = false;
        private bool enableHeadpatDuringFaceTracking = false;
        private bool enableBoopDuringFaceTracking = false;

        // Manual Gestures
        private bool enableManualGestures = false;
        private int manualGestureCount = 1;
        private const string ManualGestureParam = "ManualGesture";

        // Gesture definitions
        private static readonly string[] GestureNames = new string[]
        {
            "Idle", "Fist", "OpenHand", "Point", "Peace", "RockNRoll", "Gun", "ThumbsUp"
        };

        private enum HandPriority
        {
            Left,
            Right
        }

        // State references for transition creation
        private AnimatorState idleState;
        private Dictionary<string, AnimatorState> gestureStates = new Dictionary<string, AnimatorState>();
        private List<AnimatorState> manualStates = new List<AnimatorState>();

        [MenuItem("Terrorized/Tools/Facial Expression Controller Generator")]
        public static void ShowWindow()
        {
            GetWindow<FacialGestureLayerGenerator>("Facial Expression Controller Generator");
        }

        private void OnGUI()
        {
            // Title
            EditorGUILayout.Space(10);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Facial Expression Controller Generator", titleStyle);
            EditorGUILayout.Space(5);

            // Subtitle
            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            GUILayout.Label("VRChat Gesture System with Face Tracking & Avatar Dynamics", subtitleStyle);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(10);

            // Scrollable content
            EditorGUILayout.BeginVertical();

            // Basic Settings Section
            DrawSection("Basic Settings", () =>
            {
                writeDefaults = EditorGUILayout.Toggle(new GUIContent("Write Defaults",
                    "Enable Write Defaults for all states"), writeDefaults);
                handPriority = (HandPriority)EditorGUILayout.EnumPopup(new GUIContent("Prioritize Hand",
                    "Which hand gesture takes priority when both are active"), handPriority);
            });

            EditorGUILayout.Space(5);

            // Gesture Options Section
            DrawSection("Gesture Options", () =>
            {
                useGestureWeightForFist = EditorGUILayout.Toggle(new GUIContent("Use Gesture Weight for Fist",
                    "Creates blend trees for Fist gestures using GestureLeftWeight/GestureRightWeight parameters"),
                    useGestureWeightForFist);

                toggleForGestures = EditorGUILayout.Toggle(new GUIContent("Toggle For Gestures",
                    "Adds Utility/FacialExpressions bool to enable/disable all gestures"),
                    toggleForGestures);

                disableDuringFaceTracking = EditorGUILayout.Toggle(new GUIContent("Disable During Face Tracking",
                    "Blocks gestures when EyeTrackingActive or LipTrackingActive is true"),
                    disableDuringFaceTracking);
            });

            EditorGUILayout.Space(5);

            // Avatar Dynamics Section
            DrawSection("Avatar Dynamics", () =>
            {
                enableHeadpat = EditorGUILayout.Toggle(new GUIContent("Enable Headpat",
                    "Adds Headpat state with Utility/Headpat and AD/Headpat conditions"),
                    enableHeadpat);

                if (enableHeadpat && disableDuringFaceTracking)
                {
                    EditorGUI.indentLevel++;
                    enableHeadpatDuringFaceTracking = EditorGUILayout.Toggle(new GUIContent("Allow During Face Tracking",
                        "Allows Headpat to work during face tracking (creates Headpat_FT state)"),
                        enableHeadpatDuringFaceTracking);
                    EditorGUI.indentLevel--;
                }

                enableBoop = EditorGUILayout.Toggle(new GUIContent("Enable Boop",
                    "Adds Boop state with Utility/Boop and AD/Boop conditions"),
                    enableBoop);

                if (enableBoop && disableDuringFaceTracking)
                {
                    EditorGUI.indentLevel++;
                    enableBoopDuringFaceTracking = EditorGUILayout.Toggle(new GUIContent("Allow During Face Tracking",
                        "Allows Boop to work during face tracking (creates Boop_FT state)"),
                        enableBoopDuringFaceTracking);
                    EditorGUI.indentLevel--;
                }
            });

            EditorGUILayout.Space(5);

            // Manual Gestures Section
            DrawSection("Manual Gestures", () =>
            {
                enableManualGestures = EditorGUILayout.Toggle(new GUIContent("Enable Manual Gestures",
                    "Adds a ManualGesture int. At 0 the normal face logic runs, at any higher value the matching Manual state is forced"),
                    enableManualGestures);

                if (enableManualGestures)
                {
                    EditorGUI.indentLevel++;
                    manualGestureCount = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Manual Gesture Count",
                        "Number of manual gesture states to create (ManualGesture = 1 to this value)"),
                        manualGestureCount), 1, 255);
                    EditorGUI.indentLevel--;
                }
            });

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(10);

            // Generate Button
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 35
            };

            if (GUILayout.Button("Generate Controller", buttonStyle))
            {
                GenerateController();
            }

            EditorGUILayout.Space(10);
        }

        private void DrawSection(string title, System.Action content)
        {
            // Section header
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            EditorGUILayout.LabelField(title, headerStyle);

            // Section content box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(3);
            content?.Invoke();
            EditorGUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        private void GenerateController()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Facial Gesture Controller",
                "FacialGestureController",
                "controller",
                "Choose where to save the controller"
            );

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            AnimatorController controller = CreateAnimatorController(path);

            if (controller != null)
            {
                EditorUtility.DisplayDialog("Success", "Facial Gesture Controller created successfully!", "OK");
                Selection.activeObject = controller;
            }
        }

        private AnimatorController CreateAnimatorController(string path)
        {
            // Create controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Add parameters
            AddParameters(controller);

            // Setup state machine
            SetupStateMachine(controller);

            // Save
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return controller;
        }

        private void AddParameters(AnimatorController controller)
        {
            // Base gesture parameters (always added)
            controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);
            controller.AddParameter("GestureRight", AnimatorControllerParameterType.Int);

            // Gesture weight parameters
            if (useGestureWeightForFist)
            {
                controller.AddParameter("GestureLeftWeight", AnimatorControllerParameterType.Float);
                controller.AddParameter("GestureRightWeight", AnimatorControllerParameterType.Float);
            }

            // Toggle parameter
            if (toggleForGestures)
            {
                controller.AddParameter("Utility/FacialExpressions", AnimatorControllerParameterType.Bool);
            }

            // Face tracking parameters
            if (disableDuringFaceTracking)
            {
                controller.AddParameter("EyeTrackingActive", AnimatorControllerParameterType.Bool);
                controller.AddParameter("LipTrackingActive", AnimatorControllerParameterType.Bool);
            }

            // Avatar Dynamics parameters
            if (enableHeadpat || enableBoop)
            {
                // IsLocal parameter (set by VRChat automatically)
                controller.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            }

            // Headpat parameters
            if (enableHeadpat)
            {
                controller.AddParameter("Utility/Headpat", AnimatorControllerParameterType.Bool);
                controller.AddParameter("AD/Headpat", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Utility/HeadpatSync", AnimatorControllerParameterType.Bool);
            }

            // Boop parameters
            if (enableBoop)
            {
                controller.AddParameter("Utility/Boop", AnimatorControllerParameterType.Bool);
                controller.AddParameter("AD/Boop", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Utility/BoopSync", AnimatorControllerParameterType.Bool);
            }

            // Manual gesture parameter
            if (enableManualGestures)
            {
                controller.AddParameter(ManualGestureParam, AnimatorControllerParameterType.Int);
            }
        }

        private void SetupStateMachine(AnimatorController controller)
        {
            // Get base layer
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer baseLayer = layers[0];
            AnimatorStateMachine stateMachine = baseLayer.stateMachine;

            // Set node positions
            stateMachine.anyStatePosition = new Vector3(50.00f, 20.00f, 0.00f);
            stateMachine.entryPosition = new Vector3(50.00f, 130.00f, 0.00f);
            stateMachine.exitPosition = new Vector3(50.00f, -20.00f, 0.00f);

            // Create states
            CreateStates(stateMachine, controller);

            // Create transitions
            CreateTransitions(stateMachine);

            // Apply changes
            controller.layers = layers;
        }

        private void CreateStates(AnimatorStateMachine sm, AnimatorController controller)
        {
            gestureStates.Clear();

            // Create Idle state
            idleState = sm.AddState("Idle", new Vector3(30.00f, 70.00f, 0.00f));
            idleState.writeDefaultValues = writeDefaults;
            sm.defaultState = idleState;

            // Add parameter driver to Idle
            AddParameterDriver(idleState, false, false);

            // Create gesture states
            CreateGestureStates(sm, controller);

            // Create Avatar Dynamics states
            CreateAvatarDynamicsStates(sm);

            // Create Manual Gesture states
            CreateManualGestureStates(sm);
        }

        private void CreateManualGestureStates(AnimatorStateMachine sm)
        {
            manualStates.Clear();

            if (!enableManualGestures)
                return;

            int x = 520;
            int startY = -130;
            int spacing = 50;

            for (int i = 1; i <= manualGestureCount; i++)
            {
                AnimatorState manualState = sm.AddState("Manual" + i, new Vector3(x, startY + (i - 1) * spacing, 0));
                manualState.writeDefaultValues = writeDefaults;
                AddParameterDriver(manualState, false, false);
                manualStates.Add(manualState);
            }
        }

        private void CreateGestureStates(AnimatorStateMachine sm, AnimatorController controller)
        {
            // Determine which gestures to create based on hand priority
            bool createLeft = true;
            bool createRight = true;

            int leftX = -210;
            int rightX = 270;
            int startY = -130;
            int spacing = 50;

            // Create gesture states for each hand
            for (int i = 1; i < GestureNames.Length; i++)
            {
                string gestureName = GestureNames[i];
                int yPos = startY + (i - 1) * spacing;

                // Left hand gesture
                if (createLeft)
                {
                    string leftStateName = "Left" + gestureName;
                    AnimatorState leftState = sm.AddState(leftStateName, new Vector3(leftX, yPos, 0));
                    leftState.writeDefaultValues = writeDefaults;

                    // Use blend tree for Fist if enabled
                    if (gestureName == "Fist" && useGestureWeightForFist)
                    {
                        BlendTree blendTree = CreateFistBlendTree(leftStateName, "GestureLeftWeight", controller);
                        leftState.motion = blendTree;
                    }

                    // Add parameter driver
                    AddParameterDriver(leftState, false, false);

                    gestureStates[leftStateName] = leftState;
                }

                // Right hand gesture
                if (createRight)
                {
                    string rightStateName = "Right" + gestureName;
                    AnimatorState rightState = sm.AddState(rightStateName, new Vector3(rightX, yPos, 0));
                    rightState.writeDefaultValues = writeDefaults;

                    // Use blend tree for Fist if enabled
                    if (gestureName == "Fist" && useGestureWeightForFist)
                    {
                        BlendTree blendTree = CreateFistBlendTree(rightStateName, "GestureRightWeight", controller);
                        rightState.motion = blendTree;
                    }

                    // Add parameter driver
                    AddParameterDriver(rightState, false, false);

                    gestureStates[rightStateName] = rightState;
                }
            }
        }

        private void CreateAvatarDynamicsStates(AnimatorStateMachine sm)
        {
            // Headpat state
            if (enableHeadpat)
            {
                AnimatorState headpatState = sm.AddState("Headpat", new Vector3(-170.00f, -210.00f, 0.00f));
                headpatState.writeDefaultValues = writeDefaults;
                AddParameterDriver(headpatState, true, false); // Headpat active
                gestureStates["Headpat"] = headpatState;

                // Headpat_FT state
                if (disableDuringFaceTracking && enableHeadpatDuringFaceTracking)
                {
                    AnimatorState headpatFTState = sm.AddState("Headpat_FT", new Vector3(-80.00f, -260.00f, 0.00f));
                    headpatFTState.writeDefaultValues = writeDefaults;
                    AddParameterDriver(headpatFTState, true, false); // Headpat active
                    gestureStates["Headpat_FT"] = headpatFTState;
                }
            }

            // Boop state
            if (enableBoop)
            {
                AnimatorState boopState = sm.AddState("Boop", new Vector3(250.00f, -210.00f, 0.00f));
                boopState.writeDefaultValues = writeDefaults;
                AddParameterDriver(boopState, false, true); // Boop active
                gestureStates["Boop"] = boopState;

                // Boop_FT state
                if (disableDuringFaceTracking && enableBoopDuringFaceTracking)
                {
                    AnimatorState boopFTState = sm.AddState("Boop_FT", new Vector3(140.00f, -260.00f, 0.00f));
                    boopFTState.writeDefaultValues = writeDefaults;
                    AddParameterDriver(boopFTState, false, true); // Boop active
                    gestureStates["Boop_FT"] = boopFTState;
                }
            }
        }

        private BlendTree CreateFistBlendTree(string gestureName, string weightParam, AnimatorController controller)
        {
            BlendTree blendTree = new BlendTree
            {
                name = gestureName + " Weight",
                blendType = BlendTreeType.Simple1D,
                blendParameter = weightParam,
                useAutomaticThresholds = false
            };

            // Add empty motion children
            blendTree.AddChild(null, 0.25f);
            blendTree.AddChild(null, 0.9f);

            // Save blend tree as sub-asset
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            return blendTree;
        }

        private void CreateTransitions(AnimatorStateMachine sm)
        {
            // Order matters! Add transitions in priority order:
            // 0. Manual gesture states (if enabled, override everything)
            // 1. Face tracking to Idle transitions (if enabled)
            // 2. Avatar Dynamics _FT states (highest priority during face tracking)
            // 3. Avatar Dynamics regular states
            // 4. Toggle-off to Idle (if Toggle For Gestures enabled)
            // 5. Gesture states (in priority order based on hand setting)
            // 6. Idle transition when both gestures = 0

            // 0. Manual gesture states
            if (enableManualGestures)
            {
                CreateManualGestureTransitions(sm);
            }

            // 1. Face tracking to Idle transitions
            if (disableDuringFaceTracking)
            {
                CreateFaceTrackingIdleTransitions(sm);
            }

            // 2. Avatar Dynamics _FT states
            if (disableDuringFaceTracking)
            {
                if (enableHeadpat && enableHeadpatDuringFaceTracking && gestureStates.ContainsKey("Headpat_FT"))
                {
                    CreateHeadpatFTTransition(sm);
                }

                if (enableBoop && enableBoopDuringFaceTracking && gestureStates.ContainsKey("Boop_FT"))
                {
                    CreateBoopFTTransition(sm);
                }
            }

            // 3. Avatar Dynamics regular states
            if (enableHeadpat && gestureStates.ContainsKey("Headpat"))
            {
                CreateHeadpatTransition(sm);
            }

            if (enableBoop && gestureStates.ContainsKey("Boop"))
            {
                CreateBoopTransition(sm);
            }

            // 4. Toggle-off to Idle
            if (toggleForGestures)
            {
                CreateToggleOffTransition(sm);
            }

            // 5. Gesture states
            CreateGestureTransitions(sm);

            // 6. Idle transition when both gestures = 0
            CreateIdleTransition(sm);

            // Normal face logic only runs while ManualGesture = 0
            if (enableManualGestures)
            {
                AddManualGestureGuards(sm);
            }
        }

        private void CreateManualGestureTransitions(AnimatorStateMachine sm)
        {
            for (int i = 0; i < manualStates.Count; i++)
            {
                AnimatorStateTransition transition = sm.AddAnyStateTransition(manualStates[i]);
                transition.hasExitTime = false;
                transition.duration = 0.1f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, i + 1, ManualGestureParam);
            }
        }

        private void AddManualGestureGuards(AnimatorStateMachine sm)
        {
            foreach (AnimatorStateTransition transition in sm.anyStateTransitions)
            {
                if (manualStates.Contains(transition.destinationState))
                    continue;

                transition.AddCondition(AnimatorConditionMode.Equals, 0, ManualGestureParam);
            }
        }

        private void CreateHeadpatFTTransition(AnimatorStateMachine sm)
        {
            // LOCAL TRANSITIONS (IsLocal = True, uses AD/Headpat contact receiver)
            // Transition for EyeTrackingActive
            AnimatorStateTransition localEye = sm.AddAnyStateTransition(gestureStates["Headpat_FT"]);
            localEye.hasExitTime = false;
            localEye.duration = 0.1f;
            localEye.canTransitionToSelf = false;
            localEye.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            localEye.AddCondition(AnimatorConditionMode.If, 0, "Utility/Headpat");
            localEye.AddCondition(AnimatorConditionMode.If, 0, "AD/Headpat");
            localEye.AddCondition(AnimatorConditionMode.If, 0, "EyeTrackingActive");

            // Transition for LipTrackingActive
            AnimatorStateTransition localLip = sm.AddAnyStateTransition(gestureStates["Headpat_FT"]);
            localLip.hasExitTime = false;
            localLip.duration = 0.1f;
            localLip.canTransitionToSelf = false;
            localLip.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            localLip.AddCondition(AnimatorConditionMode.If, 0, "Utility/Headpat");
            localLip.AddCondition(AnimatorConditionMode.If, 0, "AD/Headpat");
            localLip.AddCondition(AnimatorConditionMode.If, 0, "LipTrackingActive");

            // REMOTE TRANSITIONS (IsLocal = False, uses Utility/HeadpatSync)
            // Transition for EyeTrackingActive
            AnimatorStateTransition remoteEye = sm.AddAnyStateTransition(gestureStates["Headpat_FT"]);
            remoteEye.hasExitTime = false;
            remoteEye.duration = 0.1f;
            remoteEye.canTransitionToSelf = false;
            remoteEye.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            remoteEye.AddCondition(AnimatorConditionMode.If, 0, "Utility/HeadpatSync");
            remoteEye.AddCondition(AnimatorConditionMode.If, 0, "EyeTrackingActive");

            // Transition for LipTrackingActive
            AnimatorStateTransition remoteLip = sm.AddAnyStateTransition(gestureStates["Headpat_FT"]);
            remoteLip.hasExitTime = false;
            remoteLip.duration = 0.1f;
            remoteLip.canTransitionToSelf = false;
            remoteLip.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            remoteLip.AddCondition(AnimatorConditionMode.If, 0, "Utility/HeadpatSync");
            remoteLip.AddCondition(AnimatorConditionMode.If, 0, "LipTrackingActive");
        }

        private void CreateBoopFTTransition(AnimatorStateMachine sm)
        {
            // LOCAL TRANSITIONS (IsLocal = True, uses AD/Boop contact receiver)
            // Transition for EyeTrackingActive
            AnimatorStateTransition localEye = sm.AddAnyStateTransition(gestureStates["Boop_FT"]);
            localEye.hasExitTime = false;
            localEye.duration = 0.1f;
            localEye.canTransitionToSelf = false;
            localEye.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            localEye.AddCondition(AnimatorConditionMode.If, 0, "Utility/Boop");
            localEye.AddCondition(AnimatorConditionMode.If, 0, "AD/Boop");
            localEye.AddCondition(AnimatorConditionMode.If, 0, "EyeTrackingActive");

            // Transition for LipTrackingActive
            AnimatorStateTransition localLip = sm.AddAnyStateTransition(gestureStates["Boop_FT"]);
            localLip.hasExitTime = false;
            localLip.duration = 0.1f;
            localLip.canTransitionToSelf = false;
            localLip.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            localLip.AddCondition(AnimatorConditionMode.If, 0, "Utility/Boop");
            localLip.AddCondition(AnimatorConditionMode.If, 0, "AD/Boop");
            localLip.AddCondition(AnimatorConditionMode.If, 0, "LipTrackingActive");

            // REMOTE TRANSITIONS (IsLocal = False, uses Utility/BoopSync)
            // Transition for EyeTrackingActive
            AnimatorStateTransition remoteEye = sm.AddAnyStateTransition(gestureStates["Boop_FT"]);
            remoteEye.hasExitTime = false;
            remoteEye.duration = 0.1f;
            remoteEye.canTransitionToSelf = false;
            remoteEye.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            remoteEye.AddCondition(AnimatorConditionMode.If, 0, "Utility/BoopSync");
            remoteEye.AddCondition(AnimatorConditionMode.If, 0, "EyeTrackingActive");

            // Transition for LipTrackingActive
            AnimatorStateTransition remoteLip = sm.AddAnyStateTransition(gestureStates["Boop_FT"]);
            remoteLip.hasExitTime = false;
            remoteLip.duration = 0.1f;
            remoteLip.canTransitionToSelf = false;
            remoteLip.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            remoteLip.AddCondition(AnimatorConditionMode.If, 0, "Utility/BoopSync");
            remoteLip.AddCondition(AnimatorConditionMode.If, 0, "LipTrackingActive");
        }

        private void CreateHeadpatTransition(AnimatorStateMachine sm)
        {
            // LOCAL TRANSITION (IsLocal = True, uses AD/Headpat contact receiver)
            AnimatorStateTransition localTransition = sm.AddAnyStateTransition(gestureStates["Headpat"]);
            localTransition.hasExitTime = false;
            localTransition.duration = 0.1f;
            localTransition.canTransitionToSelf = false;
            localTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            localTransition.AddCondition(AnimatorConditionMode.If, 0, "Utility/Headpat");
            localTransition.AddCondition(AnimatorConditionMode.If, 0, "AD/Headpat");

            // If face tracking disable is enabled, only allow when face tracking is OFF
            if (disableDuringFaceTracking)
            {
                localTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "EyeTrackingActive");
                localTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "LipTrackingActive");
            }

            // REMOTE TRANSITION (IsLocal = False, uses Utility/HeadpatSync)
            AnimatorStateTransition remoteTransition = sm.AddAnyStateTransition(gestureStates["Headpat"]);
            remoteTransition.hasExitTime = false;
            remoteTransition.duration = 0.1f;
            remoteTransition.canTransitionToSelf = false;
            remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            remoteTransition.AddCondition(AnimatorConditionMode.If, 0, "Utility/HeadpatSync");

            // If face tracking disable is enabled, only allow when face tracking is OFF
            if (disableDuringFaceTracking)
            {
                remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "EyeTrackingActive");
                remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "LipTrackingActive");
            }
        }

        private void CreateBoopTransition(AnimatorStateMachine sm)
        {
            // LOCAL TRANSITION (IsLocal = True, uses AD/Boop contact receiver)
            AnimatorStateTransition localTransition = sm.AddAnyStateTransition(gestureStates["Boop"]);
            localTransition.hasExitTime = false;
            localTransition.duration = 0.1f;
            localTransition.canTransitionToSelf = false;
            localTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            localTransition.AddCondition(AnimatorConditionMode.If, 0, "Utility/Boop");
            localTransition.AddCondition(AnimatorConditionMode.If, 0, "AD/Boop");

            // If face tracking disable is enabled, only allow when face tracking is OFF
            if (disableDuringFaceTracking)
            {
                localTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "EyeTrackingActive");
                localTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "LipTrackingActive");
            }

            // REMOTE TRANSITION (IsLocal = False, uses Utility/BoopSync)
            AnimatorStateTransition remoteTransition = sm.AddAnyStateTransition(gestureStates["Boop"]);
            remoteTransition.hasExitTime = false;
            remoteTransition.duration = 0.1f;
            remoteTransition.canTransitionToSelf = false;
            remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            remoteTransition.AddCondition(AnimatorConditionMode.If, 0, "Utility/BoopSync");

            // If face tracking disable is enabled, only allow when face tracking is OFF
            if (disableDuringFaceTracking)
            {
                remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "EyeTrackingActive");
                remoteTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "LipTrackingActive");
            }
        }

        private void CreateToggleOffTransition(AnimatorStateMachine sm)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(idleState);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;

            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Utility/FacialExpressions");

            // Block when Avatar Dynamics utility is enabled (AD should work even when toggle is off)
            if (enableHeadpat)
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Utility/Headpat");
            }

            if (enableBoop)
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Utility/Boop");
            }
        }

        private void CreateGestureTransitions(AnimatorStateMachine sm)
        {
            // Create transitions based on hand priority
            if (handPriority == HandPriority.Left)
            {
                // Left hand takes priority
                for (int i = 1; i < GestureNames.Length; i++)
                {
                    CreateGestureTransition(sm, i, true);  // Left
                    CreateGestureTransition(sm, i, false); // Right
                }
            }
            else
            {
                // Right hand takes priority
                for (int i = 1; i < GestureNames.Length; i++)
                {
                    CreateGestureTransition(sm, i, false); // Right
                    CreateGestureTransition(sm, i, true);  // Left
                }
            }
        }

        private void CreateGestureTransition(AnimatorStateMachine sm, int gestureValue, bool isLeft)
        {
            string hand = isLeft ? "Left" : "Right";
            string stateName = hand + GestureNames[gestureValue];

            if (!gestureStates.ContainsKey(stateName))
                return;

            string primaryParam = isLeft ? "GestureLeft" : "GestureRight";
            string secondaryParam = isLeft ? "GestureRight" : "GestureLeft";

            // Create multiple transitions to handle all AD state combinations
            // We need separate transitions because Unity doesn't support OR logic

            if (enableHeadpat && enableBoop)
            {
                // Both Headpat and Boop are enabled - need 5 transitions

                // 1. Local: Both contacts inactive
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("AD/Headpat", false), ("AD/Boop", false) });

                // 2. Local: Headpat util off, Boop contact inactive
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("Utility/Headpat", false), ("AD/Boop", false) });

                // 3. Local: Headpat contact inactive, Boop util off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("AD/Headpat", false), ("Utility/Boop", false) });

                // 4. Local: Both utils off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("Utility/Headpat", false), ("Utility/Boop", false) });

                // 5. Remote: Both syncs off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, false,
                    new[] { ("Utility/HeadpatSync", false), ("Utility/BoopSync", false) });
            }
            else if (enableHeadpat)
            {
                // Only Headpat enabled - need 3 transitions

                // 1. Local: Contact inactive
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("AD/Headpat", false) });

                // 2. Local: Util off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("Utility/Headpat", false) });

                // 3. Remote: Sync off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, false,
                    new[] { ("Utility/HeadpatSync", false) });
            }
            else if (enableBoop)
            {
                // Only Boop enabled - need 3 transitions

                // 1. Local: Contact inactive
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("AD/Boop", false) });

                // 2. Local: Util off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, true,
                    new[] { ("Utility/Boop", false) });

                // 3. Remote: Sync off
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, false,
                    new[] { ("Utility/BoopSync", false) });
            }
            else
            {
                // No AD enabled - single transition
                CreateSingleGestureTransition(sm, stateName, gestureValue, primaryParam, secondaryParam, null, null);
            }
        }

        private void CreateSingleGestureTransition(AnimatorStateMachine sm, string stateName, int gestureValue,
            string primaryParam, string secondaryParam, bool? isLocal, (string param, bool value)[] adConditions)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(gestureStates[stateName]);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;

            // IsLocal condition
            if (isLocal.HasValue)
            {
                if (isLocal.Value)
                    transition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                else
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            }

            // Gesture conditions
            transition.AddCondition(AnimatorConditionMode.Equals, gestureValue, primaryParam);

            // Only check opposite hand if this gesture is on the non-priority hand
            bool isLeftGesture = primaryParam == "GestureLeft";
            bool isPriorityHand = (handPriority == HandPriority.Left && isLeftGesture) ||
                                  (handPriority == HandPriority.Right && !isLeftGesture);

            if (!isPriorityHand)
            {
                // Non-priority hand must check that priority hand is Idle
                transition.AddCondition(AnimatorConditionMode.Equals, 0, secondaryParam);
            }

            // Toggle condition
            if (toggleForGestures)
            {
                transition.AddCondition(AnimatorConditionMode.If, 0, "Utility/FacialExpressions");
            }

            // Face tracking conditions
            if (disableDuringFaceTracking)
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "EyeTrackingActive");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "LipTrackingActive");
            }

            // Avatar Dynamics conditions
            if (adConditions != null)
            {
                foreach (var (param, value) in adConditions)
                {
                    if (value)
                        transition.AddCondition(AnimatorConditionMode.If, 0, param);
                    else
                        transition.AddCondition(AnimatorConditionMode.IfNot, 0, param);
                }
            }
        }

        private void CreateIdleTransition(AnimatorStateMachine sm)
        {
            // Create multiple Idle transitions to handle all AD state combinations

            if (enableHeadpat && enableBoop)
            {
                // Both enabled - 5 transitions matching gesture logic

                // 1. Local: Both contacts inactive
                CreateSingleIdleTransition(sm, true, new[] { ("AD/Headpat", false), ("AD/Boop", false) });

                // 2. Local: Headpat util off, Boop contact inactive
                CreateSingleIdleTransition(sm, true, new[] { ("Utility/Headpat", false), ("AD/Boop", false) });

                // 3. Local: Headpat contact inactive, Boop util off
                CreateSingleIdleTransition(sm, true, new[] { ("AD/Headpat", false), ("Utility/Boop", false) });

                // 4. Local: Both utils off
                CreateSingleIdleTransition(sm, true, new[] { ("Utility/Headpat", false), ("Utility/Boop", false) });

                // 5. Remote: Both syncs off
                CreateSingleIdleTransition(sm, false, new[] { ("Utility/HeadpatSync", false), ("Utility/BoopSync", false) });
            }
            else if (enableHeadpat)
            {
                // Only Headpat - 3 transitions

                // 1. Local: Contact inactive
                CreateSingleIdleTransition(sm, true, new[] { ("AD/Headpat", false) });

                // 2. Local: Util off
                CreateSingleIdleTransition(sm, true, new[] { ("Utility/Headpat", false) });

                // 3. Remote: Sync off
                CreateSingleIdleTransition(sm, false, new[] { ("Utility/HeadpatSync", false) });
            }
            else if (enableBoop)
            {
                // Only Boop - 3 transitions

                // 1. Local: Contact inactive
                CreateSingleIdleTransition(sm, true, new[] { ("AD/Boop", false) });

                // 2. Local: Util off
                CreateSingleIdleTransition(sm, true, new[] { ("Utility/Boop", false) });

                // 3. Remote: Sync off
                CreateSingleIdleTransition(sm, false, new[] { ("Utility/BoopSync", false) });
            }
            else
            {
                // No AD - single transition
                CreateSingleIdleTransition(sm, null, null);
            }
        }

        private void CreateSingleIdleTransition(AnimatorStateMachine sm, bool? isLocal, (string param, bool value)[] adConditions)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(idleState);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;

            // IsLocal condition
            if (isLocal.HasValue)
            {
                if (isLocal.Value)
                    transition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                else
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            }

            // Both gestures must be 0 (Idle)
            transition.AddCondition(AnimatorConditionMode.Equals, 0, "GestureLeft");
            transition.AddCondition(AnimatorConditionMode.Equals, 0, "GestureRight");

            // Toggle condition
            if (toggleForGestures)
            {
                transition.AddCondition(AnimatorConditionMode.If, 0, "Utility/FacialExpressions");
            }

            // Face tracking conditions
            if (disableDuringFaceTracking)
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "EyeTrackingActive");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "LipTrackingActive");
            }

            // Avatar Dynamics conditions
            if (adConditions != null)
            {
                foreach (var (param, value) in adConditions)
                {
                    if (value)
                        transition.AddCondition(AnimatorConditionMode.If, 0, param);
                    else
                        transition.AddCondition(AnimatorConditionMode.IfNot, 0, param);
                }
            }
        }

        private void AddParameterDriver(AnimatorState state, bool headpatActive, bool boopActive)
        {
            // Only add parameter drivers if AD is enabled
            if (!enableHeadpat && !enableBoop)
                return;

            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = true;
            driver.parameters = new List<VRC.SDKBase.VRC_AvatarParameterDriver.Parameter>();

            if (enableHeadpat)
            {
                driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                {
                    name = "Utility/HeadpatSync",
                    value = headpatActive ? 1 : 0,
                    type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set
                });
            }

            if (enableBoop)
            {
                driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                {
                    name = "Utility/BoopSync",
                    value = boopActive ? 1 : 0,
                    type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set
                });
            }
        }

        private void CreateFaceTrackingIdleTransitions(AnimatorStateMachine sm)
        {
            // Transition to Idle when EyeTrackingActive is true (but not when AD contact is actually active)
            AnimatorStateTransition eyeTransition = sm.AddAnyStateTransition(idleState);
            eyeTransition.hasExitTime = false;
            eyeTransition.duration = 0.1f;
            eyeTransition.canTransitionToSelf = false;
            eyeTransition.AddCondition(AnimatorConditionMode.If, 0, "EyeTrackingActive");

            // Don't add blocking - the _FT AD transitions have higher priority
            // If AD_FT conditions are met, those transitions fire first

            // Transition to Idle when LipTrackingActive is true (but not when AD is actually active)
            AnimatorStateTransition lipTransition = sm.AddAnyStateTransition(idleState);
            lipTransition.hasExitTime = false;
            lipTransition.duration = 0.1f;
            lipTransition.canTransitionToSelf = false;
            lipTransition.AddCondition(AnimatorConditionMode.If, 0, "LipTrackingActive");

            // Only block when the AD is actually active (check sync parameters for both local and remote)
            if (enableHeadpat && enableHeadpatDuringFaceTracking)
            {
                lipTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Utility/HeadpatSync");
            }
            if (enableBoop && enableBoopDuringFaceTracking)
            {
                lipTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Utility/BoopSync");
            }
        }
    }
}
