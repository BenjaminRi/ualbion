﻿using System.Numerics;
using UAlbion.Formats.Assets.Map;

namespace UAlbion.Game
{
    public interface ICollisionManager
    {
        bool IsOccupied(Vector2 tilePosition);
        Passability GetPassability(Vector2 tilePosition);
        void Register(ICollider collider);
        void Unregister(ICollider collider);
    }
}
