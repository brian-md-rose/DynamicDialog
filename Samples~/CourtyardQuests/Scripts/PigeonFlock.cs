using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// A few pigeons that hop and peck around a landing spot. When the player gets close, the whole
    /// flock flies off to another spot. This object stays at the flock's centre, so a
    /// <see cref="DynamicDialog.DialogTag"/> on it ("pigeons") follows the birds around.
    /// The birds are made of primitives when the scene starts.
    /// </summary>
    public class PigeonFlock : MonoBehaviour
    {
        [Tooltip("How many birds to make when the scene starts.")]
        [Min(1)] public int count = 5;
        public Vector3[] landingSpots;
        public float scareRadius = 1.3f;
        public float flightTime = 1.8f;
        public float flightHeight = 2.2f;
        public float spread = 0.5f;

        int spot;
        bool flying;
        float flightStart;
        int fromSpot;
        Vector3[] offsets, from, hopFrom, hopTo;
        float[] hopStart, nextHop, nextPeck;
        Transform[] birds, heads, wingsL, wingsR;

        void Start()
        {
            if (landingSpots == null || landingSpots.Length == 0) landingSpots = new[] { transform.position };
            birds = BuildBirds(count);
            int n = birds.Length;
            offsets = new Vector3[n]; from = new Vector3[n]; hopFrom = new Vector3[n]; hopTo = new Vector3[n];
            hopStart = new float[n]; nextHop = new float[n]; nextPeck = new float[n];
            heads = new Transform[n]; wingsL = new Transform[n]; wingsR = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                heads[i] = birds[i].Find("Head");
                wingsL[i] = birds[i].Find("Wing L");
                wingsR[i] = birds[i].Find("Wing R");
                offsets[i] = RandomOffset();
                birds[i].position = hopFrom[i] = hopTo[i] = Ground(landingSpots[spot] + offsets[i]);
                hopStart[i] = -10f;
                nextHop[i] = Time.time + Random.Range(0.5f, 3f);
            }
            transform.position = landingSpots[spot];
        }

        void Update()
        {
            if (birds == null || birds.Length == 0 || landingSpots == null || landingSpots.Length < 2) return;
            if (flying) Fly();
            else
            {
                if (Scared()) TakeOff();
                else for (int i = 0; i < birds.Length; i++) Idle(i);
            }
        }

        bool Scared()
        {
            var player = PlayerCarry.Instance;
            if (player == null) return false;
            Vector3 p = player.transform.position;
            foreach (var b in birds)
            {
                Vector3 d = b.position - p;
                d.y = 0f;
                if (d.sqrMagnitude < scareRadius * scareRadius) return true;
            }
            return false;
        }

        void TakeOff()
        {
            // Fly to the spot furthest from the player.
            Vector3 p = PlayerCarry.Instance.transform.position;
            int best = spot;
            float bestSq = -1f;
            for (int s = 0; s < landingSpots.Length; s++)
            {
                if (s == spot) continue;
                float sq = (landingSpots[s] - p).sqrMagnitude;
                if (sq > bestSq) { bestSq = sq; best = s; }
            }
            fromSpot = spot;
            spot = best;
            for (int i = 0; i < birds.Length; i++)
            {
                from[i] = birds[i].position;
                offsets[i] = RandomOffset();
            }
            flying = true;
            flightStart = Time.time;
        }

        void Fly()
        {
            float t = Mathf.Clamp01((Time.time - flightStart) / flightTime);
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < birds.Length; i++)
            {
                // Stagger the birds slightly so they don't move as one block.
                float ti = Mathf.Clamp01(eased * (1f + i * 0.04f));
                Vector3 to = Ground(landingSpots[spot] + offsets[i]);
                Vector3 pos = Vector3.Lerp(from[i], to, ti) + Vector3.up * Mathf.Sin(ti * Mathf.PI) * flightHeight;
                Face(i, pos - birds[i].position);
                birds[i].position = pos;
                float flap = ti < 1f ? Mathf.Sin(Time.time * 28f + i) * 55f : 0f;
                if (wingsL[i] != null) wingsL[i].localRotation = Quaternion.Euler(0f, 0f, flap);
                if (wingsR[i] != null) wingsR[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            transform.position = Vector3.Lerp(landingSpots[fromSpot], landingSpots[spot], eased);
            if (t >= 1f)
            {
                flying = false;
                for (int i = 0; i < birds.Length; i++)
                {
                    hopFrom[i] = hopTo[i] = birds[i].position = Ground(landingSpots[spot] + offsets[i]);
                    nextHop[i] = Time.time + Random.Range(0.5f, 2f);
                }
            }
        }

        void Idle(int i)
        {
            var b = birds[i];
            float now = Time.time;
            if (now >= nextHop[i])
            {
                // Hop a short way, staying near the landing spot.
                Vector3 target = hopTo[i] + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
                Vector3 centre = landingSpots[spot];
                if ((target - centre).sqrMagnitude > spread * spread) target = Vector3.Lerp(target, centre, 0.5f);
                hopFrom[i] = b.position;
                hopTo[i] = Ground(target);
                hopStart[i] = now;
                nextHop[i] = now + Random.Range(0.8f, 3.5f);
                Face(i, hopTo[i] - hopFrom[i]);
            }
            float h = Mathf.Clamp01((now - hopStart[i]) / 0.25f);
            b.position = Vector3.Lerp(hopFrom[i], hopTo[i], h) + Vector3.up * Mathf.Sin(h * Mathf.PI) * 0.06f;

            // Peck now and then.
            if (heads[i] != null)
            {
                if (now >= nextPeck[i]) nextPeck[i] = now + Random.Range(0.6f, 2.5f);
                float peck = Mathf.Clamp01(1f - (nextPeck[i] - now) / 0.2f);
                heads[i].localRotation = Quaternion.Euler(Mathf.Sin(peck * Mathf.PI) * 50f, 0f, 0f);
            }
            if (wingsL[i] != null) wingsL[i].localRotation = Quaternion.identity;
            if (wingsR[i] != null) wingsR[i].localRotation = Quaternion.identity;
        }

        void Face(int i, Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) birds[i].rotation = Quaternion.LookRotation(dir);
        }

        Vector3 RandomOffset()
        {
            Vector2 r = Random.insideUnitCircle * spread;
            return new Vector3(r.x, 0f, r.y);
        }

        static Vector3 Ground(Vector3 p) { p.y = 0.02f; return p; }

        /// <summary>Primitive pigeons: a body, a tail, a head with a beak, and two wings that flap.</summary>
        static Transform[] BuildBirds(int n)
        {
            Color grey = new Color(0.56f, 0.58f, 0.64f), dark = new Color(0.34f, 0.36f, 0.44f), beak = new Color(0.9f, 0.6f, 0.3f);
            // Not under the flock: the birds move on their own and the flock object follows their centre.
            var root = new GameObject("Pigeon Birds").transform;
            var birds = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                var bird = new GameObject("Pigeon " + (i + 1)).transform;
                bird.SetParent(root, false);
                SampleShapes.Part("Body", bird, PrimitiveType.Sphere, new Vector3(0f, 0.065f, 0f), new Vector3(0.1f, 0.085f, 0.15f), grey);
                SampleShapes.Part("Tail", bird, PrimitiveType.Cube, new Vector3(0f, 0.075f, -0.09f), new Vector3(0.06f, 0.012f, 0.06f), dark);
                var head = SampleShapes.Empty("Head", bird, new Vector3(0f, 0.1f, 0.05f));
                SampleShapes.Part("Skull", head, PrimitiveType.Sphere, new Vector3(0f, 0.025f, 0.02f), Vector3.one * 0.065f, dark);
                SampleShapes.Part("Beak", head, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.055f), new Vector3(0.015f, 0.015f, 0.03f), beak);
                foreach (float side in new[] { -1f, 1f })
                {
                    var wing = SampleShapes.Empty(side < 0 ? "Wing L" : "Wing R", bird, new Vector3(0.045f * side, 0.085f, 0f));
                    SampleShapes.Part("Feathers", wing, PrimitiveType.Cube, new Vector3(0.05f * side, 0f, 0f), new Vector3(0.1f, 0.012f, 0.09f), dark);
                }
                birds[i] = bird;
            }
            return birds;
        }
    }
}
