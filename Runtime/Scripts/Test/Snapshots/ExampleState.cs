using System;
using UnityEngine;

namespace HelloDev.Saving.Test
{
    /// <summary>
    /// Serializable snapshot of a Transform's local position, rotation, and scale.
    /// Vector3/Quaternion are natively supported by JsonUtility (they expose
    /// public x/y/z/w fields), so no custom conversion is needed.
    /// </summary>
    [Serializable]
    public class ExampleState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        
        public Color MaterialColor;
    }
}