// Resource Checker
// (c) 2012 Simon Oliver / HandCircus / hello@handcircus.com
// (c) 2015 Brice Clocher / Mangatome / hello@mangatome.net
// Public domain, do with whatever you like, commercial or not
// This comes with no warranty, use at your own risk!
// https://github.com/handcircus/Unity-Resource-Checker

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
//using UnityEngine.U2D;
using Application = UnityEngine.Application;
using Object = UnityEngine.Object;

public class TextureDetails : IEquatable<TextureDetails>
{
	public bool isCubeMap;
	public float memSizeKB;
	public Texture texture;
	public TextureFormat format;
	public int mipMapCount;
	public List<Object> FoundInMaterials=new List<Object>();
	public List<Object> FoundInRenderers=new List<Object>();
	public List<Object> FoundInAnimators = new List<Object>();
	public List<Object> FoundInScripts = new List<Object>();
	public List<Object> FoundInGraphics = new List<Object>();
	public List<Object> FoundInButtons = new List<Object>();
	//public List<Object> FoundInProjectFolder = new List<Object>();
	public bool isSky;
	public bool instance;
	public bool isgui;
	public TextureDetails() {}

    public bool Equals(TextureDetails other)
    {
        return texture != null && other.texture != null &&
			texture.GetNativeTexturePtr() == other.texture.GetNativeTexturePtr();
    }

    public override int GetHashCode()
    {
		return (int)texture.GetNativeTexturePtr();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TextureDetails);
    }
};

public class MaterialDetails
{

	public Material material;

	public List<Renderer> FoundInRenderers=new List<Renderer>();
	public List<Graphic> FoundInGraphics=new List<Graphic>();
	public bool instance;
	public bool isgui;
	public bool isSky;

	public MaterialDetails()
	{
		instance = false;
		isgui = false;
		isSky = false;
	}
};

public class MeshDetails
{

	public Mesh mesh;

	public List<MeshFilter> FoundInMeshFilters=new List<MeshFilter>();
	public List<SkinnedMeshRenderer> FoundInSkinnedMeshRenderer=new List<SkinnedMeshRenderer>();
	public List<GameObject> StaticBatchingEnabled =new List<GameObject>();
	public bool instance;

	public MeshDetails()
	{
		instance = false;
	}
};

public class ClipDetails
{

	public AudioClip clip;

	public List<AudioSource> FoundInAudioSources = new List<AudioSource>();

	public ClipDetails() {}
};

public class Missing{
	public Transform Object;
	public string type;
	public string name;
}

public class ResourceChecker : EditorWindow {


	string[] inspectToolbarStrings = {"Textures", "Materials", "Meshes", "Audio" };
	string[] inspectToolbarStrings2 = {"Textures", "Materials", "Meshes", "Audio", "Missing" };

	enum InspectType 
	{
		Textures, Materials, Meshes, AudioClips, Missing
	};

	bool IncludeDisabledObjects=true;
	bool IncludeSpriteAnimations=true;
	bool IncludeScriptReferences=true;
	bool IncludeGuiElements=true;
	bool IncludeLightmapTextures=true;
	bool IncludeSelectedFolder = false;
	bool thingsMissing = false;

	InspectType ActiveInspectType=InspectType.Textures;

	float ThumbnailWidth=40;
	float ThumbnailHeight=40;

	List<TextureDetails> ActiveTextures=new List<TextureDetails>();
	List<MaterialDetails> ActiveMaterials=new List<MaterialDetails>();
	List<MeshDetails> ActiveMeshDetails=new List<MeshDetails>();
	List<ClipDetails> ActiveClipDetails = new List<ClipDetails>();
	List<Missing> MissingObjects = new List<Missing> ();

	Vector2 textureListScrollPos=new Vector2(0,0);
	Vector2 materialListScrollPos=new Vector2(0,0);
	Vector2 meshListScrollPos=new Vector2(0,0);
	Vector2 audioListScrollPos = new Vector2(0, 0);
	Vector2 missingListScrollPos = new Vector2 (0,0);

	float TotalTextureMemory=0;
	int TotalMeshVertices=0;

	bool ctrlPressed=false;

	static int MinWidth=475;
	Color defColor;

	bool collectedInPlayingMode;

	System.Text.StringBuilder tmpStringBuilder = new();

	[MenuItem ("Window/Resource Checker")]
	static void Init ()
	{  
		ResourceChecker window = (ResourceChecker) EditorWindow.GetWindow (typeof (ResourceChecker));
		window.CheckResources();
		window.minSize=new Vector2(MinWidth,475);
	}

	void OnGUI ()
	{
		defColor = GUI.color;
		IncludeDisabledObjects = GUILayout.Toggle(IncludeDisabledObjects, "Include disabled objects", GUILayout.Width(300));
		IncludeSpriteAnimations = GUILayout.Toggle(IncludeSpriteAnimations, "Look in sprite animations (Textures)", GUILayout.Width(300));
		GUI.color = new Color (0.8f, 0.8f, 1.0f, 1.0f);
		IncludeScriptReferences = GUILayout.Toggle(IncludeScriptReferences, "Look in behavior fields (Textures, mats, meshes)", GUILayout.Width(300));
		GUI.color = new Color (1.0f, 0.95f, 0.8f, 1.0f);
		IncludeGuiElements = GUILayout.Toggle(IncludeGuiElements, "Look in GUI elements (Textures, mats)", GUILayout.Width(300));
		IncludeLightmapTextures = GUILayout.Toggle(IncludeLightmapTextures, "Look in Lightmap textures", GUILayout.Width(300));
		IncludeSelectedFolder = GUILayout.Toggle(IncludeSelectedFolder, "Look in Selected Folders (Textures, Audio)", GUILayout.Width(300));
		GUI.color = defColor;
		
		GUILayout.BeginArea(new Rect(position.width-85,5,100,85));
		if (GUILayout.Button("Calculate",GUILayout.Width(80), GUILayout.Height(40)))
			CheckResources();
		if (GUILayout.Button("CleanUp",GUILayout.Width(80), GUILayout.Height(20)))
			Resources.UnloadUnusedAssets();
		/*if (GUILayout.Button("Log", GUILayout.Width(80), GUILayout.Height(20)))
		{
			// TODO
			// Write to log file
		}*/
		GUILayout.EndArea();
		RemoveDestroyedResources();

		GUILayout.Space(120);
		if (thingsMissing == true) {
			EditorGUI.HelpBox (new Rect(8,135,300,25),"Some GameObjects are missing elements.", MessageType.Error);
		}
		EditorGUI.HelpBox(new Rect(8, 165, 300, 25), "It always checks GOs in opened scenes.", MessageType.Info);
		EditorGUI.HelpBox(new Rect(8, 195, 300, 25), "Crunched formats use VRAM as uncrunched ones.", MessageType.Warning);

		GUILayout.BeginHorizontal();
		GUILayout.Label("Textures "+ActiveTextures.Count+" - "+FormatSizeString(TotalTextureMemory));
		GUILayout.Label("Materials "+ActiveMaterials.Count);
		GUILayout.Label("Meshes "+ActiveMeshDetails.Count+" - "+TotalMeshVertices+" verts");
		GUILayout.Label("Audio Clips "+ActiveClipDetails.Count);
		if (thingsMissing == true)
		{
			GUILayout.Label("Missings " + MissingObjects.Count);
		}
		GUILayout.EndHorizontal();

		if (thingsMissing == true)
		{
			
			ActiveInspectType = (InspectType)GUILayout.Toolbar((int)ActiveInspectType, inspectToolbarStrings2);
		}
		else
		{
			ActiveInspectType = (InspectType)GUILayout.Toolbar((int)ActiveInspectType, inspectToolbarStrings);
		}

		ctrlPressed =Event.current.control || Event.current.command;

		switch (ActiveInspectType)
		{
		case InspectType.Textures:
			ListTextures();
			break;
		case InspectType.Materials:
			ListMaterials();
			break;
		case InspectType.Meshes:
			ListMeshes();
			break;
		case InspectType.Missing:
			ListMissing();
			break;
		case InspectType.AudioClips:
			ListAudioClips();
			break;		
		}
	}

