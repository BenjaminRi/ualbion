﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Core.Visual;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets;
using UAlbion.Game.Events;

namespace UAlbion.Game.Entities
{
    public class PlayerSprite : Component
    {
        public enum Animation
        {
            WalkN,
            WalkE,
            WalkS,
            WalkW,
            SitN,
            SitE,
            SitS,
            SitW,
            UpperBody
        }

        static readonly IDictionary<Animation, int[]> Frames = new Dictionary<Animation, int[]>
        {
            { Animation.WalkN, new[] { 2,1,0 } },
            { Animation.WalkE, new[] { 5,4,3 } },
            { Animation.WalkS, new[] { 8,7,6 } },
            { Animation.WalkW, new[] { 11,10,9 } },
            { Animation.SitN,  new[] { 12 } },
            { Animation.SitE,  new[] { 13 } },
            { Animation.SitS,  new[] { 14 } },
            { Animation.SitW,  new[] { 15 } },
            { Animation.UpperBody, new[] { 16 } },
        };

        static readonly HandlerSet Handlers = new HandlerSet(
            H<PlayerSprite, RenderEvent>((x,e) => x.Render(e)),
            H<PlayerSprite, WorldCoordinateSelectEvent>((x, e) => x.Select(e)),
            H<PlayerSprite, UpdateEvent>((x, e) =>
            {
                var cycle = Frames[x._animation];
                x._frame--;
                if (!cycle.Contains(x._frame))
                    x._frame = cycle[0];
            }));

        readonly LargePartyGraphicsId _id;
        Vector2 _position;
        Vector2 _size = Vector2.One;
        Animation _animation;
        int _frame;
        public Vector3 Normal => Vector3.UnitZ;
        public override string ToString() => $"NpcSprite {_id} {_animation}:{_frame}";

        public PlayerSprite(LargePartyGraphicsId id, IAssetManager assets) : base(Handlers)
        {
            _id = id;
            _animation = (Animation)new Random().Next((int)Animation.UpperBody);

            var texture = assets.LoadTexture(_id);
            if (texture != null)
            {
                texture.GetSubImageDetails(_frame, out var size, out _, out _, out _);
                _size = size; // TODO: Update to handle variable sized sprites
            }
        }

        void Select(WorldCoordinateSelectEvent e)
        {
            var map = Resolve<IMapManager>().Current;
            float denominator = Vector3.Dot(Normal, e.Direction);
            if (Math.Abs(denominator) < 0.00001f)
                return;

            var pixelPosition = _position * new Vector2(map.TileSize.X, map.TileSize.Y);
            float t = Vector3.Dot(new Vector3(pixelPosition, 0.0f) - e.Origin, Normal) / denominator;
            if (t < 0)
                return;

            var intersectionPoint = e.Origin + t * e.Direction;
            int x = (int)(intersectionPoint.X - pixelPosition.X);
            int y = (int)(intersectionPoint.Y - pixelPosition.Y);

            if (x < 0 || x >= _size.X ||
                y < 0 || y >= _size.Y)
                return;

            e.RegisterHit(t, this);
        }

        void Render(RenderEvent e)
        {
            var map = Resolve<IMapManager>().Current;
            var pixelPosition = _position * new Vector2(map.TileSize.X, map.TileSize.Y);
            var positionLayered = new Vector3(pixelPosition, DrawLayer.Characters1.ToZCoordinate(_position.Y));
            var npcSprite = new Sprite<LargePartyGraphicsId>(
                _id,
                _frame,
                positionLayered,
                (int)DrawLayer.Characters1,
                0);

            e.Add(npcSprite);
        }
    }
}