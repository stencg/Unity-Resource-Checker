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
using Object = UnityEngine.Object;

public class TextureDetails : IEquatable<TextureDetails>
{
    public bool isCubeMap;
    public float memSizeKB;
    public Texture texture;
    public TextureFormat format;
    public int mipMapCount;
    public List<Object> FoundInMaterials = new List<Object>();
    public List<Renderer> FoundInRenderers = new List<Renderer>();
    public List<Object> FoundInAnimators = new List<Object>();
    public List<Object> FoundInScripts = new List<Object>();
    public List<Object> FoundInGraphics = new List<Object>();
    public List<Object> FoundInButtons = new List<Object>();
    public bool isSky;
    public bool instance;
    public bool isgui;
    public TextureDetails() { }

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
}

public class MaterialDetails
{

    public Material material;

    public List<Renderer> FoundInRenderers = new List<Renderer>();
    public List<Graphic> FoundInGraphics = new List<Graphic>();
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

    public List<MeshFilter> FoundInMeshFilters = new List<MeshFilter>();
    public List<SkinnedMeshRenderer> FoundInSkinnedMeshRenderer = new List<SkinnedMeshRenderer>();
    public List<GameObject> StaticBatchingEnabled = new List<GameObject>();
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

    public ClipDetails() { }
};

public class Missing
{
    public Transform Object;
    public string type;
    public string name;
}

public class ResourceChecker : EditorWindow
{
    static readonly string[] inspectToolbarStrings = { "Textures", "Materials", "Meshes", "Audio" };
    static readonly string[] inspectToolbarStringsWithMissing = { "Textures", "Materials", "Meshes", "Audio", "Missing" };

    enum InspectType
    {
        Textures, Materials, Meshes, AudioClips, Missing
    };

    bool IncludeDisabledObjects = true;
    bool IncludeSpriteAnimations = true;
    bool IncludeScriptReferences = true;
    bool IncludeGuiElements = true;
    bool IncludeLightmapTextures = true;
    bool IncludeSelectedFolder = false;

    InspectType ActiveInspectType = InspectType.Textures;

    const int ThumbnailWidth = 40;
    const int ThumbnailHeight = 40;
    const string SpriteType = "m_Sprite";
    private const string AudioClipTypeFilter = "t:AudioClip";
    private const string TextureTypeFilter = "t:Texture";
    const int StandardLineHeight = 15;
    int topPanelHeight = 0;

    List<TextureDetails> ActiveTextures = new List<TextureDetails>();
    List<MaterialDetails> ActiveMaterials = new List<MaterialDetails>();
    List<MeshDetails> ActiveMeshDetails = new List<MeshDetails>();
    List<ClipDetails> ActiveClipDetails = new List<ClipDetails>();
    List<Missing> MissingObjects = new List<Missing>();

    Vector2 textureListScrollPos = new Vector2(0, 0);
    Vector2 materialListScrollPos = new Vector2(0, 0);
    Vector2 meshListScrollPos = new Vector2(0, 0);
    Vector2 audioListScrollPos = new Vector2(0, 0);
    Vector2 missingListScrollPos = new Vector2(0, 0);

    float TotalTextureMemory = 0;
    int TotalMeshVertices = 0;

    bool ctrlPressed = false;
    bool thingsMissing = false;
    bool collectedInPlayingMode;
    bool isUpdating = false;
    bool isChecking = false;

    Color defColor;

    [MenuItem("Window/Resource Checker")]
    static void Init()
    {
        ResourceChecker window = (ResourceChecker)GetWindow(typeof(ResourceChecker));
        window.isUpdating = false;
        window.isChecking = false;
        window.CheckResources();
        window.minSize = new Vector2(475, 475);
    }