	private void RemoveDestroyedResources()
	{
		if (collectedInPlayingMode != Application.isPlaying)
		{
			ActiveTextures.Clear();
			ActiveMaterials.Clear();
			ActiveMeshDetails.Clear();
			ActiveClipDetails.Clear();
			MissingObjects.Clear();
			thingsMissing = false;
			collectedInPlayingMode = Application.isPlaying;
		}
		ActiveClipDetails.RemoveAll(x => !x.clip);
		ActiveClipDetails.ForEach(delegate (ClipDetails obj) {
			obj.FoundInAudioSources.RemoveAll(x => !x);
		});
		ActiveTextures.RemoveAll(x => !x.texture);
		ActiveTextures.ForEach(delegate(TextureDetails obj) {
			obj.FoundInAnimators.RemoveAll(x => !x);
			obj.FoundInMaterials.RemoveAll(x => !x);
			obj.FoundInRenderers.RemoveAll(x => !x);
			obj.FoundInScripts.RemoveAll(x => !x);
			obj.FoundInGraphics.RemoveAll(x => !x);
			//obj.FoundInProjectFolder.RemoveAll(x => !x);
		});

		ActiveMaterials.RemoveAll(x => !x.material);
		ActiveMaterials.ForEach(delegate(MaterialDetails obj) {
			obj.FoundInRenderers.RemoveAll(x => !x);
			obj.FoundInGraphics.RemoveAll(x => !x);
		});

		ActiveMeshDetails.RemoveAll(x => !x.mesh);
		ActiveMeshDetails.ForEach(delegate(MeshDetails obj) {
			obj.FoundInMeshFilters.RemoveAll(x => !x);
			obj.FoundInSkinnedMeshRenderer.RemoveAll(x => !x);
			obj.StaticBatchingEnabled.RemoveAll(x => !x);
		});

		TotalTextureMemory = 0;
		foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

		TotalMeshVertices = 0;
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;
	}

    float GetBitsPerPixel(TextureFormat format)
	{
		#pragma warning disable CS0618 // Type PVRTC is obsolete
        switch (format)
		{
			case TextureFormat.Alpha8: //	 Alpha-only texture format.
				return 8;
			case TextureFormat.ARGB4444: //	 A 16 bits/pixel texture format. Texture stores color with an alpha channel.
				return 16;
			case TextureFormat.RGBA4444: //	 A 16 bits/pixel texture format. Texture stores color with an alpha channel.
				return 16;
			case TextureFormat.RGB24:	// A color texture format.
				return 24;
			case TextureFormat.RGBA32:	// Color with an alpha channel texture format.
				return 32;
			case TextureFormat.ARGB32:	// Color with an alpha channel texture format.
				return 32;
			case TextureFormat.RGB9e5Float:	// Floating-point color texture format.
				return 32;
			case TextureFormat.RGB565:	//	 A 16 bit color texture format.
				return 16;
			case TextureFormat.DXT1:	// Compressed color texture format.
				return 4;
			case TextureFormat.DXT1Crunched:    // Crunched formats uses VRAM as uncrunched ones.
				return 4;
			case TextureFormat.DXT5:	// Compressed color with alpha channel texture format.
				return 8;
			case TextureFormat.DXT5Crunched: // Crunched formats uses VRAM as uncrunched ones.
				return 8;
			case TextureFormat.BC4:    // Compressed R channel texture format. 
				return 4;
			case TextureFormat.BC5:    // Compressed color with alpha channel texture format.
				return 8;
			case TextureFormat.BC6H:    // Compressed HDR color texture format.
				return 8;
			case TextureFormat.BC7:    // Compressed color with alpha channel texture format.
				return 8;
				/*
				case TextureFormat.WiiI4:	// Wii texture format.
				case TextureFormat.WiiI8:	// Wii texture format. Intensity 8 bit.
				case TextureFormat.WiiIA4:	// Wii texture format. Intensity + Alpha 8 bit (4 + 4).
				case TextureFormat.WiiIA8:	// Wii texture format. Intensity + Alpha 16 bit (8 + 8).
				case TextureFormat.WiiRGB565:	// Wii texture format. RGB 16 bit (565).
				case TextureFormat.WiiRGB5A3:	// Wii texture format. RGBA 16 bit (4443).
				case TextureFormat.WiiRGBA8:	// Wii texture format. RGBA 32 bit (8888).
				case TextureFormat.WiiCMPR:	//	 Compressed Wii texture format. 4 bits/texel, ~RGB8A1 (Outline alpha is not currently supported).
					return 0;  //Not supported yet
				*/
			case TextureFormat.PVRTC_RGB2://	 PowerVR (iOS) 2 bits/pixel compressed color texture format.
				return 2;
			case TextureFormat.PVRTC_RGBA2://	 PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format
				return 2;
			case TextureFormat.PVRTC_RGB4://	 PowerVR (iOS) 4 bits/pixel compressed color texture format.
				return 4;
			case TextureFormat.PVRTC_RGBA4://	 PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format
				return 4;
			case TextureFormat.ETC_RGB4:
				return 4;
			case TextureFormat.ETC_RGB4Crunched: // Crunched formats uses VRAM as uncrunched ones.
				return 4;
			case TextureFormat.ETC2_RGBA8:
				return 8;
			case TextureFormat.ETC2_RGB:
				return 4;
			case TextureFormat.ETC2_RGBA8Crunched: // Crunched format uses VRAM as uncrunched one.
				return 4;
			case TextureFormat.EAC_R:
				return 4;
			case TextureFormat.BGRA32://	 Format returned by iPhone camera
				return 32;		
			case TextureFormat.ASTC_4x4:
				return 8;
			case TextureFormat.ASTC_5x5:
				return 5.12f;	
			case TextureFormat.ASTC_6x6:
				return 3.56f;	
			case TextureFormat.ASTC_8x8:
				return 2;	
			case TextureFormat.ASTC_10x10:
				return 1.28f;	
			case TextureFormat.ASTC_12x12:
				return 0.89f;	
			case TextureFormat.ASTC_HDR_4x4:
				return 8;
			case TextureFormat.ASTC_HDR_5x5:
				return 5.12f;
			case TextureFormat.ASTC_HDR_6x6:
				return 3.56f;
			case TextureFormat.ASTC_HDR_8x8:
				return 2;
			case TextureFormat.ASTC_HDR_10x10:
				return 1.28f;
			case TextureFormat.ASTC_HDR_12x12:
				return 0.89f;
		}
		#pragma warning restore CS0618 // Type PVRTC is obsolete
        return 0;
	}

