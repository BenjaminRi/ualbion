﻿using System.Collections.Generic;
using System.Linq;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Formats.Config;
using UAlbion.Game.Events;
using Veldrid;

namespace UAlbion.Game.Input
{
    public class DebugPickInputMode : Component
    {
        static readonly HandlerSet Handlers = new HandlerSet(
            H<DebugPickInputMode, SetInputModeEvent>((x,e) => x._isActive = e.Mode == InputMode.DebugPick),
            H<DebugPickInputMode, InputEvent>((x,e) => x.OnInput(e))
        );

        bool _isActive;

        void OnInput(InputEvent e)
        {
            if (!_isActive)
                return;

            if(e.Snapshot.MouseEvents.Any(x => x.MouseButton == MouseButton.Left && x.Down))
            {
                //Raise(new PopInputModeEvent());
                Raise(new PopInputModeEvent());
                return;
            }

            IList<(float, Selection)> hits = new List<(float, Selection)>();
            Raise(new ScreenCoordinateSelectEvent(e.Snapshot.MousePosition, (t, selection) => hits.Add((t, selection))));
            var orderedHits = hits.OrderBy(x => x.Item1).Select(x => x.Item2).ToList();
            Raise(new ShowDebugInfoEvent(orderedHits, e.Snapshot.MousePosition));
        }

        public DebugPickInputMode() : base(Handlers) { }
    }
}