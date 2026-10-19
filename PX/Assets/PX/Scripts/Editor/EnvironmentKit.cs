using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static PX.EditorTools.BuilderKit;
using LocalKeyword = UnityEngine.Rendering.LocalKeyword;

namespace PX.EditorTools
{
    /// <summary>
    /// Simple art objects for Level 01, after Soheil's concept drafts: dark stone halls with glowing Persian lattice,
    /// pointed arches, pillars banded in gold, the mandala gate, hanging lamps, patterned banners, broken platforms
    /// edged with light over dark water, hanging shards, scaffolds and neon signs, and the giant face. Everything is
    /// primitives and generated textures, in two palettes: the teal-and-orange gate halls and the green-and-magenta
    /// face shrine. The mood comes from darkness, emissive accents, fog and bloom rather than from the geometry.
    /// </summary>
    internal static class EnvironmentKit
    {
        public const string Folder = "Assets/PX/Art/Level01";
        public const string MaterialFolder = Folder + "/Materials";
        public const string TextureFolder = Folder + "/Textures";

        /// <summary>The materials and colours of one act.</summary>
        public sealed class Palette
        {
            public string Name;
            public Material Stone;
            public Material DeepStone;
            public Material Trim;
            public Material Glow;
            public Material Glow2;
            public Material Lattice;
            public Material Banner;
            public Material Water;
            public Material Edge;
            public Material Cloth;
            public Material Neon;
            public Color GlowColor;
            public Color Glow2Color;
            public Color FogColor;
            public Color AmbientSky;
            public Color AmbientEquator;
            public Color AmbientGround;
            public float FogDensity;
        }

        /// <summary>Act 1: the gate halls. Dark teal stone, hot orange light, gold trim, deep water.</summary>
        public static Palette GateHalls()
        {
            var glow = new Color(1f, 0.36f, 0.06f);
            var glow2 = new Color(0.15f, 0.9f, 0.8f);
            return Build("Hall", new Color(0.26f, 0.4f, 0.44f), new Color(0.15f, 0.24f, 0.28f), new Color(0.78f, 0.58f, 0.22f),
                glow, glow2, new Color(0.03f, 0.12f, 0.15f), new Color(0.22f, 0.4f, 0.42f))
                .WithAtmosphere(new Color(0.1f, 0.22f, 0.27f), 0.006f,
                    new Color(0.55f, 0.72f, 0.82f), new Color(0.48f, 0.58f, 0.62f), new Color(0.26f, 0.3f, 0.32f));
        }

        /// <summary>Act 2: the face shrine and the bazaar scaffolds. Olive stone, magenta neon, green glow, ochre haze.</summary>
        public static Palette FaceShrine()
        {
            var glow = new Color(1f, 0.12f, 0.55f);
            var glow2 = new Color(0.3f, 1f, 0.5f);
            return Build("Shrine", new Color(0.38f, 0.42f, 0.31f), new Color(0.22f, 0.25f, 0.18f), new Color(0.7f, 0.6f, 0.3f),
                glow, glow2, new Color(0.04f, 0.09f, 0.05f), new Color(0.32f, 0.35f, 0.24f))
                .WithAtmosphere(new Color(0.32f, 0.3f, 0.2f), 0.006f,
                    new Color(0.65f, 0.65f, 0.48f), new Color(0.52f, 0.53f, 0.4f), new Color(0.26f, 0.28f, 0.2f));
        }