	float CalculateTextureSizeBytes(Texture tTexture)
	{

		int tWidth=tTexture.width;
		int tHeight=tTexture.height;
		if (tTexture is Texture2D)
		{
			Texture2D tTex2D=tTexture as Texture2D;
			float bitsPerPixel=GetBitsPerPixel(tTex2D.format);
			int mipMapCount=tTex2D.mipmapCount;
			int mipLevel=1;
			float tSize=0;
			while (mipLevel<=mipMapCount)
			{
				tSize+=tWidth*tHeight*bitsPerPixel/8;
				tWidth=tWidth/2;
				tHeight=tHeight/2;
				mipLevel++;
			}
			return tSize;
		}
		//TODO: Calculate size precisely
		if (tTexture is Texture2DArray)
		{
			Texture2DArray tTex2D = tTexture as Texture2DArray;
			float bitsPerPixel = GetBitsPerPixel(tTex2D.format);
			int mipMapCount = 10;
			int mipLevel = 0;
			float tSize = 0;
			while (mipLevel <= mipMapCount)
			{
				tSize += tWidth * tHeight * bitsPerPixel / 8;
				tWidth = tWidth / 2;
				tHeight = tHeight / 2;
				mipLevel++;
			}
			return tSize * ((Texture2DArray)tTex2D).depth;
		}
		//TODO: Calculate size of cubemap depending on mapping type (Cubic, Cylindrical, Spheremap)
		if (tTexture is Cubemap)
		{
			Cubemap tCubemap = tTexture as Cubemap;
			float bitsPerPixel = GetBitsPerPixel(tCubemap.format);
			int mipMapCount = tCubemap.mipmapCount;
			int mipLevel = 0;
			float tSize = 0;
			while (mipLevel <= mipMapCount)
			{
				tSize += tWidth * tHeight * bitsPerPixel / 8;
				tWidth = tWidth / 2;
				tHeight = tHeight / 2;
				mipLevel++;
			}
			return tSize * 6;
		}
		return 0;
	}
	
	void SelectObject(Object selectedObject,bool append)
	{
		if (append)
		{
			List<Object> currentSelection=new List<Object>(Selection.objects);
			// Allow toggle selection
			if (currentSelection.Contains(selectedObject)) currentSelection.Remove(selectedObject);
			else currentSelection.Add(selectedObject);

			Selection.objects=currentSelection.ToArray();
		}
		else Selection.activeObject=selectedObject;
	}

	void SelectObjects(List<Object> selectedObjects,bool append)
	{
		if (append)
		{
			List<Object> currentSelection=new List<Object>(Selection.objects);
			currentSelection.AddRange(selectedObjects);
			Selection.objects=currentSelection.ToArray();
		}
		else Selection.objects=selectedObjects.ToArray();
	}

