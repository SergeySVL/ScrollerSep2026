using UnityEngine;

namespace ShopSideScroller
{
    [RequireComponent(typeof(Camera))]
    public sealed class ShopRoomCamera : MonoBehaviour
    {
        public Vector2 roomCenter;
        public Vector2 roomSize;
        public float padding = 0.18f;
        private Camera view;

        private void Awake() { view = GetComponent<Camera>(); }
        private void LateUpdate()
        {
            view.orthographicSize = Mathf.Max(roomSize.y * 0.5f + padding,
                (roomSize.x * 0.5f + padding) / Mathf.Max(0.1f, view.aspect));
            transform.position = new Vector3(roomCenter.x, roomCenter.y, -10f);
        }
    }
}
