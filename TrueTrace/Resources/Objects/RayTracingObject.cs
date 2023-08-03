using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CommonVars;

namespace TrueTrace {
	[ExecuteInEditMode][System.Serializable]
	public class RayTracingObject : MonoBehaviour {
		public enum Options {Diffuse, Disney, Cutout, Volumetric, Video};
		public Options[] MaterialOptions;
		[SerializeField] public Vector3[] TransmissionColor, BaseColor;
		[SerializeField] public float[] emmission; 
		[SerializeField] public Vector3[] EmissionColor;
		[SerializeField] public float[] Roughness;
		[SerializeField] public float[] IOR;
		[SerializeField] public float[] Metallic;
		[SerializeField] public float[] SpecularTint;
		[SerializeField] public float[] Sheen;
		[SerializeField] public float[] SheenTint;
		[SerializeField] public float[] ClearCoat;
		[SerializeField] public float[] ClearCoatGloss;
		[SerializeField] public float[] Anisotropic;
		[SerializeField] public float[] Flatness;
		[SerializeField] public float[] DiffTrans;
		[SerializeField] public float[] SpecTrans;
		[SerializeField] public int[] Thin;
		[SerializeField] public bool[] FollowMaterial;
		[SerializeField] public float[] ScatterDist;
		public string[] Names;
		[SerializeField] public float[] Specular;
		[SerializeField] public int Selected;
		public int[] Indexes;
		public bool NeedsToUpdate;
		[SerializeField] public bool IsReady = false;

		[HideInInspector] public int[] MaterialIndex;
		[HideInInspector] public int[] LocalMaterialIndex;
		AssetManager Assets;
		[HideInInspector] public bool JustCreated = true;
		private bool WasDeleted = false;

		public void CallMaterialOverride() {
			Material[] SharedMaterials = (GetComponent<Renderer>() != null) ? GetComponent<Renderer>().sharedMaterials : GetComponent<SkinnedMeshRenderer>().sharedMaterials;
			for(int i = 0; i < Names.Length; i++) {
				 if(FollowMaterial[i]) {
					int Index = AssetManager.ShaderNames.IndexOf(SharedMaterials[i].shader.name);
					 if (Index == -1) {
					 	Debug.Log("Material Not Added");
					 	return;
					 }
					 MaterialShader RelevantMat = AssetManager.data.Material[Index];
					if(!RelevantMat.MetallicRange.Equals("null")) Metallic[i] = SharedMaterials[i].GetFloat(RelevantMat.MetallicRange);
                    if(!RelevantMat.RoughnessRange.Equals("null")) Roughness[i] = SharedMaterials[i].GetFloat(RelevantMat.RoughnessRange);
                    if(!RelevantMat.BaseColorValue.Equals("null")) BaseColor[i] = new Vector3(SharedMaterials[i].GetColor(RelevantMat.BaseColorValue).r, SharedMaterials[i].GetColor(RelevantMat.BaseColorValue).g, SharedMaterials[i].GetColor(RelevantMat.BaseColorValue).b);
                    else BaseColor[i] = Vector3.one;
				 }

			}
			if(Assets == null) Assets = GameObject.Find("Scene").GetComponent<AssetManager>();
			if(gameObject.activeInHierarchy && Assets != null) Assets.MaterialsChanged.Add(this);
		}


		public void CallMaterialEdited() {
			if(Assets == null) Assets = GameObject.Find("Scene").GetComponent<AssetManager>();
			if(gameObject.activeInHierarchy && Assets != null) Assets.MaterialsChanged.Add(this);
			System.Array.Fill(FollowMaterial, false);
		}

