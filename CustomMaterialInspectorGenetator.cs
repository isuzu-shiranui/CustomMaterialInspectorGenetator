using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IsuzuEditorExtension
{
    public class CustomMaterialInspectorGenetator : EditorWindow
    {
        private UnityEngine.Object shaderFile;
        private UnityEngine.Object templateFile;

        private string className = "";

        [MenuItem("IsuzuEditorExtension/GenerateShaderInspector")]
        private static void Create()
        {
            GetWindow<CustomMaterialInspectorGenetator>("PropGen");
        }

        private void OnGUI()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.VerticalScope())
                {
                    this.shaderFile = EditorGUILayout.ObjectField(
                        new GUIContent("Shader File"),
                        shaderFile,
                        typeof(UnityEngine.Object),
                        false);

                    if(shaderFile != null) EditorGUILayout.LabelField("Path:" + Application.dataPath.Replace("Assets", string.Empty) + AssetDatabase.GetAssetPath(shaderFile));

                    GUILayout.Space(5);

                    this.templateFile = EditorGUILayout.ObjectField(
                        new GUIContent("Template File"),
                        templateFile,
                        typeof(UnityEngine.Object),
                        false);

                    if(templateFile != null) EditorGUILayout.LabelField("Path:" + Application.dataPath.Replace("Assets", string.Empty) + AssetDatabase.GetAssetPath(templateFile));

                    GUILayout.Space(5);

                    this.className = EditorGUILayout.TextField(new GUIContent("Class Name(File Name)"), className);

                }

                GUILayout.FlexibleSpace();
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("OK"))
                {
                    this.Generate();
                    this.Close();
                }

                if (GUILayout.Button("Cancel"))
                {
                    this.Close();
                }
            }
        }

        private void Generate()
        {
            var file = File.ReadAllText(Application.dataPath.Replace("Assets", string.Empty) + AssetDatabase.GetAssetPath(shaderFile));

            var regex = new Regex("(?<=(properties)).*?(?=(subshader))", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var match = regex.Match(file).Value;

            if(string.IsNullOrEmpty(match)) return;

            var lines = match
                .Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim().Trim('\t'))
                .Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("/") && x.Length > 4);

            var attrebutes = new List<ShaderPropertyAttrebute>();

            var attrebuteTemp = new List<ShaderPropertyAttrebute>();

            foreach (var line in lines)
            {
                var attrebute = ShaderPropertyAttrebute.ParseToAttrebute(line);

                if(string.IsNullOrEmpty(attrebute.Name))
                {
                    attrebuteTemp.Add(attrebute);
                }
                else
                {
                    attrebuteTemp.Select(x =>
                    {
                        return x.GetType().GetProperties()
                            .Where(y => !y.Name.Contains("Name"))
                            .Select(y =>
                            {
                                var value = y.GetValue(x, null);
                                if (Equals(value, null) || Equals(value, false)) return y;

                                attrebute.GetType().GetProperties().First(z => z.Name == y.Name).SetValue(attrebute, value, null);
                                return y;
                            })
                        .ToList();
                    })
                    .ToList();
                    attrebutes.Add(attrebute);
                    attrebuteTemp.Clear();
                }
            }

            var predicate = new Func<ShaderPropertyAttrebute, bool>(x => !x.HasHideInInspector && !x.HasPerRendererData);

            var maxVariableLength = attrebutes
                .Where(predicate)
                .Max(x => x.Name.Length);

            var fields = attrebutes
                .Where(predicate)
                .Select(x =>
                {
                    var camelVariable = x.Name.ToCamelCase();
                    return string.Format
                    (
                        "private string {0}{1}= \"_{2}\"; ",
                        camelVariable,
                        "".AppendIndent(maxVariableLength - camelVariable.Length),
                        x.Name
                    ).AppendIndent(4);
                }).ToList();

            var foldFields = attrebutes
                .Where(x => x.HasHeader)
                .Select(x => string.Format("private bool {0}Fold = false;", x.Name.ToCamelCase()).AppendIndent(4))
                .ToList();

            var headers = attrebutes.Where(x => x.HasHeader).Select(x => x.Header);

            var toggleFields = attrebutes
                .Where(x => x.Name.ToLower().Contains("use") && headers.Any(y => y.ToLower().Contains(x.Name.ToLower().Replace("use", string.Empty))))
                .Select(x =>
                {
                    var camelVariable = x.Name.ToCamelCase();
                    return new
                    {
                        Field = string.Format("private bool {0}Toggle = true;", camelVariable).AppendIndent(4),
                        Variable = string.Format("{0}Toggle", camelVariable),
                        PropertyName = x.Name
                    };
                })
                .ToList();

            fields.AddRange(foldFields);
            fields.AddRange(toggleFields.Select(x => x.Field));

            var template = File.ReadAllText(Application.dataPath.Replace("Assets", string.Empty) + AssetDatabase.GetAssetPath(templateFile));

            var fieldsArray = fields.ToArray();

            template = template.Replace("<#fields#>", string.Join(Environment.NewLine, fieldsArray));

            var inFold = false;
            var propertyFoldGroup = string.Empty;
            var properties = new List<string>();

            var attrebuteProps = attrebutes
                .Where(predicate)
                .Select(x =>
                {
                    var camelVariable = x.Name.ToCamelCase();
                    return new
                    {
                        Attrebute = x,
                        FindProperty = string.Format
                        (
                            "var {0}Prop {1}= FindProperty(this.{0}, properties);",
                            camelVariable,
                            "".AppendIndent(maxVariableLength - camelVariable.Length)
                        ).AppendIndent(8),
                        PropertyVariable = string.Format("{0}Prop", camelVariable)
                    };
                })
                .ToList();

            var findProps = string.Join(Environment.NewLine, attrebuteProps.Select(x => x.FindProperty).ToArray());
            template = template.Replace("<#findProps#>", findProps);

            var setKeywords = toggleFields
                .Select(x => string.Format("UiUtils.SetKeyword({0}Prop, this.{1});", x.PropertyName.ToCamelCase(), x.Variable).AppendIndent(12))
                .ToArray();
            template = template.Replace("<#setKeywords#>", string.Join(Environment.NewLine, setKeywords));

            var initializeToggles = toggleFields
                .Select(x => string.Format("this.{0} = {1}Prop.floatValue > 0;", x.Variable, x.PropertyName.ToCamelCase()).AppendIndent(16))
                .ToArray();
            template = template.Replace("<#initialToggle#>", string.Join(Environment.NewLine, initializeToggles));

            foreach (var toggleField in toggleFields)
            {
                attrebuteProps.RemoveAll(x => x.Attrebute.Name.Contains(toggleField.PropertyName));
            }

            foreach (var attrebute in attrebuteProps)
            {
                if (attrebute.Attrebute.HasHeader)
                {
                    if (inFold)
                    {
                        propertyFoldGroup += string.Format("}});{0}{0}", Environment.NewLine).AppendIndent(12);
                    }

                    var toggle = toggleFields.FirstOrDefault(x => attrebute.Attrebute.Header.ToLower().Contains(x.PropertyName.ToLower().Replace("use", string.Empty)));
                    if(toggle == null)
                    {
                        propertyFoldGroup += string.Format
                        (
                            "UiUtils.PropertyFoldGroup(\"{0}\", ref this.{1}Fold, () => {2}",
                            attrebute.Attrebute.Header,
                            attrebute.Attrebute.Name.ToCamelCase(),
                            Environment.NewLine
                        ).AppendIndent(12);
                    }
                    else
                    {
                        propertyFoldGroup += string.Format
                        (
                            "UiUtils.PropertyToggleFoldGroup(\"{0}\", ref this.{1}Fold, ref this.{2}, () => {3}",
                            attrebute.Attrebute.Header,
                            attrebute.Attrebute.Name.ToCamelCase(),
                            toggle.Variable,
                            Environment.NewLine
                        ).AppendIndent(12);
                    }

                    propertyFoldGroup += string.Format("{{{0}", Environment.NewLine).AppendIndent(12);
                    inFold = true;
                }

                if (attrebute.Attrebute.HasNoScaleOffset)
                {
                    propertyFoldGroup += string.Format
                    (
                        "materialEditor.TexturePropertySingleLine(new GUIContent(\"{0}\"), {1}); ",
                        attrebute.Attrebute.DisplayName,
                        attrebute.PropertyVariable
                    ).AppendIndent(16) + Environment.NewLine;
                }
                else if(attrebute.Attrebute.IsTexture)
                {
                    propertyFoldGroup += string.Format
                    (
                        "materialEditor.ShaderProperty({0}, \"{1}\");",
                        attrebute.PropertyVariable,
                        attrebute.Attrebute.DisplayName
                    ).AppendIndent(16) + Environment.NewLine;
                }
                else
                {
                    propertyFoldGroup += string.Format
                    (
                        "materialEditor.ShaderProperty({0}, \"{1}\");",
                        attrebute.PropertyVariable,
                        attrebute.Attrebute.DisplayName
                    ).AppendIndent(16) + Environment.NewLine;
                }
            }

            if (inFold)
            {
                propertyFoldGroup += string.Format("}});{0}{0}", Environment.NewLine).AppendIndent(12);
            }

            template = template.Replace("<#propGroup#>", propertyFoldGroup);

            template = template.Replace("<#=generatorName#>", "ShaderPropertyGenerator");
            template = template.Replace("<#=dateTimeNow#>", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            template = template.Replace("<#=className#>", className.ToPascalCase());
            template = template.Replace("<#=guid#>", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shaderFile)));

            Directory.CreateDirectory(Application.dataPath.Replace("Assets", string.Empty) + "/Editor/");

            File.WriteAllText(Application.dataPath + "/Editor/" + className.ToPascalCase() + ".cs", template);

            AssetDatabase.Refresh();
        }
    }

    public static class StringExtension
    {
        public static string ToPascalCase(this string str)
        {
            return char.ToUpper(str[0]) + str.Substring(1);
        }

        public static string ToCamelCase(this string str)
        {
            return char.ToLower(str[0]) + str.Substring(1);
        }

        public static string AppendIndent(this string str, int indent)
        {
            return indent > 0 ? Enumerable.Range(0, indent).Select(_ => " ").Aggregate((now, next) => now + next) + str : str;
        }
    }

    public class ShaderPropertyAttrebute
    {
        private static readonly Regex AttrebutesRegex = new Regex(@"(\[.*?\])");
        private static readonly Regex NamesRegex = new Regex(@"_(?<name>.*?)\(.*?""(?<displayName>.*?)""");
        private static readonly Regex HeaderRegex = new Regex(@"(?<=\().*?(?=(\)(\s.*)?\]))");
        private static readonly Regex ContentRegex = new Regex(@"(?<=\().*?"".*?(?=\))");

        #region Properies

        public bool HasHideInInspector { get; set; }

        public bool HasNoScaleOffset { get; set; }

        public bool HasNormal { get; set; }

        public bool HasHDR { get; set; }

        public bool HasGamma { get; set; }

        public bool HasPerRendererData { get; set; }

        public bool HasToggle { get; set; }

        public bool HasEnum { get; set; }

        //public bool HasKeyWordEnum { get; set; }

        public bool HasPowerSlider { get; set; }

        public bool HasHeader { get; set; }

        public bool IsTexture { get; set; }

        public string Header { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        #endregion

        public static ShaderPropertyAttrebute ParseToAttrebute(string line)
        {
            var result = new ShaderPropertyAttrebute();

            var matches = AttrebutesRegex.Matches(line).Cast<Match>().Select(x => x.Value);

            result.HasHideInInspector = matches.Any(x => x.ToLower().Contains("hideininspector"));
            result.HasNoScaleOffset = matches.Any(x => x.ToLower().Contains("noscaleoffset"));
            result.HasNormal = matches.Any(x => x.ToLower().Contains("normal"));
            result.HasHDR = matches.Any(x => x.ToLower().Contains("hdr"));
            result.HasGamma = matches.Any(x => x.ToLower().Contains("gamma"));
            result.HasPerRendererData = matches.Any(x => x.ToLower().Contains("perrenderdata"));
            result.HasToggle = matches.Any(x => x.ToLower().Contains("toggle"));
            result.HasEnum = matches.Any(x => x.ToLower().Contains("enum"));
            result.HasPowerSlider = matches.Any(x => x.ToLower().Contains("powerslider"));
            result.HasHeader = matches.Any(x => x.ToLower().Contains("header"));

            if (result.HasHeader)
            {
                var headerAtter = matches.First(x => x.ToLower().Contains("header"));
                result.Header = HeaderRegex.Match(headerAtter).Value;
            }

            line = AttrebutesRegex.Replace(line, string.Empty);

            var content = ContentRegex.Match(line).Value.Split(',');

            result.IsTexture = content.Any(x => x.Contains("2D") || x.Contains("Cube") || x.Contains("3D"));

            var names = NamesRegex.Matches(line)
                .Cast<Match>()
                .Select(x => x.Groups.Cast<Group>())
                .SelectMany(x => x)
                .Skip(1);

            if (names.Any())
            {
                result.Name = names.FirstOrDefault().Value.Trim();
                result.DisplayName = names.LastOrDefault().Value.Trim();
            }

            return result;
        }
    }
}
