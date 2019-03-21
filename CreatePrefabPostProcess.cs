////////////////////////////////////////////////////////////////////////
// Created By Games Made Right 2017
// Free for use anywhere and everywhere.
// I there are questions or comments please contact me at
// 
//               http://www.gamesmaderight.com
//                  adam@gamesmaderight.com
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Directory = System.IO.Directory;

/// <summary>
/// A post processor that handles the creation of our character prefabs.
/// Also enforces import settings for different file types
/// </summary>
class postProcessCharacterMesh : AssetPostprocessor
{
    /// <summary>
    /// For our example character, limit the path to only be conan
    /// </summary>
    private static readonly string _baseRigPath = "Assets/Characters/Conan";

    /// <summary>
    /// name of our base file to work on
    /// </summary>
    private static string _baseRigString = "_baseRig";

    /// <summary>
    /// the name of prefab being saved
    /// </summary>
    private static string _prefabName;

                ///////////////////////////////////////////////
                /// Animation Variables
                ///////////////////////////////////////////////

    /// <summary>
    /// Returns the animation path of this character
    /// </summary>
    private static string AnimationPath
    {
        get
        {
            return _baseRigPath + "/Animation";
        }
    }

    private static Animator _animationComponent;
    private static RuntimeAnimatorController _animatorController;
    private static string animControllerName = "_Controller";

                ///////////////////////////////////////////////
                /// Material Variables
                ///////////////////////////////////////////////
     
    /// <summary>
    /// returns the texture path relative to the base rig path
    /// </summary>
    private static string TexturePath
    {
        get
        {
            return _baseRigPath + "/Textures"; 
            
        }
    }

    /// <summary>
    /// Returns the materials path for this character
    /// </summary>
    private static string MaterialsPath
    {
        get
        {
            return _baseRigPath + "/Materials";
        }
    }
    private static List<string> _materialFiles;

    /// <summary>
    /// Main material asset on disk
    /// </summary>
    private static Material _mainMaterial;

    /// <summary>
    /// Names for texture maps
    /// </summary>
    private static string normalMap = "_normal";
    private static string specularMap = "_specular";
    private static string diffuseMap = "_diffuse";

                ///////////////////////////////////////////////
                ///                 Methods
                ///////////////////////////////////////////////