	void ListTextures()
	{
		textureListScrollPos = EditorGUILayout.BeginScrollView(textureListScrollPos);

		foreach (TextureDetails tDetails in ActiveTextures)
		{			

			GUILayout.BeginHorizontal ();
			Texture tex =tDetails.texture;			
			if(tDetails.texture.GetType() == typeof(Texture2DArray) || tDetails.texture.GetType() == typeof(Cubemap)){
				tex = AssetPreview.GetMiniThumbnail(tDetails.texture);
			}
			GUILayout.Box(tex, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

			if (tDetails.instance == true)
				GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
			if (tDetails.isgui == true)
				GUI.color = new Color (defColor.r, 0.95f, 0.8f, 1.0f);
			if (tDetails.isSky)
				GUI.color = new Color (0.9f, defColor.g, defColor.b, 1.0f);
			if(GUILayout.Button(tDetails.texture.name,GUILayout.Width(150)))
			{
				SelectObject(tDetails.texture,ctrlPressed);
			}
			GUI.color = defColor;

			string sizeLabel=""+tDetails.texture.width+"x"+tDetails.texture.height;
			if (tDetails.isCubeMap) sizeLabel+="x6";
			if (tDetails.texture.GetType() == typeof(Texture2DArray))
				sizeLabel+= "[]\n" + ((Texture2DArray)tDetails.texture).depth+"depths";
			sizeLabel+=" - "+tDetails.mipMapCount+"mip\n"+FormatSizeString(tDetails.memSizeKB)+" - "+tDetails.format;

			GUILayout.Label (sizeLabel,GUILayout.Width(120));

			if(GUILayout.Button(tDetails.FoundInMaterials.Count+" Mat",GUILayout.Width(50)))
			{
				SelectObjects(tDetails.FoundInMaterials,ctrlPressed);
			}

			HashSet<Object> FoundObjects = new HashSet<Object>();
			foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
			foreach (Animator animator in tDetails.FoundInAnimators) FoundObjects.Add(animator.gameObject);
			foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
			foreach (Button button in tDetails.FoundInButtons) FoundObjects.Add(button.gameObject);
			foreach (MonoBehaviour script in tDetails.FoundInScripts) FoundObjects.Add(script.gameObject);
			if (GUILayout.Button(FoundObjects.Count+" GO",GUILayout.Width(50)))
			{
				SelectObjects(new List<Object>(FoundObjects),ctrlPressed);
			}
			GUILayout.Label(AssetDatabase.GetAssetPath(tDetails.texture), GUILayout.Width(1000));
			GUILayout.EndHorizontal();	
		}
		if (ActiveTextures.Count>0)
		{
			EditorGUILayout.Space();
			GUILayout.BeginHorizontal ();
			//GUILayout.Box(" ",GUILayout.Width(ThumbnailWidth),GUILayout.Height(ThumbnailHeight));
			if(GUILayout.Button("Select \n All",GUILayout.Width(ThumbnailWidth*2)))
			{
				List<Object> AllTextures=new List<Object>();
				foreach (TextureDetails tDetails in ActiveTextures) AllTextures.Add(tDetails.texture);
				SelectObjects(AllTextures,ctrlPressed);
			}
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndScrollView();
	}

	void ListMaterials()
	{
		materialListScrollPos = EditorGUILayout.BeginScrollView(materialListScrollPos);

		foreach (MaterialDetails tDetails in ActiveMaterials)
		{			
			if (tDetails.material!=null)
			{
				GUILayout.BeginHorizontal ();

				GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.material), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

				if (tDetails.instance == true)
					GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				if (tDetails.isgui == true)
					GUI.color = new Color (defColor.r, 0.95f, 0.8f, 1.0f);
				if (tDetails.isSky)
					GUI.color = new Color (0.9f, defColor.g, defColor.b, 1.0f);
				if(GUILayout.Button(tDetails.material.name,GUILayout.Width(150)))
				{
					SelectObject(tDetails.material,ctrlPressed);
				}
				GUI.color = defColor;

				string shaderLabel = tDetails.material.shader != null ? tDetails.material.shader.name : "no shader";
				shaderLabel += "\n" + AssetDatabase.GetAssetPath(tDetails.material);
				GUILayout.Label (shaderLabel, GUILayout.Width(400));

				if(GUILayout.Button((tDetails.FoundInRenderers.Count + tDetails.FoundInGraphics.Count) +" GO",GUILayout.Width(50)))
				{
					List<Object> FoundObjects=new List<Object>();
					foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
					foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
					SelectObjects(FoundObjects,ctrlPressed);
				}

				var queue = tDetails.material.renderQueue;
				EditorGUI.BeginChangeCheck();
				queue = EditorGUILayout.DelayedIntField(queue, GUILayout.Width(40));
				if (EditorGUI.EndChangeCheck())
				{
					tDetails.material.renderQueue = queue;
					ActiveMaterials.Sort(MaterialSorter);
					GUIUtility.ExitGUI();
					break;
				}
				//GUILayout.Label(AssetDatabase.GetAssetPath(tDetails.material), GUILayout.Width(300));
				GUILayout.EndHorizontal();	
			}
		}
		EditorGUILayout.EndScrollView();		
	}

	/// <summary>
	/// Sort by RenderQueue
	/// </summary>
	static int MaterialSorter(MaterialDetails first, MaterialDetails second)
	{
		var firstIsNull = first.material == null;
		var secondIsNull = second.material == null;
		
		if (firstIsNull && secondIsNull) return 0;
		if (firstIsNull) return int.MaxValue;
		if (secondIsNull) return int.MinValue;

		return first.material.renderQueue - second.material.renderQueue;
	}
	void ListMeshes()
	{
		meshListScrollPos = EditorGUILayout.BeginScrollView(meshListScrollPos);

		foreach (MeshDetails tDetails in ActiveMeshDetails)
		{			
			if (tDetails.mesh!=null)
			{
				GUILayout.BeginHorizontal ();
				GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.mesh), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
				string name = tDetails.mesh.name;
				if (name == null || name.Count() < 1)
					name = tDetails.FoundInMeshFilters[0].gameObject.name;
				if (tDetails.instance == true)
					GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				if(GUILayout.Button(name,GUILayout.Width(150)))
				{
					SelectObject(tDetails.mesh,ctrlPressed);
				}
				GUI.color = defColor;
				string sizeLabel=""+tDetails.mesh.vertexCount+" vert";

				GUILayout.Label (sizeLabel,GUILayout.Width(100));


				if(GUILayout.Button(tDetails.FoundInMeshFilters.Count + " GO",GUILayout.Width(50)))
				{
					List<Object> FoundObjects=new List<Object>();
					foreach (MeshFilter meshFilter in tDetails.FoundInMeshFilters) FoundObjects.Add(meshFilter.gameObject);
					SelectObjects(FoundObjects,ctrlPressed);
				}
				if (tDetails.FoundInSkinnedMeshRenderer.Count > 0) {
					if (GUILayout.Button (tDetails.FoundInSkinnedMeshRenderer.Count + " skinned mesh GO", GUILayout.Width (120))) {
						List<Object> FoundObjects = new List<Object> ();
						foreach (SkinnedMeshRenderer skinnedMeshRenderer in tDetails.FoundInSkinnedMeshRenderer)
							FoundObjects.Add (skinnedMeshRenderer.gameObject);
						SelectObjects (FoundObjects, ctrlPressed);
					}
				} else {
					GUI.color = new Color (defColor.r, defColor.g, defColor.b, 0.5f);
					GUILayout.Label(" 0 skinned mesh", GUILayout.Width(120));
					GUI.color = defColor;
				}
				
				if (tDetails.StaticBatchingEnabled.Count > 0) {
					if (GUILayout.Button (tDetails.StaticBatchingEnabled.Count + " Static Batching", GUILayout.Width (140))) {
						List<Object> FoundObjects = new List<Object> ();
						foreach (var obj in tDetails.StaticBatchingEnabled)
							FoundObjects.Add (obj);
						SelectObjects (FoundObjects, ctrlPressed);
					}
				} else {
					GUI.color = new Color (defColor.r, defColor.g, defColor.b, 0.5f);
					GUILayout.Label(" 0 static batching", GUILayout.Width(120));
					GUI.color = defColor;
				}

				GUILayout.Label(AssetDatabase.GetAssetPath(tDetails.mesh), GUILayout.Width(300));
				GUILayout.EndHorizontal();	
			}
		}
		EditorGUILayout.EndScrollView();		
	}
	void ListMissing(){
		missingListScrollPos = EditorGUILayout.BeginScrollView(missingListScrollPos);
		foreach (Missing dMissing in MissingObjects) {
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button (dMissing.name, GUILayout.Width (150)))
				SelectObject (dMissing.Object, ctrlPressed);
			GUILayout.Label ("missing ", GUILayout.Width(48));
			switch (dMissing.type) {
			case "lod":
				GUI.color = new Color (defColor.r, defColor.b, 0.8f, 1.0f);
				break;
			case "mesh":
				GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				break;
			case "sprite":
				GUI.color = new Color (defColor.r, 0.8f, 0.8f, 1.0f);
				break;
			case "material":
				GUI.color = new Color (0.8f, defColor.g, 0.8f, 1.0f);
				break;
			}
			GUILayout.Label (dMissing.type);
			GUI.color = defColor;
			GUILayout.EndHorizontal ();
		}
		EditorGUILayout.EndScrollView();
	}
	void ListAudioClips() {
		audioListScrollPos = EditorGUILayout.BeginScrollView(audioListScrollPos);
		foreach (var aDetails in ActiveClipDetails) {
			AudioClip clip = aDetails.clip;
			GUILayout.BeginHorizontal();
			GUILayout.Box(AssetPreview.GetAssetPreview(clip), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
			if (GUILayout.Button(clip.name, GUILayout.Width(150)))
			{
				SelectObject(clip, ctrlPressed);
			}
			GUI.color = defColor;

			string audioLabel = "Chs: " + clip.channels + " - " + clip.frequency + " Hz";
			//if (audio.clip.)
				//audioLabel += "[]\n" + "";
			audioLabel += "\n" + clip.length + " s";

			GUILayout.Label(audioLabel, GUILayout.Width(140));

			HashSet<Object> FoundObjects = new HashSet<Object>();
			foreach (AudioSource source in aDetails.FoundInAudioSources) FoundObjects.Add(source.gameObject);
			if (GUILayout.Button(FoundObjects.Count + " GO", GUILayout.Width(50)))
			{
				SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
			}

			GUILayout.Label(AssetDatabase.GetAssetPath(clip), GUILayout.Width(300));
			GUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();
	}

	string FormatSizeString(float memSizeKB)
	{
		if (memSizeKB<1024) return ""+memSizeKB+"k";
		else
		{
			float memSizeMB=((float)memSizeKB)/1024.0f;
			return memSizeMB.ToString("0.00")+"Mb";
		}
	}
	
	TextureDetails FindTextureDetails(Texture tTexture)
	{
		foreach (TextureDetails tTextureDetails in ActiveTextures)
		{
			if (tTextureDetails.texture==tTexture) return tTextureDetails;
		}
		return null;

	}

	MaterialDetails FindMaterialDetails(Material tMaterial)
	{
		foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
		{
			if (tMaterialDetails.material==tMaterial) return tMaterialDetails;
		}
		return null;

	}

	MeshDetails FindMeshDetails(Mesh tMesh)
	{
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails)
		{
			if (tMeshDetails.mesh==tMesh) return tMeshDetails;
		}
		return null;

	}

	ClipDetails FindClipDetails(AudioClip tClip)
	{
		foreach (ClipDetails tClipDetails in ActiveClipDetails)
		{
			if (tClipDetails.clip == tClip) return tClipDetails;
		}
		return null;

	}

	void CheckResources()
	{
		ActiveTextures.Clear();
		ActiveMaterials.Clear();
		ActiveMeshDetails.Clear();
		MissingObjects.Clear();
		ActiveClipDetails.Clear();
		thingsMissing = false;

		ReflectionProbe[] reflectionProbes = FindObjects<ReflectionProbe>();

		foreach (ReflectionProbe reflectionProbe in reflectionProbes)
		{
			if (reflectionProbe.bakedTexture != null)
			{
				ActiveTextures.Add(GetTextureDetail(reflectionProbe.bakedTexture));
			}
			if (reflectionProbe.customBakedTexture != null)
			{
				ActiveTextures.Add(GetTextureDetail(reflectionProbe.customBakedTexture));
			}
		}

		if (RenderSettings.customReflectionTexture != null)
		{
			ActiveTextures.Add(GetTextureDetail(RenderSettings.customReflectionTexture));
		}

		Renderer[] renderers = FindObjects<Renderer>();

		MaterialDetails skyMat = new MaterialDetails ();
		skyMat.material = RenderSettings.skybox;
		skyMat.isSky = true;
		ActiveMaterials.Add(skyMat);

		//Debug.Log("Total renderers "+renderers.Length);
		foreach (Renderer renderer in renderers)
		{
			//Debug.Log("Renderer is "+renderer.name);
			foreach (Material material in renderer.sharedMaterials)
			{

				MaterialDetails tMaterialDetails = FindMaterialDetails(material);
				if (tMaterialDetails == null)
				{
					tMaterialDetails = new MaterialDetails();
					tMaterialDetails.material = material;
					ActiveMaterials.Add(tMaterialDetails);
				}
				tMaterialDetails.FoundInRenderers.Add(renderer);
			}

			if (renderer is SpriteRenderer)
			{
				SpriteRenderer tSpriteRenderer = (SpriteRenderer)renderer;

				if (tSpriteRenderer.sprite != null) {
					var tSpriteTextureDetail = GetTextureDetail(tSpriteRenderer.sprite.texture, renderer);
					if (!ActiveTextures.Contains (tSpriteTextureDetail)) {
						ActiveTextures.Add(tSpriteTextureDetail);
					}
#if UNITY_2021 || UNITY_2020
					for (int i = 0; i < 3; i++) //TODO Get secondaries array length instead
					{
						if (tSpriteRenderer.sprite.getSecondaryTexture(i) == null) continue;
						var tSpriteSecondaryTextureDetail = GetTextureDetail(tSpriteRenderer.sprite.getSecondaryTexture(i), renderer);
						if (!ActiveTextures.Contains(tSpriteSecondaryTextureDetail)) {
							ActiveTextures.Add(tSpriteSecondaryTextureDetail);
						}
					}
#else
					var secondarySpriteTextureResult = new SecondarySpriteTexture[tSpriteRenderer.sprite.GetSecondaryTextureCount()];
					tSpriteRenderer.sprite.GetSecondaryTextures(secondarySpriteTextureResult);
					foreach (var sst in secondarySpriteTextureResult)
					{
						var tSpriteSecondaryTextureDetail = GetTextureDetail(sst.texture, renderer);
						if (!ActiveTextures.Contains(tSpriteSecondaryTextureDetail))
						{
							ActiveTextures.Add(tSpriteSecondaryTextureDetail);
						}
					}
#endif
					if (!ActiveTextures.Contains (tSpriteTextureDetail)) {
						ActiveTextures.Add(tSpriteTextureDetail);
					}
				} else if (tSpriteRenderer.sprite == null) {
					Missing tMissing = new Missing ();
					tMissing.Object = tSpriteRenderer.transform;
					tMissing.type = "sprite";
					tMissing.name = tSpriteRenderer.transform.name;
					MissingObjects.Add(tMissing);
					thingsMissing = true;
				}
			}

			/*if (renderer is SpriteShapeRenderer)
			{
				var s = renderer.gameObject.GetComponent<SpriteShapeController>();
				if (s == null) continue;
				if (s.spriteShape.fillTexture != null) {
					var tShapeFillTextureDetail = GetTextureDetail(s.spriteShape.fillTexture, renderer);
					if (!ActiveTextures.Contains(tShapeFillTextureDetail)) {
						ActiveTextures.Add(tShapeFillTextureDetail);
					}
				}

				var angles = s.spriteShape.angleRanges;
				foreach (var angle in angles) {
					for (int i = 0; i < angle.sprites.Count; i++)
					{
						if (angle.sprites[i] == null) continue;
						var tShapeAngleTextureDetail = GetTextureDetail(angle.sprites[i].texture, renderer);
						if (!ActiveTextures.Contains(tShapeAngleTextureDetail))
						{
							ActiveTextures.Add(tShapeAngleTextureDetail);
						}
					}
				}
			}*/
		}

		if (IncludeLightmapTextures) {
			LightmapData[] lightmapTextures = LightmapSettings.lightmaps;

			// Unity lightmaps
			foreach (LightmapData lightmapData in lightmapTextures)
			{
				if (lightmapData.lightmapColor != null) 
				{
					var textureDetail = GetTextureDetail (lightmapData.lightmapColor);
					
					if (!ActiveTextures.Contains (textureDetail)) 
						ActiveTextures.Add (textureDetail);
				}
				
				if (lightmapData.lightmapDir != null) 
				{
					var textureDetail = GetTextureDetail (lightmapData.lightmapDir);
					
					if (!ActiveTextures.Contains (textureDetail)) 
						ActiveTextures.Add (textureDetail);
				}
				
				if (lightmapData.shadowMask != null) 
				{
					var textureDetail = GetTextureDetail (lightmapData.shadowMask);
					
					if (!ActiveTextures.Contains (textureDetail)) 
						ActiveTextures.Add (textureDetail);
				}
			}
		}
		
		if (IncludeGuiElements)
		{
			Graphic[] graphics = FindObjects<Graphic>();

			foreach(Graphic graphic in graphics)
			{
				if (graphic.mainTexture)
				{
					var tSpriteTextureDetail = GetTextureDetail(graphic.mainTexture, graphic);
					if (!ActiveTextures.Contains(tSpriteTextureDetail))
					{
						ActiveTextures.Add(tSpriteTextureDetail);
					}
				}

				if (graphic.materialForRendering)
				{
					MaterialDetails tMaterialDetails = FindMaterialDetails(graphic.materialForRendering);
					if (tMaterialDetails == null)
					{
						tMaterialDetails = new MaterialDetails();
						tMaterialDetails.material = graphic.materialForRendering;
						tMaterialDetails.isgui = true;
						ActiveMaterials.Add(tMaterialDetails);
					}
					tMaterialDetails.FoundInGraphics.Add(graphic);
				}
			}

			Button[] buttons = FindObjects<Button>();
			foreach (Button button in buttons)
			{
				CheckButtonSpriteState(button, button.spriteState.disabledSprite);
				CheckButtonSpriteState(button, button.spriteState.highlightedSprite);
				CheckButtonSpriteState(button, button.spriteState.pressedSprite);
			}
		}

		foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
		{
			Material tMaterial = tMaterialDetails.material;
			if (tMaterial != null)
			{
				var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
				foreach (Object obj in dependencies)
				{
					if (obj is Texture)
					{
						Texture tTexture = obj as Texture;
						var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMaterialDetails);
						tTextureDetail.isSky = tMaterialDetails.isSky;
						tTextureDetail.instance = tMaterialDetails.instance;
						tTextureDetail.isgui = tMaterialDetails.isgui;
						ActiveTextures.Add(tTextureDetail);
					}
				}

				//if the texture was downloaded, it won't be included in the editor dependencies
				if (tMaterial.HasProperty ("_MainTex")) {
					if (tMaterial.mainTexture != null && !dependencies.Contains (tMaterial.mainTexture)) {
						var tTextureDetail = GetTextureDetail (tMaterial.mainTexture, tMaterial, tMaterialDetails);
						ActiveTextures.Add (tTextureDetail);
					}
				}
			}
		}

		MeshFilter[] meshFilters = FindObjects<MeshFilter>();

		foreach (MeshFilter tMeshFilter in meshFilters)
		{
			Mesh tMesh = tMeshFilter.sharedMesh;
			if (tMesh != null)
			{
				MeshDetails tMeshDetails = FindMeshDetails(tMesh);
				if (tMeshDetails == null)
				{
					tMeshDetails = new MeshDetails();
					tMeshDetails.mesh = tMesh;
					ActiveMeshDetails.Add(tMeshDetails);
				}
				tMeshDetails.FoundInMeshFilters.Add(tMeshFilter);

				if (GameObjectUtility.AreStaticEditorFlagsSet(tMeshFilter.gameObject, StaticEditorFlags.BatchingStatic))
				{
					tMeshDetails.StaticBatchingEnabled.Add(tMeshFilter.gameObject);
				}
				
			} else if (tMesh == null && tMeshFilter.transform.GetComponent("TMPro.TextContainer")== null) {
				Missing tMissing = new Missing ();
				tMissing.Object = tMeshFilter.transform;
				tMissing.type = "mesh";
				tMissing.name = tMeshFilter.transform.name;
				MissingObjects.Add(tMissing);
				thingsMissing = true;
			}

			var meshRenderrer = tMeshFilter.transform.GetComponent<MeshRenderer>();
				
			if (meshRenderrer == null || meshRenderrer.sharedMaterial == null) {
				Missing tMissing = new Missing ();
				tMissing.Object = tMeshFilter.transform;
				tMissing.type = "material";
				tMissing.name = tMeshFilter.transform.name;
				MissingObjects.Add(tMissing);
				thingsMissing = true;
			}
		}

		SkinnedMeshRenderer[] skinnedMeshRenderers = FindObjects<SkinnedMeshRenderer>();

		foreach (SkinnedMeshRenderer tSkinnedMeshRenderer in skinnedMeshRenderers)
		{
			Mesh tMesh = tSkinnedMeshRenderer.sharedMesh;
			if (tMesh != null)
			{
				MeshDetails tMeshDetails = FindMeshDetails(tMesh);
				if (tMeshDetails == null)
				{
					tMeshDetails = new MeshDetails();
					tMeshDetails.mesh = tMesh;
					ActiveMeshDetails.Add(tMeshDetails);
				}
				tMeshDetails.FoundInSkinnedMeshRenderer.Add(tSkinnedMeshRenderer);
			} else if (tMesh == null) {
				Missing tMissing = new Missing ();
				tMissing.Object = tSkinnedMeshRenderer.transform;
				tMissing.type = "mesh";
				tMissing.name = tSkinnedMeshRenderer.transform.name;
				MissingObjects.Add(tMissing);
				thingsMissing = true;
			}
			if (tSkinnedMeshRenderer.sharedMaterial == null) {
				Missing tMissing = new Missing ();
				tMissing.Object = tSkinnedMeshRenderer.transform;
				tMissing.type = "material";
				tMissing.name = tSkinnedMeshRenderer.transform.name;
				MissingObjects.Add(tMissing);
				thingsMissing = true;
			}
		}

		LODGroup[] lodGroups = FindObjects<LODGroup>();
		if (lodGroups != null) {
			// Check if any LOD groups have no renderers
			foreach (var group in lodGroups)
			{
				var lods = group.GetLODs();
				for (int i = 0, l = lods.Length; i < l; i++)
				{
					if (lods[i].renderers.Length == 0)
					{
						Missing tMissing = new Missing();
						tMissing.Object = group.transform;
						tMissing.type = "lod";
						tMissing.name = group.transform.name;
						MissingObjects.Add(tMissing);
						thingsMissing = true;
					}
				}
			}
		}

		if (IncludeSelectedFolder) {
			if (Selection.objects.Length != 0)
			{
				var folders = new List<string>();
				foreach (var obj in Selection.objects)
				{
					if (obj.GetType() != typeof(DefaultAsset)) continue;
					var path = AssetDatabase.GetAssetPath(obj);
					folders.Add(path);
				}

				if (folders.Count != 0)
				{
					var guids = AssetDatabase.FindAssets("t:Texture", folders.ToArray());
					if (guids.Length != 0)
					{
						foreach (var guid in guids)
						{
							var path = AssetDatabase.GUIDToAssetPath(guid);
							var item = AssetDatabase.LoadAssetAtPath<Texture>(path);
							var textureDetail = GetTextureDetail(item);
							if (!ActiveTextures.Contains(textureDetail))
								ActiveTextures.Add(textureDetail);
						}
					}
					guids = AssetDatabase.FindAssets("t:AudioClip", folders.ToArray());
					if (guids.Length != 0)
					{
						foreach (var guid in guids)
						{
							var path = AssetDatabase.GUIDToAssetPath(guid);
							var item = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
							var tClipDetails = FindClipDetails(item);
							if (tClipDetails == null)
							{
								tClipDetails = new ClipDetails();
								tClipDetails.clip = item;
								ActiveClipDetails.Add(tClipDetails);
							}
						}
						
					}
				}
			}
		}
		
		if (IncludeSpriteAnimations)
		{
			Animator[] animators = FindObjects<Animator>();
			foreach (Animator anim in animators)
			{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
				UnityEditorInternal.AnimatorController ac = anim.runtimeAnimatorController as UnityEditorInternal.AnimatorController;
#elif UNITY_5 || UNITY_5_3_OR_NEWER
				UnityEditor.Animations.AnimatorController ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
#endif

				//Skip animators without layers, this can happen if they don't have an animator controller.
				if (!ac || ac.layers == null || ac.layers.Length == 0)
					continue;

				for (int x = 0; x < anim.layerCount; x++)
				{										
					UnityEditor.Animations.AnimatorStateMachine sm = ac.layers[x].stateMachine;
					int cnt = sm.states.Length;
					
					for (int i = 0; i < cnt; i++)
					{												
						UnityEditor.Animations.AnimatorState state = sm.states[i].state;
						Motion m = state.motion;						
                        if (m != null)
						{
							AnimationClip clip = m as AnimationClip;

						    if (clip != null)
						    {
						        EditorCurveBinding[] ecbs = AnimationUtility.GetObjectReferenceCurveBindings(clip);

						        foreach (EditorCurveBinding ecb in ecbs)
						        {
						            if (ecb.propertyName == "m_Sprite")
						            {
						                foreach (ObjectReferenceKeyframe keyframe in AnimationUtility.GetObjectReferenceCurve(clip, ecb))
						                {
						                    Sprite tSprite = keyframe.value as Sprite;

						                    if (tSprite != null)
						                    {
						                        var tTextureDetail = GetTextureDetail(tSprite.texture, anim);
						                        if (!ActiveTextures.Contains(tTextureDetail))
						                        {
						                            ActiveTextures.Add(tTextureDetail);
						                        }
						                    }
						                }
						            }
						        }
						    }
						}
					}
				}

			}
		}

		if (IncludeScriptReferences)
		{
			MonoBehaviour[] scripts = FindObjects<MonoBehaviour>();
			foreach (MonoBehaviour script in scripts)
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.Instance; // only public non-static fields are bound to by Unity.
				FieldInfo[] fields = script.GetType().GetFields(flags);

				foreach (FieldInfo field in fields)
				{
					System.Type fieldType = field.FieldType;
					// TODO
					// Handle directly AudioClip, Texture2D (+ other Textures) types
					if (fieldType == typeof(Sprite))
					{
						Sprite tSprite = field.GetValue(script) as Sprite;
						if (tSprite != null)
						{
							var tSpriteTextureDetail = GetTextureDetail(tSprite.texture, script);
							if (!ActiveTextures.Contains(tSpriteTextureDetail))
							{
								ActiveTextures.Add(tSpriteTextureDetail);
							}
						}
					}if (fieldType == typeof(Mesh))
					{
						Mesh tMesh = field.GetValue(script) as Mesh;
						if (tMesh != null)
						{
							MeshDetails tMeshDetails = FindMeshDetails(tMesh);
							if (tMeshDetails == null)
							{
								tMeshDetails = new MeshDetails();
								tMeshDetails.mesh = tMesh;
								tMeshDetails.instance = true;
								ActiveMeshDetails.Add(tMeshDetails);
							}
						}
					}if (fieldType == typeof(Material))
					{
						Material tMaterial = field.GetValue(script) as Material;
						if (tMaterial != null)
						{
							MaterialDetails tMatDetails = FindMaterialDetails(tMaterial);
							if (tMatDetails == null)
							{
								tMatDetails = new MaterialDetails();
								tMatDetails.instance = true;
								tMatDetails.material = tMaterial;
								if(!ActiveMaterials.Contains(tMatDetails))
									ActiveMaterials.Add(tMatDetails);
							}
							if (tMaterial.mainTexture)
							{
								var tSpriteTextureDetail = GetTextureDetail(tMaterial.mainTexture);
								if (!ActiveTextures.Contains(tSpriteTextureDetail))
								{
									ActiveTextures.Add(tSpriteTextureDetail);
								}
							}
							var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
							foreach (Object obj in dependencies)
							{
								if (obj is Texture)
								{
									Texture tTexture = obj as Texture;
									var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMatDetails);
									if(!ActiveTextures.Contains(tTextureDetail))
										ActiveTextures.Add(tTextureDetail);
								}
							}
						}
					}
				}
			}
		}
