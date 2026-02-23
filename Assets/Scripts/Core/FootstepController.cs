using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// Monitors player movement and triggers footstep sounds via <see cref="AudioManager"/>.
    /// Attaches to the same GameObject as a <see cref="CharacterController"/>.
    /// 
    /// Design: single-responsibility component — does not modify PlayerMovement.
    /// Step accumulator fires a sound every <see cref="_stepInterval"/> metres of horizontal travel.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootstepController : MonoBehaviour
    {
        [SerializeField] private float _stepInterval = 0.45f;   // metres per step

        private CharacterController _cc;
        private Vector3 _lastPosition;
        private float _distanceAccumulator;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _lastPosition = transform.position;
            Debug.Log("[FootstepController] Initialised.");
        }

        private void Update()
        {
            if (_cc == null) return;
            if (!_cc.isGrounded) return;

            Vector3 current = transform.position;
            // Only count horizontal movement
            Vector3 delta = current - _lastPosition;
            delta.y = 0f;
            float dist = delta.magnitude;
            _lastPosition = current;

            if (dist < 0.001f) return;     // standing still — skip

            _distanceAccumulator += dist;
            if (_distanceAccumulator >= _stepInterval)
            {
                _distanceAccumulator -= _stepInterval;
                PlayStep();
            }
        }

        private void PlayStep()
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.PlaySFX(SFXType.Footstep, transform.position);
            Debug.Log($"[FootstepController] Step played at {transform.position}");
        }
    }
}
