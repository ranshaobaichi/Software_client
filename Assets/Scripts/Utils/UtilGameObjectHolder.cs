using System;
using UnityEngine;

namespace Utils {
    public class UtilGameObjectHolder : MonoBehaviour{
        private void Awake() => DontDestroyOnLoad(gameObject);
    }
}