		public void matfill() {
			WasDeleted = false;
			this.gameObject.isStatic = false;
			Assets = GameObject.Find("Scene").GetComponent<AssetManager>();
			 Mesh mesh = new Mesh();
			 int SubMeshCount;
			 if(GetComponent<MeshRenderer>() != null) { 
			 	mesh = GetComponent<MeshFilter>().sharedMesh;
			 	SubMeshCount = (GetComponent<MeshRenderer>().sharedMaterials).Length;
		 	} else {
		 		if(this.GetComponent<SkinnedMeshRenderer>() == null) DestroyImmediate(this);
		 		GetComponent<SkinnedMeshRenderer>().BakeMesh(mesh);
				SubMeshCount = (GetComponent<SkinnedMeshRenderer>().sharedMaterials).Length;
				// this.transform.localScale = new Vector3(1,1,1);
				// this.transform.position = new Vector3(0,0,0);
				// this.transform.eulerAngles = new Vector3(0,0,0);
		 	}
		 	Material[] SharedMaterials = (GetComponent<Renderer>() != null) ? GetComponent<Renderer>().sharedMaterials : GetComponent<SkinnedMeshRenderer>().sharedMaterials;
		 	if(mesh == null || SharedMaterials == null || SharedMaterials.Length == 0 || mesh.GetTopology(0) != MeshTopology.Triangles || mesh.vertexCount == 0) {
		 		DestroyImmediate(this);
		 		WasDeleted = true;
		 		return;
		 	}
			SubMeshCount = Mathf.Min(mesh.subMeshCount, SubMeshCount);
		 	if(SubMeshCount == 0) {
		 		DestroyImmediate(this);
		 		WasDeleted = true;
		 		return;
		 	}
			for(int i = 0; i < SharedMaterials.Length; i++) {	
				if(SharedMaterials[i] == null || SubMeshCount == 0) {
					Debug.LogError("GameObject " + this.name + " is Missing a Material and will NOT be Included");
					DestroyImmediate(this);
					WasDeleted = true;
					return;
				}
				if(SharedMaterials[i].shader.name.Contains("InternalErrorShader")) {
					SharedMaterials[i].shader = Shader.Find("Standard");
				}
			}
			if(ScatterDist == null || ScatterDist.Length != SubMeshCount) ScatterDist = new float[SubMeshCount];
			List<string> PropertyNames = new List<string>();
		 	if(Indexes == null || Indexes.Length != Mathf.Max(mesh.subMeshCount, SubMeshCount)) Indexes = new int[Mathf.Max(mesh.subMeshCount, SubMeshCount)];
		 	if(Specular == null || Specular.Length != SubMeshCount) Specular = new float[SubMeshCount];
			if(FollowMaterial == null || FollowMaterial.Length != SubMeshCount) {FollowMaterial = new bool[SubMeshCount]; System.Array.Fill(FollowMaterial, true);}
			try {
				if(Names == null || Names.Length == 0) {
					Names = new string[SubMeshCount];
					TransmissionColor = new Vector3[SubMeshCount];
					EmissionColor = new Vector3[SubMeshCount];				
					IOR = new float[SubMeshCount];
					Metallic = new float[SubMeshCount];
					SpecularTint = new float[SubMeshCount];
					Sheen = new float[SubMeshCount];
					SheenTint = new float[SubMeshCount];
					ClearCoat = new float[SubMeshCount];
					ClearCoatGloss = new float[SubMeshCount];
					Anisotropic = new float[SubMeshCount];
					Flatness = new float[SubMeshCount];
					DiffTrans = new float[SubMeshCount];
					SpecTrans = new float[SubMeshCount];
					Thin = new int[SubMeshCount];
					MaterialOptions = new Options[SubMeshCount];
					LocalMaterialIndex = new int[SubMeshCount];
					emmission = new float[SubMeshCount];
					Roughness = new float[SubMeshCount];
					System.Array.Fill(IOR, 1);
					BaseColor = new Vector3[SubMeshCount];
					MaterialIndex = new int[SubMeshCount];
					ScatterDist = new float[SubMeshCount];
					for(int i = 0; i < SubMeshCount; i++) {
						MaterialOptions[i] = Options.Disney;
						Names[i] = SharedMaterials[i].name;
						BaseColor[i] = new Vector3(1,1,1);
					}
				}
			} catch(System.Exception e) {
				Debug.Log("ERROR AT: " + this.gameObject.name + ": " + e);
			}
			IsReady = true;
			mesh = null;
		}

		public void ResetData() {
			emmission = null;
			Roughness = null;
			TransmissionColor = null;
			MaterialOptions = null;
			BaseColor = null;
		}
		
	    private void OnEnable() {
	    	// if(this.gameObject.GetComponent<SkinnedMeshRenderer>() != null) this.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
	    	if(gameObject.scene.isLoaded && this.transform.parent.GetComponent<ParentObject>() != null) {
	    		matfill();
	    		if(WasDeleted) return;
		    	this.transform.parent.GetComponent<ParentObject>().NeedsToUpdate = true;
				if(Assets != null && Assets.UpdateQue != null && !Assets.UpdateQue.Contains(this.transform.parent.GetComponent<ParentObject>())) Assets.UpdateQue.Add(this.transform.parent.GetComponent<ParentObject>());
	    	} else if(gameObject.scene.isLoaded && this.transform.GetComponent<ParentObject>() != null) {
	    		matfill();
	    		if(WasDeleted) return;
				if(Assets != null && Assets.UpdateQue != null && !Assets.UpdateQue.Contains(this.transform.parent.GetComponent<ParentObject>())) Assets.UpdateQue.Add(this.transform.parent.GetComponent<ParentObject>());
		    	this.transform.GetComponent<ParentObject>().NeedsToUpdate = true;
	    	}
	    }

	    private void OnDisable() {
	    	if(gameObject.scene.isLoaded && this.transform.parent.GetComponent<ParentObject>() != null) {
	    		this.transform.parent.GetComponent<ParentObject>().NeedsToUpdate = true;
	    		if(Assets != null && Assets.UpdateQue != null && !Assets.UpdateQue.Contains(this.transform.parent.GetComponent<ParentObject>())) Assets.UpdateQue.Add(this.transform.parent.GetComponent<ParentObject>());
	    	} else if(gameObject.scene.isLoaded && this.transform.GetComponent<ParentObject>() != null) {
		    	this.transform.GetComponent<ParentObject>().NeedsToUpdate = true;
	    	}
	    }

	}
}