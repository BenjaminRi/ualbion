﻿using System.Numerics;

namespace UAlbion.Core
{
    public struct MaterialProperties
    {
        public Vector3 SpecularIntensity;
        public float SpecularPower;
#pragma warning disable 0169
        Vector3 _padding0;
#pragma warning restore 0169
        public float Reflectivity;
    }
}
