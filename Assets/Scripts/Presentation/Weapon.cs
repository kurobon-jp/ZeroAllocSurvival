using UnityEngine;

namespace ZeroAllocSurvival.Presentation
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private Transform _muzzle;
        [SerializeField] private GameObject _fireEffect;
        [SerializeField] private float _fireEffectDuration = 0.05f;

        private float _elapsed;

        public Transform Muzzle => _muzzle;
    
        public void Fire()
        {
            _elapsed = 0f;
            if (_fireEffect == null) return;
            _fireEffect.SetActive(true);
        }

        private void Update()
        {
            if (_fireEffect == null) return;
            if (_elapsed >= _fireEffectDuration)
            {
                _fireEffect.SetActive(false);
            }
        
            _elapsed += Time.deltaTime;
        }
    }
}