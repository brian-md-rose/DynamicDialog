using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Builds the courtyard's scenery from primitives: lawn, paths, walls, the clinic building, trees, hedges,
    /// lamps, the fountain, the garden, the nurse station and benches. None of it matters to the dialog; the
    /// characters, props and areas the rules talk about are ordinary objects in the scene.
    /// </summary>
    /// <remarks>
    /// The scenery is built in code, with the render pipeline's default material, so the sample works in any
    /// pipeline. In the editor it also builds a preview (not saved with the scene) so you can see the layout
    /// without pressing Play.
    /// </remarks>
    [ExecuteAlways]
    public class CourtyardScenery : MonoBehaviour
    {
        static readonly Color Grass = new Color(0.47f, 0.66f, 0.38f), GrassOuter = new Color(0.4f, 0.58f, 0.33f);
        static readonly Color Stone = new Color(0.82f, 0.78f, 0.7f), Paving = new Color(0.74f, 0.7f, 0.63f);
        static readonly Color Wall = new Color(0.93f, 0.9f, 0.84f), Hedge = new Color(0.24f, 0.48f, 0.26f);
        static readonly Color Soil = new Color(0.42f, 0.31f, 0.22f), Water = new Color(0.35f, 0.65f, 0.9f);
        static readonly Color Wood = new Color(0.6f, 0.42f, 0.26f), White = new Color(0.95f, 0.96f, 0.98f);
        static readonly Color Roof = new Color(0.74f, 0.42f, 0.32f), Teal = new Color(0.2f, 0.58f, 0.6f);
        static readonly Color Glass = new Color(0.5f, 0.68f, 0.82f), Door = new Color(0.36f, 0.26f, 0.2f);
        static readonly Color Bark = new Color(0.43f, 0.31f, 0.22f), Leaves = new Color(0.3f, 0.56f, 0.28f);
        static readonly Color LeavesDark = new Color(0.22f, 0.45f, 0.25f), Iron = new Color(0.18f, 0.2f, 0.22f);
        static readonly Color Bulb = new Color(1f, 0.92f, 0.7f), Red = new Color(0.85f, 0.25f, 0.25f);
        static readonly Color[] FlowerColors =
        {
            new Color(0.78f, 0.55f, 0.85f), new Color(0.95f, 0.55f, 0.65f), new Color(0.98f, 0.82f, 0.35f), new Color(0.97f, 0.96f, 0.92f),
        };

        Transform built;

        void OnEnable()
        {
            SampleShapes.Flags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave | HideFlags.NotEditable;
            built = new GameObject(Application.isPlaying ? "Scenery" : "Scenery (preview, not saved)") { hideFlags = SampleShapes.Flags }.transform;
            built.SetParent(transform, false);
            Build(built);
            SampleShapes.Flags = HideFlags.None;
        }

        void OnDisable()
        {
            if (Application.isPlaying || built == null) return;
            DestroyImmediate(built.gameObject);
            SampleShapes.ReleaseMaterials();
        }

        static void Build(Transform env)
        {
            SampleShapes.Block("Ground", env, PrimitiveType.Plane, Vector3.zero, new Vector3(1.6f, 1f, 1.6f), Grass);
            SampleShapes.Block("Ground (outside)", env, PrimitiveType.Plane, new Vector3(0f, -0.02f, 0f), new Vector3(6f, 1f, 6f), GrassOuter, false);
            SampleShapes.Block("Path NS", env, PrimitiveType.Cube, new Vector3(0f, 0.005f, 0f), new Vector3(1.4f, 0.01f, 16f), Stone);
            SampleShapes.Block("Path EW", env, PrimitiveType.Cube, new Vector3(0f, 0.005f, 1.5f), new Vector3(12f, 0.01f, 1.2f), Stone);
            // A cylinder primitive has a capsule collider, which would be a dome here.
            SampleShapes.Block("Fountain Plaza", env, PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 1.5f), new Vector3(3.6f, 0.004f, 3.6f), Paving, false);
            for (float z = -7.4f; z < 7.6f; z += 0.9f)
                if (Mathf.Abs(z - 1.5f) > 1.9f)
                    SampleShapes.Block("Paver", env, PrimitiveType.Cube, new Vector3(0f, 0.011f, z), new Vector3(1.2f, 0.004f, 0.75f), Paving, false);
            for (float x = -5.4f; x < 5.6f; x += 0.9f)
                if (Mathf.Abs(x) > 1.9f)
                    SampleShapes.Block("Paver", env, PrimitiveType.Cube, new Vector3(x, 0.011f, 1.5f), new Vector3(0.75f, 0.004f, 1.0f), Paving, false);

            SampleShapes.Block("Wall S", env, PrimitiveType.Cube, new Vector3(0f, 0.4f, -8f), new Vector3(16f, 0.8f, 0.3f), Wall);
            SampleShapes.Block("Wall E", env, PrimitiveType.Cube, new Vector3(8f, 0.4f, 0f), new Vector3(0.3f, 0.8f, 16f), Wall);
            SampleShapes.Block("Wall W", env, PrimitiveType.Cube, new Vector3(-8f, 0.4f, 0f), new Vector3(0.3f, 0.8f, 16f), Wall);
            SampleShapes.Block("Wall Cap S", env, PrimitiveType.Cube, new Vector3(0f, 0.83f, -8f), new Vector3(16.2f, 0.06f, 0.42f), Stone, false);
            SampleShapes.Block("Wall Cap E", env, PrimitiveType.Cube, new Vector3(8f, 0.83f, 0f), new Vector3(0.42f, 0.06f, 16.2f), Stone, false);
            SampleShapes.Block("Wall Cap W", env, PrimitiveType.Cube, new Vector3(-8f, 0.83f, 0f), new Vector3(0.42f, 0.06f, 16.2f), Stone, false);
            foreach (var p in new[] { new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f), new Vector3(-8f, 0f, 0f), new Vector3(8f, 0f, 0f), new Vector3(0f, 0f, -8f) })
                SampleShapes.Block("Pillar", env, PrimitiveType.Cube, p + Vector3.up * 0.48f, new Vector3(0.5f, 0.96f, 0.5f), Stone);

            ClinicBuilding(env);

            for (int i = 0; i < 7; i++)
            {
                float t = -6.5f + i * 1.9f;
                Shrub(env, new Vector3(-7.4f, 0f, t - 0.6f), i % 2 == 0 ? Leaves : LeavesDark);
                Shrub(env, new Vector3(7.4f, 0f, t), i % 2 == 0 ? LeavesDark : Leaves);
                if (Mathf.Abs(t) > 1.5f) Shrub(env, new Vector3(t, 0f, -7.4f), i % 2 == 0 ? Leaves : LeavesDark);
            }

            // A big oak in the south-east corner (the "oak" area), smaller trees elsewhere, and a ring of trees
            // outside the walls so the courtyard doesn't float in empty space.
            Tree("Old Oak", env, new Vector3(5.8f, 0f, -5.4f), 1.6f, Leaves, LeavesDark, true);
            Tree("Tree SW", env, new Vector3(-6.2f, 0f, -6.2f), 1.0f, LeavesDark, Leaves, true);
            Tree("Tree W", env, new Vector3(-6.4f, 0f, -2.6f), 0.85f, Leaves, LeavesDark, true);
            Tree("Tree E", env, new Vector3(6.7f, 0f, -0.4f), 0.9f, LeavesDark, Leaves, true);
            for (int i = 0; i < 6; i++)
            {
                float z = -7f + i * 3f;
                Tree("Outside Tree", env, new Vector3(-10.5f - (i % 2), 0f, z), 1.3f + (i % 3) * 0.2f, LeavesDark, Leaves, false);
                Tree("Outside Tree", env, new Vector3(10.5f + (i % 2), 0f, z + 1.2f), 1.2f + (i % 2) * 0.3f, Leaves, LeavesDark, false);
            }

            foreach (var p in new[] { new Vector3(1f, 0f, -6.5f), new Vector3(-1f, 0f, -6.5f), new Vector3(1f, 0f, 6.2f), new Vector3(-1f, 0f, 6.2f), new Vector3(6.3f, 0f, 2.4f), new Vector3(-6.3f, 0f, 0.6f) })
                Lamp(env, p);

            // Planters either side of the clinic door
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                SampleShapes.Block("Planter", env, PrimitiveType.Cube, new Vector3(x, 0.15f, 7.5f), new Vector3(0.8f, 0.3f, 0.5f), Stone);
                SampleShapes.Block("Planter Soil", env, PrimitiveType.Cube, new Vector3(x, 0.301f, 7.5f), new Vector3(0.7f, 0.01f, 0.4f), Soil, false);
                for (int i = 0; i < 3; i++)
                    SampleShapes.Block("Flower", env, PrimitiveType.Sphere, new Vector3(x - 0.22f + i * 0.22f, 0.38f, 7.5f), Vector3.one * 0.18f, FlowerColors[(i + (x > 0 ? 1 : 2)) % FlowerColors.Length], false);
            }

            Fountain(env, new Vector3(0f, 0f, 1.5f));
            Garden(env, new Vector3(-4.5f, 0f, 4.5f));
            NurseStation(env, new Vector3(4.8f, 0f, 5f));

            Bench("Bench East", env, new Vector3(3.2f, 0f, -1.2f), 90f);
            Bench("Bench West", env, new Vector3(-3f, 0f, -2.5f), 90f);
            Bench("Bench Oak", env, new Vector3(4.6f, 0f, -4.6f), -45f); // faces the fountain
        }

        /// <summary>The clinic's back wall along the north side: windows, a door with an awning, and a sign.</summary>
        static void ClinicBuilding(Transform parent)
        {
            var b = SampleShapes.Empty("Clinic Building", parent, Vector3.zero);
            const float front = 8f;

            SampleShapes.Block("Body", b, PrimitiveType.Cube, new Vector3(0f, 1.1f, front + 1.2f), new Vector3(16.6f, 2.2f, 2.4f), Wall);
            SampleShapes.Block("Base Trim", b, PrimitiveType.Cube, new Vector3(0f, 0.08f, front - 0.02f), new Vector3(16.6f, 0.16f, 0.06f), Teal, false);
            SampleShapes.Block("Eave", b, PrimitiveType.Cube, new Vector3(0f, 2.24f, front + 1.1f), new Vector3(17f, 0.1f, 2.9f), Roof, false);
            SampleShapes.Block("Roof", b, PrimitiveType.Cube, new Vector3(0f, 2.42f, front + 1.3f), new Vector3(16.4f, 0.28f, 2.2f), Roof, false);

            foreach (float y in new[] { 0.72f, 1.6f })
                foreach (float x in new[] { -6.5f, -4.5f, -2.5f, 2.5f, 4.5f, 6.5f })
                {
                    SampleShapes.Block("Window Frame", b, PrimitiveType.Cube, new Vector3(x, y, front - 0.02f), new Vector3(0.95f, 0.66f, 0.04f), White, false);
                    SampleShapes.Block("Window", b, PrimitiveType.Cube, new Vector3(x, y, front - 0.04f), new Vector3(0.8f, 0.52f, 0.04f), Glass, false, 0.85f);
                    SampleShapes.Block("Mullion", b, PrimitiveType.Cube, new Vector3(x, y, front - 0.065f), new Vector3(0.04f, 0.52f, 0.02f), White, false);
                    SampleShapes.Block("Sill", b, PrimitiveType.Cube, new Vector3(x, y - 0.34f, front - 0.06f), new Vector3(1.05f, 0.04f, 0.1f), White, false);
                }

            SampleShapes.Block("Door Frame", b, PrimitiveType.Cube, new Vector3(0f, 0.62f, front - 0.02f), new Vector3(1.2f, 1.24f, 0.04f), Teal, false);
            SampleShapes.Block("Door", b, PrimitiveType.Cube, new Vector3(0f, 0.55f, front - 0.04f), new Vector3(1.0f, 1.1f, 0.04f), Door, false);
            SampleShapes.Block("Door Glass", b, PrimitiveType.Cube, new Vector3(0f, 0.78f, front - 0.065f), new Vector3(0.6f, 0.36f, 0.02f), Glass, false, 0.85f);
            SampleShapes.Block("Handle", b, PrimitiveType.Cube, new Vector3(0.36f, 0.5f, front - 0.08f), new Vector3(0.04f, 0.14f, 0.03f), White, false);
            var awning = SampleShapes.Block("Door Awning", b, PrimitiveType.Cube, new Vector3(0f, 1.32f, front - 0.32f), new Vector3(1.7f, 0.05f, 0.7f), Teal, false);
            awning.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

            SampleShapes.Block("Sign", b, PrimitiveType.Cube, new Vector3(0f, 1.78f, front - 0.03f), new Vector3(2.6f, 0.36f, 0.05f), Teal, false);
            SampleShapes.Block("Sign Cross V", b, PrimitiveType.Cube, new Vector3(-1.05f, 1.78f, front - 0.06f), new Vector3(0.07f, 0.24f, 0.01f), White, false);
            SampleShapes.Block("Sign Cross H", b, PrimitiveType.Cube, new Vector3(-1.05f, 1.78f, front - 0.06f), new Vector3(0.24f, 0.07f, 0.01f), White, false);

            var text = new GameObject("Sign Text", typeof(MeshRenderer), typeof(TextMesh)) { hideFlags = SampleShapes.Flags };
            text.transform.SetParent(b, false);
            text.transform.position = new Vector3(0.12f, 1.78f, front - 0.065f);
#if UNITY_2022_2_OR_NEWER
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            var mesh = text.GetComponent<TextMesh>();
            mesh.text = "WILLOW CLINIC";
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.035f;
            mesh.fontStyle = FontStyle.Bold;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        static void Tree(string name, Transform parent, Vector3 position, float size, Color leafA, Color leafB, bool collider)
        {
            var t = SampleShapes.Empty(name, parent, Vector3.zero);
            t.position = position;
            t.rotation = Quaternion.Euler(0f, position.x * 37f + position.z * 11f, 0f); // vary the silhouette
            SampleShapes.Block("Trunk", t, PrimitiveType.Cylinder, position + Vector3.up * 0.5f * size, new Vector3(0.16f, 0.5f, 0.16f) * size, Bark, collider);
            (Vector3 offset, float scale, bool a)[] blobs =
            {
                (new Vector3(0f, 1.35f, 0f), 1.3f, true),
                (new Vector3(0.38f, 1.12f, 0.2f), 0.9f, false),
                (new Vector3(-0.32f, 1.18f, -0.25f), 0.85f, false),
                (new Vector3(0.05f, 1.78f, 0.05f), 0.8f, true),
            };
            // No colliders on the leaves, so vision raycasts and the player stay clear of them.
            foreach (var blob in blobs)
                SampleShapes.Part("Canopy", t, PrimitiveType.Sphere, blob.offset * size, Vector3.one * blob.scale * size, blob.a ? leafA : leafB);
            if (Application.isPlaying) t.gameObject.AddComponent<HideWhenBlockingView>();
        }

        static void Shrub(Transform parent, Vector3 position, Color leaves)
        {
            SampleShapes.Block("Shrub", parent, PrimitiveType.Sphere, position + Vector3.up * 0.2f, new Vector3(0.75f, 0.5f, 0.7f), leaves);
            SampleShapes.Block("Shrub Top", parent, PrimitiveType.Sphere, position + new Vector3(0.15f, 0.36f, -0.05f), new Vector3(0.45f, 0.35f, 0.45f), leaves, false);
        }

        static void Lamp(Transform parent, Vector3 position)
        {
            var lamp = SampleShapes.Empty("Lamp Post", parent, Vector3.zero);
            lamp.position = position;
            SampleShapes.Block("Post", lamp, PrimitiveType.Cylinder, position + Vector3.up * 0.55f, new Vector3(0.05f, 0.55f, 0.05f), Iron, true, 0.4f);
            SampleShapes.Block("Base", lamp, PrimitiveType.Cylinder, position + Vector3.up * 0.04f, new Vector3(0.12f, 0.04f, 0.12f), Iron, false, 0.4f);
            SampleShapes.Block("Bulb", lamp, PrimitiveType.Sphere, position + Vector3.up * 1.15f, Vector3.one * 0.16f, Bulb, false, 0.5f);
            SampleShapes.Block("Cap", lamp, PrimitiveType.Cylinder, position + Vector3.up * 1.25f, new Vector3(0.2f, 0.02f, 0.2f), Iron, false, 0.4f);
        }

        static void Fountain(Transform parent, Vector3 at)
        {
            var fountain = SampleShapes.Empty("Fountain", parent, Vector3.zero);
            fountain.position = at;
            SampleShapes.Block("Basin", fountain, PrimitiveType.Cylinder, at + Vector3.up * 0.12f, new Vector3(1.8f, 0.12f, 1.8f), Stone);
            SampleShapes.Block("Water", fountain, PrimitiveType.Cylinder, at + Vector3.up * 0.2f, new Vector3(1.55f, 0.06f, 1.55f), Water);
            SampleShapes.Block("Spout", fountain, PrimitiveType.Cylinder, at + Vector3.up * 0.4f, new Vector3(0.2f, 0.3f, 0.2f), Stone);
            SampleShapes.Block("Spout Bowl", fountain, PrimitiveType.Cylinder, at + Vector3.up * 0.72f, new Vector3(0.4f, 0.04f, 0.4f), Stone, false);

            // Droplets arcing up out of the spout and falling back into the basin.
            var go = new GameObject("Spray", typeof(ParticleSystem)) { hideFlags = SampleShapes.Flags };
            go.transform.SetParent(fountain, false);
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.76f, Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 0.55f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.75f, 0.9f, 1f);
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.02f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            renderer.sharedMaterial = SampleShapes.Material(Water);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Flower beds inside a hedge with a gap on the east side.</summary>
        static void Garden(Transform parent, Vector3 g)
        {
            var garden = SampleShapes.Empty("Garden", parent, Vector3.zero);
            SampleShapes.Block("Hedge N", garden, PrimitiveType.Cube, g + new Vector3(0f, 0.25f, 2f), new Vector3(4.5f, 0.5f, 0.4f), Hedge);
            SampleShapes.Block("Hedge W", garden, PrimitiveType.Cube, g + new Vector3(-2.1f, 0.25f, 0f), new Vector3(0.4f, 0.5f, 4.4f), Hedge);
            SampleShapes.Block("Hedge S", garden, PrimitiveType.Cube, g + new Vector3(-0.6f, 0.25f, -2f), new Vector3(3.3f, 0.5f, 0.4f), Hedge);
            SampleShapes.Block("Bed A", garden, PrimitiveType.Cube, g + new Vector3(-1f, 0.06f, 0.9f), new Vector3(1.4f, 0.12f, 0.8f), Soil);
            SampleShapes.Block("Bed B", garden, PrimitiveType.Cube, g + new Vector3(-1f, 0.06f, -0.6f), new Vector3(1.4f, 0.12f, 0.8f), Soil);
            for (int i = 0; i < 10; i++)
            {
                var pos = g + new Vector3(-1.56f + (i % 5) * 0.28f, 0.19f, (i < 5 ? 0.9f : -0.6f) + ((i % 2) * 0.16f - 0.08f));
                SampleShapes.Block("Stem", garden, PrimitiveType.Cylinder, pos - Vector3.up * 0.04f, new Vector3(0.03f, 0.05f, 0.03f), LeavesDark, false);
                SampleShapes.Block("Flower", garden, PrimitiveType.Sphere, pos + Vector3.up * 0.03f, Vector3.one * 0.17f, FlowerColors[(i + i / 5) % FlowerColors.Length], false);
            }
            Shrub(garden, g + new Vector3(1.2f, 0f, 1.3f), Leaves);
            Shrub(garden, g + new Vector3(-1.6f, 0f, -1.5f), LeavesDark);
        }

        static void NurseStation(Transform parent, Vector3 n)
        {
            var station = SampleShapes.Empty("Nurse Station", parent, Vector3.zero);
            SampleShapes.Block("Counter", station, PrimitiveType.Cube, n + new Vector3(0f, 0.25f, 0.6f), new Vector3(2.2f, 0.5f, 0.5f), White);
            SampleShapes.Block("Counter Side", station, PrimitiveType.Cube, n + new Vector3(1.35f, 0.25f, -0.1f), new Vector3(0.5f, 0.5f, 1.9f), White);
            // Awning over the counter, held up by two posts at the back so nobody walks into them.
            foreach (float x in new[] { -1.2f, 1.6f })
                SampleShapes.Block("Awning Post", station, PrimitiveType.Cube, n + new Vector3(x, 0.6f, 1.3f), new Vector3(0.08f, 1.2f, 0.08f), White);
            var awning = SampleShapes.Block("Awning", station, PrimitiveType.Cube, n + new Vector3(0.2f, 1.22f, 0.45f), new Vector3(3.2f, 0.05f, 1.9f), Teal, false);
            awning.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);
            var sign = SampleShapes.Block("Station Sign", station, PrimitiveType.Cube, n + new Vector3(0f, 0.95f, 1.32f), new Vector3(0.5f, 0.5f, 0.04f), White).transform;
            SampleShapes.Block("Cross V", station, PrimitiveType.Cube, sign.position + new Vector3(0f, 0f, -0.025f), new Vector3(0.1f, 0.34f, 0.01f), Red, false);
            SampleShapes.Block("Cross H", station, PrimitiveType.Cube, sign.position + new Vector3(0f, 0f, -0.025f), new Vector3(0.34f, 0.1f, 0.01f), Red, false);
        }

        static void Bench(string name, Transform parent, Vector3 position, float yaw)
        {
            var bench = SampleShapes.Empty(name, parent, Vector3.zero);
            bench.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var seat = SampleShapes.Block("Seat", bench, PrimitiveType.Cube, Vector3.zero, new Vector3(1.4f, 0.06f, 0.45f), Wood);
            seat.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            seat.transform.localRotation = Quaternion.identity;
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                var leg = SampleShapes.Block("Leg", bench, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 0.22f, 0.4f), Wood);
                leg.transform.localPosition = new Vector3(x, 0.11f, 0f);
                leg.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
