using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Unity 6 compatible Fog Tile Creator
/// Usage: In Unity Editor, go to Tools > Create Fog Tile (Unity 6)
/// </summary>
public class FogTileCreatorUnity6 : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Fog Tile (Unity 6)")]
    static void CreateFogTile()
    {
        // Create a simple 16x16 white texture
        Texture2D texture = new Texture2D(16, 16);
        Color[] pixels = new Color[16 * 16];
        
        // Fill with white (will be tinted by tilemap color)
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();

        // Save texture as PNG
        byte[] pngData = texture.EncodeToPNG();
        string texturePath = "Assets/FogTexture.png";
        System.IO.File.WriteAllBytes(texturePath, pngData);
        
        AssetDatabase.Refresh();

        // Import as sprite
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;  // Match your tile size
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        }

        // Load sprite
        Sprite fogSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);

        // Create Tile asset using CreateInstance (Unity 6 way)
        Tile fogTile = ScriptableObject.CreateInstance<Tile>();
        fogTile.sprite = fogSprite;
        fogTile.color = Color.white;
        fogTile.colliderType = Tile.ColliderType.None;

        // Save tile asset
        string tilePath = "Assets/FogTile.asset";
        AssetDatabase.CreateAsset(fogTile, tilePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select the created tile
        Selection.activeObject = fogTile;
        EditorGUIUtility.PingObject(fogTile);

        Debug.Log($"✓ Fog tile created at: {tilePath}\n✓ Fog texture created at: {texturePath}");
    }

    [MenuItem("Tools/Create Fog Tile from Selected Sprite")]
    static void CreateFogTileFromSprite()
    {
        // Get selected sprite
        Sprite selectedSprite = Selection.activeObject as Sprite;
        
        if (selectedSprite == null)
        {
            Debug.LogError("Please select a Sprite in the Project window first!");
            return;
        }

        // Create Tile asset
        Tile fogTile = ScriptableObject.CreateInstance<Tile>();
        fogTile.sprite = selectedSprite;
        fogTile.color = Color.white;
        fogTile.colliderType = Tile.ColliderType.None;

        // Save tile asset
        string tilePath = "Assets/FogTile_" + selectedSprite.name + ".asset";
        AssetDatabase.CreateAsset(fogTile, tilePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select the created tile
        Selection.activeObject = fogTile;
        EditorGUIUtility.PingObject(fogTile);

        Debug.Log($"✓ Fog tile created at: {tilePath}");
    }
#endif
}