    /// <summary>
    /// 
    /// 1. Detect importing of base mesh and get the capture the name from Unity. 
    /// 2. Validate the import settings of the baseMesh based on our requirements above.
    /// 3. Check the animation import settings based on our requirements.
    /// 
    /// Catches imports of certain types and sets the import settings properly.
    /// Needs to run first so that it doesnt trigger multiple imports and rebuilds of prefabs
    /// </summary>
    public void OnPreprocessModel()
    {
        //An instance of the model import to pass around
        ModelImporter modelImporter = (ModelImporter)assetImporter;

        //Check to see if this asset is in the animation directory and process it
        if (modelImporter.assetPath.IndexOf(AnimationPath, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            SetAnimationSettings(modelImporter);
        }
        
        //Check to see if this file is in the baseRig Path AND has the baseRig string in it
        else if (modelImporter.assetPath.IndexOf(_baseRigPath, StringComparison.OrdinalIgnoreCase) >= 0 &&
            modelImporter.assetPath.IndexOf(_baseRigString, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            SetBaseRigSettings(modelImporter);
        }

    }

    /// <summary>
    /// Manages known settings for base meshes on import
    /// </summary>
    /// <param name="modelImporter"></param>
    private void SetBaseRigSettings(ModelImporter modelImporter)
    {
        modelImporter.meshCompression = ModelImporterMeshCompression.Off;
        modelImporter.optimizeMesh = false;
        modelImporter.isReadable = false;
        modelImporter.importBlendShapes = false;
        modelImporter.weldVertices = false;
        modelImporter.importMaterials = false;
        modelImporter.globalScale = 1;
        modelImporter.animationType = ModelImporterAnimationType.Human;

        Debug.Log(string.Format("Set BaseRig settings on {0}", assetPath));

    }

    /// <summary>
    /// Manages known settings for animation imports
    /// </summary>
    /// <param name="modelImporter"></param>
    private void SetAnimationSettings(ModelImporter modelImporter)
    {
        modelImporter.globalScale = 1;
        modelImporter.importMaterials = false;
        modelImporter.optimizeMesh = true;
        modelImporter.isReadable = false;
        modelImporter.importBlendShapes = false;
        modelImporter.importNormals = ModelImporterNormals.None;
        modelImporter.meshCompression = ModelImporterMeshCompression.High;

        modelImporter.animationType = ModelImporterAnimationType.Human;

        Debug.Log(string.Format("Set Animation settings on {0}", assetPath));
    }

    /// <summary>
    /// 
    /// 4. Check all texture import settings based on requirements.
    ///
    /// Check the file path and the post fix for the file
    /// </summary>
    private void OnPreprocessTexture()
    {
        TextureImporter texturImporter = (TextureImporter) assetImporter;

        if (assetPath.IndexOf(TexturePath, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (assetPath.IndexOf(normalMap, StringComparison.OrdinalIgnoreCase) >= 0 )
            {
                texturImporter.textureType = TextureImporterType.NormalMap;
                texturImporter.mipmapEnabled = false;
                texturImporter.isReadable = false;

                Debug.Log(string.Format("Set normalMap settings on {0}", assetPath));

            }

            if (assetPath.IndexOf(specularMap, StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf(diffuseMap, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                texturImporter.textureType = TextureImporterType.Default;
                texturImporter.mipmapEnabled = false;
                texturImporter.isReadable = false;
                texturImporter.sRGBTexture = true;

                Debug.Log(string.Format("Set Spec/Color settings on {0}", assetPath));

            }
        }

    }

    /// <summary>
    /// Runs after all assets are imported and creates any character meshes
    /// </summary>
    /// <param name="importedAssets"></param>
    /// <param name="deletedAssets"></param>
    /// <param name="movedAssets"></param>
    /// <param name="movedFromAssetPaths"></param>
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
        string[] movedFromAssetPaths)
    {

        List<string> baseMeshImports = new List<string>();

        //Pull out the assets inside the proper folders
        foreach (string importedAsset in importedAssets)
        {
            if (!importedAsset.Contains(_baseRigPath))
            {
                return;
            }

            //ignore the casing, in case things are typed wrong.
            //Grab only assets that have the _baseRigString in its name.
            Match match = Regex.Match(importedAsset, _baseRigString, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                //Add it to our import List
                baseMeshImports.Add(importedAsset);
            }
        }

        //Figure out the name of each asset and build it based on that
        foreach (var baseMesh in baseMeshImports)
        {
            //Remove the extension from the filename so we dont have to mess with it.
            var fileName = Path.GetFileNameWithoutExtension(baseMesh);

            if (fileName != null)
            {
                //Name the prefab without the _baseRigString so it clear what it is
                _prefabName = fileName.Replace(_baseRigString, "");

                //Begin construction
                ConstructPrefab(baseMesh);
            }
        }

    }

    /// <summary>
    /// Initial call to create a character mesh. 
    /// </summary>
    /// <param name="importedFilePath"></param>
    private static void ConstructPrefab(string importedFilePath)
    {
        //Load the asset from the project
        var importedFile = (GameObject) AssetDatabase.LoadAssetAtPath(importedFilePath, typeof(GameObject));

        //Instantiate the prefab in to the scene so we can start to work with it
        var instantiatedBaseMesh = GameObject.Instantiate(importedFile);

        //5. Create a material file to contain the textures
        GetorCreateMaterial();

        //6. Get all textures associated with this baseMesh
        //7. Set the materials textures to use the available textures.
        SetMaterialTextures();

        //8.Grab all the mesh objects and apply the new material to it.
        ApplyMaterialsToMesh(instantiatedBaseMesh);

        //9. Get all animations associated with this base mesh and create a controller
        //10. Create an animation controller for this base mesh
        SetupAnimationController();

        //Start putting the prefab together
        CreateCharacterPrefab(instantiatedBaseMesh);

    }

    /// <summary>
    /// Creates a character prefab, materials and controller
    /// </summary>
    /// <param name="characterPrefab"></param>
    private static void CreateCharacterPrefab(GameObject characterPrefab)
    {
        Selection.activeGameObject = characterPrefab;

        //Set the name of the object to be the prefab name
        characterPrefab.name = _prefabName;

        //11. Link animation controller to the prefab
        _animationComponent = characterPrefab.GetComponent<Animator>();
        _animationComponent.runtimeAnimatorController = _animatorController;
        
        //12. Save\Create the prefab in the project hierarchy 
        string prefabSavePath = string.Format("{0}/{1}_PF.prefab", _baseRigPath, _prefabName);
        PrefabUtility.CreatePrefab(prefabSavePath, characterPrefab);

        GameObject.DestroyImmediate(characterPrefab);
    }

    /// <summary>
    /// Tries to get a material from the asset directory based on the asset name
    /// Ignores it if there is no material
    /// </summary>
    /// <param name="mesh"></param>
    public static void GetorCreateMaterial()
    {
        string materialName = _prefabName + "_Material.mat";

        //clear out the main material in case it is filled in from previous import
        _mainMaterial = null;

        //Make sure the materials directoy exists before adding stuff to it
        if (!Directory.Exists(MaterialsPath))
        {
            Directory.CreateDirectory(MaterialsPath);
        }

        Debug.Log(Directory.Exists(MaterialsPath));

        //Check all the files in the directory for the main material
        foreach(var file in Directory.GetFiles(MaterialsPath, "*.mat", SearchOption.TopDirectoryOnly))
        {
            //compare strings an check for the materialname in the file
            Match match = Regex.Match(file, materialName, RegexOptions.IgnoreCase);

            //If the file exists, set the main material variable with the project file
            if (match.Success)
            {
                _mainMaterial = (Material)AssetDatabase.LoadAssetAtPath(file, typeof(Material));
            }
        }

        //If the main material is still empty, create a material
        if (_mainMaterial == null)
        {
            //create a new material
            var tempMaterial = new Material(Shader.Find("Standard (Specular setup)"));

            //save material to the project directory
            AssetDatabase.CreateAsset(tempMaterial, Path.Combine(MaterialsPath, materialName));

            //load up the the new material and store it
            _mainMaterial = (Material)AssetDatabase.LoadAssetAtPath(Path.Combine(MaterialsPath, materialName), typeof(Material));

        }

    }

    /// <summary>
    /// Applys the material to all meshes in the prefab
    /// Doesnt allow for multiple meshes or materials
    /// </summary>
    private static void ApplyMaterialsToMesh(GameObject baseMesh)
    {
        foreach (var mesh in baseMesh.GetComponentsInChildren<Renderer>())
        {
            mesh.sharedMaterial = _mainMaterial;
        }
    }

    /// <summary>
    /// Grabs the specific texture files and applies them to the existing material
    /// </summary>
    private static void SetMaterialTextures()
    {
        //Grab the diffuse texture if it exists
        string texturePath = GetFilesPathNotMeta(TexturePath, _prefabName + diffuseMap); 

        Texture2D diffuseTex = (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));

        if (diffuseTex != null)
        {
            _mainMaterial.SetTexture("_MainTex", diffuseTex);
        }

        //Grab the specular map path if it exists
        texturePath = GetFilesPathNotMeta(TexturePath, _prefabName + specularMap);

        Texture2D specMapTex = (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));

        if (specMapTex != null)
        {
            _mainMaterial.SetTexture("_SpecGlossMap", specMapTex);
        }

        //Grab the Normal map path if it exists
        texturePath = GetFilesPathNotMeta(TexturePath, _prefabName + normalMap);

        Texture2D normalMapTex = (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));

        if (normalMapTex != null)
        {
            _mainMaterial.SetTexture("_BumpMap", normalMapTex);
        }
    }

    /// <summary>
    /// Checks to see if an animation controller already exists, creates one if not
    /// </summary>
    private static void SetupAnimationController()
    {
        //Clear out the controller in case it is left over from a previous import
        _animatorController = null;

        string controllerFilePath = GetFilesPathNotMeta(AnimationPath, _prefabName + animControllerName);

        if (!string.IsNullOrEmpty(controllerFilePath))
        {
            _animatorController = (RuntimeAnimatorController)AssetDatabase.LoadAssetAtPath(controllerFilePath, typeof(RuntimeAnimatorController));
        }else
        {
            string controllerSavePath = Path.Combine(AnimationPath, _prefabName + animControllerName + ".controller");
            _animatorController = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerSavePath);
        }
    }

    /// <summary>
    /// Return the raw fbx file, not a meta file
    /// </summary>
    /// <param name="filePath">Root Directory to start with</param>
    ///
    /// <param name="searchString"> string to search in the file path</param>
    /// <returns></returns>
    private static string GetFilesPathNotMeta(string filePath, string searchString)
    {
        return Directory.GetFiles(filePath).FirstOrDefault(
            x => x.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0 &&
                 x.IndexOf(".meta") < 0);
    }
}
