UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var issues=new System.Collections.Generic.List<string>();int icons=0,refs=0,nested=0;
foreach(var id in UnityEditor.AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Sprites/Items"})){
 var p=UnityEditor.AssetDatabase.GUIDToAssetPath(id);var s=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(p);var i=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(p);
 if(s==null){issues.Add(p+" missing Sprite");continue;}icons++;
 if(s.texture.width<128||s.texture.height<128)issues.Add(p+" low resolution");
 if(i.filterMode!=UnityEngine.FilterMode.Point||i.mipmapEnabled)issues.Add(p+" filtering");
}
foreach(var id in UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition")){
 var item=UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectMayham.Items.ItemDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(id));
 if(item!=null&&item.Icon!=null&&UnityEditor.AssetDatabase.GetAssetPath(item.Icon).StartsWith("Assets/Sprites/Items/"))refs++;
}
foreach(var id in UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Rooms"})){
 var p=UnityEditor.AssetDatabase.GUIDToAssetPath(id);var root=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
 foreach(var sr in root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true)){
  if(sr.sprite==null)continue;var sp=UnityEditor.AssetDatabase.GetAssetPath(sr.sprite);if(!sp.Contains("/Office Furniture/"))continue;
  float expected=ProjectMayham.EditorTools.FurnitureScalePolicy.Multiplier(System.IO.Path.GetFileNameWithoutExtension(sp));if(UnityEngine.Mathf.Approximately(expected,1))continue;
  var source=UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(sr);
  if(source!=null&&!new UnityEditor.SerializedObject(sr.transform).FindProperty("m_LocalScale").prefabOverride){
   nested++;if(sr.transform.localScale!=source.transform.localScale)issues.Add(p+" inherited size "+sr.name);
  }
 }
}
var builder=typeof(ProjectMayham.EditorTools.OfficeMazeBuilder).GetNestedType("Builder",System.Reflection.BindingFlags.NonPublic);
bool footprint=builder.GetMethod("Footprint",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!=null;
if(!footprint)issues.Add("Scaled footprint method not compiled");
var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var result="Icons="+icons+"; ItemDefinition references="+refs+"; inherited room props="+nested+"; scaled generator footprint="+footprint+"; active="+active.path+"; dirty="+active.isDirty+"; issues="+issues.Count+"\n"+string.Join("\n",issues);
System.IO.File.WriteAllText("Docs/Items/validation.txt",result);return result;


