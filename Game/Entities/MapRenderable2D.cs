﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Core.Textures;
using UAlbion.Core.Visual;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets;
using UAlbion.Game.Events;
using UAlbion.Game.State;

namespace UAlbion.Game.Entities
{
    public class MapRenderable2D : Component
    {
        readonly SpriteInstanceData _blankInstance = new SpriteInstanceData(
            Vector3.Zero, Vector2.Zero, 
            Vector2.Zero, Vector2.Zero, 0, 0);

        const int TicksPerFrame = 10;
        int[] _animatedUnderlayIndices;
        int[] _animatedOverlayIndices;
        readonly MapData2D _mapData;
        readonly ITexture _tileset;
        readonly TilesetData _tileData;
        readonly MultiSprite _underlay;
        readonly MultiSprite _overlay;
        bool _renderUnderlay = true;
        bool _renderOverlay = true;

        static readonly HandlerSet Handlers = new HandlerSet(
            H<MapRenderable2D, RenderEvent>((x, e) => x.Render(e)),
            H<MapRenderable2D, UpdateEvent>((x, e) => x.Update()),
            H<MapRenderable2D, ToggleUnderlayEvent>((x,e) => x._renderUnderlay = !x._renderUnderlay),
            H<MapRenderable2D, ToggleOverlayEvent>((x,e) => x._renderOverlay = !x._renderOverlay)
        );

        public Vector2 TileSize { get; }
        public PaletteId Palette => (PaletteId)_mapData.PaletteId;
        public Vector2 SizePixels => new Vector2(_mapData.Width, _mapData.Height) * TileSize;
        public int? HighlightIndex { get; set; }
        int? _highLightEvent;

        public MapRenderable2D(MapData2D mapData, ITexture tileset, TilesetData tileData) : base(Handlers)
        {
            _mapData = mapData;
            _tileset = tileset;
            _tileData = tileData;
            _tileset.GetSubImageDetails(0, out var tileSize, out _, out _, out _);
            TileSize = tileSize;

            var underlay = new SpriteInstanceData[_mapData.Width * _mapData.Height];
            var overlay = new SpriteInstanceData[_mapData.Width * _mapData.Height];

            _underlay = new MultiSprite(new SpriteKey(
                _tileset,
                (int)DrawLayer.Underlay,
                underlay[0].Flags))
            {
                Instances = underlay.ToArray()
            };

            _overlay = new MultiSprite(new SpriteKey(
                _tileset,
                (int)DrawLayer.Overlay3,
                overlay[0].Flags))
            {
                Instances = overlay.ToArray()
            };
        }

        public override void Subscribed()
        {
            Raise(new LoadPaletteEvent(Palette));
            Update(true);
        }

        SpriteInstanceData BuildInstanceData(int i, int j, TilesetData.TileData tile, int tickCount)
        {
            if (tile == null || tile.Flags.HasFlag(TilesetData.TileFlags.Debug))
                return _blankInstance;

            int index = j * _mapData.Width + i;
            int subImage = tile.GetSubImageForTile(tickCount);

            _tileset.GetSubImageDetails(
                subImage,
                out var tileSize,
                out var texPosition,
                out var texSize,
                out var layer);

            DrawLayer drawLayer = tile.Layer.ToDrawLayer();
            var instance = new SpriteInstanceData(
                new Vector3(
                    new Vector2(i, j) * tileSize,
                    drawLayer.ToZCoordinate(j)),
                tileSize,
                texPosition,
                texSize,
                layer,
                0);

            int zoneNum = _mapData.ZoneLookup[index];
            int eventNum = zoneNum == -1 ? -1 : _mapData.Zones[zoneNum].EventNumber;

            instance.Flags =
                (_tileset is EightBitTexture ? SpriteFlags.UsePalette : 0)
                // | (HighlightIndex == index ? SpriteFlags.Highlight : 0)
                //| (eventNum != -1 && _highLightEvent != eventNum ? SpriteFlags.Highlight : 0)
                // | (_highLightEvent == eventNum ? SpriteFlags.GreenTint : 0)
                // | (tile.Collision != TilesetData.Passability.Passable ? SpriteFlags.RedTint : 0)
                //| ((tile.Flags & TilesetData.TileFlags.TextId) != 0 ? SpriteFlags.RedTint : 0)
                //| (((int) tile.Type) == 8 ? SpriteFlags.GreenTint : 0)
                //| (((int) tile.Type) == 12 ? SpriteFlags.BlueTint : 0)
                //| (((int) tile.Type) == 14 ? SpriteFlags.GreenTint | SpriteFlags.RedTint : 0) //&& tickCount % 2 == 0 ? SpriteFlags.Transparent : 0)
                ;

            return instance;
        }

        void Update(bool updateAll = false)
        {
            var state = Resolve<IGameState>();
            if (HighlightIndex.HasValue)
            {
                int zoneNum = _mapData.ZoneLookup[HighlightIndex.Value];
                _highLightEvent = zoneNum == -1 ? -1 : _mapData.Zones[zoneNum].EventNumber;
                if (_highLightEvent == -1)
                    _highLightEvent = null;
            }
            else _highLightEvent = null;

            if (updateAll)
            {
                var animatedUnderlayTiles = new List<int>();
                var animatedOverlayTiles = new List<int>();

                int index = 0;
                for (int j = 0; j < _mapData.Height; j++)
                {
                    for (int i = 0; i < _mapData.Width; i++)
                    {
                        var underlayTileId = _mapData.Underlay[index];
                        var underlayTile = underlayTileId == -1 ? null : _tileData.Tiles[underlayTileId];
                        _underlay.Instances[index] = BuildInstanceData(i, j, underlayTile, state.TickCount / TicksPerFrame);
                        if(underlayTile?.FrameCount > 1)
                            animatedUnderlayTiles.Add(index);

                        var overlayTileId = _mapData.Overlay[index];
                        var overlayTile = overlayTileId == -1 ? null : _tileData.Tiles[overlayTileId];
                        _overlay.Instances[index] = BuildInstanceData(i, j, overlayTile, state.TickCount / TicksPerFrame);
                        if(overlayTile?.FrameCount > 1)
                            animatedOverlayTiles.Add(index);
                        index++;
                    }
                }

                _animatedUnderlayIndices = animatedUnderlayTiles.ToArray();
                _animatedOverlayIndices = animatedOverlayTiles.ToArray();
            }
            else
            {
                foreach (var index in _animatedUnderlayIndices)
                {
                    var underlayTileId = _mapData.Underlay[index];
                    var underlayTile = underlayTileId == -1 ? null : _tileData.Tiles[underlayTileId];
                    _underlay.Instances[index] = BuildInstanceData(
                        index % _mapData.Width,
                        index / _mapData.Width,
                        underlayTile,
                        3 * state.TickCount / 2);
                }

                foreach(var index in _animatedOverlayIndices)
                {
                    var overlayTileId = _mapData.Overlay[index];
                    var overlayTile = overlayTileId == -1 ? null : _tileData.Tiles[overlayTileId];
                    _overlay.Instances[index] = BuildInstanceData(
                        index % _mapData.Width,
                        index / _mapData.Width,
                        overlayTile,
                        3 * state.TickCount / 2);
                }
            }
        }

        void Render(RenderEvent e)
        {
            if (_renderUnderlay)
                e.Add(_underlay);
            if (_renderOverlay)
                e.Add(_overlay);
        }
    }
}
