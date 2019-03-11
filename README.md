# CustomMaterialInspectorGenetator

## 使い方
1. リポジトリをクローンするかダウンロードしてください。
2. Unityを開いて、ダウンロードしたものをインポートしてください。
3. メニューを開いてください。(Toolbar/IsuzuEditorExtension/GenerateShaderInspector)にあります。
4. ShaderFileに対象の.shaderファイルを選択してください。
5. TemplateFileに、Templateフォルダ内のファイルを選択してください。
6. クラス名を入力してください。ファイル名になるのでお好きなように。その後、OKボタンを押します。
7. 出力されるファイルは、UiUlils.csファイルが必須ですので、同じところに出力してください。
8 シェーダーファイルを編集します。詳しくは[こちら](https://docs.unity3d.com/ja/current/Manual/SL-CustomEditor.html)にあります。
nameの部分は6でつけたクラス名になります。

## How to use.

1. Clone or download this repository.
2. Open Unity. and import download files.(UiUtils.cs, CustomMaterialInspectorGenetator.cs and "Template" folder)
3. Open menu. (Toolbar/IsuzuEditorExtension/GenerateShaderInspector)
4. Select shader file. but .shader file only.
5. Select template file. usualy select default .ct template file in Template folder.
6. Enter class name. and click "OK" button.
7. Please note that the output file is dependent on Uiutils.cs.Put on the same folder hierarchy.
8. Edit shader file. [more](https://docs.unity3d.com/ja/current/Manual/SL-CustomEditor.html) "name" is class name given in the 6th step.