    void OnGUI()
    {
        if (isChecking || isUpdating)
            return;

        try
        {
            isUpdating = true;
            defColor = GUI.color;

            // all toggle settings Left side
            IncludeDisabledObjects = GUILayout.Toggle(IncludeDisabledObjects, "Include disabled objects", GUILayout.Width(300), GUILayout.Height(StandardLineHeight));
            IncludeSpriteAnimations = GUILayout.Toggle(IncludeSpriteAnimations, "Look in sprite animations (Textures)", GUILayout.Width(300), GUILayout.Height(StandardLineHeight));
            GUI.color = new Color(0.8f, 0.8f, 1.0f, 1.0f);
            IncludeScriptReferences = GUILayout.Toggle(IncludeScriptReferences, "Look in behavior fields (Textures, mats, meshes)", GUILayout.Width(300), GUILayout.Height(StandardLineHeight));
            GUI.color = new Color(1.0f, 0.95f, 0.8f, 1.0f);
            IncludeGuiElements = GUILayout.Toggle(IncludeGuiElements, "Look in GUI elements (Textures, mats)", GUILayout.Width(300), GUILayout.Height(StandardLineHeight));
            IncludeLightmapTextures = GUILayout.Toggle(IncludeLightmapTextures, "Look in Lightmap textures", GUILayout.Width(300), GUILayout.Height(StandardLineHeight));
            GUI.color = defColor;
            IncludeSelectedFolder = GUILayout.Toggle(IncludeSelectedFolder, "Look in Selected Folders (Textures, Audio)", GUILayout.Width(300), GUILayout.Height(StandardLineHeight));
            topPanelHeight = StandardLineHeight * 6;

            // buttons on right side
            GUILayout.BeginArea(new Rect(position.width - 85, 5, 100, 85));
            try
            {
                if (GUILayout.Button("Calculate", GUILayout.Width(80), GUILayout.Height(40)))
                    CheckResources();

                if (GUILayout.Button("CleanUp", GUILayout.Width(80), GUILayout.Height(20)))
                    DestroyAll();
            }
            finally
            {
                GUILayout.EndArea();
            }

            RemoveDestroyedResources();

            // Info area
            GUILayout.Space(120);
            topPanelHeight += 120;
            if (thingsMissing == true)
            {
                EditorGUI.HelpBox(new Rect(8, 105, 300, StandardLineHeight), "Some GameObjects are missing elements.", MessageType.Error);
                topPanelHeight += StandardLineHeight;
            }
            EditorGUI.HelpBox(new Rect(8, 135, 300, StandardLineHeight), "It always checks GOs in opened scenes.", MessageType.Info);
            EditorGUI.HelpBox(new Rect(8, 160, 300, StandardLineHeight), "Crunched formats use VRAM as uncrunched ones.", MessageType.Warning);
            topPanelHeight += StandardLineHeight * 2;
            // EditorGUI.HelpBox(new Rect(8, 185, 300, 25), "ASTC is not yet supported ", MessageType.Warning);

            // resource type Tab buttons
            GUILayout.BeginHorizontal(GUILayout.Height(StandardLineHeight));
            try
            {
                GUILayout.Label("Textures " + ActiveTextures.Count + " - " + FormatSizeString(TotalTextureMemory));
                GUILayout.Label("Materials " + ActiveMaterials.Count);
                GUILayout.Label("Meshes " + ActiveMeshDetails.Count + " - " + TotalMeshVertices + " verts");
                GUILayout.Label("Audio Clips " + ActiveClipDetails.Count);
                if (thingsMissing == true)
                {
                    GUILayout.Label("Missings " + MissingObjects.Count);
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            topPanelHeight += StandardLineHeight;

            ActiveInspectType = (InspectType)GUILayout.Toolbar((int)ActiveInspectType, thingsMissing ? inspectToolbarStringsWithMissing : inspectToolbarStrings);

            ctrlPressed = Event.current.control || Event.current.command;

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
        finally
        {
            isUpdating = false;
        }
    }

    void CheckResources()
    {
        try
        {
            isChecking = true;
            DestroyAll();
            textureListScrollPos = Vector2.zero;
            materialListScrollPos = Vector2.zero;
            meshListScrollPos = Vector2.zero;
            audioListScrollPos = Vector2.zero;
            missingListScrollPos = Vector2.zero;
            MaterialDetails skyMat = new MaterialDetails();
            skyMat.material = RenderSettings.skybox;
            skyMat.isSky = true;
            ActiveMaterials.Add(skyMat);

            //Debug.Log("Total renderers "+renderers.Length);
            foreach (Renderer renderer in FindObjects<Renderer>())
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

                    if (tSpriteRenderer.sprite != null)
                    {
                        TextureDetails tTextureDetails = GetTextureDetail(tSpriteRenderer.sprite.texture);
                        tTextureDetails.FoundInRenderers.Add(renderer);
#if UNITY_2021 || UNITY_2020
					for (int i = 0; i < 3; i++) //TODO Get secondaries array length instead
					{
						if (tSpriteRenderer.sprite.getSecondaryTexture(i) == null) continue;
						var tSpriteSecondaryTextureDetail = GetTextureDetail(tSpriteRenderer.sprite.getSecondaryTexture(i), renderer);
						if (!ActiveTextures.Contains(tSpriteSecondaryTextureDetail)) {
							ActiveTextures.Add(tSpriteSecondaryTextureDetail);
						}
					}
#elif UNITY_2022_1_OR_NEWER
                        var secondarySpriteTextureResult = new SecondarySpriteTexture[tSpriteRenderer.sprite.GetSecondaryTextureCount()];
                        tSpriteRenderer.sprite.GetSecondaryTextures(secondarySpriteTextureResult);
                        foreach (var sst in secondarySpriteTextureResult)
                        {
                            var tSpriteSecondaryTextureDetail = GetTextureDetail(sst.texture);
                            tSpriteSecondaryTextureDetail.FoundInRenderers.Add(renderer);
                        }
#endif
                    }
                    else if (tSpriteRenderer.sprite == null)
                    {
                        AddMissing(tSpriteRenderer.transform, "sprite");
                    }
                }

                if (renderer is SkinnedMeshRenderer)
                {
                    var tSkinnedMeshRenderer = ((SkinnedMeshRenderer)renderer);
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
                    }
                    else if (tMesh == null)
                    {
                        AddMissing(tSkinnedMeshRenderer.transform, "mesh");
                    }
                    if (tSkinnedMeshRenderer.sharedMaterial == null)
                    {
                        AddMissing(tSkinnedMeshRenderer.transform, "material");
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

            if (IncludeLightmapTextures)
            {
                // Unity lightmaps
                foreach (LightmapData lightmapData in LightmapSettings.lightmaps)
                {
                    if (lightmapData.lightmapColor != null)
                    {
                        GetTextureDetail(lightmapData.lightmapColor);
                    }

                    if (lightmapData.lightmapDir != null)
                    {
                        GetTextureDetail(lightmapData.lightmapDir);
                    }

                    if (lightmapData.shadowMask != null)
                    {
                        GetTextureDetail(lightmapData.shadowMask);
                    }
                }
            }

            if (IncludeGuiElements)
            {
                foreach (Graphic graphic in FindObjects<Graphic>())
                {
                    if (graphic.mainTexture)
                    {
                        TextureDetails tTextureDetails = GetTextureDetail(graphic.mainTexture);
                        tTextureDetails.FoundInGraphics.Add(graphic);
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

                foreach (Button button in FindObjects<Button>())
                {
                    CheckButtonSpriteState(button, button.spriteState.disabledSprite);
                    CheckButtonSpriteState(button, button.spriteState.highlightedSprite);
                    CheckButtonSpriteState(button, button.spriteState.pressedSprite);
                }
            }

            foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
            {
                Material tMaterial = tMaterialDetails.material;
                if (tMaterial == null)
                    continue;

                var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
                foreach (Object obj in dependencies)
                {
                    if (!(obj is Texture))
                        continue;

                    Texture tTexture = obj as Texture;
                    var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMaterialDetails);
                    tTextureDetail.isSky = tMaterialDetails.isSky;
                    tTextureDetail.instance = tMaterialDetails.instance;
                    tTextureDetail.isgui = tMaterialDetails.isgui;
                }

                //if the texture was downloaded, it won't be included in the editor dependencies
                if (tMaterial.HasProperty("_MainTex") &&
                    tMaterial.mainTexture != null && !dependencies.Contains(tMaterial.mainTexture))
                {
                    GetTextureDetail(tMaterial.mainTexture, tMaterial, tMaterialDetails);
                }
            }

            foreach (MeshFilter tMeshFilter in FindObjects<MeshFilter>())
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
                }
                else if (tMeshFilter.transform.GetComponent("TextContainer") == null)
                {
                    AddMissing(tMeshFilter.transform, "mesh");
                }

                var meshRenderrer = tMeshFilter.transform.GetComponent<MeshRenderer>();

                if (meshRenderrer == null || meshRenderrer.sharedMaterial == null)
                {
                    AddMissing(tMeshFilter.transform, "material");
                }
            }

            // Check if any LOD groups have no renderers
            foreach (var group in FindObjects<LODGroup>())
            {
                var lods = group.GetLODs();
                foreach (var lod in lods)
                {
                    if (lod.renderers.Length != 0)
                        continue;
                    AddMissing(group.transform, "lods");
                }
            }

            if (IncludeSelectedFolder && Selection.objects.Length != 0)
            {
                var folders = new List<string>();
                foreach (var obj in Selection.objects)
                {
                    if (obj.GetType() != typeof(DefaultAsset))
                        continue;
                    var path = AssetDatabase.GetAssetPath(obj);
                    folders.Add(path);
                }

                if (folders.Count != 0)
                {
                    foreach (var guid in AssetDatabase.FindAssets(TextureTypeFilter, folders.ToArray()))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var item = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        GetTextureDetail(item);
                    }

                    foreach (var guid in AssetDatabase.FindAssets(AudioClipTypeFilter, folders.ToArray()))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var item = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        AddClipDetails(item);
                    }
                }
            }
            if (IncludeSpriteAnimations)
            {
                foreach (Animator anim in FindObjects<Animator>())
                {
                    UnityEditor.Animations.AnimatorController ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;

                    //Skip animators without layers, this can happen if they don't have an animator controller.
                    if (!ac || ac.layers == null || ac.layers.Length == 0)
                        continue;

                    foreach (var layer in ac.layers)
                    {
                        foreach (var state in layer.stateMachine.states)
                        {
                            if (state.state.motion == null || !(state.state.motion is AnimationClip))
                                continue;

                            foreach (EditorCurveBinding ecb in AnimationUtility.GetObjectReferenceCurveBindings(state.state.motion as AnimationClip))
                            {
                                if (ecb.propertyName == null || !ecb.propertyName.Equals(SpriteType))
                                    continue;

                                foreach (ObjectReferenceKeyframe keyframe in AnimationUtility.GetObjectReferenceCurve(state.state.motion as AnimationClip, ecb))
                                {
                                    if (keyframe.value == null || !(keyframe.value is Sprite))
                                        continue;

                                    TextureDetails tTextureDetails = GetTextureDetail(((Sprite)keyframe.value).texture);
                                    tTextureDetails.FoundInAnimators.Add(anim);
                                }
                            }
                        }
                    }
                }
            }

            if (IncludeScriptReferences)
            {
                foreach (MonoBehaviour script in FindObjects<MonoBehaviour>())
                {
                    BindingFlags flags = BindingFlags.Public | BindingFlags.Instance; // only public non-static fields are bound to by Unity.

                    foreach (FieldInfo field in script.GetType().GetFields(flags))
                    {
                        System.Type fieldType = field.FieldType;
                        // TODO
                        // Handle directly AudioClip, Texture2D (+ other Textures) types
                        if (fieldType == typeof(Sprite))
                        {
                            Sprite tSprite = field.GetValue(script) as Sprite;
                            if (tSprite != null)
                            {
                                TextureDetails tTextureDetails = GetTextureDetail(tSprite.texture);
                                tTextureDetails.FoundInScripts.Add(script);
                            }
                        }
                        if (fieldType == typeof(Mesh))
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
                        }
                        if (fieldType == typeof(Material))
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
                                    if (!ActiveMaterials.Contains(tMatDetails))
                                        ActiveMaterials.Add(tMatDetails);
                                }
                                if (tMaterial.mainTexture)
                                {
                                    GetTextureDetail(tMaterial.mainTexture);
                                }

                                foreach (Object obj in EditorUtility.CollectDependencies(new Object[] { tMaterial }))
                                {
                                    if (obj is Texture)
                                    {
                                        GetTextureDetail(obj as Texture, tMaterial, tMatDetails);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (AudioSource tAudioSource in FindObjects<AudioSource>())
            {
                AudioClip tClip = tAudioSource.clip;
                if (tClip != null)
                {
                    AddClipDetails(tClip, tAudioSource);
                }
                else if (tClip == null)
                {
                    AddMissing(tAudioSource.transform, "audio clip");
                }
            }

            TotalTextureMemory = 0;
            foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

            TotalMeshVertices = 0;
            foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;

            // Sort by size, descending
            ActiveTextures.Sort(delegate (TextureDetails details1, TextureDetails details2) { return (int)(details2.memSizeKB - details1.memSizeKB); });
            ActiveTextures = ActiveTextures.Distinct().ToList();
            ActiveMeshDetails.Sort(delegate (MeshDetails details1, MeshDetails details2) { return details2.mesh.vertexCount - details1.mesh.vertexCount; });

            collectedInPlayingMode = UnityEngine.Application.isPlaying;

            // Sort by render queue
            ActiveMaterials.Sort(MaterialSorterRenderQueue);
        }
        finally
        {
            isChecking = false;
        }
    }

    void ListTextures()
    {
        textureListScrollPos = EditorGUILayout.BeginScrollView(textureListScrollPos);
        try
        {
            //assuming all elements with fixed size height
            int totalCount = ActiveTextures.Count;
            float thumbnailHeightFactor = ThumbnailHeight * totalCount * 0.1f;
            float scrollViewSize = position.height - topPanelHeight - ThumbnailHeight;
            float scrollStartHeight = textureListScrollPos.y - (ThumbnailHeight * 0.75f);
            float scrollEndHeight = textureListScrollPos.y + (ThumbnailHeight * 0.75f) + scrollViewSize;

            for (int i = 0; i < totalCount; i++)
            {
                GUILayout.BeginHorizontal();

                try
                {
                    float elementYPos = ThumbnailHeight * i + ((float)i / totalCount) * thumbnailHeightFactor;
                    bool isElementInView = elementYPos > scrollStartHeight && elementYPos < scrollEndHeight;

                    //skip the detail if not visible on screen and fill with empty box just to maintain scrollable length
                    if (!isElementInView)
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                    }
                    else
                    {
                        TextureDetails tDetails = ActiveTextures[i];
                        Texture tex = tDetails.texture;
                        if (tDetails.texture.GetType() == typeof(Texture2DArray) || tDetails.texture.GetType() == typeof(Cubemap))
                        {
                            tex = AssetPreview.GetMiniThumbnail(tDetails.texture);
                        }
                        GUILayout.Box(tex, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

                        if (tDetails.instance == true)
                            GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                        if (tDetails.isgui == true)
                            GUI.color = new Color(defColor.r, 0.95f, 0.8f, 1.0f);
                        if (tDetails.isSky)
                            GUI.color = new Color(0.9f, defColor.g, defColor.b, 1.0f);
                        if (GUILayout.Button(tDetails.texture.name, GUILayout.Width(150)))
                        {
                            SelectObject(tDetails.texture, ctrlPressed);
                        }
                        GUI.color = defColor;
                        if (GUILayout.Button(tDetails.FoundInMaterials.Count + " Mat", GUILayout.Width(50)))
                        {
                            SelectObjects(tDetails.FoundInMaterials, ctrlPressed);
                        }

                        HashSet<Object> FoundObjects = new HashSet<Object>();
                        foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
                        foreach (Animator animator in tDetails.FoundInAnimators) FoundObjects.Add(animator.gameObject);
                        foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
                        foreach (Button button in tDetails.FoundInButtons) FoundObjects.Add(button.gameObject);
                        foreach (MonoBehaviour script in tDetails.FoundInScripts) FoundObjects.Add(script.gameObject);
                        if (GUILayout.Button(FoundObjects.Count + " GO", GUILayout.Width(50)))
                        {
                            SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
                        }

                        string sharedLabel = tDetails.texture.width + "x" + tDetails.texture.height;
                        if (tDetails.isCubeMap) sharedLabel += "x6";
                        if (tDetails.texture.GetType() == typeof(Texture2DArray))
                            sharedLabel += "[" + ((Texture2DArray)tDetails.texture).depth + "]";
                        sharedLabel += " | " + tDetails.mipMapCount + "mip | " + tDetails.format;
                        sharedLabel += "\n" + FormatSizeString(tDetails.memSizeKB) + " | " + AssetDatabase.GetAssetPath(tDetails.texture);

                        GUILayout.Label(sharedLabel, GUILayout.MinWidth(150));
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    void ListMaterials()
    {
        materialListScrollPos = EditorGUILayout.BeginScrollView(materialListScrollPos);
        try
        {
            //assuming all elements with fixed size height
            int totalCount = ActiveMaterials.Count;
            float thumbnailHeightFactor = ThumbnailHeight * totalCount * 0.1f;
            float scrollViewSize = position.height - topPanelHeight - ThumbnailHeight;
            float scrollStartHeight = materialListScrollPos.y - (ThumbnailHeight * 0.75f);
            float scrollEndHeight = materialListScrollPos.y + (ThumbnailHeight * 0.75f) + scrollViewSize;

            for (int i = 0; i < totalCount; i++)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    float elementYPos = ThumbnailHeight * i + ((float)i / totalCount) * thumbnailHeightFactor;
                    bool isElementInView = elementYPos > scrollStartHeight && elementYPos < scrollEndHeight;

                    //skip the detail if not visible on screen and fill with empty box just to maintain scrollable length
                    if (!isElementInView)
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                    }
                    else
                    {
                        MaterialDetails tDetails = ActiveMaterials[i];
                        GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.material), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

                        if (tDetails.instance == true)
                            GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                        if (tDetails.isgui == true)
                            GUI.color = new Color(defColor.r, 0.95f, 0.8f, 1.0f);
                        if (tDetails.isSky)
                            GUI.color = new Color(0.9f, defColor.g, defColor.b, 1.0f);
                        if (GUILayout.Button(tDetails.material.name, GUILayout.Width(150)))
                        {
                            SelectObject(tDetails.material, ctrlPressed);
                        }
                        GUI.color = defColor;

                        if (GUILayout.Button((tDetails.FoundInRenderers.Count + tDetails.FoundInGraphics.Count) + " GO", GUILayout.Width(50)))
                        {
                            List<Object> FoundObjects = new List<Object>();
                            foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
                            foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
                            SelectObjects(FoundObjects, ctrlPressed);
                        }

                        string shaderLabel = tDetails.material.renderQueue.ToString() + " | " + (tDetails.material.shader != null ? tDetails.material.shader.name : "no shader");
                        shaderLabel += "\n" + AssetDatabase.GetAssetPath(tDetails.material);
                        GUILayout.Label(shaderLabel, GUILayout.MinWidth(150));
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    void ListMeshes()
    {
        meshListScrollPos = EditorGUILayout.BeginScrollView(meshListScrollPos);
        try
        {
            //assuming all elements with fixed size height
            int totalCount = ActiveMeshDetails.Count;
            float thumbnailHeightFactor = ThumbnailHeight * totalCount * 0.1f;
            float scrollViewSize = position.height - topPanelHeight - ThumbnailHeight;
            float scrollStartHeight = meshListScrollPos.y - (ThumbnailHeight * 0.75f);
            float scrollEndHeight = meshListScrollPos.y + (ThumbnailHeight * 0.75f) + scrollViewSize;

            for (int i = 0; i < totalCount; i++)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    float elementYPos = ThumbnailHeight * i + ((float)i / totalCount) * thumbnailHeightFactor;
                    bool isElementInView = elementYPos > scrollStartHeight && elementYPos < scrollEndHeight;

                    //skip the detail if not visible on screen and fill with empty box just to maintain scrollable length
                    if (!isElementInView)
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                    }
                    else
                    {
                        MeshDetails tDetails = ActiveMeshDetails[i];
                        GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.mesh), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

                        string name = tDetails.mesh.name;
                        if (name == null || name.Count() < 1)
                            name = tDetails.FoundInMeshFilters[0].gameObject.name;
                        if (tDetails.instance == true)
                            GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                        if (GUILayout.Button(name, GUILayout.Width(150)))
                        {
                            SelectObject(tDetails.mesh, ctrlPressed);
                        }
                        GUI.color = defColor;

                        if (GUILayout.Button((tDetails.FoundInMeshFilters.Count + tDetails.FoundInSkinnedMeshRenderer.Count) + " GO", GUILayout.Width(50)))
                        {
                            List<Object> FoundObjects = new List<Object>();
                            foreach (MeshFilter meshFilter in tDetails.FoundInMeshFilters) FoundObjects.Add(meshFilter.gameObject);
                            foreach (SkinnedMeshRenderer SkinnedMesh in tDetails.FoundInSkinnedMeshRenderer) FoundObjects.Add(SkinnedMesh.gameObject);
                            SelectObjects(FoundObjects, ctrlPressed);
                        }

                        string sharedLabel = "" + tDetails.mesh.vertexCount + " vert | " + tDetails.FoundInSkinnedMeshRenderer.Count + " skinned mesh | " + tDetails.StaticBatchingEnabled.Count + " static batching";
                        sharedLabel += "\n" + AssetDatabase.GetAssetPath(tDetails.mesh);

                        GUILayout.Label(sharedLabel, GUILayout.MinWidth(150));
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    void ListMissing()
    {
        missingListScrollPos = EditorGUILayout.BeginScrollView(missingListScrollPos);
        try
        {
            //assuming all elements with fixed size height
            int totalCount = MissingObjects.Count;
            float thumbnailHeightFactor = ThumbnailHeight * totalCount * 0.1f;
            float scrollViewSize = position.height - topPanelHeight - ThumbnailHeight;
            float scrollStartHeight = missingListScrollPos.y - (ThumbnailHeight * 0.75f);
            float scrollEndHeight = missingListScrollPos.y + (ThumbnailHeight * 0.75f) + scrollViewSize;

            for (int i = 0; i < totalCount; i++)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    float elementYPos = ThumbnailHeight * i + ((float)i / totalCount) * thumbnailHeightFactor;
                    bool isElementInView = elementYPos > scrollStartHeight && elementYPos < scrollEndHeight;

                    //skip the detail if not visible on screen and fill with empty box just to maintain scrollable length
                    if (!isElementInView)
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                    }
                    else
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                        if (GUILayout.Button(MissingObjects[i].name, GUILayout.Width(150)))
                            SelectObject(MissingObjects[i].Object, ctrlPressed);
                        GUILayout.Label("missing ", GUILayout.Width(48));
                        switch (MissingObjects[i].type)
                        {
                            case "lod":
                                GUI.color = new Color(defColor.r, defColor.b, 0.8f, 1.0f);
                                break;
                            case "mesh":
                                GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                                break;
                            case "sprite":
                                GUI.color = new Color(defColor.r, 0.8f, 0.8f, 1.0f);
                                break;
                            case "material":
                                GUI.color = new Color(0.8f, defColor.g, 0.8f, 1.0f);
                                break;
                        }
                        GUILayout.Label(MissingObjects[i].type, GUILayout.MinWidth(48));
                        GUI.color = defColor;
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    void ListAudioClips()
    {
        audioListScrollPos = EditorGUILayout.BeginScrollView(audioListScrollPos);
        try
        {
            //assuming all elements with fixed size height
            int totalCount = ActiveClipDetails.Count;
            float thumbnailHeightFactor = ThumbnailHeight * totalCount * 0.1f;
            float scrollViewSize = position.height - topPanelHeight - ThumbnailHeight;
            float scrollStartHeight = audioListScrollPos.y - (ThumbnailHeight * 0.75f);
            float scrollEndHeight = audioListScrollPos.y + (ThumbnailHeight * 0.75f) + scrollViewSize;

            for (int i = 0; i < totalCount; i++)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    float elementYPos = ThumbnailHeight * i + ((float)i / totalCount) * thumbnailHeightFactor;
                    bool isElementInView = elementYPos > scrollStartHeight && elementYPos < scrollEndHeight;
                    //skip the detail if not visible on screen and fill with empty box just to maintain scrollable length
                    if (!isElementInView)
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                    }
                    else
                    {
                        ClipDetails aDetails = ActiveClipDetails[i];
                        AudioClip clip = aDetails.clip;
                        GUILayout.Box(AssetPreview.GetAssetPreview(clip), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

                        if (GUILayout.Button(clip.name, GUILayout.Width(150)))
                        {
                            SelectObject(clip, ctrlPressed);
                        }
                        GUI.color = defColor;
                        HashSet<Object> FoundObjects = new HashSet<Object>();
                        foreach (AudioSource source in aDetails.FoundInAudioSources) FoundObjects.Add(source.gameObject);
                        if (GUILayout.Button(FoundObjects.Count + " GO", GUILayout.Width(50)))
                        {
                            SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
                        }

                        string audioLabel = "Chs: " + clip.channels + " | " + clip.frequency + " Hz | " + clip.length + " s";
                        audioLabel += "\n" + AssetDatabase.GetAssetPath(clip);

                        GUILayout.Label(audioLabel, GUILayout.MinWidth(150));
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
        }
        finally
        {
            GUILayout.EndScrollView();
        }
    }


    #region Helper Functions

    TextureDetails FindTextureDetails(Texture tTexture)
    {
        foreach (TextureDetails tTextureDetails in ActiveTextures)
        {
            if (tTextureDetails.texture == tTexture) return tTextureDetails;
        }
        return null;

    }

    MaterialDetails FindMaterialDetails(Material tMaterial)
    {
        foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
        {
            if (tMaterialDetails.material == tMaterial) return tMaterialDetails;
        }
        return null;

    }

    MeshDetails FindMeshDetails(Mesh tMesh)
    {
        foreach (MeshDetails tMeshDetails in ActiveMeshDetails)
        {
            if (tMeshDetails.mesh == tMesh) return tMeshDetails;
        }
        return null;

    }

    void AddClipDetails(AudioClip tClip, AudioSource foundIn = null)
    {
        foreach (ClipDetails item in ActiveClipDetails)
        {
            if (item.clip == tClip)
            {
                if (foundIn != null)
                    item.FoundInAudioSources.Add(foundIn);
                return;
            }
        }
        var clipDetails = new ClipDetails { clip = tClip };
        ActiveClipDetails.Add(clipDetails);
        if (foundIn != null)
            clipDetails.FoundInAudioSources.Add(foundIn);
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
            if (tTexture is Texture2D)
            {
                tFormat = (tTexture as Texture2D).format;
                tMipMapCount = (tTexture as Texture2D).mipmapCount;
            }
            if (tTexture is Cubemap)
            {
                tFormat = (tTexture as Cubemap).format;
                memSize = 8 * tTexture.height * tTexture.width;
            }
            if (tTexture is Texture2DArray)
            {
                tFormat = (tTexture as Texture2DArray).format;
                tMipMapCount = 10;
            }

            tTextureDetails.memSizeKB = memSize / 1024;
            tTextureDetails.format = tFormat;
            tTextureDetails.mipMapCount = tMipMapCount;

            ActiveTextures.Add(tTextureDetails);
        }

        return tTextureDetails;
    }

    private static GameObject[] GetAllRootGameObjects()
    {
#if !UNITY_5 && !UNITY_5_3_OR_NEWER
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToArray();
#else
        List<GameObject> allGo = new List<GameObject>();
        for (int sceneIdx = 0; sceneIdx < UnityEngine.SceneManagement.SceneManager.sceneCount; ++sceneIdx)
        {
            //only add the scene to the list if it's currently loaded.
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIdx).isLoaded)
            {
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

                if (dontDestryScene.IsValid())
                {
                    objs = dontDestryScene.GetRootGameObjects().ToList();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
            finally
            {
                if (temp != null)
                    DestroyImmediate(temp);
            }
        }
        return objs;
    }

    private void CheckButtonSpriteState(Button button, Sprite sprite)
    {
        if (sprite == null) return;

        var texture = sprite.texture;
        TextureDetails tTextureDetails = GetTextureDetail(texture);
        if (!tTextureDetails.FoundInButtons.Contains(button))
        {
            tTextureDetails.FoundInButtons.Add(button);
        }
    }

    static int MaterialSorterRenderQueue(MaterialDetails first, MaterialDetails second)
    {
        var firstIsNull = first.material == null;
        var secondIsNull = second.material == null;

        if (firstIsNull && secondIsNull) return 0;
        if (firstIsNull) return int.MaxValue;
        if (secondIsNull) return int.MinValue;

        return first.material.renderQueue - second.material.renderQueue;
    }

    private void RemoveDestroyedResources()
    {
        if (collectedInPlayingMode != Application.isPlaying)
        {
            DestroyAll();
            collectedInPlayingMode = Application.isPlaying;
        }
        ActiveClipDetails.RemoveAll(x => !x.clip);
        ActiveClipDetails.ForEach(delegate (ClipDetails obj)
        {
            obj.FoundInAudioSources.RemoveAll(x => !x);
        });
        ActiveTextures.RemoveAll(x => !x.texture);
        ActiveTextures.ForEach(delegate (TextureDetails obj)
        {
            obj.FoundInAnimators.RemoveAll(x => !x);
            obj.FoundInMaterials.RemoveAll(x => !x);
            obj.FoundInRenderers.RemoveAll(x => !x);
            obj.FoundInScripts.RemoveAll(x => !x);
            obj.FoundInGraphics.RemoveAll(x => !x);
            //obj.FoundInProjectFolder.RemoveAll(x => !x);
        });

        ActiveMaterials.RemoveAll(x => !x.material);
        ActiveMaterials.ForEach(delegate (MaterialDetails obj)
        {
            obj.FoundInRenderers.RemoveAll(x => !x);
            obj.FoundInGraphics.RemoveAll(x => !x);
        });

        ActiveMeshDetails.RemoveAll(x => !x.mesh);
        ActiveMeshDetails.ForEach(delegate (MeshDetails obj)
        {
            obj.FoundInMeshFilters.RemoveAll(x => !x);
            obj.FoundInSkinnedMeshRenderer.RemoveAll(x => !x);
            obj.StaticBatchingEnabled.RemoveAll(x => !x);
        });

        TotalTextureMemory = 0;
        foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

        TotalMeshVertices = 0;
        foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;
    }

    private void DestroyAll()
    {
        ActiveTextures.Clear();
        ActiveMaterials.Clear();
        ActiveMeshDetails.Clear();
        ActiveClipDetails.Clear();
        MissingObjects.Clear();
        thingsMissing = false;
        Resources.UnloadUnusedAssets();
        GC.Collect(2, GCCollectionMode.Optimized);
    }

    private T[] FindObjects<T>() where T : Object
    {

#if UNITY_2020_1_OR_NEWER
        var t = FindObjectsOfType<T>(IncludeDisabledObjects);
        return t != null ? t : new T[] { };
#else
        if (IncludeDisabledObjects)
        {
            List<T> t = new List<T>();
            GameObject[] allGo = GetAllRootGameObjects();
            foreach (GameObject go in allGo)
            {
                Transform[] tgo = go.GetComponentsInChildren<Transform>(true).ToArray();
                foreach (Transform tr in tgo)
                {
                    if (tr.GetComponent<T>())
                        t.Add(tr.GetComponent<T>());
                }
            }
            return (T[])t.ToArray();
        }
        else
            return (T[])FindObjectsOfType(typeof(T));
#endif
    }

    string FormatSizeString(float memSizeKB)
    {
        if (memSizeKB < 1024)
        {
            if (memSizeKB < 10)
                return "" + memSizeKB.ToString("0.00") + "k";
            else if (memSizeKB < 100)
                return "" + memSizeKB.ToString("0.0") + "k";
            else
                return "" + memSizeKB.ToString("0") + "k";
        }
        else
        {
            float memSizeMB = ((float)memSizeKB) / 1024.0f;
            if (memSizeMB < 10)
                return "" + memSizeMB.ToString("0.00") + "Mb";
            else if (memSizeMB < 100)
                return "" + memSizeMB.ToString("0.0") + "Mb";
            else
                return "" + memSizeMB.ToString("0") + "Mb";
        }
    }

    float GetBitsPerPixel(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Alpha8: //	 Alpha-only texture format.
                return 8;
            case TextureFormat.ARGB4444: //	 A 16 bits/pixel texture format. Texture stores color with an alpha channel.
                return 16;
            case TextureFormat.RGBA4444: //	 A 16 bits/pixel texture format.
                return 16;
            case TextureFormat.RGB24:   // A color texture format.
                return 24;
            case TextureFormat.RGBA32:  // Color with an alpha channel texture format.
                return 32;
            case TextureFormat.ARGB32:  // Color with an alpha channel texture format.
                return 32;
            case TextureFormat.RGB565:  //	 A 16 bit color texture format.
                return 16;
            case TextureFormat.DXT1:    // Compressed color texture format.
                return 4;
            case TextureFormat.DXT1Crunched:    // Crunched formats uses VRAM as uncrunched ones.
                return 4;
            case TextureFormat.DXT5:    // Compressed color with alpha channel texture format.
                return 8;
            case TextureFormat.DXT5Crunched: // Crunched formats uses VRAM as uncrunched ones.
                return 8;
            case TextureFormat.BC4:    // Compressed R channel texture format. 
                return 4;
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
            case TextureFormat.ETC2_RGBA8:
                return 8;
            case TextureFormat.ETC2_RGB:
                return 4;
            case TextureFormat.EAC_R:
                return 4;
            case TextureFormat.BGRA32://	 Format returned by iPhone camera
                return 32;
#if UNITY_2019_1_OR_NEWER
            case TextureFormat.ETC2_RGBA8Crunched: // Crunched format uses VRAM as uncrunched one.
                return 4;
            case TextureFormat.ETC_RGB4Crunched: // Crunched formats uses VRAM as uncrunched ones.
                return 4;
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
#endif
        }
        return 0;
    }

    float CalculateTextureSizeBytes(Texture tTexture)
    {

        int tWidth = tTexture.width;
        int tHeight = tTexture.height;
        if (tTexture is Texture2D)
        {
            Texture2D tTex2D = tTexture as Texture2D;
            float bitsPerPixel = GetBitsPerPixel(tTex2D.format);
            int mipMapCount = tTex2D.mipmapCount;
            int mipLevel = 1;
            float tSize = 0;
            while (mipLevel <= mipMapCount)
            {
                tSize += tWidth * tHeight * bitsPerPixel / 8;
                tWidth = tWidth / 2;
                tHeight = tHeight / 2;
                mipLevel++;
            }
            return tSize;
        }
        if (tTexture is Texture2DArray)
        {
            Texture2DArray tTex2D = tTexture as Texture2DArray;
            float bitsPerPixel = GetBitsPerPixel(tTex2D.format);
            int mipMapCount = 10;
            int mipLevel = 1;
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
        if (tTexture is Cubemap)
        {
            Cubemap tCubemap = tTexture as Cubemap;
            float bitsPerPixel = GetBitsPerPixel(tCubemap.format);
            return tWidth * tHeight * 6 * bitsPerPixel / 8;
        }
        return 0;
    }

    void SelectObject(Object selectedObject, bool append)
    {
        if (append)
        {
            List<Object> currentSelection = new List<Object>(Selection.objects);
            // Allow toggle selection
            if (currentSelection.Contains(selectedObject)) currentSelection.Remove(selectedObject);
            else currentSelection.Add(selectedObject);

            Selection.objects = currentSelection.ToArray();
        }
        else Selection.activeObject = selectedObject;
    }

    void SelectObjects(List<Object> selectedObjects, bool append)
    {
        if (append)
        {
            List<Object> currentSelection = new List<Object>(Selection.objects);
            currentSelection.AddRange(selectedObjects);
            Selection.objects = currentSelection.ToArray();
        }
        else Selection.objects = selectedObjects.ToArray();
    }

    void AddMissing(Transform transform, string type)
    {
        MissingObjects.Add(new Missing
        {
            Object = transform,
            type = type,
            name = transform.name
        });
        thingsMissing = true;
    }

    #endregion Helper Functions
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