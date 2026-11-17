UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var issues=new System.Collections.Generic.List<string>();int sprites=0;
foreach(var row in System.IO.File.ReadAllLines("Docs/ArtRefresh-2026-10-30/expected.tsv")){
 var p=row.Split('|');var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(p[0]);
 if(sprite==null){issues.Add("Missing sprite "+p[0]);continue;}sprites++;
 var inv=System.Globalization.CultureInfo.InvariantCulture;
 if(sprite.rect.width!=int.Parse(p[1])||sprite.rect.height!=int.Parse(p[2])||sprite.pixelsPerUnit!=float.Parse(p[3],inv))issues.Add("Canvas or PPU "+p[0]+" rect="+sprite.rect+" ppu="+sprite.pixelsPerUnit);
 var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(p[0]);
 if(importer.filterMode!=UnityEngine.FilterMode.Point||importer.mipmapEnabled)issues.Add("Filtering "+p[0]);
}
var tyler=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Sprites/Npcs/Tyler.png");
var player=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Player.prefab").GetComponentInChildren<UnityEngine.SpriteRenderer>().sprite;
if(player==tyler)issues.Add("Tyler still shares player sprite");
foreach(var path in new[]{"Assets/Resources/Npcs/Tyler.asset","Assets/Data/Traders/Tyler.asset"})
 if(new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadMainAssetAtPath(path)).FindProperty("portrait").objectReferenceValue!=tyler)issues.Add("Wrong Tyler portrait "+path);
var carry=UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectMayham.Items.ItemDefinition>("Assets/Data/Items/Npcs/Intern.asset");
if(carry.HeldSprite==null||!UnityEditor.AssetDatabase.GetAssetPath(carry.HeldSprite).EndsWith("/Intern-Carried.png"))issues.Add("Intern carried reference");
int barricades=0;float maxDelta=0;
var draw=new System.Collections.Generic.List<string>();
System.Func<float,string> num=f=>f.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
foreach(var id in UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Barricades"})){
 var path=UnityEditor.AssetDatabase.GUIDToAssetPath(id);var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
 try{
  var box=root.GetComponent<UnityEngine.BoxCollider2D>();var body=root.GetComponent<UnityEngine.Rigidbody2D>();
  var b=ProjectMayham.EditorTools.BarricadeVisualFitter.VisualBounds(root);
  if(UnityEngine.Mathf.Abs(b.size.x-box.size.x)>0.001f||UnityEngine.Mathf.Abs(b.size.y-box.size.y)>0.001f||((UnityEngine.Vector2)b.center-box.offset).sqrMagnitude>0.000001f)issues.Add("Barricade bounds "+path);
  if(box.size!=new UnityEngine.Vector2(2.8f,0.85f)||body.mass!=5f||body.linearDamping!=4f)issues.Add("Barricade physics "+path);
  var renderers=root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true);
  var before=new System.Collections.Generic.Dictionary<int,UnityEngine.Matrix4x4>();
  foreach(var sr in renderers)before[sr.GetInstanceID()]=sr.transform.localToWorldMatrix;
  ProjectMayham.EditorTools.BarricadeVisualFitter.Fit(root);
  foreach(var sr in renderers){var m=sr.transform.localToWorldMatrix;var old=before[sr.GetInstanceID()];for(int i=0;i<16;i++)maxDelta=UnityEngine.Mathf.Max(maxDelta,UnityEngine.Mathf.Abs(m[i]-old[i]));}
  draw.Add("B|"+root.name+"|"+num(box.size.x)+"|"+num(box.size.y));
  foreach(var sr in renderers){
   if(sr.sprite==null){issues.Add("Missing barricade piece "+path);continue;}
   var m=root.transform.worldToLocalMatrix*sr.transform.localToWorldMatrix;
   var origin=sr.sprite.rect.position+sr.sprite.pivot;
   draw.Add("S|"+root.name+"|"+UnityEditor.AssetDatabase.GetAssetPath(sr.sprite)+"|"+num(m.m00)+"|"+num(m.m01)+"|"+num(m.m10)+"|"+num(m.m11)+"|"+num(m.m03)+"|"+num(m.m13)+"|"+num(sr.sprite.pixelsPerUnit)+"|"+num(origin.x)+"|"+num(origin.y)+"|"+sr.sortingOrder);
  }
  barricades++;
 }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
if(maxDelta>0.0001f)issues.Add("Repeated barricade fit changes transforms by "+maxDelta);
var method=typeof(ProjectMayham.EditorTools.ShelterSetup).GetMethod("TylerSprite",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
if(method==null)issues.Add("Shelter Tyler setup not compiled");
System.IO.File.WriteAllLines("Docs/ArtRefresh-2026-10-30/barricade-draw.tsv",draw);
var result="Sprites="+sprites+"; barricades="+barricades+"; max repeat-fit delta="+num(maxDelta)+"; issues="+issues.Count+"\n"+string.Join("\n",issues);
System.IO.File.WriteAllText("Docs/ArtRefresh-2026-10-30/validation.txt",result);return result;