        private static Palette Build(string name, Color stone, Color deep, Color trim, Color glow, Color glow2, Color water, Color cloth)
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);
            Texture2D lattice = LatticeTexture();
            Texture2D tile = TileTexture();
            return new Palette
            {
                Name = name,
                Stone = LoadOrCreateMaterial(MaterialFolder, $"{name} Stone", stone, lit: true, smoothness: 0.3f),
                DeepStone = LoadOrCreateMaterial(MaterialFolder, $"{name} Deep Stone", deep, lit: true, smoothness: 0.2f),
                Trim = LoadOrCreateMetal(MaterialFolder, $"{name} Trim", trim),
                Glow = LoadOrCreateEmissive(MaterialFolder, $"{name} Glow", glow * 0.5f, glow, 3.2f),
                Glow2 = LoadOrCreateEmissive(MaterialFolder, $"{name} Glow 2", glow2 * 0.4f, glow2, 2.2f),
                Lattice = LoadOrCreateCutout(MaterialFolder, $"{name} Lattice", lattice, trim * 0.6f, glow, 2.4f),
                Banner = LoadOrCreatePatterned(MaterialFolder, $"{name} Banner", tile, cloth, glow, 1.6f),
                Water = LoadOrCreateWater(MaterialFolder, $"{name} Water", water),
                Edge = LoadOrCreateEmissive(MaterialFolder, $"{name} Edge", glow * 0.3f, glow, 2.6f),
                Cloth = LoadOrCreateMaterial(MaterialFolder, $"{name} Cloth", cloth, lit: true, smoothness: 0.1f),
                Neon = LoadOrCreateEmissive(MaterialFolder, $"{name} Neon", glow2 * 0.4f, glow2, 2.2f),
                GlowColor = glow,
                Glow2Color = glow2,
            };
        }

        private static Palette WithAtmosphere(this Palette palette, Color fog, float density, Color sky, Color equator, Color ground)
        {
            palette.FogColor = fog;
            palette.FogDensity = density;
            palette.AmbientSky = sky;
            palette.AmbientEquator = equator;
            palette.AmbientGround = ground;
            return palette;
        }

        // ------------------------------------------------------------ materials

        public static Material LoadOrCreateEmissive(string folder, string name, Color baseColor, Color emission, float intensity)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(FindShader("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", 0.4f);
            SetEmission(material, emission, intensity);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material LoadOrCreateMetal(string folder, string name, Color color)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(FindShader("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0.75f);
            material.SetFloat("_Smoothness", 0.65f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Lines of light on a see-through surface: the lattice screens and grilles.</summary>
        private static Material LoadOrCreateCutout(string folder, string name, Texture2D pattern, Color baseColor, Color emission, float intensity)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(FindShader("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", pattern);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", 0.5f);
            material.SetTexture("_EmissionMap", pattern);
            material.SetTextureScale("_BaseMap", new Vector2(2.5f, 2.5f));
            SetEmission(material, emission, intensity);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Dark cloth with a glowing pattern woven through it: the banners and the hulls of the craft.</summary>
        private static Material LoadOrCreatePatterned(string folder, string name, Texture2D pattern, Color baseColor, Color emission, float intensity)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(FindShader("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetTexture("_EmissionMap", pattern);
            material.SetTextureScale("_BaseMap", new Vector2(2f, 6f));
            SetEmission(material, emission, intensity);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LoadOrCreateWater(string folder, string name, Color color)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(FindShader("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0.2f);
            material.SetFloat("_Smoothness", 0.96f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void SetEmission(Material material, Color emission, float intensity)
        {
            material.SetColor("_EmissionColor", emission * intensity);
            EnableEmission(material);
        }

        /// <summary>
        /// Turns the Lit shader's emission on. The keyword is set through the shader's own local keyword as well as by
        /// name: set by name alone before the asset exists, it was not written to the file.
        /// </summary>
        public static void EnableEmission(Material material)
        {
            var keyword = new LocalKeyword(material.shader, "_EMISSION");
            if (keyword.isValid)
                material.SetKeyword(keyword, true);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
        }

        /// <summary>Every material in a folder with an emission colour gets its emission keyword, in case it was lost.</summary>
        public static int RepairEmission(string folder)
        {
            int repaired = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_EmissionColor"))
                    continue;
                if (material.GetColor("_EmissionColor").maxColorComponent <= 0.01f)
                    continue;
                if (material.IsKeywordEnabled("_EMISSION"))
                    continue;
                EnableEmission(material);
                repaired++;
            }
            return repaired;
        }

        /// <summary>A particle material tinted a colour: the rings of light on the water and in the shrines.</summary>
        public static Material LoadOrCreateRingMaterial(string folder, string name, Color tint)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Material template = LoadOrCreateParticleMaterial(folder, "WaterRing", ring: true, additive: true);
            material = new Material(template);
            material.SetColor("_BaseColor", tint);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------ textures

        /// <summary>
        /// A tiling girih lattice: an eight-pointed star in each tile, its points joined to the tile's edges, drawn as
        /// soft white lines on transparency. Used as a cutout so light shows through the gaps, and as an emission map.
        /// </summary>
        public static Texture2D LatticeTexture()
        {
            string path = $"{TextureFolder}/Lattice.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int size = 256;
            var segments = new List<(Vector2 a, Vector2 b)>();
            Vector2 c = new Vector2(0.5f, 0.5f);
            const float r = 0.3f;
            // Two squares, one turned by 45 degrees, make the star.
            for (int k = 0; k < 4; k++)
            {
                float a0 = k * Mathf.PI * 0.5f, a1 = (k + 1) * Mathf.PI * 0.5f;
                segments.Add((c + Polar(r, a0), c + Polar(r, a1)));
                segments.Add((c + Polar(r, a0 + Mathf.PI * 0.25f), c + Polar(r, a1 + Mathf.PI * 0.25f)));
            }
            // Each point of the star reaches out to the edge of the tile, where it meets the next tile's lines.
            for (int k = 0; k < 8; k++)
            {
                float angle = k * Mathf.PI * 0.25f;
                Vector2 point = c + Polar(r, angle);
                Vector2 edge = c + Polar(0.5f / Mathf.Max(Mathf.Abs(Mathf.Cos(angle)), Mathf.Abs(Mathf.Sin(angle))), angle);
                segments.Add((point, edge));
            }
            // A thin frame around the tile.
            segments.Add((new Vector2(0f, 0f), new Vector2(1f, 0f)));
            segments.Add((new Vector2(0f, 0f), new Vector2(0f, 1f)));
            return DrawLines(path, size, segments, 4f, 0.07f);
        }

        /// <summary>A tiling diamond trellis with small rosettes at the crossings: the pattern on the banners and hulls.</summary>
        public static Texture2D TileTexture()
        {
            string path = $"{TextureFolder}/Tile.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int size = 128;
            var segments = new List<(Vector2 a, Vector2 b)>
            {
                (new Vector2(0f, 0.5f), new Vector2(0.5f, 0f)), (new Vector2(0.5f, 0f), new Vector2(1f, 0.5f)),
                (new Vector2(1f, 0.5f), new Vector2(0.5f, 1f)), (new Vector2(0.5f, 1f), new Vector2(0f, 0.5f)),
            };
            var pixels = new Color32[size * size];
            Vector2 centre = new Vector2(0.5f, 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float a = LineAlpha(p, segments, 1.6f / size);
                    // A small ring in the middle of each diamond and a dot at each corner.
                    float ring = Mathf.Clamp01(1f - Mathf.Abs((p - centre).magnitude - 0.12f) / (1.5f / size));
                    float dot = 0f;
                    foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                        dot = Mathf.Max(dot, Mathf.Clamp01(1f - ((p - corner).magnitude - 0.05f) / (1.5f / size)));
                    a = Mathf.Max(a, Mathf.Max(ring, dot));
                    byte v = (byte)(a * 255f);
                    pixels[y * size + x] = new Color32(v, v, v, v);
                }
            }
            return SaveTexture(path, size, size, pixels, clamp: false, mipmaps: true);
        }

        private static Vector2 Polar(float radius, float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        private static Texture2D DrawLines(string path, int size, List<(Vector2 a, Vector2 b)> segments, float widthPixels, float glow)
        {
            var pixels = new Color32[size * size];
            float width = widthPixels / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    // Lines on the tile and on its neighbours, so the pattern tiles without seams.
                    float a = 0f;
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        for (int oy = -1; oy <= 1; oy++)
                            a = Mathf.Max(a, LineAlpha(p - new Vector2(ox, oy), segments, width));
                    }
                    byte v = (byte)(Mathf.Max(a, glow * a) * 255f);
                    pixels[y * size + x] = new Color32(v, v, v, v);
                }
            }
            return SaveTexture(path, size, size, pixels, clamp: false, mipmaps: true);
        }

        private static float LineAlpha(Vector2 p, List<(Vector2 a, Vector2 b)> segments, float width)
        {
            float best = float.MaxValue;
            foreach ((Vector2 a, Vector2 b) in segments)
            {
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return Mathf.Clamp01(1f - (best - width * 0.5f) / (width * 0.5f));
        }

        // ------------------------------------------------------------- modules

        /// <summary>A pointed arch on the far side of the rail: two piers, two leaning lintels meeting in a point, a glowing keystone.</summary>
        public static Transform AddArch(Transform parent, Rail rail, float distance, float lateral, float width, float height, Palette palette)
        {
            Transform arch = PlaceAlongRail(parent, "Arch", rail, distance, 0f, lateral);
            float pier = height * 0.62f;
            AddPart(arch, PrimitiveType.Cube, new Vector3(0f, pier * 0.5f, -width * 0.5f), new Vector3(1.2f, pier, 1.4f), palette.Stone);
            AddPart(arch, PrimitiveType.Cube, new Vector3(0f, pier * 0.5f, width * 0.5f), new Vector3(1.2f, pier, 1.4f), palette.Stone);
            // Gold bands at the top of each pier.
            AddPart(arch, PrimitiveType.Cube, new Vector3(0f, pier - 0.3f, -width * 0.5f), new Vector3(1.35f, 0.25f, 1.55f), palette.Trim);
            AddPart(arch, PrimitiveType.Cube, new Vector3(0f, pier - 0.3f, width * 0.5f), new Vector3(1.35f, 0.25f, 1.55f), palette.Trim);
            float rise = height - pier;
            float span = Mathf.Sqrt(width * width * 0.25f + rise * rise) + 0.6f;
            float tilt = Mathf.Atan2(rise, width * 0.5f) * Mathf.Rad2Deg;
            GameObject left = AddPart(arch, PrimitiveType.Cube, new Vector3(0f, pier + rise * 0.5f, -width * 0.25f), new Vector3(1.0f, 0.9f, span), palette.Stone);
            left.transform.localRotation = Quaternion.Euler(-tilt, 0f, 0f);
            GameObject right = AddPart(arch, PrimitiveType.Cube, new Vector3(0f, pier + rise * 0.5f, width * 0.25f), new Vector3(1.0f, 0.9f, span), palette.Stone);
            right.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            AddPart(arch, PrimitiveType.Cube, new Vector3(0f, height + 0.2f, 0f), new Vector3(1.1f, 0.9f, 0.9f), palette.Glow);
            // A lattice screen fills the arch, so light from behind shows through it in a pattern.
            GameObject screen = AddPart(arch, PrimitiveType.Quad, new Vector3(0.65f, pier * 0.5f, 0f), new Vector3(width - 1.2f, pier, 1f), palette.Lattice);
            screen.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return arch;
        }

        /// <summary>A round pillar banded in gold, with a glowing capital.</summary>
        public static Transform AddPillar(Transform parent, Rail rail, float distance, float lateral, float height, Palette palette, float radius = 0.7f)
        {
            Transform pillar = PlaceAlongRail(parent, "Pillar", rail, distance, 0f, lateral);
            AddPart(pillar, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(radius * 3.2f, 0.8f, radius * 3.2f), palette.DeepStone);
            AddPart(pillar, PrimitiveType.Cylinder, new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), palette.Stone);
            for (float h = 2.5f; h < height - 1f; h += 3f)
                AddPart(pillar, PrimitiveType.Cylinder, new Vector3(0f, h, 0f), new Vector3(radius * 2.2f, 0.12f, radius * 2.2f), palette.Trim);
            AddPart(pillar, PrimitiveType.Cylinder, new Vector3(0f, height - 0.5f, 0f), new Vector3(radius * 2.4f, 0.1f, radius * 2.4f), palette.Glow);
            AddPart(pillar, PrimitiveType.Cube, new Vector3(0f, height + 0.3f, 0f), new Vector3(radius * 3f, 0.6f, radius * 3f), palette.Stone);
            return pillar;
        }

        /// <summary>
        /// The mandala gate: a great disc of glowing lattice in nested rings of stone, standing against the far wall.
        /// Its core is a flat orange glow that shows through the lattice.
        /// </summary>
        public static Transform AddGate(Transform parent, Rail rail, float distance, float lateral, float radius, Palette palette)
        {
            Transform gate = PlaceAlongRail(parent, "Gate", rail, distance, radius + 1f, lateral);
            // The glowing core and the lattice over it face the camera: along the rail's side direction.
            GameObject core = AddPart(gate, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 1.6f, 0.15f, radius * 1.6f), palette.Glow);
            core.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject screen = AddPart(gate, PrimitiveType.Quad, new Vector3(0.35f, 0f, 0f), new Vector3(radius * 1.9f, radius * 1.9f, 1f), palette.Lattice);
            screen.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            // Rings of stone blocks around it, each ring turned a little against the last.
            int ring = 0;
            foreach (float r in new[] { radius * 0.95f, radius * 1.15f, radius * 1.4f })
            {
                int count = 10 + ring * 6;
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + ring * 0.5f) / count * 360f;
                    Transform piece = CreateGroup("Ring", gate);
                    piece.localRotation = Quaternion.Euler(angle, 0f, 0f);
                    float length = 2f * Mathf.PI * r / count * (ring == 2 ? 0.55f : 0.8f);
                    AddPart(piece, PrimitiveType.Cube, new Vector3(0f, r, 0f), new Vector3(1.2f + ring * 0.5f, 0.6f + ring * 0.2f, length), ring == 1 ? palette.Trim : palette.Stone);
                    if (ring == 2 && i % 3 == 0)
                        AddPart(piece, PrimitiveType.Cube, new Vector3(0f, r + 1.1f, 0f), new Vector3(0.5f, 1.6f, 0.5f), palette.Glow2);
                }
                ring++;
            }
            AddPointLight(gate, new Vector3(2.5f, 0f, 0f), palette.GlowColor, radius * 4f, 6f);
            gate.gameObject.AddComponent<Pulse>();
            return gate;
        }

        /// <summary>
        /// A lantern hanging from above on a thin chain: a glowing octahedron in a cage, with its own light. The tether's
        /// anchors are these; the builder adds the anchor component to the ones it wants.
        /// </summary>
        public static Transform AddLamp(Transform parent, Rail rail, float distance, float height, float lateral, float chainTop, Palette palette)
        {
            Transform lamp = PlaceAlongRail(parent, "Lamp", rail, distance, height, lateral);
            float chain = Mathf.Max(0.1f, chainTop - height);
            AddPart(lamp, PrimitiveType.Cylinder, new Vector3(0f, chain * 0.5f + 0.4f, 0f), new Vector3(0.05f, chain * 0.5f, 0.05f), palette.Trim);
            GameObject body = AddPart(lamp, PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), palette.Glow);
            body.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            AddPart(lamp, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.25f, 0.2f, 0.25f), palette.Trim);
            AddPart(lamp, PrimitiveType.Cube, new Vector3(0f, -0.45f, 0f), new Vector3(0.18f, 0.2f, 0.18f), palette.Trim);
            AddPointLight(lamp, Vector3.zero, palette.GlowColor, 7f, 2.5f);
            var pulse = lamp.gameObject.AddComponent<Pulse>();
            WireFloat(pulse, "period", 2.6f + (distance % 5f) * 0.3f);
            WireFloat(pulse, "swing", 0.18f);
            var bob = lamp.gameObject.AddComponent<Bob>();
            WireFloat(bob, "amplitude", 0.08f);
            WireFloat(bob, "period", 3.5f + (distance % 7f) * 0.3f);
            return lamp;
        }

        /// <summary>A long banner hanging on the far side, dark cloth with a glowing woven pattern, swaying slowly.</summary>
        public static Transform AddBanner(Transform parent, Rail rail, float distance, float lateral, float top, float length, Palette palette, float width = 1.6f)
        {
            Transform banner = PlaceAlongRail(parent, "Banner", rail, distance, top, lateral);
            AddPart(banner, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(0.08f, width * 0.6f, 0.08f), palette.Trim)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject cloth = AddPart(banner, PrimitiveType.Quad, new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length, 1f), palette.Banner);
            cloth.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var sway = banner.gameObject.AddComponent<Sway>();
            WireFloat(sway, "degrees", 4f);
            WireFloat(sway, "period", 4f + (distance % 5f) * 0.4f);
            return banner;
        }

        /// <summary>
        /// A slab of floor along the rail with strips of light along its two long edges, like the lit platforms over the
        /// water in the paintings. The strips are visuals only.
        /// </summary>
        public static void AddPlatform(Transform parent, Rail rail, string name, float from, float to, float bottom, float top, Palette palette,
            float depth = FloorDepth, bool glowEdges = true, float lateral = 0f)
        {
            AddBlocks(parent, rail, name, from, to, bottom, top, palette.Stone, lateral, depth);
            if (!glowEdges)
                return;
            const float strip = 0.12f;
            AddBlocks(parent, rail, name + " edge", from + 0.1f, to - 0.1f, top - 0.02f, top + 0.03f, palette.Edge, lateral - depth * 0.5f + strip, strip, collide: false);
            AddBlocks(parent, rail, name + " edge", from + 0.1f, to - 0.1f, top - 0.02f, top + 0.03f, palette.Edge, lateral + depth * 0.5f - strip, strip, collide: false);
        }

        /// <summary>Dark water either side of the rail, with slow rings of light on it. Falling in is a death; the builder adds the kill plane.</summary>
        public static void AddWater(Transform parent, Rail rail, float from, float to, float level, Palette palette, float width = 60f)
        {
            AddBlocks(parent, rail, "Water", from, to, level - 0.5f, level, palette.Water, 0f, width, collide: false);
            Material ringMaterial = LoadOrCreateRingMaterial(MaterialFolder, $"{palette.Name} Water Ring", palette.Glow2Color * 0.45f);
            for (float d = from + 3f; d < to - 2f; d += 6.5f)
            {
                float side = (d % 13f) < 6.5f ? -6f : 7f;
                float size = 2f + (d % 5f) * 0.8f;
                Transform ring = PlaceAlongRail(parent, "Water ring", rail, d, level + 0.03f, side);
                GameObject quad = AddPart(ring, PrimitiveType.Quad, Vector3.zero, new Vector3(size, size, 1f), ringMaterial);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var pulse = ring.gameObject.AddComponent<Pulse>();
                WireFloat(pulse, "period", 5f + (d % 4f));
                WireFloat(pulse, "scaleSwing", 0.25f);
            }
        }

        /// <summary>A great dark shard hanging from the dark above, as in the paintings: a tall, thin prism, tilted.</summary>
        public static Transform AddShard(Transform parent, Rail rail, float distance, float lateral, float top, float length, float width, float tilt, Palette palette)
        {
            Transform shard = PlaceAlongRail(parent, "Shard", rail, distance, top, lateral);
            shard.localRotation *= Quaternion.Euler(tilt, 0f, tilt * 0.4f);
            GameObject body = AddPart(shard, PrimitiveType.Cube, new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length, width * 0.6f), palette.DeepStone);
            body.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            AddPart(shard, PrimitiveType.Cube, new Vector3(0f, -length - width * 0.4f, 0f), new Vector3(width * 0.5f, width * 0.8f, width * 0.3f), palette.DeepStone)
                .transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            // A thin line of light down one face, like the circuitry on the shards in the paintings.
            AddPart(shard, PrimitiveType.Cube, new Vector3(width * 0.3f, -length * 0.55f, 0f), new Vector3(0.06f, length * 0.6f, 0.06f), palette.Glow2);
            return shard;
        }

        /// <summary>A wall on the far side of the rail: deep stone with a band of lattice glowing on it.</summary>
        public static void AddWall(Transform parent, Rail rail, float from, float to, float bottom, float top, float lateral, Palette palette, float latticeBottom = 2f, float latticeHeight = 4f)
        {
            AddBlocks(parent, rail, "Wall", from, to, bottom, top, palette.DeepStone, lateral, 2f, collide: false);
            if (latticeHeight <= 0f)
                return;
            // Square panels of lattice along the wall, so the pattern keeps its size however long the wall is.
            for (float d = from + latticeHeight * 0.5f + 0.5f; d + latticeHeight * 0.5f < to - 0.5f; d += latticeHeight)
            {
                Transform panel = PlaceAlongRail(parent, "Wall lattice", rail, d, latticeBottom + latticeHeight * 0.5f, lateral + 1.05f);
                GameObject quad = AddPart(panel, PrimitiveType.Quad, Vector3.zero, new Vector3(latticeHeight, latticeHeight, 1f), palette.Lattice);
                quad.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
        }

        /// <summary>A bar of scaffolding: dark poles, planks and a rail, as in the bazaar painting.</summary>
        public static void AddScaffold(Transform parent, Rail rail, float from, float to, float height, Palette palette, float lateral = 0f, float depth = 4f, bool floor = true)
        {
            if (floor)
            {
                AddBlocks(parent, rail, "Planks", from, to, height - 0.25f, height, palette.Cloth, lateral, depth);
                AddBlocks(parent, rail, "Plank edge", from + 0.1f, to - 0.1f, height - 0.02f, height + 0.03f, palette.Edge, lateral - depth * 0.5f + 0.1f, 0.1f, collide: false);
            }
            for (float d = from + 0.5f; d < to; d += 3f)
            {
                foreach (float side in new[] { -depth * 0.5f + 0.2f, depth * 0.5f - 0.2f })
                {
                    Transform pole = PlaceAlongRail(parent, "Pole", rail, d, 0f, lateral + side);
                    AddPart(pole, PrimitiveType.Cylinder, new Vector3(0f, (height + 1.2f) * 0.5f, 0f), new Vector3(0.16f, (height + 1.2f) * 0.5f, 0.16f), palette.DeepStone);
                }
                Transform crossbar = PlaceAlongRail(parent, "Crossbar", rail, d, height + 1.1f, lateral);
                AddPart(crossbar, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, depth * 0.5f, 0.1f), palette.Trim)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        /// <summary>A neon sign: a glowing panel in a dark frame, hung on the far side, with a line of pattern below it.</summary>
        public static Transform AddSign(Transform parent, Rail rail, float distance, float lateral, float height, float width, float tall, Palette palette, bool secondary = false)
        {
            Transform sign = PlaceAlongRail(parent, "Sign", rail, distance, height, lateral);
            AddPart(sign, PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, tall + 0.3f, width + 0.3f), palette.DeepStone);
            AddPart(sign, PrimitiveType.Cube, new Vector3(0.2f, 0f, 0f), new Vector3(0.1f, tall, width), secondary ? palette.Glow : palette.Neon);
            GameObject pattern = AddPart(sign, PrimitiveType.Quad, new Vector3(0.28f, 0f, 0f), new Vector3(width * 0.9f, tall * 0.9f, 1f), palette.Lattice);
            pattern.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            AddPointLight(sign, new Vector3(1.5f, 0f, 0f), secondary ? palette.GlowColor : palette.Glow2Color, 8f, 2f);
            var pulse = sign.gameObject.AddComponent<Pulse>();
            WireFloat(pulse, "period", 0.9f + (distance % 3f) * 0.5f);
            WireFloat(pulse, "flicker", 0.35f);
            return sign;
        }

        /// <summary>
        /// The giant face of the shrine: a great head of stone with glowing eyes, veils of light hanging over it and a
        /// crown of dark pylons, rising behind the boss arena.
        /// </summary>
        public static Transform AddFace(Transform parent, Rail rail, float distance, float lateral, float baseHeight, Palette palette)
        {
            Transform face = PlaceAlongRail(parent, "Face", rail, distance, baseHeight, lateral);
            const float scale = 7f;
            // The plinth, the head and its brow.
            AddPart(face, PrimitiveType.Cube, new Vector3(0f, -1f, 0f), new Vector3(scale * 1.4f, 2f, scale * 1.8f), palette.DeepStone);
            GameObject head = AddPart(face, PrimitiveType.Sphere, new Vector3(0f, scale * 0.6f, 0f), new Vector3(scale * 0.9f, scale * 1.2f, scale), palette.Stone);
            AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.25f, scale * 0.95f, 0f), new Vector3(scale * 0.5f, 0.5f, scale * 0.8f), palette.DeepStone);
            // Eyes and the line of the mouth.
            foreach (float z in new[] { -scale * 0.2f, scale * 0.2f })
            {
                AddPart(face, PrimitiveType.Sphere, new Vector3(scale * 0.43f, scale * 0.78f, z), new Vector3(scale * 0.1f, scale * 0.05f, scale * 0.14f), palette.Glow);
                AddPointLight(face, new Vector3(scale * 0.6f, scale * 0.78f, z), palette.GlowColor, 12f, 3f);
            }
            AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.47f, scale * 0.36f, 0f), new Vector3(0.25f, 0.14f, scale * 0.34f), palette.DeepStone);
            AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.48f, scale * 0.3f, 0f), new Vector3(0.2f, 0.1f, scale * 0.26f), palette.DeepStone);
            // The nose, the brow and the cheekbones.
            AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.5f, scale * 0.6f, 0f), new Vector3(scale * 0.12f, scale * 0.38f, scale * 0.1f), palette.Stone)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.45f, scale * 0.88f, 0f), new Vector3(scale * 0.14f, scale * 0.06f, scale * 0.62f), palette.Stone);
            foreach (float z in new[] { -scale * 0.3f, scale * 0.3f })
                AddPart(face, PrimitiveType.Sphere, new Vector3(scale * 0.38f, scale * 0.62f, z), new Vector3(scale * 0.22f, scale * 0.2f, scale * 0.22f), palette.Stone);
            // Veils of light hanging over the face, and the crown of pylons.
            for (int i = 0; i < 9; i++)
            {
                float z = (i - 4) * scale * 0.11f;
                float length = scale * (0.5f + ((i * 7) % 5) * 0.12f);
                AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.5f + (i % 2) * 0.3f, scale * 1.2f - length * 0.5f, z), new Vector3(0.08f, length, 0.08f), palette.Neon);
                float pylon = scale * (0.6f + ((i * 3) % 4) * 0.2f);
                AddPart(face, PrimitiveType.Cube, new Vector3(0f, scale * 1.15f + pylon * 0.5f, z * 1.3f), new Vector3(scale * 0.3f, pylon, scale * 0.08f), palette.DeepStone);
                if (i % 2 == 0)
                    AddPart(face, PrimitiveType.Cube, new Vector3(scale * 0.16f, scale * 1.15f + pylon, z * 1.3f), new Vector3(0.1f, 0.6f, 0.1f), palette.Glow);
            }
            face.gameObject.AddComponent<Pulse>();
            return face;
        }

        /// <summary>The flying craft's hull as a visual: a patterned lozenge with a ring, a glowing eye and a barrel. Used by the sentinel prefab.</summary>
        public static void AddCraftVisual(Transform parent, Palette palette)
        {
            GameObject hull = AddPart(parent, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.0f, 0.55f, 1.9f), palette.Banner);
            hull.name = "Hull";
            AddPart(parent, PrimitiveType.Sphere, new Vector3(0f, 0.25f, 0.2f), new Vector3(0.7f, 0.45f, 1.1f), palette.Cloth);
            AddPart(parent, PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.95f), new Vector3(0.28f, 0.28f, 0.28f), palette.Neon).name = "Eye";
            AddPart(parent, PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 1.1f), new Vector3(0.14f, 0.5f, 0.14f), palette.Trim)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // A ring of dark blades around the hull.
            for (int i = 0; i < 8; i++)
            {
                Transform spoke = CreateGroup("Spoke", parent);
                spoke.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                AddPart(spoke, PrimitiveType.Cube, new Vector3(0f, -0.05f, 0.95f), new Vector3(0.1f, 0.12f, 0.6f), palette.DeepStone);
            }
            AddPart(parent, PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), new Vector3(2.3f, 0.03f, 2.3f), palette.Trim);
            AddPart(parent, PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0f), new Vector3(0.5f, 0.06f, 0.5f), palette.Glow2).name = "Thruster";
            AddPointLight(parent, new Vector3(0f, -0.6f, 0f), palette.Glow2Color, 5f, 1.5f);
        }

        public static Light AddPointLight(Transform parent, Vector3 localPosition, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
