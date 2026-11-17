if(UnityEditor.EditorApplication.isPlaying) return "Stop Play mode before applying furniture sizes.";
string reportPath="Docs/FurnitureScale/applied.txt";
if(System.IO.File.Exists(reportPath)) return "Furniture scaling already applied; see "+reportPath;
var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var shelterScales=new System.Collections.Generic.Dictionary<int,UnityEngine.Vector3>();
foreach(var sr in UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
 if(sr.gameObject.scene.path=="Assets/Scenes/Shelter.unity") shelterScales[sr.GetInstanceID()]=sr.transform.localScale;
var report=new System.Collections.Generic.List<string>();
var changed=new System.Collections.Generic.HashSet<string>();
var all=UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"});
System.Action<string> backup=path=>{var dest="Docs/ArtBackups/FurnitureScale-2026-10-30/"+path;System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));if(!System.IO.File.Exists(dest))System.IO.File.Copy(path,dest);};
int objects=0,composites=0,sceneObjects=0,scenes=0;
foreach(var id in all){
 var p=UnityEditor.AssetDatabase.GUIDToAssetPath(id);
 var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
 var sr=asset.GetComponent<UnityEngine.SpriteRenderer>();
 if(sr==null||sr.sprite==null)continue;
 var sp=UnityEditor.AssetDatabase.GetAssetPath(sr.sprite);
 if(!sp.Contains("/Office Furniture/"))continue;
 float factor=ProjectMayham.EditorTools.FurnitureScalePolicy.Multiplier(System.IO.Path.GetFileNameWithoutExtension(sp));
 if(UnityEngine.Mathf.Approximately(factor,1))continue;
 backup(p);
 var root=UnityEditor.PrefabUtility.LoadPrefabContents(p);
 try{var old=root.transform.localScale;root.transform.localScale=old*factor;UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,p);report.Add(p+" | "+old+" -> "+root.transform.localScale);changed.Add(p);objects++;}
 finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
UnityEditor.AssetDatabase.SaveAssets();
foreach(var id in all){
 var p=UnityEditor.AssetDatabase.GUIDToAssetPath(id);if(changed.Contains(p))continue;
 bool depends=false;foreach(var d in UnityEditor.AssetDatabase.GetDependencies(p))if(changed.Contains(d)){depends=true;break;}if(!depends)continue;
 var root=UnityEditor.PrefabUtility.LoadPrefabContents(p);bool edited=false;
 try{
  foreach(var sr in root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true)){
   if(sr.sprite==null)continue;var sp=UnityEditor.AssetDatabase.GetAssetPath(sr.sprite);if(!sp.Contains("/Office Furniture/"))continue;
   float f=ProjectMayham.EditorTools.FurnitureScalePolicy.Multiplier(System.IO.Path.GetFileNameWithoutExtension(sp));if(UnityEngine.Mathf.Approximately(f,1))continue;
   var prop=new UnityEditor.SerializedObject(sr.transform).FindProperty("m_LocalScale");
   if(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(sr)==null||prop.prefabOverride){
    if(!edited)backup(p);sr.transform.localScale*=f;UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(sr.transform);edited=true;objects++;
   }
  }
  if(edited){UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,p);composites++;report.Add(p+" | scaled explicit overrides");}
 }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
foreach(var id in UnityEditor.AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"})){
 var p=UnityEditor.AssetDatabase.GUIDToAssetPath(id);if(p=="Assets/Scenes/Shelter.unity")continue;
 var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(p);bool wasOpen=scene.IsValid()&&scene.isLoaded;
 if(!wasOpen)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(p,UnityEditor.SceneManagement.OpenSceneMode.Additive);
 bool edited=false;
 try{
  foreach(var root in scene.GetRootGameObjects())foreach(var sr in root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true)){
   if(sr.sprite==null)continue;var sp=UnityEditor.AssetDatabase.GetAssetPath(sr.sprite);if(!sp.Contains("/Office Furniture/"))continue;
   float f=ProjectMayham.EditorTools.FurnitureScalePolicy.Multiplier(System.IO.Path.GetFileNameWithoutExtension(sp));if(UnityEngine.Mathf.Approximately(f,1))continue;
   var source=UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(sr);
   var prop=new UnityEditor.SerializedObject(sr.transform).FindProperty("m_LocalScale");
   if(source!=null&&!prop.prefabOverride)continue;
   if(!edited)backup(p);UnityEditor.Undo.RecordObject(sr.transform,"Match Shelter furniture size");sr.transform.localScale*=f;
   if(source!=null)UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(sr.transform);
   edited=true;sceneObjects++;
  }
  if(edited){UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);scenes++;report.Add(p+" | updated direct objects and scale overrides");}
 }finally{if(!wasOpen)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
int shelterChanges=0;
foreach(var sr in UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
 if(shelterScales.TryGetValue(sr.GetInstanceID(),out var before)&&sr.transform.localScale!=before)shelterChanges++;
report.Insert(0,"Base prefabs="+changed.Count+"; composite prefabs="+composites+"; prefab transforms="+objects+"; scenes="+scenes+"; direct scene transforms="+sceneObjects+"; Shelter scale changes="+shelterChanges);
System.IO.Directory.CreateDirectory("Docs/FurnitureScale");System.IO.File.WriteAllLines(reportPath,report);
return report[0];

