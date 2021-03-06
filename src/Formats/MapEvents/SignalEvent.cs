﻿using SerdesNet;
using UAlbion.Api;

namespace UAlbion.Formats.MapEvents
{
    public class SignalEvent : MapEvent
    {
        public static SignalEvent Serdes(SignalEvent e, ISerializer s)
        {
            e ??= new SignalEvent();
            s.Begin();
            e.SignalId = s.UInt8(nameof(SignalId), e.SignalId);
            e.Unk2 = s.UInt8(nameof(Unk2), e.Unk2);
            e.Unk3 = s.UInt8(nameof(Unk3), e.Unk3);
            e.Unk4 = s.UInt8(nameof(Unk4), e.Unk4);
            e.Unk5 = s.UInt8(nameof(Unk5), e.Unk5);
            e.Unk6 = s.UInt16(nameof(Unk6), e.Unk6);
            e.Unk8 = s.UInt16(nameof(Unk8), e.Unk8);
            ApiUtil.Assert(e.Unk2 == 0);
            ApiUtil.Assert(e.Unk3 == 0);
            ApiUtil.Assert(e.Unk4 == 0);
            ApiUtil.Assert(e.Unk5 == 0);
            ApiUtil.Assert(e.Unk6 == 0);
            ApiUtil.Assert(e.Unk8 == 0);
            s.End();
            return e;
        }

        public byte SignalId { get; private set; }
        byte Unk2 { get; set; }
        byte Unk3 { get; set; }
        byte Unk4 { get; set; }
        byte Unk5 { get; set; }
        ushort Unk6 { get; set; }
        ushort Unk8 { get; set; }
        public override string ToString() => $"signal {SignalId}";
        public override MapEventType EventType => MapEventType.Signal;
    }
}
