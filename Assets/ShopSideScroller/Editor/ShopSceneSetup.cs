using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ShopSideScroller;

public static class ShopSceneSetup
{
    public const string Folder = "Assets/ShopSideScroller";
    public const string ReportFolder = "C:/Users/MrDer/Documents/Codex/2026-09-06/computer-plugin-computer-use-openai-bundled-6/work/sidescroller";
    private const string Sheet = "Assets/pixellab-In-the-provided-image--I-want--1791043563247.png";

    [MenuItem("Tools/Shop SideScroller/Set Up Player and Ground")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before setup.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open the verified SampleScene first.");
        if (GameObject.Find("Shop SideScroller")) throw new InvalidOperationException("Player setup already exists; it was not duplicated.");
        var background = GameObject.Find("Background")?.GetComponent<SpriteRenderer>();
        if (!background || !background.sprite || !AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet))
            throw new InvalidOperationException("Expected shop artwork or character sheet is missing.");
        var room = background.bounds;
        var camera = Camera.main;
        if (!camera) throw new InvalidOperationException("Main camera missing.");
        Directory.CreateDirectory(ReportFolder);
        EditorSceneManager.SaveScene(scene);
        File.Copy(scene.path, Path.Combine(ReportFolder, "SampleScene.before-player." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity"));
        FolderAt(Folder + "/Art");
        FolderAt(Folder + "/Frames");
        FolderAt(Folder + "/Physics");
        string copy = Folder + "/Art/CharacterSheet.png";
        if (!AssetDatabase.LoadAssetAtPath<Texture2D>(copy) && !AssetDatabase.CopyAsset(Sheet, copy))
            throw new InvalidOperationException("Could not preserve a working copy of the sheet.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(copy);
        // Sprite objects are stored as separate assets; the original importer slicing is untouched.
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(copy);
        float ppu = 18f;
        var idle = Frames(texture, "Idle", ppu, new [] {
            new Rect(0,229,24,25),new Rect(25,229,24,25),new Rect(51,229,24,25),new Rect(76,229,24,25)});
        var run = Frames(texture, "Run", ppu, new [] {
            new Rect(1,178,20,23),new Rect(27,178,20,23),new Rect(52,178,20,23),new Rect(78,178,20,23),
            new Rect(104,178,19,23),new Rect(129,178,20,23),new Rect(155,178,20,23),new Rect(180,178,20,23),
            new Rect(206,178,20,23),new Rect(232,178,19,23)});
        var jump = Frames(texture, "Jump", ppu, new [] {
            new Rect(1,99,21,22),new Rect(27,99,20,24),new Rect(52,104,22,23),new Rect(78,105,22,23),new Rect(103,99,22,21)});
        var crouch = Frames(texture, "Crouch", ppu, new [] {new Rect(104,128,22,23)});
        var action = Frames(texture, "Sword", ppu, new [] {
            new Rect(0,24,26,24),new Rect(25,24,26,24),new Rect(50,24,27,24),new Rect(76,24,27,24),
            new Rect(101,24,27,24),new Rect(127,24,27,24),new Rect(153,24,27,24),new Rect(178,24,27,24)});

        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up shop side-scroller");
        var root = new GameObject("Shop SideScroller");
        Undo.RegisterCreatedObjectUndo(root, "Create player setup");
        var originals = new GameObject("Original character references (preserved)");
        Undo.RegisterCreatedObjectUndo(originals, "Preserve references");
        originals.transform.SetParent(root.transform);
        foreach (var name in new [] {"pixellab-In-the-provided-image--I-want--1791043563247_0", "Screenshot 2026-10-01 100517_0"})
        {
            var go = GameObject.Find(name);
            if (!go) continue;
            Undo.SetTransformParent(go.transform, originals.transform, "Preserve reference");
        }
        originals.SetActive(false);
        // These pre-existing large boxes cover the drawn shelves/truck and block a side-scrolling lane.
        // Keep every object and its dimensions intact, disabling only those collision components.
        var oldCollision = GameObject.Find("ShopBackground");
        int disabled = 0;
        if (oldCollision) foreach (var col in oldCollision.GetComponentsInChildren<Collider2D>())
        { Undo.RecordObject(col, "Keep original collision as reference"); col.enabled = false; disabled++; }

        float floor = room.min.y + room.size.y * (16f / 192f);
        float left = room.min.x + room.size.x * (28f / 344f);
        float right = room.min.x + room.size.x * (324f / 344f);
        var collision = new GameObject("Solid floor and room boundaries");
        collision.transform.SetParent(root.transform);
        var material = new PhysicsMaterial2D("Player - no friction") { friction = 0, bounciness = 0 };
        AssetDatabase.CreateAsset(material, Folder + "/Physics/PlayerNoFriction.physicsMaterial2D");
        Box(collision.transform, "Floor - aligned to metal deck", new Vector2((left+right)*.5f, floor-.25f), new Vector2(right-left+1f,.5f),material);
        Box(collision.transform, "Left boundary", new Vector2(left-.25f,floor+3), new Vector2(.5f,8),material);
        Box(collision.transform, "Right boundary", new Vector2(right+.25f,floor+3), new Vector2(.5f,8),material);

        var player = new GameObject("Player - Ninja Frog");
        player.transform.SetParent(root.transform);
        player.transform.position = new Vector3(Mathf.Lerp(left,right,.32f), floor + .05f, -.1f);
        var sprite = player.AddComponent<SpriteRenderer>();
        sprite.sprite = idle[0];
        sprite.sortingOrder = 20;
        sprite.sharedMaterial = background.sharedMaterial;
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var capsule = player.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(.7f,1.25f);
        capsule.offset = new Vector2(0,.625f);
        capsule.sharedMaterial = material;
        var controller = player.AddComponent<ShopPlayerController>();
        controller.standingHeight = 1.25f;
        controller.Configure(sprite,idle,run,jump,crouch,action,player.transform.position);
        controller.killY = floor-4f;

        Undo.RecordObject(camera,"Frame shop scene");
        Undo.RecordObject(camera.transform,"Center shop camera");
        camera.orthographic = true;
        camera.transform.position = new Vector3(room.center.x,room.center.y,-10);
        camera.transform.localScale = Vector3.one;
        camera.orthographicSize = Mathf.Max(room.extents.y+.18f,(room.extents.x+.18f)/Mathf.Max(.1f,camera.aspect));
        camera.allowMSAA = false;
        camera.allowHDR = false;
        camera.backgroundColor = new Color(.055f,.062f,.08f,1);
        var framing = Undo.AddComponent<ShopRoomCamera>(camera.gameObject);
        framing.roomCenter = room.center;
        framing.roomSize = room.size;
        EditorUtility.SetDirty(framing);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = player;
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(new Bounds(room.center,room.size),false);
        File.WriteAllText(Path.Combine(ReportFolder,"setup-report.txt"),
            "Scene: "+scene.path+"\nBackground transform preserved.\nRoom bounds: "+room+"\nFloor: "+floor+
            "\nLeft/right: "+left+", "+right+"\nSpawn: "+player.transform.position+"\nOriginal colliders preserved/disabled: "+disabled+
            "\nFrames: idle4, run10, jump5, crouch1, sword8\nRender pipeline: "+(GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.GetType().FullName:"BuiltIn"));
        Debug.Log("SHOP SETUP COMPLETE: Player, sprite sequences, ground and boundaries saved in SampleScene.");
    }

    private static Sprite[] Frames(Texture2D texture,string sequence,float ppu,Rect[] rects)
    {
        return rects.Select((rect,i)=>{
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,0),ppu,0,SpriteMeshType.FullRect);
            sprite.name=sequence+"_"+i.ToString("00");
            AssetDatabase.CreateAsset(sprite,Folder+"/Frames/"+sprite.name+".asset");
            return sprite;
        }).ToArray();
    }
    private static void Box(Transform parent,string name,Vector2 position,Vector2 size,PhysicsMaterial2D material)
    { var go=new GameObject(name); go.transform.SetParent(parent); go.transform.position=position; var box=go.AddComponent<BoxCollider2D>(); box.size=size;box.sharedMaterial=material; }
    private static void FolderAt(string path)
    { if(AssetDatabase.IsValidFolder(path))return; var parent=Path.GetDirectoryName(path).Replace('\\','/'); FolderAt(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
}
