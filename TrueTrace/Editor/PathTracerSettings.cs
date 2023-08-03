using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 using UnityEditor;
using CommonVars;
 using System.Xml;
 using System.IO;
 using UnityEngine.UIElements;
 using UnityEngine.Profiling;
using UnityEditor.UIElements;
using System.Xml.Serialization;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TrueTrace {
   public class EditModeFunctions : EditorWindow {
        [MenuItem("PathTracer/Pathtracer Settings")]
        public static void ShowWindow() {
            GetWindow<EditModeFunctions>("Pathtracing Settings");
        }

        public Toggle NEEToggle;
        public Button BVHBuild;
        public Button ScreenShotButton;
        public Button StaticButton;
        public Button ClearButton;
        public Button QuickStartButton;
        public Button ForceInstancesButton;
        public Toggle AlteredPipelineToggle;
        public RayTracingMaster RayMaster;
        public AssetManager Assets;
        public InstancedManager Instancer;
         [SerializeField] public Camera SelectedCamera;
         [SerializeField] public ObjectField CameraField;
         [SerializeField] public int BounceCount = 7;
         [SerializeField] public float RenderRes = 1;
         [SerializeField] public bool NEE = false;
         [SerializeField] public bool Accumulate = true;
         [SerializeField] public bool RR = true;
         [SerializeField] public bool Moving = true;
         [SerializeField] public bool Volumetrics = false;
         [SerializeField] public float VolumDens = 0;
         [SerializeField] public bool MeshSkin = true;
         [SerializeField] public bool Bloom = false;
         [SerializeField] public float BloomStrength = 0.5f;
         [SerializeField] public bool DoF = false;
         [SerializeField] public float DoFAperature = 0.1f;
         [SerializeField] public float DoFFocal = 0.1f;
         [SerializeField] public bool DoExposure = false;
         [SerializeField] public bool ReSTIRGI = false;
         [SerializeField] public bool SampleValid = false;
         [SerializeField] public int UpdateRate = 7;
         [SerializeField] public bool GITemporal = true;
         [SerializeField] public int GITemporalMCap = 12;
         [SerializeField] public bool GISpatial = true;
         [SerializeField] public int GISpatialSampleCount = 6;
         [SerializeField] public bool SpatialStabalizer = false;
         [SerializeField] public bool TAA = false;
         [SerializeField] public bool SVGF = false;
         [SerializeField] public int SVGFSize = 4;
         [SerializeField] public bool ASVGF = false;
         [SerializeField] public int ASVGFSize = 4;
         [SerializeField] public bool ToneMap = false;
         [SerializeField] public bool TAAU = true;
         [SerializeField] public int AtmoScatter = 4;
         [SerializeField] public bool ShowFPS = true;
         [SerializeField] public float Exposure = 0;
         [SerializeField] public int AtlasSize = 16300;
         [SerializeField] public bool UseAlteredPipeline = false;
         [SerializeField] public bool DoCheckerboarding = false;
         [SerializeField] public bool DoFirefly = false;
         [SerializeField] public bool DoIndirectClamping = true;
         [SerializeField] public float MinSpatialSize = 10;
         [SerializeField] public int RISCount = 5;
         private bool IsInMainField = false;
         void OnEnable() {
            EditorSceneManager.activeSceneChangedInEditMode += EvaluateScene;
            if(EditorPrefs.GetString("EditModeFunctions", JsonUtility.ToJson(this, false)) != null) {
               var data = EditorPrefs.GetString("EditModeFunctions", JsonUtility.ToJson(this, false));
               JsonUtility.FromJsonOverwrite(data, this);
            }
         }
         void OnDisable() {
            var data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString("EditModeFunctions", data);
         }

         List<List<GameObject>> Objects;
         List<Mesh> SourceMeshes;

         private void OnStartAsyncCombined() {
            EditorUtility.SetDirty(GameObject.Find("Scene").GetComponent<AssetManager>());
            GameObject.Find("Scene").GetComponent<AssetManager>().EditorBuild();
         }


         List<Transform> ChildObjects;
         private void GrabChildren(Transform Parent) {
            ChildObjects.Add(Parent);
            int ChildCount = Parent.childCount;
            for(int i = 0; i < ChildCount; i++) {
               if(Parent.GetChild(i).gameObject.activeInHierarchy) GrabChildren(Parent.GetChild(i));
            }
         }


         private void ConstructInstances() {
            SourceMeshes = new List<Mesh>();
            Objects = new List<List<GameObject>>();
            ChildObjects = new List<Transform>();
            Transform Source = GameObject.Find("Scene").transform;
            Transform InstanceStorage = GameObject.Find("InstancedStorage").transform;
            int ChildrenLeft = Source.childCount;
            int CurrentChild = 0;
            while(CurrentChild < ChildrenLeft) {
               Transform CurrentObject = Source.GetChild(CurrentChild++);
               if(CurrentObject.gameObject.activeInHierarchy) GrabChildren(CurrentObject); 
            }

            int ChildCount = ChildObjects.Count;
            for(int i = ChildCount - 1; i >= 0; i--) {
               if(ChildObjects[i].GetComponent<ParentObject>() != null || ChildObjects[i].GetComponent<InstancedObject>() != null) {
                  continue;
               }
               if(ChildObjects[i].GetComponent<RayTracingObject>() != null) {
                     var mesh = ChildObjects[i].GetComponent<MeshFilter>().sharedMesh;
                     if(SourceMeshes.Contains(mesh)) {
                        int Index = SourceMeshes.IndexOf(mesh);
                        Objects[Index].Add(ChildObjects[i].gameObject);
                     } else {
                        SourceMeshes.Add(mesh);
                        Objects.Add(new List<GameObject>());
                        Objects[Objects.Count - 1].Add(ChildObjects[i].gameObject);
                     }
               }
            }
            int UniqueMeshCounts = SourceMeshes.Count;
            for(int i = 0; i < UniqueMeshCounts; i++) {
               if(Objects[i].Count > 1) {
                  int Count = Objects[i].Count;
                  GameObject InstancedParent = Instantiate(Objects[i][0], new Vector3(0,-100,0), Quaternion.identity, InstanceStorage);
                  InstancedParent.AddComponent<ParentObject>();
                  for(int i2 = Count - 1; i2 >= 0; i2--) {
                     DestroyImmediate(Objects[i][i2].GetComponent<RayTracingObject>());
                     Objects[i][i2].AddComponent<InstancedObject>();
                     Objects[i][i2].GetComponent<InstancedObject>().InstanceParent = InstancedParent.GetComponent<ParentObject>();
                  }
               }
            }
         }

         private void OptimizeForStatic() {
            GameObject[] AllObjects = GameObject.FindObjectsOfType<GameObject>();//("Untagged");
            foreach(GameObject obj in AllObjects) {
               
               if(PrefabUtility.IsAnyPrefabInstanceRoot(obj)) PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            foreach(GameObject obj in AllObjects) {
               
               if(obj.name.Contains("LOD1") || obj.name.Contains("LOD2")) DestroyImmediate(obj);
            }

            ChildObjects = new List<Transform>();
            Transform Source = GameObject.Find("Scene").transform;
            if(GameObject.Find("Terrain") != null) GameObject.Find("Terrain").transform.parent = Source;
            int ChildrenLeft = Source.childCount;
            Transform Parent;
            if(GameObject.Find("Static Objects") == null) {
               GameObject TempObject = new GameObject("Static Objects", typeof(ParentObject));
               Parent = TempObject.transform;
            }
            else Parent = GameObject.Find("Static Objects").transform;
            Parent.parent = Source;
            int CurrentChild = 0;
            while(CurrentChild < ChildrenLeft) {
               Transform CurrentObject = Source.GetChild(CurrentChild++);
               if(CurrentObject.gameObject.activeInHierarchy && !CurrentObject.gameObject.name.Equals("Static Objects")) GrabChildren(CurrentObject); 
            }
            CurrentChild = 0;
            ChildrenLeft = Parent.childCount;
            while(CurrentChild < ChildrenLeft) {
               Transform CurrentObject = Parent.GetChild(CurrentChild++);
               if(CurrentObject.gameObject.activeInHierarchy && !CurrentObject.gameObject.name.Equals("Static Objects")) GrabChildren(CurrentObject); 
            }
            int ChildCount = ChildObjects.Count;
            for(int i = ChildCount - 1; i >= 0; i--) {
               if(ChildObjects[i].GetComponent<ParentObject>() != null) {
                  DestroyImmediate(ChildObjects[i].GetComponent<ParentObject>());
               }
               if(ChildObjects[i].GetComponent<Light>() != null) {
                  continue;
               } else if(ChildObjects[i].GetComponent<MeshFilter>() != null || ChildObjects[i].GetComponent<Terrain>() != null) {
                  ChildObjects[i].parent = Parent;
               } else if(ChildObjects[i].GetComponent<InstancedObject>() != null) {
                  ChildObjects[i].parent = Source;
               } else {
                  ChildObjects[i].parent = null;
               }
            }

         }
         public struct ParentData {
            public Transform This;
            public List<ParentData> Children;
         }

         private ParentData GrabChildren2(Transform Parent) {
            ParentData Parents = new ParentData();
            Parents.Children = new List<ParentData>();
            Parents.This = Parent;
            int ChildCount = Parent.childCount;
            for(int i = 0; i < ChildCount; i++) {
               if(Parent.GetChild(i).gameObject.activeInHierarchy) Parents.Children.Add(GrabChildren2(Parent.GetChild(i)));

            }
            return Parents;
         }

         private bool TraverseFirstLevel(ParentData Parent) {
            int ChildLength = Parent.Children.Count;
            for(int i = 0; i < ChildLength; i++) {
               if(Parent.Children[i].This.gameObject.GetComponent<MeshFilter>() != null) {
                  return false;
               }
            }
            return true;
         }

         private void ReduceChildren(ParentData Parent) {
            int ChildLength = Parent.Children.Count;
            for(int i = 0; i < ChildLength; i++) {
               if(Parent.Children[i].This.gameObject.GetComponent<ParentObject>() != null) {
                  if(TraverseFirstLevel(Parent.Children[i])) {
                     DestroyImmediate(Parent.Children[i].This.gameObject.GetComponent<ParentObject>());
                  }
               }
               ReduceChildren(Parent.Children[i]);
            }
         }

         private void SolveChildren(ParentData Parent) {
            int ChildLength = Parent.Children.Count;
            for(int i = 0; i < ChildLength; i++) {
               SolveChildren(Parent.Children[i]);
            }
            if(((Parent.This.gameObject.GetComponent<MeshFilter>() != null && Parent.This.gameObject.GetComponent<MeshFilter>().sharedMesh != null) || (Parent.This.gameObject.GetComponent<SkinnedMeshRenderer>() != null && Parent.This.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh != null)) && Parent.This.gameObject.GetComponent<InstancedObject>() == null) {
               if(Parent.This.gameObject.GetComponent<RayTracingObject>() == null) {
                     if((Parent.This.gameObject.GetComponent<MeshRenderer>() != null && Parent.This.gameObject.GetComponent<MeshRenderer>().sharedMaterials != null && Parent.This.gameObject.GetComponent<MeshRenderer>().sharedMaterials.Length != 0) || (Parent.This.gameObject.GetComponent<SkinnedMeshRenderer>() != null && Parent.This.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMaterials != null && Parent.This.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMaterials.Length != 0)) {
                        Parent.This.gameObject.AddComponent<RayTracingObject>();
                     }

                  }
            }
            int RayTracingObjectChildCount = 0;
            bool HasSkinnedMeshAsChild = false;
            bool HasNormalMeshAsChild = false;
            for(int i = 0; i < ChildLength; i++) {
               if(Parent.Children[i].This.gameObject.GetComponent<RayTracingObject>() != null && Parent.Children[i].This.gameObject.GetComponent<ParentObject>() == null) RayTracingObjectChildCount++;
               if(Parent.Children[i].This.gameObject.GetComponent<MeshFilter>() != null && Parent.Children[i].This.gameObject.GetComponent<ParentObject>() == null) HasNormalMeshAsChild = true;
               if(Parent.Children[i].This.gameObject.GetComponent<SkinnedMeshRenderer>() != null && Parent.Children[i].This.gameObject.GetComponent<ParentObject>() == null) HasSkinnedMeshAsChild = true;
               if(Parent.Children[i].This.gameObject.GetComponent<Light>() != null && Parent.Children[i].This.gameObject.GetComponent<RayTracingLights>() == null) Parent.Children[i].This.gameObject.AddComponent<RayTracingLights>(); 
            }
            bool ReductionNeeded = false;
            for(int i = 0; i < ChildLength; i++) {
               if(Parent.Children[i].This.gameObject.GetComponent<SkinnedMeshRenderer>() != null && Parent.This.gameObject.GetComponent<MeshFilter>() == null) {
                  ReductionNeeded = true;
               }
            }
            if(ReductionNeeded) ReduceChildren(Parent);
            if(RayTracingObjectChildCount > 0) {
               if(Parent.This.gameObject.GetComponent<AssetManager>() == null) {
                  if(Parent.This.gameObject.GetComponent<ParentObject>() == null) {
                     Parent.This.gameObject.AddComponent<ParentObject>();
                  }
               }
               else {
                  for(int i = 0; i < ChildLength; i++) {
                     if(Parent.Children[i].This.gameObject.GetComponent<RayTracingObject>() != null && Parent.Children[i].This.gameObject.GetComponent<ParentObject>() == null) Parent.Children[i].This.gameObject.AddComponent<ParentObject>();
                  }               
               }
            } else {
               for(int i = 0; i < ChildLength; i++) {
                  if(Parent.Children[i].This.gameObject.GetComponent<RayTracingObject>() != null && Parent.Children[i].This.gameObject.GetComponent<ParentObject>() == null && Parent.This.gameObject.GetComponent<ParentObject>() == null) Parent.This.gameObject.AddComponent<ParentObject>();
               }
            }
            if(HasNormalMeshAsChild && HasSkinnedMeshAsChild) {
               for(int i = 0; i < ChildLength; i++) {
                  if(Parent.Children[i].This.gameObject.GetComponent<SkinnedMeshRenderer>() != null && Parent.Children[i].This.gameObject.GetComponent<ParentObject>() == null) {
                     Parent.Children[i].This.gameObject.AddComponent<ParentObject>();
                  }
               }  
            }


         }


         private void QuickStart() {
            // RayTracingObject[] TempObjects = GameObject.FindObjectsOfType<RayTracingObject>();
            // foreach(var a in TempObjects) {
            //    DestroyImmediate(a);
            // }         
            // ParentObject[] TempObjects2 = GameObject.FindObjectsOfType<ParentObject>();
            // foreach(var a in TempObjects2) {
            //    DestroyImmediate(a);
            // }
           UnityEngine.Video.VideoPlayer[] VideoObjects = GameObject.FindObjectsOfType<UnityEngine.Video.VideoPlayer>();
           if(VideoObjects.Length != 0) {
               if(VideoObjects[0].gameObject.GetComponent<VideoObject>() == null) {
                  VideoObjects[0].gameObject.AddComponent<VideoObject>();
               }
           }
            ParentData SourceParent = GrabChildren2(Assets.transform);

            SolveChildren(SourceParent);


               Terrain[] Terrains = GameObject.FindObjectsOfType<Terrain>();
               foreach(var TerrainComponent in Terrains) {
                  if(TerrainComponent.gameObject.GetComponentInParent<AssetManager>() == null) {
                     TerrainComponent.gameObject.transform.parent = Assets.transform;
                  }
                  if(TerrainComponent.gameObject.GetComponent<TerrainObject>() == null) TerrainComponent.gameObject.AddComponent<TerrainObject>();
               }
         }
      IntegerField RemainingObjectsField;
      IntegerField SampleCountField;
      private void ReArrangeHierarchy() {
         if(GameObject.Find("Scene") == null) {
                  List<GameObject> Objects = new List<GameObject>();
                  UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(Objects);
                  GameObject SceneObject = new GameObject("Scene", typeof(AssetManager));
                  foreach(GameObject Obj in Objects) {
                     if(Obj.GetComponent<Camera>() == null && !Obj.name.Equals("InstancedStorage")) {
                        Obj.transform.SetParent(SceneObject.transform);
                     }
                  }
                  Assets = GameObject.Find("Scene").GetComponent<AssetManager>();
                  QuickStart();
               }
            if(Instancer == null && GameObject.Find("InstancedStorage") != null) Instancer = GameObject.Find("InstancedStorage").GetComponent<InstancedManager>();
            if(GameObject.Find("InstancedStorage") == null) {
               GameObject InstanceObject = new GameObject("InstancedStorage", typeof(InstancedManager));
            }
            OnFocus();
      }

         public void OnFocus() {
            if(Camera.main != null) {
               if(Camera.main.gameObject.GetComponent<RayTracingMaster>() != null) {
                  DestroyImmediate(Camera.main.gameObject.GetComponent<RayTracingMaster>());
                  Camera.main.gameObject.AddComponent<RenderHandle>();
               }
               if(Camera.main.gameObject.GetComponent<RenderHandle>() == null) Camera.main.gameObject.AddComponent<RenderHandle>();

            }
           if(Assets == null) {
               if( GameObject.Find("Scene") != null) {
                  Assets = GameObject.Find("Scene").GetComponent<AssetManager>();
                  if(Assets == null) {
                     Assets = GameObject.Find("Scene").AddComponent<AssetManager>();
                  }
               }
           }
           if(RayMaster == null) {
               if(GameObject.Find("Scene") != null) {
                  if(GameObject.Find("Scene").GetComponent<RayTracingMaster>() == null) GameObject.Find("Scene").AddComponent(typeof(RayTracingMaster));
                  RayMaster = GameObject.Find("Scene").GetComponent<RayTracingMaster>();
               }
            }
            if(RayMaster == null) return;
            if(Instancer == null && GameObject.Find("InstancedStorage") != null) Instancer = GameObject.Find("InstancedStorage").GetComponent<InstancedManager>();
            if(GameObject.Find("InstancedStorage") == null) {
               GameObject InstanceObject = new GameObject("InstancedStorage", typeof(InstancedManager));
            }
         }
Button LowSettings;
Toggle RRToggle;
Toggle MovingToggle;
Toggle AccumToggle;
Toggle SkinToggle;
Toggle BloomToggle;        
FloatField ResField;
Toggle SVGFToggle;
Toggle ASVGFToggle;
Toggle TAAUToggle;
FloatField AtmoScatterField;
Toggle GIToggle;
FloatField GIUpdateRateField;
FloatField TeporalGIMCapField;
FloatField MinSpatialSizeField;
Toggle TemporalGIToggle;
Toggle SpatialGIToggle;
Toggle TAAToggle;
Toggle DoCheckerboardingToggle;
Toggle DoFireflyToggle;
FloatField SVGFSizeField;
Toggle SampleValidToggle;
Toggle IndirectClampingToggle;
FloatField RISCountField;


private void StandardSet() {
         BounceCount = 7;
         RenderRes = 1;
         NEE = false;
         Accumulate = true;
         RR = true;
         Moving = true;
         Volumetrics = false;
         VolumDens = 0;
         MeshSkin = true;
         Bloom = false;
         BloomStrength = 0.5f;
         DoF = false;
         DoFAperature = 0.1f;
         DoFFocal = 0.1f;
         DoExposure = false;
         ReSTIRGI = false;
         SampleValid = false;
         UpdateRate = 7;
         GITemporal = true;
         GITemporalMCap = 488;
         GISpatial = true;
         GISpatialSampleCount = 12;
         SpatialStabalizer = false;
         TAA = false;
         SVGF = false;
         SVGFSize = 4;
         ASVGF = false;
         ASVGFSize = 4;
         ToneMap = false;
         TAAU = true;
         AtmoScatter = 4;
         ShowFPS = true;
         Exposure = 0;
         AtlasSize = 16300;
         UseAlteredPipeline = false;
         DoCheckerboarding = false;
         DoFirefly = false;
         DoIndirectClamping = true;
         MinSpatialSize = 30;
         RISCount = 12;
}

      private void LowSettingsAssign() {
         StandardSet();
         RRToggle.value = true;
         ResField.value = 0.25f;
         MovingToggle.value = true;
         NEEToggle.value = true;
         AccumToggle.value = true;
         SkinToggle.value = true;
         BloomToggle.value = false;
         SVGFToggle.value = false;
         SVGFSizeField.value = 6;
         ASVGFToggle.value = true;
         TAAUToggle.value = true;
         AtmoScatterField.value = 1;
         GIToggle.value = false;
         DoCheckerboardingToggle.value = false;
      }

      private void HighSettingsAssign() {
         RRToggle.value = true;
         ResField.value = 0.5f;
         MovingToggle.value = true;
         NEEToggle.value = true;
         AccumToggle.value = false;
         SkinToggle.value = true;
         BloomToggle.value = false;
         SVGFToggle.value = false;
         ASVGFToggle.value = true;
         TAAUToggle.value = true;
         AtmoScatterField.value = 4;
         GIToggle.value = false;
      }

      private void QualityRenderPreview() {
         RRToggle.value = true;
         ResField.value = 1.0f;
         MovingToggle.value = true;
         NEEToggle.value = true;
         AccumToggle.value = true;
         SkinToggle.value = true;
         BloomToggle.value = true;
         SVGFToggle.value = false;
         ASVGFToggle.value = false;
         TAAUToggle.value = false;
         AtmoScatterField.value = 4;
         GIToggle.value = true;
         TAAToggle.value = true;
         TeporalGIMCapField.value = 488;
         GIUpdateRateField.value = 7;
         TemporalGIToggle.value = true;
         SpatialGIToggle.value = true;
         SpatialGIToggle.value = true;
         SampleValidToggle.value = true;
      }

      private void UpdatingRenderSettings() {
         RRToggle.value = true;
         ResField.value = 1.0f;
         MovingToggle.value = true;
         NEEToggle.value = true;
         AccumToggle.value = true;
         SkinToggle.value = true;
         BloomToggle.value = true;
         SVGFToggle.value = false;
         ASVGFToggle.value = false;
         TAAUToggle.value = false;
         AtmoScatterField.value = 4;
         GIToggle.value = true;
         TAAToggle.value = true;
         TeporalGIMCapField.value = 122;
         GIUpdateRateField.value = 7;
         TemporalGIToggle.value = true;
         SpatialGIToggle.value = true;
         SpatialGIToggle.value = true;
         SampleValidToggle.value = true;
      }


      private void QuickRenderPreview() {
         RRToggle.value = true;
         ResField.value = 1.0f;
         MovingToggle.value = true;
         NEEToggle.value = true;
         AccumToggle.value = true;
         SkinToggle.value = true;
         BloomToggle.value = true;
         SVGFToggle.value = false;
         ASVGFToggle.value = false;
         TAAUToggle.value = false;
         AtmoScatterField.value = 4;
         GIToggle.value = true;
         TAAToggle.value = true;
         TeporalGIMCapField.value = 12;
         GIUpdateRateField.value = 0;
         TemporalGIToggle.value = true;
         SpatialGIToggle.value = true;
         SpatialGIToggle.value = true;
         SampleValidToggle.value = false;
      }

VisualElement MainSource;
VisualElement RearrangeElement;
Toolbar toolbar;
      void OnGUI() {
         Event e = Event.current;
         if(e.commandName == "ConfirmedButton") ConfirmPopup();  
      }
      public void ConfirmPopup() {
         ReArrangeHierarchy();  
         OnFocus(); 
         if(Camera.main.gameObject.GetComponent<FlyCamera>() == null) Camera.main.gameObject.AddComponent<FlyCamera>();
         rootVisualElement.Remove(RearrangeElement); 
         rootVisualElement.Add(MainSource); 
         Assets.UpdateMaterialDefinition();
         IsInMainField = true;
      }

      VisualElement SceneSettingsMenu;
      PopupField<string> BackgroundSettingsField;
      ObjectField InputHDRIField;
      ColorField BackgroundColorField;
      FloatField BackgroundIntensityField;
      FloatField UnityLightModifierField;
      FloatField IndirectBoostField;


      void AddNormalSettings() {
         List<string> BackgroundSettings = new List<string>();
         BackgroundSettings.Add("Atmosphere");
         BackgroundSettings.Add("HDRI");
         BackgroundSettings.Add("Solid Color");

         VisualElement BlankElement = new VisualElement();

         InputHDRIField = new ObjectField();
         InputHDRIField.objectType = typeof(Texture);
         InputHDRIField.label = "Drag your skybox here ->";
         if(RayMaster.SkyboxTexture != null) InputHDRIField.value = RayMaster.SkyboxTexture;
         InputHDRIField.RegisterValueChangedCallback(evt => RayMaster.SkyboxTexture = evt.newValue as Texture);
         BackgroundColorField = new ColorField();
         BackgroundColorField.value = new Color(RayMaster.SceneBackgroundColor.x, RayMaster.SceneBackgroundColor.y, RayMaster.SceneBackgroundColor.z, 1);
         BackgroundColorField.RegisterValueChangedCallback(evt => RayMaster.SceneBackgroundColor = new Vector3(evt.newValue.r,evt.newValue.g,evt.newValue.b));

         BackgroundSettingsField = new PopupField<string>("Background Type");
         BackgroundSettingsField.choices = BackgroundSettings;
         BackgroundSettingsField.index = RayMaster.BackgroundType;
         switch(BackgroundSettingsField.index) {
            case 0:
               BackgroundSettingsField.Add(BlankElement);
            break;
            case 1:
               BackgroundSettingsField.Add(InputHDRIField);
            break;
            case 2:
               BackgroundSettingsField.Add(BackgroundColorField);
            break;
         }

         SceneSettingsMenu.Add(BackgroundSettingsField);
         BackgroundSettingsField.RegisterValueChangedCallback(evt => {
            RayMaster.BackgroundType = BackgroundSettingsField.index;
            switch(BackgroundSettingsField.index) {
               case 0:
                  BackgroundSettingsField.Add(BlankElement);
               break;
               case 1:
                  BackgroundSettingsField.Add(InputHDRIField);
               break;
               case 2:
                  BackgroundSettingsField.Add(BackgroundColorField);
               break;
            }
            BackgroundSettingsField.RemoveAt(2);

            });
      
      BackgroundIntensityField = new FloatField() {value = RayMaster.BackgroundIntensity, label = "Background Intensity"};
      BackgroundIntensityField.RegisterValueChangedCallback(evt => RayMaster.BackgroundIntensity = evt.newValue);
      SceneSettingsMenu.Add(BackgroundIntensityField);

      UnityLightModifierField = new FloatField() {value = Assets.LightEnergyScale, label = "Unity Light Intensity Modifier"};
      UnityLightModifierField.RegisterValueChangedCallback(evt => Assets.LightEnergyScale = evt.newValue);
      SceneSettingsMenu.Add(UnityLightModifierField);

      IndirectBoostField = new FloatField() {value = RayMaster.IndirectBoost, label = "Indirect Lighting Boost"};
      IndirectBoostField.RegisterValueChangedCallback(evt => RayMaster.IndirectBoost = evt.newValue);
      SceneSettingsMenu.Add(IndirectBoostField);


      }


      VisualElement MaterialPairingMenu;
      ObjectField InputMaterialField;
      PopupField<string> BaseColorField;
      PopupField<string> BaseColorTextureField;
      PopupField<string> NormalTextureField;
      PopupField<string> EmissionTextureField;
      PopupField<string> MetallicRangeField;
      PopupField<string> MetallicTextureField;
      PopupField<string> MetallicChannelField;
      PopupField<string> RoughnessRangeField;
      PopupField<string> RoughnessTextureField;
      PopupField<string> RoughnessChannelField;
      Toggle GlassToggle;
      Toggle CutoutToggle;
      MaterialShader MatShader;
      int Index;
      void ConfirmMats() {
         MatShader.BaseColorTex = BaseColorTextureField.value;
         MatShader.BaseColorValue = BaseColorField.value;
         MatShader.NormalTex = NormalTextureField.value;
         MatShader.EmissionTex = EmissionTextureField.value;
         MatShader.MetallicTex = MetallicTextureField.value;
         MatShader.MetallicRange = MetallicRangeField.value;
         MatShader.MetallicTexChannel = MetallicChannelField.index;
         MatShader.RoughnessTex = RoughnessTextureField.value;
         MatShader.RoughnessRange = RoughnessRangeField.value;
         MatShader.RoughnessTexChannel = RoughnessChannelField.index;
         MatShader.IsGlass = GlassToggle.value;
         MatShader.IsCutout = CutoutToggle.value;
         AssetManager.data.Material[Index] = MatShader;
         using(StreamWriter writer = new StreamWriter(Application.dataPath + "/TrueTrace/Resources/Utility/MaterialMappings.xml")) {
            var serializer = new XmlSerializer(typeof(Materials));
            serializer.Serialize(writer.BaseStream, AssetManager.data);
         }
      }
      void AddAssetsToMenu() {
         Shader shader = (InputMaterialField.value as Material).shader;
         List<string> FloatProperties = new List<string>();
         List<string> ColorProperties = new List<string>();
         List<string> TextureProperties = new List<string>();
         List<string> ChannelProperties = new List<string>();
         int PropCount = shader.GetPropertyCount();
         ColorProperties.Add("null");
         FloatProperties.Add("null");
         TextureProperties.Add("null");
         ChannelProperties.Add("R");
         ChannelProperties.Add("G");
         ChannelProperties.Add("B");
         ChannelProperties.Add("A");
         for(int i = 0; i < PropCount; i++) {
            if(shader.GetPropertyType(i) == ShaderPropertyType.Texture) TextureProperties.Add(shader.GetPropertyName(i));
            if(shader.GetPropertyType(i) == ShaderPropertyType.Color) ColorProperties.Add(shader.GetPropertyName(i));
            if(shader.GetPropertyType(i) == ShaderPropertyType.Range) FloatProperties.Add(shader.GetPropertyName(i));
         }
         MatShader = AssetManager.data.Material.Find((s1) => s1.Name.Equals(shader.name));
         Index = AssetManager.data.Material.IndexOf(MatShader);
         VisualElement BaseColorRow = new VisualElement();
         BaseColorRow.style.flexDirection = FlexDirection.Row;
            BaseColorField = new PopupField<string>("Base Color ->");
            BaseColorField.choices = ColorProperties;
            BaseColorField.index = ColorProperties.IndexOf(MatShader.BaseColorValue);
            BaseColorRow.Add(BaseColorField);
            BaseColorTextureField = new PopupField<string>("Base Color Texture ->");
            BaseColorTextureField.choices = TextureProperties;
            BaseColorTextureField.index = TextureProperties.IndexOf(MatShader.BaseColorTex);
            BaseColorRow.Add(BaseColorTextureField);
         MaterialPairingMenu.Add(BaseColorRow);

         NormalTextureField = new PopupField<string>("Normal Texture ->");
         NormalTextureField.choices = TextureProperties;
         NormalTextureField.index = TextureProperties.IndexOf(MatShader.NormalTex);
         MaterialPairingMenu.Add(NormalTextureField);
         EmissionTextureField = new PopupField<string>("Emission Texture ->");
         EmissionTextureField.choices = TextureProperties;
         EmissionTextureField.index = TextureProperties.IndexOf(MatShader.EmissionTex);
         MaterialPairingMenu.Add(EmissionTextureField);
         VisualElement MetallicRow = new VisualElement();
         MetallicRow.style.flexDirection = FlexDirection.Row;
            MetallicRangeField = new PopupField<string>("Metallic Float ->");
            MetallicRangeField.choices = FloatProperties;
            MetallicRangeField.index = FloatProperties.IndexOf(MatShader.MetallicRange);
            MetallicRow.Add(MetallicRangeField);
            MetallicTextureField = new PopupField<string>("Metallic Texture ->");
            MetallicTextureField.choices = TextureProperties;
            MetallicTextureField.index = TextureProperties.IndexOf(MatShader.MetallicTex);
            MetallicRow.Add(MetallicTextureField);
            MetallicChannelField = new PopupField<string>("Texture Channel Of Metallic ->");
            MetallicChannelField.choices = ChannelProperties;
            MetallicChannelField.index = MatShader.MetallicTexChannel;
            MetallicRow.Add(MetallicChannelField);
         MaterialPairingMenu.Add(MetallicRow);

         VisualElement RoughnessRow = new VisualElement();
         RoughnessRow.style.flexDirection = FlexDirection.Row;
            RoughnessRangeField = new PopupField<string>("Roughness Float ->");
            RoughnessRangeField.choices = FloatProperties;
            RoughnessRangeField.index = FloatProperties.IndexOf(MatShader.RoughnessRange);
            RoughnessRow.Add(RoughnessRangeField);
            RoughnessTextureField = new PopupField<string>("Roughness Texture ->");
            RoughnessTextureField.choices = TextureProperties;
            RoughnessTextureField.index = TextureProperties.IndexOf(MatShader.RoughnessTex);
            RoughnessRow.Add(RoughnessTextureField);
            RoughnessChannelField = new PopupField<string>("Texture Channel Of Roughness ->");
            RoughnessChannelField.choices = ChannelProperties;
            RoughnessChannelField.index = MatShader.RoughnessTexChannel;
            RoughnessRow.Add(RoughnessChannelField);
         MaterialPairingMenu.Add(RoughnessRow);

         GlassToggle = new Toggle() {value = MatShader.IsGlass, text = "Force Glass On All Objects With This Material"};
         MaterialPairingMenu.Add(GlassToggle);
         CutoutToggle = new Toggle() {value = MatShader.IsCutout, text = "Force All Objects With This Material To Be Cutout"};
         MaterialPairingMenu.Add(CutoutToggle);

         Button ConfirmMaterialButton = new Button(() => ConfirmMats()) {text = "Apply Material Links"};
         MaterialPairingMenu.Add(ConfirmMaterialButton);
      }

         void EvaluateScene(Scene Current, Scene Next) {
            rootVisualElement.Clear();
            MainSource.Clear();
            CreateGUI();
         }
        public void CreateGUI() {
            OnFocus();
            MainSource = new VisualElement();
            MaterialPairingMenu = new VisualElement();
            SceneSettingsMenu = new VisualElement();
            InputMaterialField = new ObjectField();
            InputMaterialField.objectType = typeof(Material);
            InputMaterialField.label = "Drag a material with the desired shader here ->";
            InputMaterialField.RegisterValueChangedCallback(evt => {MaterialPairingMenu.Clear(); MaterialPairingMenu.Add(InputMaterialField); AddAssetsToMenu();});
            MaterialPairingMenu.Add(InputMaterialField);
            toolbar = new Toolbar();
            rootVisualElement.Add(toolbar);
            if(Assets == null) {
               IsInMainField = false;
               RearrangeElement = new VisualElement();
               Button RearrangeButton = new Button(() => {UnityEditor.PopupWindow.Show(new Rect(0,0,10,10), new PopupWarningWindow());}) {text="Arrange Hierarchy"};
               RearrangeElement.Add(RearrangeButton);
               rootVisualElement.Add(RearrangeElement);
            } else {
               {rootVisualElement.Clear(); rootVisualElement.Add(toolbar); rootVisualElement.Add(MainSource); MaterialPairingMenu.Clear();}
               Assets.UpdateMaterialDefinition();
            }
            Button MainSourceButton = new Button(() => {rootVisualElement.Clear(); rootVisualElement.Add(toolbar); rootVisualElement.Add(MainSource); MaterialPairingMenu.Clear();});
            Button MaterialPairButton = new Button(() => {rootVisualElement.Clear(); rootVisualElement.Add(toolbar); InputMaterialField.value = null; MaterialPairingMenu.Add(InputMaterialField); rootVisualElement.Add(MaterialPairingMenu);});
            Button SceneSettingsButton = new Button(() => {rootVisualElement.Clear(); rootVisualElement.Add(toolbar); rootVisualElement.Add(SceneSettingsMenu);});
            toolbar.Add(MainSourceButton);
            toolbar.Add(MaterialPairButton);
            toolbar.Add(SceneSettingsButton);
            MainSourceButton.text = "Main Options";
            MaterialPairButton.text = "Material Pair Options";
            SceneSettingsButton.text = "Scene Settings";

            if(RayMaster != null && Assets != null) {
            AddNormalSettings();
           RayMaster.bouncecount = BounceCount;
           RayMaster.RenderScale = RenderRes;
           RayMaster.UseRussianRoulette = RR;
           RayMaster.DoTLASUpdates = Moving;
           RayMaster.AllowConverge = Accumulate;
           RayMaster.UseNEE = NEE;
           Assets.UseSkinning = MeshSkin;
           RayMaster.AllowBloom = Bloom;
           RayMaster.BloomStrength = 128 - BloomStrength * 128.0f;
           RayMaster.AllowDoF = DoF;
           RayMaster.DoFAperature = DoFAperature;
           RayMaster.DoFFocal = DoFFocal * 60.0f;
           RayMaster.AllowAutoExpose = DoExposure;
           RayMaster.UseReSTIRGI = ReSTIRGI;
           RayMaster.UseReSTIRGITemporal = GITemporal;
           RayMaster.UseReSTIRGISpatial = GISpatial;
           RayMaster.DoReSTIRGIConnectionValidation = SampleValid;
           RayMaster.ReSTIRGIUpdateRate = UpdateRate;
           RayMaster.ReSTIRGITemporalMCap = GITemporalMCap;
           RayMaster.ReSTIRGISpatialCount = GISpatialSampleCount;
           RayMaster.ReSTIRGISpatialStabalizer = SpatialStabalizer;
           RayMaster.AllowTAA = TAA;
           RayMaster.UseSVGF = SVGF;
           RayMaster.SVGFAtrousKernelSizes = SVGFSize;
           RayMaster.UseASVGF = ASVGF;
           RayMaster.AllowToneMap = ToneMap;
           RayMaster.UseTAAU = TAAU;
           RayMaster.AtmoNumLayers = AtmoScatter;
           RayMaster.Exposure = 100 * Exposure + 1;
           Assets.DesiredRes = AtlasSize;
           RayMaster.UseAlteredPipeline = UseAlteredPipeline;
           RayMaster.DoCheckerboarding = DoCheckerboarding;
           RayMaster.DoFirefly = DoFirefly;
           RayMaster.MinSpatialSize = MinSpatialSize;
           RayMaster.RISCount = RISCount;
           RayMaster.DoIndirectClamping = DoIndirectClamping;
         }


           BVHBuild = new Button(() => OnStartAsyncCombined()) {text = "Build Aggregated BVH"};
           BVHBuild.style.minWidth = 145;
           ScreenShotButton = new Button(() => {
               string dirPath = Application.dataPath + "/../Assets/ScreenShots";
               if(!System.IO.Directory.Exists(dirPath)) {
                  Debug.Log("No Folder Named ScreenShots in Assets Folder.  Please Create One");
               } else {
                  ScreenCapture.CaptureScreenshot(dirPath + "/" + System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ", " + RayMaster.SampleCount + " Samples.png");
                  UnityEditor.AssetDatabase.Refresh();
               }
           }) {text = "Take Screenshot"};
           ScreenShotButton.style.minWidth = 100;
           StaticButton = new Button(() => {if(!Application.isPlaying) OptimizeForStatic(); else Debug.Log("Cant Do This In Editor");}) {text = "Make All Static"};
           StaticButton.style.minWidth = 105;
           
           ClearButton = new Button(() => {
            if(!Application.isPlaying) {
               EditorUtility.SetDirty(Assets);
               Assets.ClearAll();
               InstancedManager Instanced = GameObject.Find("InstancedStorage").GetComponent<InstancedManager>();
               EditorUtility.SetDirty(Instanced);
               Instanced.ClearAll();
           } else Debug.Log("Cant Do This In Editor");}) {text = "Clear Parent Data"};
           ClearButton.style.minWidth = 145;
           QuickStartButton = new Button(() => QuickStart()) {text = "Auto Assign Scripts"};
           QuickStartButton.style.minWidth = 111;
           ForceInstancesButton = new Button(() => {if(!Application.isPlaying) ConstructInstances(); else Debug.Log("Cant Do This In Editor");}) {text = "Force Instances"};

           IntegerField AtlasField = new IntegerField() {value = AtlasSize, label = "Atlas Size"};
           AtlasField.RegisterValueChangedCallback(evt => {if(!Application.isPlaying) {AtlasSize = evt.newValue; AtlasSize = Mathf.Min(AtlasSize, 16384); AtlasSize = Mathf.Max(AtlasSize, 32); Assets.DesiredRes = AtlasSize;} else AtlasField.value = AtlasSize;});
               AtlasField.ElementAt(0).style.minWidth = 65;
               AtlasField.ElementAt(1).style.width = 45;

            LowSettings = new Button(() => {if(!Application.isPlaying) LowSettingsAssign(); else Debug.Log("Cant Change In Editor");}) {text = "Low Quality"};
            Button HighSettings = new Button(() => {if(!Application.isPlaying) HighSettingsAssign(); else Debug.Log("Cant Change In Editor");}) {text = "High Quality"};
            Button RenderSettings = new Button(() => {if(!Application.isPlaying) QualityRenderPreview(); else Debug.Log("Cant Change In Editor");}) {text = "Static Render"};
            Button PreviewRenderSettings = new Button(() => {if(!Application.isPlaying) QuickRenderPreview(); else Debug.Log("Cant Change In Editor");}) {text = "Fast Static Render"};
            Button UpdateRenderSettings = new Button(() => {if(!Application.isPlaying) UpdatingRenderSettings(); else Debug.Log("Cant Change In Editor");}) {text = "High Quality 1"};

           Box ButtonField1 = new Box();
           ButtonField1.style.flexDirection = FlexDirection.Row;
           ButtonField1.Add(BVHBuild);
           ButtonField1.Add(ScreenShotButton);
           ButtonField1.Add(StaticButton);
           ButtonField1.Add(LowSettings);
           ButtonField1.Add(PreviewRenderSettings);
           MainSource.Add(ButtonField1);

           Box ButtonField2 = new Box();
           ButtonField2.style.flexDirection = FlexDirection.Row;
           ButtonField2.Add(ClearButton);
           ButtonField2.Add(QuickStartButton);
           ButtonField2.Add(ForceInstancesButton);
           ButtonField2.Add(HighSettings);
           ButtonField2.Add(RenderSettings);
           MainSource.Add(ButtonField2);

           Box TopEnclosingBox = new Box();
               TopEnclosingBox.style.flexDirection = FlexDirection.Row;
               FloatField BounceField = new FloatField() {value = BounceCount, label = "Max Bounces"};
               BounceField.ElementAt(0).style.minWidth = 75;
               BounceField.ElementAt(1).style.width = 25;
               BounceField.style.paddingRight = 40;
               TopEnclosingBox.Add(BounceField);
               BounceField.RegisterValueChangedCallback(evt => {BounceCount = (int)evt.newValue; RayMaster.bouncecount = BounceCount;});        
               ResField = new FloatField("Render Scale") {value = RenderRes};
               ResField.ElementAt(0).style.minWidth = 75;
               ResField.ElementAt(1).style.width = 35;
               TopEnclosingBox.Add(ResField);
               ResField.RegisterValueChangedCallback(evt => {if(!Application.isPlaying) {RenderRes = evt.newValue; RayMaster.RenderScale = RenderRes;} else ResField.value = RenderRes;});        
               TopEnclosingBox.Add(AtlasField);
               TopEnclosingBox.Add(UpdateRenderSettings);
           MainSource.Add(TopEnclosingBox);

           RRToggle = new Toggle() {value = RR, text = "Use Russian Roulette"};
           MainSource.Add(RRToggle);
           RRToggle.RegisterValueChangedCallback(evt => {RR = evt.newValue; RayMaster.UseRussianRoulette = RR;});

           MovingToggle = new Toggle() {value = Moving, text = "Enable Object Moving"};
           MovingToggle.tooltip = "Enables realtime updating of materials and object positions, laggy to leave on for scenes with high ParentObject counts";
           MainSource.Add(MovingToggle);
           MovingToggle.RegisterValueChangedCallback(evt => {Moving = evt.newValue; RayMaster.DoTLASUpdates = Moving;});

           AccumToggle = new Toggle() {value = Accumulate, text = "Allow Image Accumulation"};
           MainSource.Add(AccumToggle);
           AccumToggle.RegisterValueChangedCallback(evt => {Accumulate = evt.newValue; RayMaster.AllowConverge = Accumulate;});

           NEEToggle = new Toggle() {value = NEE, text = "Use Next Event Estimation"};
           MainSource.Add(NEEToggle);
           VisualElement NEEBox = new VisualElement();
            Label RISLabel = new Label("RIS Count");
            RISCountField = new FloatField() {value = RISCount};
            RISCountField.RegisterValueChangedCallback(evt => {RISCount = (int)evt.newValue; RayMaster.RISCount = RISCount;});
            NEEBox.style.flexDirection = FlexDirection.Row;
            NEEBox.Add(RISLabel);
            NEEBox.Add(RISCountField);
           NEEToggle.RegisterValueChangedCallback(evt => {NEE = evt.newValue; RayMaster.UseNEE = NEE; if(evt.newValue) MainSource.Insert(MainSource.IndexOf(NEEToggle) + 1, NEEBox);else MainSource.Remove(NEEBox);});
            if(NEEToggle.value) {
               MainSource.Add(NEEBox);
            }
       

           SkinToggle = new Toggle() {value = MeshSkin, text = "Allow Mesh Skinning"};
           MainSource.Add(SkinToggle);
           SkinToggle.RegisterValueChangedCallback(evt => {MeshSkin = evt.newValue; Assets.UseSkinning = MeshSkin;});

         BloomToggle = new Toggle() {value = Bloom, text = "Enable Bloom"};
           VisualElement BloomBox = new VisualElement();
               Label BloomLabel = new Label("Bloom Strength");
               Slider BloomSlider = new Slider() {value = BloomStrength, highValue = 1.0f, lowValue = 0};
               BloomSlider.style.width = 100;
               BloomToggle.RegisterValueChangedCallback(evt => {Bloom = evt.newValue; RayMaster.AllowBloom = Bloom; if(evt.newValue) MainSource.Insert(MainSource.IndexOf(BloomToggle) + 1, BloomBox); else MainSource.Remove(BloomBox);});        
               BloomSlider.RegisterValueChangedCallback(evt => {BloomStrength = evt.newValue; RayMaster.BloomStrength = 128 - BloomStrength * 128.0f;});
               MainSource.Add(BloomToggle);
               BloomBox.style.flexDirection = FlexDirection.Row;
               BloomBox.Add(BloomLabel);
               BloomBox.Add(BloomSlider);
           if(Bloom) MainSource.Add(BloomBox);

           Label AperatureLabel = new Label("Aperature Size");
           Slider AperatureSlider = new Slider() {value = DoFAperature, highValue = 1, lowValue = 0};
           AperatureSlider.style.width = 100;
           Label FocalLabel = new Label("Focal Length");
           Slider FocalSlider = new Slider() {value = DoFFocal, highValue = 1, lowValue = 0};
           FocalSlider.style.width = 100;
           Box AperatureBox = new Box();
           AperatureBox.Add(AperatureLabel);
           AperatureBox.Add(AperatureSlider);
           AperatureBox.style.flexDirection = FlexDirection.Row;
           Box FocalBox = new Box();
           FocalBox.Add(FocalLabel);
           FocalBox.Add(FocalSlider);
           FocalBox.style.flexDirection = FlexDirection.Row;

           Toggle DoFToggle = new Toggle() {value = DoF, text = "Enable DoF"};
           VisualElement DoFFoldout = new VisualElement();
           DoFFoldout.Add(AperatureBox);
           DoFFoldout.Add(FocalBox);
           MainSource.Add(DoFToggle);
           DoFToggle.RegisterValueChangedCallback(evt => {DoF = evt.newValue; RayMaster.AllowDoF = DoF;if(evt.newValue) MainSource.Insert(MainSource.IndexOf(DoFToggle) + 1, DoFFoldout); else MainSource.Remove(DoFFoldout);});        
           AperatureSlider.RegisterValueChangedCallback(evt => {DoFAperature = evt.newValue; RayMaster.DoFAperature = DoFAperature;});
           FocalSlider.RegisterValueChangedCallback(evt => {DoFFocal = evt.newValue; RayMaster.DoFFocal = DoFFocal * 60.0f;});
           if(DoF) MainSource.Add(DoFFoldout);
           Toggle DoExposureToggle = new Toggle() {value = DoExposure, text = "Enable Auto/Manual Exposure"};
           MainSource.Add(DoExposureToggle);
           VisualElement ExposureElement = new VisualElement();
               ExposureElement.style.flexDirection = FlexDirection.Row;
               Label ExposureLabel = new Label("Exposure");
               Slider ExposureSlider = new Slider() {value = Exposure, highValue = 1, lowValue = 0};
               DoExposureToggle.tooltip = "Slide to the left for Auto";
               ExposureSlider.tooltip = "Slide to the left for Auto";
               ExposureLabel.tooltip = "Slide to the left for Auto";
               ExposureSlider.style.width = 100;
               ExposureElement.Add(ExposureLabel);
               ExposureElement.Add(ExposureSlider);
           DoExposureToggle.RegisterValueChangedCallback(evt => {DoExposure = evt.newValue; RayMaster.AllowAutoExpose = DoExposure;if(evt.newValue) MainSource.Insert(MainSource.IndexOf(DoExposureToggle) + 1, ExposureElement); else MainSource.Remove(ExposureElement);});
           ExposureSlider.RegisterValueChangedCallback(evt => {Exposure = evt.newValue; RayMaster.Exposure = Exposure * 100 + 1;});
            if(DoExposure) MainSource.Add(ExposureElement);

           GIToggle = new Toggle() {value = ReSTIRGI, text = "Use ReSTIR GI"};
           VisualElement GIFoldout = new VisualElement() {};
           Box EnclosingGI = new Box();
               Box TopGI = new Box();
                   TopGI.style.flexDirection = FlexDirection.Row;
                   SampleValidToggle = new Toggle() {value = SampleValid, text = "Do Sample Connection Validation"};
                   SampleValidToggle.tooltip = "Confirms samples are mutually visable, reduces performance but improves indirect shadow quality";
                   Label GIUpdateRateLabel = new Label("Update Rate(0 is off)");
                   GIUpdateRateLabel.tooltip = "How often a pixel should validate its entire path, good for quickly changing lighting";
                   GIUpdateRateField = new FloatField() {value = UpdateRate};
                   SampleValidToggle.RegisterValueChangedCallback(evt => {SampleValid = evt.newValue; RayMaster.DoReSTIRGIConnectionValidation = SampleValid;});
                   GIUpdateRateField.RegisterValueChangedCallback(evt => {UpdateRate = (int)evt.newValue; RayMaster.ReSTIRGIUpdateRate = UpdateRate;});
                    IndirectClampingToggle = new Toggle() {value = DoIndirectClamping, text = "Enable Indirect Clamping"};
                    IndirectClampingToggle.RegisterValueChangedCallback(evt => {DoIndirectClamping = evt.newValue; RayMaster.DoIndirectClamping = DoIndirectClamping;});
                   TopGI.Add(SampleValidToggle);
                   TopGI.Add(GIUpdateRateField);
                   TopGI.Add(GIUpdateRateLabel);
                   TopGI.Add(IndirectClampingToggle);
               EnclosingGI.Add(TopGI);
               Box TemporalGI = new Box();
                   TemporalGI.style.flexDirection = FlexDirection.Row;
                   TemporalGIToggle = new Toggle() {value = GITemporal, text = "Enable Temporal"};
                   Label TemporalGIMCapLabel = new Label("Temporal M Cap(0 is off)");
                   TemporalGIMCapLabel.tooltip = "Controls how long a sample is valid for, lower numbers update more quickly but have more noise, good for quickly changing scenes/lighting";
                   TeporalGIMCapField = new FloatField() {value = GITemporalMCap};
                   TemporalGIToggle.RegisterValueChangedCallback(evt => {GITemporal = evt.newValue; RayMaster.UseReSTIRGITemporal = GITemporal;});
                   TeporalGIMCapField.RegisterValueChangedCallback(evt => {GITemporalMCap = (int)evt.newValue; RayMaster.ReSTIRGITemporalMCap = GITemporalMCap;});
                   TemporalGI.Add(TemporalGIToggle);
                   TemporalGI.Add(TeporalGIMCapField);
                   TemporalGI.Add(TemporalGIMCapLabel);
               EnclosingGI.Add(TemporalGI);
               Box SpatialGI = new Box();
                   SpatialGI.style.flexDirection = FlexDirection.Row;
                   SpatialGIToggle = new Toggle() {value = GISpatial, text = "Enable Spatial"};
                   Label SpatialGISampleCountLabel = new Label("Spatial Sample Count");
                   Label MinSpatialSizeLabel = new Label("Minimum Spatial Radius");
                   SpatialGISampleCountLabel.tooltip = "How many neighbors are sampled, tradeoff between performance and quality";
                   FloatField SpatialGISampleCountField = new FloatField() {value = GISpatialSampleCount};
                   FloatField MinSpatialSizeField = new FloatField() {value = MinSpatialSize};
                   Toggle StabalizerToggle = new Toggle() {value = SpatialStabalizer, text = "Enable Edge Filling"};
                   StabalizerToggle.tooltip = "Replaces black edges of the screen with blurred info";
                   SpatialGIToggle.RegisterValueChangedCallback(evt => {GISpatial = evt.newValue; RayMaster.UseReSTIRGISpatial = GISpatial;});
                   SpatialGISampleCountField.RegisterValueChangedCallback(evt => {GISpatialSampleCount = (int)evt.newValue; RayMaster.ReSTIRGISpatialCount = GISpatialSampleCount;});
                   MinSpatialSizeField.RegisterValueChangedCallback(evt => {MinSpatialSize = (int)evt.newValue; RayMaster.MinSpatialSize = MinSpatialSize;});
                   StabalizerToggle.RegisterValueChangedCallback(evt => {SpatialStabalizer = evt.newValue; RayMaster.ReSTIRGISpatialStabalizer = SpatialStabalizer;});
                   SpatialGI.Add(SpatialGIToggle);
                   SpatialGI.Add(SpatialGISampleCountField);
                   SpatialGI.Add(SpatialGISampleCountLabel);
                   SpatialGI.Add(StabalizerToggle);
                   SpatialGI.Add(MinSpatialSizeField);
                   SpatialGI.Add(MinSpatialSizeLabel);
               EnclosingGI.Add(SpatialGI);
           GIFoldout.Add(EnclosingGI);
           MainSource.Add(GIToggle);
           GIToggle.RegisterValueChangedCallback(evt => {ReSTIRGI = evt.newValue; RayMaster.UseReSTIRGI = ReSTIRGI;if(evt.newValue) MainSource.Insert(MainSource.IndexOf(GIToggle) + 1, GIFoldout); else MainSource.Remove(GIFoldout);});
           if(ReSTIRGI) MainSource.Add(GIFoldout);
       

           TAAToggle = new Toggle() {value = TAA, text = "Enable TAA"};
           MainSource.Add(TAAToggle);
           TAAToggle.RegisterValueChangedCallback(evt => {TAA = evt.newValue; RayMaster.AllowTAA = TAA;});

           SVGFToggle = new Toggle() {value = SVGF, text = "Enable SVGF"};
           VisualElement SVGFFoldout = new VisualElement() {};
               SVGFFoldout.style.flexDirection = FlexDirection.Row;
               SVGFSizeField = new FloatField("SVGF Atrous Kernel Size") {value = SVGFSize};
               SVGFSizeField.RegisterValueChangedCallback(evt => {SVGFSize = (int)evt.newValue; RayMaster.SVGFAtrousKernelSizes = SVGFSize;});
               SVGFFoldout.Add(SVGFSizeField);
           MainSource.Add(SVGFToggle);
           SVGFToggle.RegisterValueChangedCallback(evt => {SVGF = evt.newValue; RayMaster.UseSVGF = SVGF;if(evt.newValue) MainSource.Insert(MainSource.IndexOf(SVGFToggle) + 1, SVGFFoldout); else MainSource.Remove(SVGFFoldout);});
           if(SVGF) MainSource.Add(SVGFFoldout);

           ASVGFToggle = new Toggle() {value = ASVGF, text = "Enable A-SVGF"};
           ASVGFToggle.tooltip = "Upgrade to SVGF, lighting updates much faster but is more costly to run";
           MainSource.Add(ASVGFToggle);
           ASVGFToggle.RegisterValueChangedCallback(evt => {ASVGF = evt.newValue; RayMaster.UseASVGF = ASVGF;});

           Toggle ToneMapToggle = new Toggle() {value = ToneMap, text = "Enable Tonemapping"};
           MainSource.Add(ToneMapToggle);
           ToneMapToggle.RegisterValueChangedCallback(evt => {ToneMap = evt.newValue; RayMaster.AllowToneMap = ToneMap;});

           TAAUToggle = new Toggle() {value = TAAU, text = "Enable TAAU"};
           TAAUToggle.tooltip = "On = Temporal Anti Aliasing Upscaling; Off = Semi Custom Upscaler, performs slightly differently";
           MainSource.Add(TAAUToggle);
           TAAUToggle.RegisterValueChangedCallback(evt => {TAAU = evt.newValue; RayMaster.UseTAAU = TAAU;});

           AlteredPipelineToggle = new Toggle() {value = UseAlteredPipeline, text = "Use Altered Throughput Pipeline"};
           AlteredPipelineToggle.RegisterValueChangedCallback(evt => {UseAlteredPipeline = evt.newValue; RayMaster.UseAlteredPipeline = UseAlteredPipeline;});
           MainSource.Add(AlteredPipelineToggle);

           DoCheckerboardingToggle = new Toggle() {value = DoCheckerboarding, text = "Use Checkerboarding"};
           DoCheckerboardingToggle.RegisterValueChangedCallback(evt => {DoCheckerboarding = evt.newValue; RayMaster.DoCheckerboarding = DoCheckerboarding;});
           MainSource.Add(DoCheckerboardingToggle);

           DoFireflyToggle = new Toggle() {value = DoFirefly, text = "Enable AntiFirefly"};
           DoFireflyToggle.RegisterValueChangedCallback(evt => {DoFirefly = evt.newValue; RayMaster.DoFirefly = DoFirefly;});
           MainSource.Add(DoFireflyToggle);


           VisualElement AtmoBox = new VisualElement();
               AtmoBox.style.flexDirection = FlexDirection.Row;
               AtmoScatterField = new FloatField("Atmospheric Scattering Samples") {value = AtmoScatter};
               AtmoScatterField.RegisterValueChangedCallback(evt => {AtmoScatter = (int)evt.newValue; RayMaster.AtmoNumLayers = AtmoScatter;});
               AtmoBox.Add(AtmoScatterField);
           MainSource.Add(AtmoBox);



           Toggle SampleShowToggle = new Toggle() {value = ShowFPS, text = "Show Sample Count"};
           // SerializedObject so = new SerializedObject(RayMaster);
           VisualElement SampleCountBox = new VisualElement();
               SampleCountBox.style.flexDirection = FlexDirection.Row;
               SampleCountField = new IntegerField("Current Sample Count") {};
               // SampleCountField.Bind(so);
               SampleCountBox.Add(SampleCountField);
           MainSource.Add(SampleShowToggle);
           SampleShowToggle.RegisterValueChangedCallback(evt => {ShowFPS = evt.newValue; if(evt.newValue) MainSource.Insert(MainSource.IndexOf(SampleShowToggle) + 1, SampleCountBox); else MainSource.Remove(SampleCountBox);});
           if(ShowFPS) MainSource.Add(SampleCountBox);

           Rect WindowRect = MainSource.layout;
           Box EnclosingBox = new Box();
               try {
                  EnclosingBox.style.position = Position.Absolute;
               } catch(System.Exception e) {}
               EnclosingBox.style.top = 70;
               EnclosingBox.style.width = 110;
               EnclosingBox.style.height = 55;
               EnclosingBox.style.left = 200;
               Label RemainingObjectsLabel = new Label("Remaining Objects");
               // RemainingObjectsLabel.style.color = Color.white;
               RemainingObjectsField = new IntegerField() {};
               Box ReadyBox = new Box();
               ReadyBox.style.height = 18;
               ReadyBox.style.backgroundColor = Color.green;
               RemainingObjectsField.RegisterValueChangedCallback(evt => {if(evt.newValue == 0) ReadyBox.style.backgroundColor = Color.green; else ReadyBox.style.backgroundColor = Color.red;});
               Label ReadyLabel = new Label("All Objects Built");
               ReadyLabel.style.color = Color.black;
               ReadyBox.style.alignItems = Align.Center;
               ReadyBox.Add(ReadyLabel);
               EnclosingBox.Add(RemainingObjectsLabel);
               EnclosingBox.Add(RemainingObjectsField);
               EnclosingBox.Add(ReadyBox);
            MainSource.Add(EnclosingBox);

        }
        void Update() {
            if(Assets != null && Instancer != null) RemainingObjectsField.value = Assets.RunningTasks + Instancer.RunningTasks;
            if(RayMaster != null) SampleCountField.value = RayMaster.SampleCount;
            if(AssetManager.SelectedCamera == null) AssetManager.SelectedCamera = SelectedCamera;
            if(ReSTIRGI) {
               ASVGFToggle.value = false;
               ASVGF = false;
               RayMaster.UseASVGF = false;
            }
            if(Assets != null && Assets.NeedsToUpdateXML) {
               using(StreamWriter writer = new StreamWriter(Application.dataPath + "/TrueTrace/Resources/Utility/MaterialMappings.xml")) {
                  var serializer = new XmlSerializer(typeof(Materials));
                  serializer.Serialize(writer.BaseStream, AssetManager.data);
               }
               Assets.NeedsToUpdateXML = false;
            }
        }

   }

    public class PopupWarningWindow : PopupWindowContent
    {
        public override Vector2 GetWindowSize()
        {
            return new Vector2(460, 50);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label("This will Re-arrange your hierarchy and remove Static flags from objects", EditorStyles.boldLabel);
            if(GUILayout.Button("Proceed")) EditorWindow.GetWindow<EditModeFunctions>().ConfirmPopup();
        }
    }
}