#if UNITY_6000_0_OR_NEWER
		var AudioSources = (AudioSource[])FindObjectsByType(typeof(AudioSource), FindObjectsSortMode.None);
#else
		var AudioSources = (AudioSource[])FindObjectsOfType(typeof(AudioSource));
#endif

		foreach (AudioSource tAudioSource in AudioSources)
		{
			AudioClip tClip = tAudioSource.clip;
			if (tClip != null)
			{
				ClipDetails tClipDetails = FindClipDetails(tClip);
				if (tClipDetails == null)
				{
					tClipDetails = new ClipDetails();
					tClipDetails.clip = tClip;
					ActiveClipDetails.Add(tClipDetails);
				}
				tClipDetails.FoundInAudioSources.Add(tAudioSource);

			}
			else if (tClip == null)
			{
				Missing tMissing = new Missing();
				tMissing.Object = tAudioSource.transform;
				tMissing.type = "audio clip";
				tMissing.name = tAudioSource.transform.name;
				MissingObjects.Add(tMissing);
				thingsMissing = true;
			}
		}
		
		TotalTextureMemory = 0;
		foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

		TotalMeshVertices = 0;
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;

		// Sort by size, descending
		ActiveTextures.Sort(delegate(TextureDetails details1, TextureDetails details2) { return (int)(details2.memSizeKB - details1.memSizeKB); });
	    ActiveTextures = ActiveTextures.Distinct().ToList();
		ActiveMeshDetails.Sort(delegate(MeshDetails details1, MeshDetails details2) { return details2.mesh.vertexCount - details1.mesh.vertexCount; });

		collectedInPlayingMode = Application.isPlaying;
		
		// Sort by render queue
		ActiveMaterials.Sort(MaterialSorter);
	}

	private void CheckButtonSpriteState(Button button, Sprite sprite) 
	{
		if (sprite == null) return;
		
		var texture = sprite.texture;
		var tButtonTextureDetail = GetTextureDetail(texture, button);
		if (!ActiveTextures.Contains(tButtonTextureDetail))
		{
			ActiveTextures.Add(tButtonTextureDetail);
		}
	}
	
    private static GameObject[] GetAllRootGameObjects()
    {
#if !UNITY_5 && !UNITY_5_3_OR_NEWER
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToArray();
#else
        List<GameObject> allGo = new List<GameObject>();
        for (int sceneIdx = 0; sceneIdx < UnityEngine.SceneManagement.SceneManager.sceneCount; ++sceneIdx){
			//only add the scene to the list if it's currently loaded.
			if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIdx).isLoaded) {
				allGo.AddRange(UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIdx).GetRootGameObjects().ToArray());
			}
        }
       
		allGo.AddRange(GetDontDestroyOnLoadRoots());
        return allGo.ToArray();
