﻿using System;
using System.Collections.Generic;
using System.Numerics;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Core.Visual;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets.Map;
using UAlbion.Game.Events;
using UAlbion.Game.Gui.Controls;
using UAlbion.Game.Scenes;
using UAlbion.Game.Text;

namespace UAlbion.Game.Entities
{
    public class LargeNpc : Component
    {
        static readonly HandlerSet Handlers = new HandlerSet(
            H<LargeNpc, SlowClockEvent>((x, e) => { x._sprite.Frame = e.FrameCount; }),
            H<LargeNpc, RightClickEvent>((x, e) => x.OnRightClick(e))
        );

        readonly MapNpc _npc;
        readonly MapSprite<LargeNpcId> _sprite;
        public override string ToString() => $"LNpc {_npc.Id} {_sprite.Id}";

        public LargeNpc(MapNpc npc) : base(Handlers)
        {
            _npc = npc ?? throw new ArgumentNullException(nameof(npc));
            _sprite = AttachChild(new MapSprite<LargeNpcId>((LargeNpcId)_npc.ObjectNumber, DrawLayer.Underlay - 1, 0, SpriteFlags.BottomAligned));
            _sprite.Selected += (sender, e) => e.SelectEvent.RegisterHit(e.HitPosition, this);
        }

        public override void Subscribed()
        {
            _sprite.TilePosition = new Vector3(
                _npc.Waypoints[0].X,
                _npc.Waypoints[0].Y,
                DepthUtil.IndoorCharacterDepth(_npc.Waypoints[0].Y)
            );
            base.Subscribed();
        }

        void OnRightClick(RightClickEvent rightClickEvent)
        {
            if (_npc.Chain == null || _npc.Id == null)
                return;

            var window = Resolve<IWindowManager>();
            var camera = Resolve<ICamera>();
            var assets = Resolve<IAssetManager>();
            var settings = Resolve<ISettings>();

            IText S(SystemTextId textId) 
                => new DynamicText(() =>
                {
                    var template = assets.LoadString(textId, settings.Gameplay.Language);
                    return new TextFormatter(assets, settings.Gameplay.Language)
                        .NoWrap()
                        .Centre()
                        .Format(template).Blocks;
                });

            var normPosition = camera.ProjectWorldToNorm(_sprite.Position);
            var uiPosition = window.NormToUi(normPosition.X, normPosition.Y);

            var heading = S(SystemTextId.MapPopup_Person);
            var options = new List<ContextMenuOption>
            {
                new ContextMenuOption(
                    S(SystemTextId.MapPopup_TalkTo),
                    new TriggerChainEvent(_npc.Chain, _npc.Chain.FirstEvent, TriggerType.TalkTo, _npc.Id.Value),
                    ContextMenuGroup.Actions),

                new ContextMenuOption(
                    S(SystemTextId.MapPopup_MainMenu),
                    new PushSceneEvent(SceneId.MainMenu),
                    ContextMenuGroup.System
                )
            };

            Raise(new ContextMenuEvent(uiPosition, heading, options));
            rightClickEvent.Propagating = false;
        }
    }
 }

