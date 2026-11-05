using UnityEngine;

namespace DynamicDialog.Samples
{
    /// <summary>Wanders between random points in the room, pausing now and then.</summary>
    public class RobotWander : MonoBehaviour
    {
        public float speed = 1.6f;
        public float roomHalfSize = 6f;

        Vector3 target;
        float pauseUntil;

        void Start() => PickTarget();

        void Update()
        {
            if (Time.time < pauseUntil) return;
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.2f)
            {
                pauseUntil = Time.time + Random.Range(0.5f, 2.5f);
                PickTarget();
                return;
            }
            Vector3 dir = to.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
            transform.position += transform.forward * (speed * Time.deltaTime);
        }

        void PickTarget()
        {
            target = new Vector3(Random.Range(-roomHalfSize, roomHalfSize), transform.position.y, Random.Range(-roomHalfSize, roomHalfSize));
        }
    }
}