#endif
    }
    
    private static List<GameObject> GetDontDestroyOnLoadRoots()
    {
	    List<GameObject> objs = new List<GameObject>();
	    if (Application.isPlaying)
	    {
		    GameObject temp = null;
		    try
		    {
			    temp = new GameObject();
			    DontDestroyOnLoad(temp);
			    UnityEngine.SceneManagement.Scene dontDestryScene = temp.scene;
			    DestroyImmediate(temp);
			    temp = null;

			    if(dontDestryScene.IsValid())
			    {
				    objs =  dontDestryScene.GetRootGameObjects().ToList();
			    }
		    }
		    catch (System.Exception e)
		    {
			    Debug.LogException(e);
			    return null;
		    }
		    finally
		    {
			    if(temp != null)
				    DestroyImmediate(temp);
		    }
	    }
	    return objs;
    }

	private T[] FindObjects<T>() where T : Object
	{
		if (IncludeDisabledObjects)
		{
			List<T> meshfilters = new List<T>();
			GameObject[] allGo = GetAllRootGameObjects();
			foreach (GameObject go in allGo)
			{
				Transform[] tgo = go.GetComponentsInChildren<Transform>(true).ToArray();
				foreach (Transform tr in tgo)
				{
					if (tr.GetComponent<T>())
						meshfilters.Add(tr.GetComponent<T>());
				}
			}
			return (T[])meshfilters.ToArray();
		}
		else
#if UNITY_6000_0_OR_NEWER
			return (T[])FindObjectsByType(typeof(T), FindObjectsSortMode.None);
#else
			return (T[])FindObjectsOfType(typeof(T));
#endif
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Material tMaterial, MaterialDetails tMaterialDetails)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInMaterials.Add(tMaterial);
		foreach (Renderer renderer in tMaterialDetails.FoundInRenderers)
		{
			if (!tTextureDetails.FoundInRenderers.Contains(renderer)) tTextureDetails.FoundInRenderers.Add(renderer);
		}
		foreach (Graphic graphic in tMaterialDetails.FoundInGraphics)
		{
			if (!tTextureDetails.FoundInGraphics.Contains(graphic)) tTextureDetails.FoundInGraphics.Add(graphic);
		}
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Renderer renderer)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInRenderers.Add(renderer);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Animator animator)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInAnimators.Add(animator);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Graphic graphic)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInGraphics.Add(graphic);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, MonoBehaviour script)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInScripts.Add(script);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Button button) 
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		if (!tTextureDetails.FoundInButtons.Contains(button))
		{
			tTextureDetails.FoundInButtons.Add(button);
		}
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture)
	{
		TextureDetails tTextureDetails = FindTextureDetails(tTexture);
		if (tTextureDetails == null)
		{
			tTextureDetails = new TextureDetails();
			tTextureDetails.texture = tTexture;
			tTextureDetails.isCubeMap = tTexture is Cubemap;

			float memSize = CalculateTextureSizeBytes(tTexture);

			TextureFormat tFormat = TextureFormat.RGBA32;
			int tMipMapCount = 1;

			switch (tTexture)
			{
				case Texture2D t2d:
					tFormat = t2d.format;
					tMipMapCount = t2d.mipmapCount;
					break;
				case Texture2DArray t2da:
					tFormat = t2da.format;
					tMipMapCount = t2da.mipmapCount;
					break;
				case Cubemap cubemap:
					tFormat = cubemap.format;
					tMipMapCount = cubemap.mipmapCount;
					break;
			}

			tTextureDetails.memSizeKB = memSize / 1024;
			tTextureDetails.format = tFormat;
			tTextureDetails.mipMapCount = tMipMapCount;

		}

		return tTextureDetails;
	}
}
#if UNITY_2021 || UNITY_2020
// Taken there https://forum.unity.com/threads/grab-secondary-textures-from-sprite-variable.951380/#post-7337899
public static class SpriteUtils
{
	private delegate Texture2D GetSecondaryTextureDelegate(Sprite sprite, int index);

	private static readonly GetSecondaryTextureDelegate GetSecondaryTextureCached =
		(GetSecondaryTextureDelegate)Delegate.CreateDelegate(
			typeof(GetSecondaryTextureDelegate),
			typeof(Sprite).GetMethod("GetSecondaryTexture", BindingFlags.NonPublic | BindingFlags.Instance) ??
			throw new Exception("Unity has changed/removed the internal method Sprite.GetSecondaryTexture"));

	public static Texture getSecondaryTexture(this Sprite sprite, int index) => GetSecondaryTextureCached(sprite, index);
}
#endif