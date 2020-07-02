﻿using UAlbion.Api;
using UAlbion.Formats.AssetIds;

namespace UAlbion.Game.Events.Transitions
{
    [Event("gravity_item_transition")]
    public class GravityItemTransitionEvent : Event, IAsyncEvent
    {
        public GravityItemTransitionEvent(ItemId itemId, float fromNormX, float fromNormY)
        {
            ItemId = itemId;
            FromNormX = fromNormX;
            FromNormY = fromNormY;
        }

        [EventPart("item_id")] public ItemId ItemId { get; }
        [EventPart("from_x")] public float FromNormX { get; }
        [EventPart("from_y")] public float FromNormY { get; }
    }
}