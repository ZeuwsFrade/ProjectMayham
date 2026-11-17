var report=new System.Collections.Generic.List<string>();
var tyler=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Sprites/Npcs/Tyler.png");
if(tyler==null)throw new System.Exception("Tyler sprite not imported.");
foreach(var path in new[]{"Assets/Resources/Npcs/Tyler.asset","Assets/Data/Traders/Tyler.asset"}){
 var asset=UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);var so=new UnityEditor.SerializedObject(asset);
 so.FindProperty("portrait").objectReferenceValue=tyler;so.ApplyModifiedPropertiesWithoutUndo();UnityEditor.EditorUtility.SetDirty(asset);report.Add("PORTRAIT|"+path);
}
int sceneChanges=0;
var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
foreach(var path in new[]{"Assets/Scenes/Shelter.unity","Assets/Scenes/Tests/ShelterTest.unity"}){
 var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool wasOpen=scene.IsValid()&&scene.isLoaded;
 if(!wasOpen)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
 bool changed=false;
 try{
  foreach(var root in scene.GetRootGameObjects())foreach(var sr in root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true)){
   bool match=false;for(var t=sr.transform;t!=null;t=t.parent)if(t.name=="Tyler"){match=true;break;}
   if(!match)continue;
   UnityEditor.Undo.RecordObject(sr,"Assign Tyler neutral sprite");sr.sprite=tyler;sr.color=UnityEngine.Color.white;
   if(UnityEditor.PrefabUtility.IsPartOfPrefabInstance(sr))UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
   changed=true;sceneChanges++;
  }
  if(changed){UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);report.Add("SCENE|"+path);}
 }finally{if(!wasOpen)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
int barricades=0;
foreach(var id in UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Barricades"})){
 var path=UnityEditor.AssetDatabase.GUIDToAssetPath(id);var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
 try{
  var box=root.GetComponent<UnityEngine.BoxCollider2D>();var body=root.GetComponent<UnityEngine.Rigidbody2D>();
  var size=box.size;var offset=box.offset;float mass=body.mass,damping=body.linearDamping;var constraints=body.constraints;
  ProjectMayham.EditorTools.BarricadeVisualFitter.Fit(root);
  var b=ProjectMayham.EditorTools.BarricadeVisualFitter.VisualBounds(root);
  if(UnityEngine.Mathf.Abs(b.size.x-size.x)>0.001f||UnityEngine.Mathf.Abs(b.size.y-size.y)>0.001f)throw new System.Exception("Visual size mismatch "+path+" "+b.size);
  if(box.size!=size||box.offset!=offset||body.mass!=mass||body.linearDamping!=damping||body.constraints!=constraints)throw new System.Exception("Barricade physics changed");
  UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,path);barricades++;
  report.Add("BARRICADE|"+path+"|visual="+b.size+"|collider="+size+"|mass="+mass+"|damping="+damping);
 }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
UnityEditor.AssetDatabase.SaveAssets();
report.Insert(0,"Tyler scene renderers="+sceneChanges+"; fitted barricade prefabs="+barricades);
System.IO.File.WriteAllLines("Docs/ArtRefresh-2026-10-30/applied.txt",report);return string.Join("\n",report);

