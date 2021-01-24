﻿using System;
using Common.Shared.Min.Attributes;
using SramComparer.Enums;
using Res = SramComparer.SoE.Properties.Resources;
using ResComp = SramComparer.Properties.Resources;

namespace SramComparer.SoE.Enums
{
	[Serializable]
	[Flags]
	public enum ComparisonFlagsSoE : uint
	{
		[DisplayNameLocalized(nameof(ResComp.EnumSlotByteComparison), typeof(Res))]
		SlotByteByByteComparison = ComparisonFlags.SlotByteComparison,

		[DisplayNameLocalized(nameof(ResComp.EnumNonSlotComparison), typeof(Res))]
		NonSlotByteByByteComparison = ComparisonFlags.NonSlotComparison,

		[DisplayNameLocalized(nameof(Res.EnumChecksumIfDifferent), typeof(Res))]
		ChecksumIfDifferent = 1 << 2,

		[DisplayNameLocalized(nameof(Res.EnumChecksum), typeof(Res))]
		Checksum = 1 << 3 | ChecksumIfDifferent,

		[DisplayNameLocalized(nameof(Res.EnumUnknown12BIfDifferent), typeof(Res))]
		Unknown12BIfDifferent = 1 << 4,

		Unknown12B = 1 << 5 | Unknown12BIfDifferent,
	}
}