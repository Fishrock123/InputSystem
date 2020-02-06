#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.UI.Editor;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Utilities;

////TODO: detect if new input system isn't enabled and provide UI to enable it
#pragma warning disable 0414
namespace UnityEngine.InputSystem.Editor
{
    /// <summary>
    /// A custom inspector for the <see cref="PlayerInput"/> component.
    /// </summary>
    [CustomEditor(typeof(PlayerInput))]
    internal class PlayerInputEditor : UnityEditor.Editor
    {
        public const string kDefaultInputActionsAssetPath =
            "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";

        public void OnEnable()
        {
            InputActionImporter.onImport += Refresh;
            InputUser.onChange += OnUserChange;
        }

        public void OnDestroy()
        {
            InputActionImporter.onImport -= Refresh;
            InputUser.onChange -= OnUserChange;
        }

        private void Refresh()
        {
            ////FIXME: doesn't seem like we're picking up the results of the latest import
            m_ActionAssetInitialized = false;
            Repaint();
        }

        private void OnUserChange(InputUser user, InputUserChange change, InputDevice device)
        {
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            ////TODO: cache properties

            EditorGUI.BeginChangeCheck();

            // Action config section.
            EditorGUI.BeginChangeCheck();
            var actionsProperty = serializedObject.FindProperty("m_Actions");
            EditorGUILayout.PropertyField(actionsProperty);
            if (EditorGUI.EndChangeCheck() || !m_ActionAssetInitialized)
                OnActionAssetChange();
            ++EditorGUI.indentLevel;
            if (m_ControlSchemeOptions != null && m_ControlSchemeOptions.Length > 1) // Don't show if <Any> is the only option.
            {
                // Default control scheme picker.

                var selected = EditorGUILayout.Popup(m_DefaultControlSchemeText, m_SelectedDefaultControlScheme,
                    m_ControlSchemeOptions);
                if (selected != m_SelectedDefaultControlScheme)
                {
                    var defaultControlSchemeProperty = serializedObject.FindProperty("m_DefaultControlScheme");
                    if (selected == 0)
                    {
                        defaultControlSchemeProperty.stringValue = null;
                    }
                    else
                    {
                        defaultControlSchemeProperty.stringValue =
                            m_ControlSchemeOptions[selected].text;
                    }
                    m_SelectedDefaultControlScheme = selected;
                }

                var neverAutoSwitchProperty = serializedObject.FindProperty("m_NeverAutoSwitchControlSchemes");
                var neverAutoSwitchValueOld = neverAutoSwitchProperty.boolValue;
                var neverAutoSwitchValueNew = !EditorGUILayout.Toggle(m_AutoSwitchText, !neverAutoSwitchValueOld);
                if (neverAutoSwitchValueOld != neverAutoSwitchValueNew)
                {
                    neverAutoSwitchProperty.boolValue = neverAutoSwitchValueNew;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            if (m_ActionMapOptions != null && m_ActionMapOptions.Length > 0)
            {
                // Default action map picker.

                var selected = EditorGUILayout.Popup(m_DefaultActionMapText, m_SelectedDefaultActionMap,
                    m_ActionMapOptions);
                if (selected != m_SelectedDefaultActionMap)
                {
                    var defaultActionMapProperty = serializedObject.FindProperty("m_DefaultActionMap");
                    if (selected == 0)
                    {
                        defaultActionMapProperty.stringValue = null;
                    }
                    else
                    {
                        // Use ID rather than name.
                        var asset = (InputActionAsset)serializedObject.FindProperty("m_Actions").objectReferenceValue;
                        var actionMap = asset.FindActionMap(m_ActionMapOptions[selected].text);
                        if (actionMap != null)
                            defaultActionMapProperty.stringValue = actionMap.id.ToString();
                    }
                    m_SelectedDefaultActionMap = selected;
                }
            }
            --EditorGUI.indentLevel;
            DoHelpCreateAssetUI();

            // Player index setting.
            var playerIndexProperty = serializedObject.FindProperty("m_PlayerIndex");
            if (m_PlayerIndexPropertyText == null)
                m_PlayerIndexPropertyText = EditorGUIUtility.TrTextContent("Player Index", playerIndexProperty.tooltip);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(playerIndexProperty, m_PlayerIndexPropertyText);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            // UI config section.
            var uiModuleProperty = serializedObject.FindProperty("m_UIInputModule");
            if (m_UIPropertyText == null)
                m_UIPropertyText = EditorGUIUtility.TrTextContent("UI Input Module", uiModuleProperty.tooltip);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(uiModuleProperty, m_UIPropertyText);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            if (uiModuleProperty.objectReferenceValue != null)
            {
                var uiModule = uiModuleProperty.objectReferenceValue as InputSystemUIInputModule;
                if (actionsProperty.objectReferenceValue != null && uiModule.actionsAsset != actionsProperty.objectReferenceValue)
                {
                    EditorGUILayout.HelpBox("The referenced InputSystemUIInputModule is configured using differnet input actions then this PlayerInput. They should match if you want to synchronize PlayerInput actions to the UI input.", MessageType.Warning);
                    if (GUILayout.Button(m_FixInputModuleText))
                        InputSystemUIInputModuleEditor.ReassignActions(uiModule, actionsProperty.objectReferenceValue as InputActionAsset);
                }
            }

            // Camera section.
            var cameraProperty = serializedObject.FindProperty("m_Camera");
            if (m_CameraPropertyText == null)
                m_CameraPropertyText = EditorGUIUtility.TrTextContent("Camera", cameraProperty.tooltip);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(cameraProperty, m_CameraPropertyText);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            // Notifications/event section.
            EditorGUI.BeginChangeCheck();
            var notificationsProperty = serializedObject.FindProperty("m_NotificationBehavior");
            EditorGUILayout.PropertyField(notificationsProperty, m_NotificationBehaviorText);
            if (EditorGUI.EndChangeCheck() || !m_NotificationBehaviorInitialized)
                OnNotificationBehaviorChange();
            switch ((PlayerNotifications)notificationsProperty.intValue)
            {
                case PlayerNotifications.SendMessages:
                case PlayerNotifications.BroadcastMessages:
                    Debug.Assert(m_SendMessagesHelpText != null);
                    EditorGUILayout.HelpBox(m_SendMessagesHelpText);
                    break;

                case PlayerNotifications.InvokeUnityEvents:
                    m_EventsGroupUnfolded = EditorGUILayout.Foldout(m_EventsGroupUnfolded, m_EventsGroupText);
                    if (m_EventsGroupUnfolded)
                    {
                        // Action events. Group by action map.
                        if (m_ActionNames != null)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                var actionEvents = serializedObject.FindProperty("m_ActionEvents");
                                for (var n = 0; n < m_NumActionMaps; ++n)
                                {
                                    m_ActionMapEventsUnfolded[n] = EditorGUILayout.Foldout(m_ActionMapEventsUnfolded[n],
                                        m_ActionMapNames[n]);
                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        if (m_ActionMapEventsUnfolded[n])
                                        {
                                            for (var i = 0; i < m_ActionNames.Length; ++i)
                                            {
                                                if (m_ActionMapIndices[i] != n)
                                                    continue;

                                                EditorGUILayout.PropertyField(actionEvents.GetArrayElementAtIndex(i), m_ActionNames[i]);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Misc events.
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DeviceLostEvent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DeviceRegainedEvent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ControlsChangedEvent"));
                    }
                    break;
            }

            // Miscellaneous buttons.
            DoUtilityButtonsUI();

            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            // Debug UI.
            if (EditorApplication.isPlaying)
                DoDebugUI();
        }

        private void DoHelpCreateAssetUI()
        {
            if (serializedObject.FindProperty("m_Actions").objectReferenceValue != null)
            {
                // All good. We already have an asset.
                return;
            }

            EditorGUILayout.HelpBox("There are no input actions associated with this input component yet. Click the button below to create "
                + "a new set of input actions or drag an existing input actions asset into the field above.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            if (GUILayout.Button(m_CreateActionsText, EditorStyles.miniButton, GUILayout.MaxWidth(120)))
            {
                // Request save file location.
                var defaultFileName = Application.productName;
                var fileName = EditorUtility.SaveFilePanel("Create Input Actions Asset", "Assets", defaultFileName,
                    InputActionAsset.Extension);

                ////TODO: take current Supported Devices into account when creating this

                // Create and import asset and open editor.
                if (!string.IsNullOrEmpty(fileName))
                {
                    if (!fileName.StartsWith(Application.dataPath))
                    {
                        Debug.LogError($"Path must be located in Assets/ folder (got: '{fileName}')");
                        EditorGUILayout.EndHorizontal();
                        return;
                    }

                    if (!fileName.EndsWith("." + InputActionAsset.Extension))
                        fileName += "." + InputActionAsset.Extension;

                    // Load default actions and update all GUIDs.
                    var defaultActionsText = File.ReadAllText(kDefaultInputActionsAssetPath);
                    var newActions = InputActionAsset.FromJson(defaultActionsText);
                    foreach (var map in newActions.actionMaps)
                    {
                        map.m_Id = Guid.NewGuid().ToString();
                        foreach (var action in map.actions)
                            action.m_Id = Guid.NewGuid().ToString();
                    }
                    newActions.name = Path.GetFileNameWithoutExtension(fileName);
                    var newActionsText = newActions.ToJson();

                    // Write it out and tell the asset DB to pick it up.
                    File.WriteAllText(fileName, newActionsText);
                    AssetDatabase.Refresh();

                    // Need to wait for import to happen. On next editor update, wire the asset
                    // into our PlayerInput component and bring up the action editor.
                    EditorApplication.delayCall +=
                        () =>
                    {
                        var relativePath = "Assets/" + fileName.Substring(Application.dataPath.Length + 1);

                        // Load imported object.
                        var importedObject = AssetDatabase.LoadAssetAtPath<InputActionAsset>(relativePath);

                        // Set it on the PlayerInput component.
                        var actionsProperty = serializedObject.FindProperty("m_Actions");
                        actionsProperty.objectReferenceValue = importedObject;
                        serializedObject.ApplyModifiedProperties();

                        // Open the asset.
                        AssetDatabase.OpenAsset(importedObject);
                    };
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();
        }

        private void DoUtilityButtonsUI()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(m_OpenSettingsText, EditorStyles.miniButton))
                InputSettingsProvider.Open();

            if (GUILayout.Button(m_OpenDebuggerText, EditorStyles.miniButton))
                InputDebuggerWindow.CreateOrShow();

            EditorGUILayout.EndHorizontal();
        }

        private void DoDebugUI()
        {
            var playerInput = (PlayerInput)target;

            if (!playerInput.user.valid)
                return;

            ////TODO: show actions when they happen

            var user = playerInput.user.index.ToString();
            var controlScheme = playerInput.user.controlScheme?.name;
            var devices = string.Join(", ", playerInput.user.pairedDevices);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(m_DebugText, EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("User", user);
            EditorGUILayout.LabelField("Control Scheme", controlScheme);
            EditorGUILayout.LabelField("Devices", devices);
            EditorGUI.EndDisabledGroup();
        }

        private void OnNotificationBehaviorChange()
        {
            Debug.Assert(m_ActionAssetInitialized);
            serializedObject.ApplyModifiedProperties();

            var notificationBehavior = (PlayerNotifications)serializedObject.FindProperty("m_NotificationBehavior").intValue;
            switch (notificationBehavior)
            {
                // Create text that lists all the messages sent by the component.
                case PlayerNotifications.BroadcastMessages:
                case PlayerNotifications.SendMessages:
                {
                    var builder = new StringBuilder();
                    builder.Append("Will ");
                    if (notificationBehavior == PlayerNotifications.BroadcastMessages)
                        builder.Append("BroadcastMessage()");
                    else
                        builder.Append("SendMessage()");
                    builder.Append(" to GameObject: ");
                    builder.Append(PlayerInput.DeviceLostMessage);
                    builder.Append(", ");
                    builder.Append(PlayerInput.DeviceRegainedMessage);
                    builder.Append(", ");
                    builder.Append(PlayerInput.ControlsChangedMessage);

                    var playerInput = (PlayerInput)target;
                    var asset = playerInput.m_Actions;
                    if (asset != null)
                    {
                        foreach (var action in asset)
                        {
                            builder.Append(", On");
                            builder.Append(CSharpCodeHelpers.MakeTypeName(action.name));
                        }
                    }

                    m_SendMessagesHelpText = new GUIContent(builder.ToString());
                    break;
                }

                case PlayerNotifications.InvokeUnityEvents:
                {
                    var playerInput = (PlayerInput)target;
                    if (playerInput.m_DeviceLostEvent == null)
                        playerInput.m_DeviceLostEvent = new PlayerInput.DeviceLostEvent();
                    if (playerInput.m_DeviceRegainedEvent == null)
                        playerInput.m_DeviceRegainedEvent = new PlayerInput.DeviceRegainedEvent();
                    if (playerInput.m_ControlsChangedEvent == null)
                        playerInput.m_ControlsChangedEvent = new PlayerInput.ControlsChangedEvent();
                    serializedObject.Update();

                    // Force action refresh.
                    m_ActionAssetInitialized = false;
                    Refresh();
                    break;
                }
            }

            m_NotificationBehaviorInitialized = true;
        }

        private void OnActionAssetChange()
        {
            serializedObject.ApplyModifiedProperties();
            m_ActionAssetInitialized = true;

            var playerInput = (PlayerInput)target;
            var asset = playerInput.actions;
            if (asset == null)
            {
                m_ControlSchemeOptions = null;
                m_ActionMapOptions = null;
                m_ActionNames = null;
                m_SelectedDefaultActionMap = -1;
                m_SelectedDefaultControlScheme = -1;
                return;
            }

            // If we're sending Unity events, read out the event list.
            if ((PlayerNotifications)serializedObject.FindProperty("m_NotificationBehavior").intValue ==
                PlayerNotifications.InvokeUnityEvents)
            {
                ////FIXME: this should preserve the same order that we have in the asset
                var newActionNames = new List<GUIContent>();
                var newActionEvents = new List<PlayerInput.ActionEvent>();
                var newActionMapIndices = new List<int>();

                m_NumActionMaps = 0;
                m_ActionMapNames = null;

                void AddEntry(InputAction action, PlayerInput.ActionEvent actionEvent)
                {
                    newActionNames.Add(new GUIContent(action.name));
                    newActionEvents.Add(actionEvent);

                    var actionMapIndex = asset.actionMaps.IndexOfReference(action.actionMap);
                    newActionMapIndices.Add(actionMapIndex);

                    if (actionMapIndex >= m_NumActionMaps)
                        m_NumActionMaps = actionMapIndex + 1;

                    ArrayHelpers.PutAtIfNotSet(ref m_ActionMapNames, actionMapIndex,
                        () => new GUIContent(action.actionMap.name));
                }

                ////REVIEW: this is destructive; we may be losing connections here that the user has set up
                ////        if the action goes missing

                // Bring over any action events that we already have and that are still in the asset.
                var oldActionEvents = playerInput.m_ActionEvents;
                if (oldActionEvents != null)
                {
                    foreach (var entry in oldActionEvents)
                    {
                        var guid = entry.actionId;
                        var action = asset.FindAction(guid);
                        if (action != null)
                            AddEntry(action, entry);
                    }
                }

                // Add any new actions.
                foreach (var action in asset)
                {
                    // Skip if it was already in there.
                    if (oldActionEvents != null && oldActionEvents.Any(x => x.actionId == action.id.ToString()))
                        continue;

                    ////FIXME: adds bindings to the name
                    AddEntry(action, new PlayerInput.ActionEvent(action.id, action.ToString()));
                }

                m_ActionNames = newActionNames.ToArray();
                m_ActionMapIndices = newActionMapIndices.ToArray();
                Array.Resize(ref m_ActionMapEventsUnfolded, m_NumActionMaps);
                playerInput.m_ActionEvents = newActionEvents.ToArray();
            }

            // Read out control schemes.
            var selectedDefaultControlScheme = playerInput.defaultControlScheme;
            m_SelectedDefaultControlScheme = 0;
            var controlSchemes = asset.controlSchemes;
            m_ControlSchemeOptions = new GUIContent[controlSchemes.Count + 1];
            m_ControlSchemeOptions[0] = new GUIContent(EditorGUIUtility.TrTextContent("<Any>"));
            ////TODO: sort alphabetically
            for (var i = 0; i < controlSchemes.Count; ++i)
            {
                var name = controlSchemes[i].name;
                m_ControlSchemeOptions[i + 1] = new GUIContent(name);

                if (selectedDefaultControlScheme != null && string.Compare(name, selectedDefaultControlScheme,
                    StringComparison.InvariantCultureIgnoreCase) == 0)
                    m_SelectedDefaultControlScheme = i + 1;
            }
            if (m_SelectedDefaultControlScheme <= 0)
                playerInput.defaultControlScheme = null;

            // Read out action maps.
            var selectedDefaultActionMap = !string.IsNullOrEmpty(playerInput.defaultActionMap)
                ? asset.FindActionMap(playerInput.defaultActionMap)
                : null;
            m_SelectedDefaultActionMap = asset.actionMaps.Count > 0 ? 1 : 0;
            var actionMaps = asset.actionMaps;
            m_ActionMapOptions = new GUIContent[actionMaps.Count + 1];
            m_ActionMapOptions[0] = new GUIContent(EditorGUIUtility.TrTextContent("<None>"));
            ////TODO: sort alphabetically
            for (var i = 0; i < actionMaps.Count; ++i)
            {
                var actionMap = actionMaps[i];
                m_ActionMapOptions[i + 1] = new GUIContent(actionMap.name);

                if (selectedDefaultActionMap != null && actionMap == selectedDefaultActionMap)
                    m_SelectedDefaultActionMap = i + 1;
            }
            if (m_SelectedDefaultActionMap <= 0)
                playerInput.defaultActionMap = null;
            else
                playerInput.defaultActionMap = m_ActionMapOptions[m_SelectedDefaultActionMap].text;

            serializedObject.Update();
        }

        [SerializeField] private bool m_EventsGroupUnfolded;
        [SerializeField] private bool[] m_ActionMapEventsUnfolded;

        [NonSerialized] private readonly GUIContent m_CreateActionsText = EditorGUIUtility.TrTextContent("Create Actions...");
        [NonSerialized] private readonly GUIContent m_FixInputModuleText = EditorGUIUtility.TrTextContent("Fix UI Input Module");
        [NonSerialized] private readonly GUIContent m_OpenSettingsText = EditorGUIUtility.TrTextContent("Open Input Settings");
        [NonSerialized] private readonly GUIContent m_OpenDebuggerText = EditorGUIUtility.TrTextContent("Open Input Debugger");
        [NonSerialized] private readonly GUIContent m_EventsGroupText =
            EditorGUIUtility.TrTextContent("Events", "UnityEvents triggered by the PlayerInput component");
        [NonSerialized] private readonly GUIContent m_NotificationBehaviorText =
            EditorGUIUtility.TrTextContent("Behavior",
                "Determine how notifications should be sent when an input-related event associated with the player happens.");
        [NonSerialized] private readonly GUIContent m_DefaultControlSchemeText =
            EditorGUIUtility.TrTextContent("Default Scheme", "Which control scheme to try by default. If not set, PlayerInput "
                + "will simply go through all control schemes in the action asset and try one after the other. If set, PlayerInput will try "
                + "the given scheme first but if using that fails (e.g. when not required devices are missing) will fall back to trying the other "
                + "control schemes in order.");
        [NonSerialized] private readonly GUIContent m_DefaultActionMapText =
            EditorGUIUtility.TrTextContent("Default Map", "Action map to enable by default. If not set, no actions will be enabled by default.");
        [NonSerialized] private readonly GUIContent m_AutoSwitchText =
            EditorGUIUtility.TrTextContent("Auto-Switch",
                "By default, when there is only a single PlayerInput, the player "
                + "is allowed to freely switch between control schemes simply by starting to use a different device. By toggling this property off, this "
                + "behavior is disabled and even with a single player, the player will stay locked onto the explicitly selected control scheme. Note "
                + "that you can still change control schemes explicitly through the PlayerInput API.\n\nWhen there are multiple PlayerInputs in the game, auto-switching is disabled automatically regardless of the value of this property.");
        [NonSerialized] private readonly GUIContent m_DebugText = EditorGUIUtility.TrTextContent("Debug");
        [NonSerialized] private GUIContent m_UIPropertyText;
        [NonSerialized] private GUIContent m_CameraPropertyText;
        [NonSerialized] private GUIContent m_SendMessagesHelpText;
        [NonSerialized] private GUIContent[] m_ActionNames;
        [NonSerialized] private GUIContent[] m_ActionMapNames;
        [NonSerialized] private int[] m_ActionMapIndices;
        [NonSerialized] private int m_NumActionMaps;
        [NonSerialized] private int m_SelectedDefaultControlScheme;
        [NonSerialized] private GUIContent[] m_ControlSchemeOptions;
        [NonSerialized] private int m_SelectedDefaultActionMap;
        [NonSerialized] private GUIContent[] m_ActionMapOptions;

        [NonSerialized] private bool m_NotificationBehaviorInitialized;
        [NonSerialized] private bool m_ActionAssetInitialized;
    }
}
#endif // UNITY_EDITOR
