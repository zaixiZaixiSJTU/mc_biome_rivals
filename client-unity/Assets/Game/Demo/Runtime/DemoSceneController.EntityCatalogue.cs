using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
        private void StartEntityCatalogueCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StartCoroutine(CaptureEntityCatalogue());
#else
            Debug.LogError("Entity catalogue requires a Development Player.");
            Application.Quit(2);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable]
        private sealed class EntityCatalogueReport
        {
            public int schemaVersion = 1;
            public int tileSize = 512;
            public string coverage = "36 registered card variants; both facings; idle t=0 and t=0.23. Oblique perspective only, not six-face or gameplay proof.";
            public List<EntityCatalogueEntry> entries = new List<EntityCatalogueEntry>();
        }

        [Serializable]
        private sealed class EntityCatalogueEntry
        {
            public string cardId, cardName, geometryId, modelKey, geometryTextSha256;
            public string overlayGeometryId;
            public bool player;
            public float yaw, idleSeconds;
            public int trackCount, page, row, column;
            public Vector3 cameraPosition, cameraTarget;
            public float fieldOfView;
            public string[] textures;
            public string image, imageSha256;
        }

        private IEnumerator CaptureEntityCatalogue()
        {
            // Give the startup scene one frame to finish; the audit camera renders only layer 30.
            yield return null;
            try
            {
                WriteEntityCatalogue(GetCommandLineValue("-captureEntityCatalogue"));
                Debug.Log("Entity catalogue completed: 36 registrations, 144 renders.");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(2);
            }
        }

        private void WriteEntityCatalogue(string requestedDirectory)
        {
            if (string.IsNullOrWhiteSpace(requestedDirectory) || !Path.IsPathRooted(requestedDirectory))
                throw new ArgumentException("Catalogue requires an absolute, new output directory.");
            var output = Path.GetFullPath(requestedDirectory);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Catalogue output already exists.");
            if (!DemoWorldAssetProvider.LocalAssetsEnabled) throw new InvalidOperationException("Local MC assets are disabled.");
            var ids = DemoMinecraftModelFactory.GetRegisteredEntityCardIds().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (ids.Length != 36 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidOperationException("Catalogue coverage contract changed; review before recording success.");
            var shader = Shader.Find("BiomeRivals/Demo/Entity");
            if (shader == null || !shader.isSupported) throw new MissingReferenceException("Real entity shader unavailable.");
            Directory.CreateDirectory(output);
            var report = new EntityCatalogueReport();
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var rig = new GameObject("EntityCatalogueRig");
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.17f, 0.20f, 1f);
            camera.fieldOfView = 35f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            var tile = new Texture2D(512, 512, TextureFormat.RGB24, false);
            Texture2D sheet = null;
            GameObject actor = null;
            var previousTarget = RenderTexture.active;
            try
            {
                var canvasRoot = new GameObject("CatalogueLabels", typeof(RectTransform), typeof(Canvas));
                canvasRoot.transform.SetParent(rig.transform, false);
                var canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                var label = CreateText(canvasRoot.transform, "Identity", new Vector2(0, 215), new Vector2(500, 75),
                    "", 16, Color.white, TextAnchor.UpperCenter, FontStyle.Normal);
                SetCatalogueLayer(canvasRoot.transform);
                for (var index = 0; index < ids.Length; index++)
                {
                    if (index % 6 == 0) sheet = new Texture2D(2048, 3072, TextureFormat.RGB24, false);
                    var id = ids[index];
                    if (!CardContentLoader.Current.TryGetName(id, out var cardName) ||
                        !DemoMinecraftModelFactory.TryGetModelKey(id, out var modelKey) ||
                        !DemoMinecraftModelFactory.TryGetGeometryId(id, out var geometryId))
                        throw new InvalidOperationException("Incomplete registered identity: " + id);
                    var geometry = Resources.Load<TextAsset>("DemoWorld/entity_models/" + modelKey);
                    if (geometry == null) throw new MissingReferenceException(modelKey);
                    for (var side = 0; side < 2; side++)
                    {
                        var usedTextures = new SortedSet<string>(StringComparer.Ordinal);
                        actor = new GameObject("Catalogue_" + id);
                        Material MaterialFor(string key)
                        {
                            var texture = DemoWorldAssetProvider.LoadBlockTexture(key);
                            if (texture == null) throw new MissingReferenceException("No placeholder allowed: " + key);
                            usedTextures.Add(key + ":" + texture.width + "x" + texture.height);
                            if (DemoMinecraftModelFactory.TryGetSurfaceBaseTextureKey(key, out var baseKey))
                            {
                                var skin = DemoWorldAssetProvider.LoadBlockTexture(baseKey);
                                if (skin == null) throw new MissingReferenceException("Missing registered skin: " + baseKey);
                                usedTextures.Add(baseKey + ":" + skin.width + "x" + skin.height);
                            }
                            if (!materials.TryGetValue(key, out var material))
                            {
                                var boost = key == "entity_blaze" || key == "entity_magma_cube" ? 0.3f : 0f;
                                material = DemoWorldAssetProvider.CreateEntityMaterial("Catalogue_" + key, Color.white, key, shader, boost);
                                materials.Add(key, material);
                            }
                            return material;
                        }
                        if (!DemoMinecraftModelFactory.TryBuild(actor.transform, id, side == 0, MaterialFor))
                            throw new InvalidOperationException("Real model build failed: " + id);
                        SetCatalogueLayer(actor.transform);
                        var renderers = actor.GetComponentsInChildren<MeshRenderer>();
                        if (renderers.Length == 0) throw new InvalidOperationException("Empty model: " + id);
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        if (bounds.size.sqrMagnitude < 0.0001f) throw new InvalidOperationException("Degenerate model: " + id);
                        var center = bounds.center;
                        var distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.35f;
                        camera.transform.position = center + new Vector3(4f, 2.4f, -6f).normalized * distance;
                        camera.transform.LookAt(center);
                        var animator = actor.GetComponent<DemoEntityIdleAnimator>();
                        if (animator != null) animator.enabled = false;
                        for (var phase = 0; phase < 2; phase++)
                        {
                            var seconds = phase == 0 ? 0f : 0.23f;
                            if (animator != null) animator.SampleForAudit(seconds);
                            label.text = id + "  " + cardName + "\n" + (side == 0 ? "Player 180" : "Opponent 0") + " | t=" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                            Canvas.ForceUpdateCanvases();
                            camera.Render();
                            RenderTexture.active = target;
                            tile.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                            tile.Apply();
                            var png = tile.EncodeToPNG();
                            var filename = id + "-" + side + "-" + phase + ".png";
                            File.WriteAllBytes(Path.Combine(output, filename), png);
                            var column = side * 2 + phase;
                            sheet.SetPixels(column * 512, (5 - index % 6) * 512, 512, 512, tile.GetPixels());
                            report.entries.Add(new EntityCatalogueEntry
                            {
                                cardId = id, cardName = cardName, geometryId = geometryId, modelKey = modelKey,
                                overlayGeometryId = DemoMinecraftModelFactory.TryGetOverlayGeometryId(id, out var overlayId) ? overlayId : null,
                                geometryTextSha256 = CatalogueHash(Encoding.UTF8.GetBytes(geometry.text)),
                                player = side == 0, yaw = actor.transform.eulerAngles.y, idleSeconds = seconds,
                                trackCount = animator == null ? 0 : animator.TrackCount,
                                cameraPosition = camera.transform.position, cameraTarget = center, fieldOfView = camera.fieldOfView,
                                textures = usedTextures.ToArray(), page = index / 6 + 1, row = index % 6, column = column,
                                image = filename, imageSha256 = CatalogueHash(png)
                            });
                        }
                        DestroyCatalogueActor(actor);
                        actor = null;
                    }
                    if (index % 6 == 5)
                    {
                        sheet.Apply();
                        File.WriteAllBytes(Path.Combine(output, "page-" + (index / 6 + 1) + ".png"), sheet.EncodeToPNG());
                        DestroyImmediate(sheet);
                        sheet = null;
                    }
                }
                File.WriteAllText(Path.Combine(output, "catalogue.json"), JsonUtility.ToJson(report, true), Encoding.UTF8);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                if (actor != null) DestroyCatalogueActor(actor);
                if (sheet != null) DestroyImmediate(sheet);
                DestroyImmediate(tile);
                target.Release();
                DestroyImmediate(target);
                DestroyImmediate(rig);
                foreach (var material in materials.Values) DestroyImmediate(material);
            }
        }

        private static void DestroyCatalogueActor(GameObject actor)
        {
            foreach (var filter in actor.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) DestroyImmediate(filter.sharedMesh);
            DestroyImmediate(actor);
        }

        private static void SetCatalogueLayer(Transform root)
        {
            root.gameObject.layer = 30;
            foreach (Transform child in root) SetCatalogueLayer(child);
        }

        private static string CatalogueHash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
#endif
    }
}
