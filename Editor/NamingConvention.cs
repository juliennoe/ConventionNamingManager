using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace JulienNoe.Tools.NamingConvention
{
    public class NamingConvention : EditorWindow
    {
        // Class representing a single naming rule configuration
        [Serializable]
        private class NamingRule
        {
            public string objectType;
            public string namingStyle;
            public bool useCustomPrefix;
            public string customPrefix;
        }

        // Wrapper used to serialize/deserialize rules from EditorPrefs
        [Serializable]
        private class RuleListWrapper
        {
            public List<NamingRule> rules;
        }

        // UI state and rules
        private List<NamingRule> namingRules = new();
        private Vector2 scroll;
        private Vector2 resultScroll;
        private Dictionary<string, (string expectedName, string type)> invalidPaths = new();

        // UI toggles
        private bool showInvalidList = true;
        private bool showHelp = false;

        private const string PREFS_KEY = "NamingConventionToolRules";

        // Supported object types
        private readonly string[] objectTypes = new[] {
            "Folder", "Animation Clip", "Audio Clip", "Audio Mixer", "Compute Shader", "Font", "GUI Skin",
            "Material", "Mesh", "Model", "Physic Material", "Prefab", "Scene", "Script", "Scriptable Object",
            "Shader", "Sprite", "Texture", "Video Clip", "Visual Effect Asset"
        };

        // Supported naming styles
        private readonly string[] namingStyles = new[]
        {
            "PascalCase", "camelCase", "snake_case", "kebab-case", "UPPER_CASE", "lowercase"
        };

        // Menu to open the tool
        [MenuItem("Tools/Naming Convention Enforcer")]
        public static void ShowWindow()
        {
            var window = GetWindow<NamingConvention>("Naming Convention Enforcer", true);
            window.minSize = new Vector2(600, 400);
            window.LoadRules();
        }

        private void OnEnable() => LoadRules();
        private void OnDisable() => SaveRules();

        private void OnGUI()
        {
            GUILayout.Label("Naming Rules", EditorStyles.boldLabel);

            // Help dropdown
            showHelp = EditorGUILayout.Foldout(showHelp, "Help", true);
            if (showHelp)
            {
                EditorGUILayout.HelpBox(
                    "Use this tool to define naming rules by asset type.\n\n" +
                    "1. Add a new rule.\n" +
                    "2. Choose a naming style (PascalCase, snake_case, etc).\n" +
                    "3. Optional: Add a prefix.\n" +
                    "4. Scan the project.\n" +
                    "5. Harmonize names in one click.", MessageType.Info);
            }

            GUILayout.Space(10);

            // Add rule button (orange)
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("+ Add Rule"))
            {
                namingRules.Add(new NamingRule
                {
                    objectType = objectTypes[0],
                    namingStyle = namingStyles[0],
                    useCustomPrefix = false,
                    customPrefix = ""
                });
                SaveRules();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // Rule list scroll view
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < namingRules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                namingRules[i].objectType = objectTypes[
                    EditorGUILayout.Popup("Type", Array.IndexOf(objectTypes, namingRules[i].objectType), objectTypes)
                ];
                namingRules[i].namingStyle = namingStyles[
                    EditorGUILayout.Popup("Style", Array.IndexOf(namingStyles, namingRules[i].namingStyle), namingStyles)
                ];
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    namingRules.RemoveAt(i);
                    SaveRules();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                namingRules[i].useCustomPrefix = EditorGUILayout.Toggle("Use Prefix", namingRules[i].useCustomPrefix);
                if (namingRules[i].useCustomPrefix)
                {
                    namingRules[i].customPrefix = EditorGUILayout.TextField("Prefix", namingRules[i].customPrefix);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // Scan Project button (cyan)
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Scan Project", GUILayout.Height(30)))
            {
                ScanProject();
            }
            GUI.backgroundColor = Color.white;

            // Harmonize button (green)
            if (invalidPaths.Count > 0)
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Harmonize All", GUILayout.Height(30)))
                {
                    HarmonizeAll();
                }
                GUI.backgroundColor = Color.white;
            }

            // Results list
            if (invalidPaths.Count > 0)
            {
                GUILayout.Space(10);
                showInvalidList = EditorGUILayout.Foldout(showInvalidList, "Non-Conforming Elements", true);
                if (showInvalidList)
                {
                    resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.Height(250));
                    foreach (var kvp in invalidPaths)
                    {
                        EditorGUILayout.BeginHorizontal("box");
                        GUILayout.Label($"{kvp.Key}\nType: {kvp.Value.type} | Expected: {kvp.Value.expectedName}");
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(kvp.Key);
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        // Save current rules
        private void SaveRules()
        {
            string json = JsonUtility.ToJson(new RuleListWrapper { rules = namingRules });
            EditorPrefs.SetString(PREFS_KEY, json);
        }

        // Load saved rules
        private void LoadRules()
        {
            if (EditorPrefs.HasKey(PREFS_KEY))
            {
                string json = EditorPrefs.GetString(PREFS_KEY);
                var wrapper = JsonUtility.FromJson<RuleListWrapper>(json);
                if (wrapper?.rules != null)
                    namingRules = wrapper.rules;
            }
        }

        // Scans project and finds invalid names
        private void ScanProject()
        {
            invalidPaths.Clear();
            string[] allAssets = AssetDatabase.GetAllAssetPaths();

            foreach (string path in allAssets)
            {
                if (!path.StartsWith("Assets")) continue;

                foreach (var rule in namingRules)
                {
                    if (MatchesObjectType(path, rule.objectType))
                    {
                        string name = Path.GetFileName(path);
                        string expected = GetExpectedName(name, rule);
                        if (name != expected)
                        {
                            invalidPaths[path] = (expected, rule.objectType);
                        }
                    }
                }
            }
        }

        // Applies correct naming to invalid files
        private void HarmonizeAll()
        {
            foreach (var kvp in invalidPaths)
            {
                string newName = kvp.Value.expectedName;
                AssetDatabase.RenameAsset(kvp.Key, Path.GetFileNameWithoutExtension(newName));
            }
            AssetDatabase.Refresh();
            ScanProject();
        }

        // Checks if file matches rule object type
        private bool MatchesObjectType(string path, string type)
        {
            if (type == "Folder") return Directory.Exists(path);
            if (!File.Exists(path)) return false;

            string ext = Path.GetExtension(path).ToLower();
            return type switch
            {
                "Animation Clip" => ext == ".anim",
                "Audio Clip" => ext == ".mp3" || ext == ".wav" || ext == ".ogg",
                "Audio Mixer" => ext == ".mixer",
                "Compute Shader" => ext == ".compute",
                "Font" => ext == ".ttf" || ext == ".otf",
                "GUI Skin" => ext == ".guiskin",
                "Material" => ext == ".mat",
                "Mesh" => ext == ".mesh",
                "Model" => ext == ".fbx" || ext == ".obj",
                "Physic Material" => ext == ".physicMaterial",
                "Prefab" => ext == ".prefab",
                "Scene" => ext == ".unity",
                "Script" => ext == ".cs",
                "Scriptable Object" => ext == ".asset",
                "Shader" => ext == ".shader",
                "Sprite" => ext == ".png" || ext == ".jpg",
                "Texture" => ext == ".tga" || ext == ".psd" || ext == ".exr" || ext == ".hdr",
                "Video Clip" => ext == ".mp4" || ext == ".mov",
                "Visual Effect Asset" => ext == ".vfx",
                _ => false
            };
        }

        // Generates the expected filename based on style and prefix
        private string GetExpectedName(string originalName, NamingRule rule)
        {
            string name = Path.GetFileNameWithoutExtension(originalName);
            string ext = Path.GetExtension(originalName);

            string transformed = rule.namingStyle switch
            {
                "PascalCase" => Regex.Replace(name, "(?:^|_)([a-z])", m => m.Groups[1].Value.ToUpper()),
                "camelCase" => Char.ToLowerInvariant(name[0]) + Regex.Replace(name[1..], "(?:_|-)([a-z])", m => m.Groups[1].Value.ToUpper()),
                "snake_case" => Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToLower(),
                "kebab-case" => Regex.Replace(name, "([a-z])([A-Z])", "$1-$2").ToLower(),
                "UPPER_CASE" => Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToUpper(),
                "lowercase" => name.ToLower(),
                _ => name
            };

            if (rule.useCustomPrefix)
                transformed = rule.customPrefix + transformed;

            return transformed + ext;
        }
    }
}
