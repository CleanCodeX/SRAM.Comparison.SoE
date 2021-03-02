﻿using System;
using System.Linq;
using System.Text;
using Common.Shared.Min.Extensions;
using IO.Extensions;
using IO.Models;
using SoE;
using SoE.Models.Structs;
using SoE.Models.Structs.Unknown;
using SRAM.Comparison.Helpers;
using SRAM.Comparison.Services;
using SRAM.Comparison.SoE.Enums;
using SRAM.Comparison.SoE.Properties;
using SRAM.SoE.Extensions;
using SRAM.SoE.Models;
using SRAM.SoE.Models.Structs;
using WRAM.Snes9x.SoE.Models.Structs.Chunks;
using static SRAM.Comparison.SoE.Helpers.UnkownBufferOffsetFinder;
using Res = SRAM.Comparison.Properties.Resources;
// ReSharper disable RedundantArgumentDefaultValue

namespace SRAM.Comparison.SoE.Services
{
	/// <summary>S-RAM comparer implementation for SoE</summary>
	/// <inheritdoc cref="SramComparerBase{TSramFile,TSaveSlot}"/>
	public class SramComparerSoE : SramComparerBase<SramFileSoE, SaveSlotSoE>
	{
		public SramComparerSoE() : base(ComparisonServices.ConsolePrinter) {}
		public SramComparerSoE(IConsolePrinter consolePrinter) :base(consolePrinter) {}

		#region Compare S-RAM

		/// <inheritdoc cref="SramComparerBase{TSramFile,TSaveSlot}"/>
		protected override int OnCompareSram(SramFileSoE currFile, SramFileSoE compFile, IOptions options)
		{
			var optionCurrSlotIndex = options.CurrentFileSaveSlot - 1;
			var optionCompSlotIndex = options.ComparisonFileSaveSlot - 1;

			if (optionCompSlotIndex > -1)
				if (optionCurrSlotIndex == -1 || optionCurrSlotIndex == optionCompSlotIndex)
					optionCompSlotIndex = -1;

			var optionFlags = (ComparisonFlagsSoE)options.ComparisonFlags;
			if(optionFlags.HasFlag(ComparisonFlagsSoE.ChecksumStatus))
				PrintSaveSlotChecksumValidation(currFile, compFile);

			ConsolePrinter.PrintLine();

			var slotComparisonMode = optionCompSlotIndex > -1 && optionCurrSlotIndex > -1
				? Res.StatusDifferentSaveSlotComparisonTemplate.InsertArgs(options.CurrentFileSaveSlot,
					options.ComparisonFileSaveSlot)
				: optionCurrSlotIndex > -1
					? string.Format(Res.StatusSingleSaveSlotComparisonTemplate, options.CurrentFileSaveSlot)
					: Res.StatusAllSaveSlotsComparison;
			ConsolePrinter.PrintColoredLine(ConsoleColor.Yellow, slotComparisonMode);
			ConsolePrinter.PrintLine();

			var allDiffBytes = 0;

			#region Preparation for additional save slot value display

			StringBuilder checksums = new(), timestamps = new();

			checksums.AppendLine($"{Resources.EnumChecksum} (2 {Res.Bytes} | {Resources.CompChangesAtEveryInSaveSlotSave}):");
			timestamps.AppendLine($"{nameof(Sizes.ScriptedEventTimer)} ({Sizes.ScriptedEventTimer} {Res.Bytes}):");

			#endregion

			#region Slot comparison

			// Single slot cimparison
			if (optionCurrSlotIndex > -1 && optionCompSlotIndex > -1)
				allDiffBytes = CompareSaveSlots(optionCurrSlotIndex, optionCompSlotIndex);
			else
				#region Slot loop

				for (var slotIndex = 0; slotIndex < 4; slotIndex++)
				{
					if (optionCurrSlotIndex > -1 && optionCurrSlotIndex != slotIndex) continue;

					allDiffBytes = CompareSaveSlots(slotIndex);
				}

				#endregion

			#endregion

			#region Non slot comparison

			if (options.ComparisonFlags.HasFlag(ComparisonFlagsSoE.NonSlotComparison))
			{
				var nonSaveSlotUnknownDiffBytes = CompareValue(nameof(currFile.Struct.Unknown19), 0, currFile.Struct.Unknown19, compFile.Struct.Unknown19, false);

				if (nonSaveSlotUnknownDiffBytes > 0)
				{
					ConsolePrinter.PrintLine();
					ConsolePrinter.PrintColoredLine(ConsoleColor.Magenta, " ".Repeat(2) + $@"[ {Res.CompSectionNonSaveSlotUnknowns} ]");
					ConsolePrinter.ResetColor();

					nonSaveSlotUnknownDiffBytes = CompareValue(nameof(currFile.Struct.Unknown19), SramOffsets.Unknown19, currFile.Struct.Unknown19, compFile.Struct.Unknown19, true);

					ConsolePrinter.PrintLine();
					ConsolePrinter.PrintColoredLine(nonSaveSlotUnknownDiffBytes > 0 ? ConsoleColor.Green : ConsoleColor.White, " ".Repeat(4) + Res.StatusNonSaveSlotUnknownsBytesTemplate.InsertArgs(nonSaveSlotUnknownDiffBytes));
					ConsolePrinter.ResetColor();

					allDiffBytes += nonSaveSlotUnknownDiffBytes;
				}
			}

			#endregion

			#region Display of comparison summary

			ConsolePrinter.PrintLine();

			const int borderLength = 50;
			var color = allDiffBytes > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
			ConsolePrinter.PrintColoredLine(color, "=".Repeat(borderLength));
			ConsolePrinter.PrintColoredLine(color,
				allDiffBytes > 0
					? @$"== {Res.StatusSramChangedBytesTemplate.InsertArgs(allDiffBytes)} ".PadRight(borderLength, '=')
					: @$"== {Res.StatusNoSramBytesChanged} =".PadRight(borderLength, '='));

			ConsolePrinter.PrintColoredLine(color, "=".Repeat(borderLength));
			ConsolePrinter.ResetColor();

			#endregion

			#region Display of additional save slot values

			if (optionFlags != default)
			{
				ConsolePrinter.PrintLine();

				if (optionFlags.HasFlag(ComparisonFlagsSoE.ChecksumIfDifferent))
					ConsolePrinter.PrintColoredLine(ConsoleColor.Cyan, FormatAdditionalValues(nameof(checksums), checksums));

				if (optionFlags.HasFlag(ComparisonFlagsSoE.ScriptedEventTimerIfDifferent))
					ConsolePrinter.PrintColoredLine(ConsoleColor.Cyan, FormatAdditionalValues(nameof(timestamps), timestamps));
			}

			#endregion

			return allDiffBytes;

			int CompareSaveSlots(int currSlotIndex, int compSlotIndex = -1)
			{
				if (compSlotIndex == -1)
					compSlotIndex = currSlotIndex;

				var currSlot = currFile.GetSegment(currSlotIndex);
				var currSlotBytes = currFile.GetSegmentBytes(currSlotIndex);

				var compSlot = compFile.GetSegment(compSlotIndex);
				var compSlotBytes = compFile.GetSegmentBytes(compSlotIndex);

				var currSlotId = currSlotIndex + 1;
				var compSlotId = compSlotIndex + 1;

				var slotIdString = currSlotId.ToString();
				if (currSlotId != compSlotId)
					slotIdString += $" ({Resources.CompComparedWithOtherSaveSlotTemplate.InsertArgs(compSlotId)})";

				#region Additional save slot value display

				const int padding = 25;
				var currSlotName = $"{$"({Res.EnumCurrentFile})".PadRight(padding)} {Res.CompSlot} {currSlotId}";
				var currSlotNameString = $"{" ".Repeat(2)}{currSlotName}";
				
				if (optionFlags.HasFlag(ComparisonFlagsSoE.Checksum) || compSlot.Checksum != currSlot.Checksum)
					checksums.AppendLine($"{currSlotNameString}: {currSlot.Checksum.PadLeft()}");

				if (optionFlags.HasFlag(ComparisonFlagsSoE.ScriptedEventTimer) || compSlot.Data.EquippedStuff_Moneys_Levels.ScriptedEventTimer != currSlot.Data.EquippedStuff_Moneys_Levels.ScriptedEventTimer)
					timestamps.AppendLine($"{currSlotNameString}: {currSlot.Data.EquippedStuff_Moneys_Levels.ScriptedEventTimer.PadLeft()}");

				#endregion

				if (compSlotBytes.SequenceEqual(currSlotBytes)) return allDiffBytes;

				#region Additional save slot value display

				var compSlotName = $"{$"({Res.EnumComparisonFile})".PadRight(padding)} {Res.CompSlot} {compSlotId}";
				var compSlotNameString = $"{" ".Repeat(2)}{compSlotName}";

				checksums.AppendLine($"{compSlotNameString}: {compSlot.Checksum.PadLeft()}");
				timestamps.AppendLine($"{compSlotNameString}: {compSlot.Data.EquippedStuff_Moneys_Levels.ScriptedEventTimer.PadLeft()}");

				#endregion

				ConsolePrinter.PrintColoredLine(ConsoleColor.Yellow, $@"[ {Res.CompSlot} {slotIdString} ]");
				ConsolePrinter.ResetColor();

				ConsolePrinter.PrintLine();
				ConsolePrinter.PrintColoredLine(ConsoleColor.Magenta, " ".Repeat(2) + $@"[ {Res.CompUnknownAreasOnly} ]");
				ConsolePrinter.ResetColor();

				var slotDiffBytes = OnCompareSaveSlot(currSlot, compSlot, options);
				if (slotDiffBytes > 0)
				{
					ConsolePrinter.PrintLine();
					ConsolePrinter.PrintColoredLine(ConsoleColor.Green, " ".Repeat(4) + Res.StatusUnknownsChangedBytesTemplate.InsertArgs(slotDiffBytes));
					ConsolePrinter.ResetColor();
				}

				if (optionFlags.HasFlag(ComparisonFlagsSoE.SlotByteComparison))
				{
					#region Slot byte by byte comparison

					ConsoleHelper.EnsureMinConsoleWidth(ComparisonConsoleWidth);

					var fieldName = $"{nameof(compFile.Struct.SaveSlots)}[{currSlotIndex}]";
					var bufferOffset = SramOffsets.FirstSaveSlot + currSlotId * SramSizes.SaveSlot;
					var slotBufferDiffBytes = CompareValue(fieldName, bufferOffset, currSlotBytes, compSlotBytes, false);
					if (slotBufferDiffBytes > slotDiffBytes)
					{
						ConsolePrinter.PrintLine();
						ConsolePrinter.PrintColoredLine(ConsoleColor.Magenta,
							$@"{" ".Repeat(2)}[ {Res.CompSectionSaveSlotChangedTemplate} ]".InsertArgs(currSlotIndex));
						// ReSharper disable once RedundantArgumentDefaultValue

						CompareValue(fieldName, bufferOffset, currSlotBytes, compSlotBytes, true,
							offset => typeof(SaveSlotDataSoE).GetFieldNameByOffset(offset), isUnknown: false);
						ConsolePrinter.PrintLine();
						ConsolePrinter.PrintColoredLine(slotDiffBytes > 0 ? ConsoleColor.Green : ConsoleColor.White,
							" ".Repeat(4) +
							Res.StatusSaveSlotChangedResultTemplate.InsertArgs(slotIdString, slotBufferDiffBytes));
						ConsolePrinter.ResetColor();
					}

					allDiffBytes += slotBufferDiffBytes;

					#endregion
				}
				else
					allDiffBytes += slotDiffBytes;

				return allDiffBytes;
			}
		}

		protected virtual string FormatAdditionalValues(string name, StringBuilder values) => values.Replace(Environment.NewLine, ConsolePrinter.NewLine).ToString();

		protected virtual void PrintSaveSlotChecksumValidation(SramFileSoE currFile, SramFileSoE compFile)
		{
			ConsolePrinter.PrintSectionHeader();
			ConsolePrinter.PrintColoredLine(ConsoleColor.DarkYellow, $@"{Resources.CompChecksumValidation}:");

			OnPrintSaveSlotChecksumValidation(Res.EnumCurrentFile, currFile);
			OnPrintSaveSlotChecksumValidation(Res.EnumComparisonFile, compFile);
		}

		protected virtual void OnPrintSaveSlotChecksumValidation(string name, IMultiSegmentFile file)
		{
			ConsolePrinter.PrintColored(ConsoleColor.Gray, $@"{name}:".PadRight(15));
			ConsolePrinter.PrintColored(ConsoleColor.DarkYellow, $@" {Res.CompSlot} (1-{file.MaxIndex + 1})");
			ConsolePrinter.PrintColored(ConsoleColor.White, @" [ ");

			for (var i = 0; i <= file.MaxIndex; i++)
			{
				if (i > 0)
					ConsolePrinter.PrintColored(ConsoleColor.White, @" | ");

				var isValid = file.IsValid(i);
				ConsolePrinter.PrintColored(isValid ? ConsoleColor.DarkGreen : ConsoleColor.Red, isValid ? Resources.Valid : Resources.Invalid);
			}

			ConsolePrinter.PrintColoredLine(ConsoleColor.White, @" ]");
		}

		#endregion CompareSram

		#region Compare SaveSlot

		/// <inheritdoc cref="SramComparerBase{TSramFile,TSaveSlot}"/>
		protected override int OnCompareSaveSlot(SaveSlotSoE currSaveSlot, SaveSlotSoE compSaveSlot, IOptions options)
		{
			ref var currData = ref currSaveSlot.Data;
			ref var compData = ref compSaveSlot.Data;

			// ReSharper disable once InlineOutVariableDeclaration
			string name;
			// ReSharper disable once JoinDeclarationAndInitializer
			int diffBytes = 0, offset;

			#region Chunk 3

			//Unknown 4
			for (var i = 0; i < 4; ++i)
			{
				var parentName = nameof(currData.BoyStatusBuffs);
				offset = GetSaveSlotOffset(BuildIndexName(parentName, i, nameof(CharacterBuffStatus.Id)), out name);
				diffBytes += CompareValue(name, offset, currData.BoyStatusBuffs.Status[i].Id, compData.BoyStatusBuffs.Status[i].Id);

				offset = GetSaveSlotOffset(BuildIndexName(parentName, i, nameof(CharacterBuffStatus.Timer)), out name);
				diffBytes += CompareValue(name, offset, currData.BoyStatusBuffs.Status[i].Timer, compData.BoyStatusBuffs.Status[i].Timer);

				offset = GetSaveSlotOffset(BuildIndexName(parentName, i, nameof(CharacterBuffStatus.Boost)), out name);
				diffBytes += CompareValue(name, offset, currData.BoyStatusBuffs.Status[i].Boost, compData.BoyStatusBuffs.Status[i].Boost);
			}
			//Unknown 4

			#endregion

			#region Chunk 10

			//Unknown 7
			for (var i = 0; i < 4; ++i)
			{
				var parentName = nameof(currData.DogStatusBuffs);
				offset = GetSaveSlotOffset(BuildIndexName(parentName, i, nameof(CharacterBuffStatus.Id)), out name);
				diffBytes += CompareValue(name, offset, currData.DogStatusBuffs.Status[i].Id, compData.DogStatusBuffs.Status[i].Id);

				offset = GetSaveSlotOffset(BuildIndexName(parentName, i, nameof(CharacterBuffStatus.Timer)), out name);
				diffBytes += CompareValue(name, offset, currData.DogStatusBuffs.Status[i].Timer, compData.DogStatusBuffs.Status[i].Timer);

				offset = GetSaveSlotOffset(BuildIndexName(parentName, i, nameof(CharacterBuffStatus.Boost)), out name);
				diffBytes += CompareValue(name, offset, currData.DogStatusBuffs.Status[i].Boost, compData.DogStatusBuffs.Status[i].Boost);
			}
			//Unknown 7

			#endregion

			#region Chunk 16

			offset = GetSaveSlotOffset(nameof(currData.EquippedStuff_Moneys_Levels.CurrentEquippedWeapon), out name);
			diffBytes += CompareValue(name, offset, currData.EquippedStuff_Moneys_Levels.CurrentEquippedWeapon.ToUShort(), compData.EquippedStuff_Moneys_Levels.CurrentEquippedWeapon.ToUShort());

			offset = GetSaveSlotOffset(nameof(currData.EquippedStuff_Moneys_Levels.Unknown9), out name);
			diffBytes += CompareValue(name, offset, currData.EquippedStuff_Moneys_Levels.Unknown9, compData.EquippedStuff_Moneys_Levels.Unknown9);

			offset = GetSaveSlotOffset(nameof(currData.EquippedStuff_Moneys_Levels.EquippedAlchemies), out name);
			diffBytes += CompareValue(name, offset, currData.EquippedStuff_Moneys_Levels.EquippedAlchemies.ToBytes(), compData.EquippedStuff_Moneys_Levels.EquippedAlchemies.ToBytes());

			offset = GetSaveSlotOffset(nameof(currData.EquippedStuff_Moneys_Levels.Unknown11), out name);
			diffBytes += CompareValue(name, offset, currData.EquippedStuff_Moneys_Levels.Unknown11, compData.EquippedStuff_Moneys_Levels.Unknown11);

			//Unknown 12
			if (options.ComparisonFlags.HasFlag(ComparisonFlagsSoE.ScriptedEventTimerIfDifferent))
			{
				offset = GetSaveSlotOffset(nameof(currData.EquippedStuff_Moneys_Levels.ScriptedEventTimer), out name);
				diffBytes += CompareValue(name, offset, currData.EquippedStuff_Moneys_Levels.ScriptedEventTimer,
					compData.EquippedStuff_Moneys_Levels.ScriptedEventTimer);
			}

			offset = GetSaveSlotOffset(nameof(currData.EquippedStuff_Moneys_Levels.ControlsPrefs_Configuration), out name);
			diffBytes += CompareValue(name, offset, currData.EquippedStuff_Moneys_Levels.ControlsPrefs_Configuration, compData.EquippedStuff_Moneys_Levels.ControlsPrefs_Configuration);
			// 

			#endregion Chunk 16

			#region Chunk 17

			offset = GetSaveSlotOffset(nameof(currData.AlchemyLevels.Unknown13), out name);
			diffBytes += CompareValue(name, offset, currData.AlchemyLevels.Unknown13, compData.AlchemyLevels.Unknown13);

			#endregion

			#region Chunk 18

			offset = GetSaveSlotOffset(nameof(currData.Collectables_Spots.Unknown14), out name);
			diffBytes += CompareValue(name, offset, 
				currData.Collectables_Spots.Unknown14.Value.ToByteArray(), 
				compData.Collectables_Spots.Unknown14.Value.ToByteArray());

			offset = GetSaveSlotOffset(nameof(currData.Collectables_Spots.Unknown14B), out name);
			diffBytes += CompareValue(name, offset,
				currData.Collectables_Spots.Unknown14B,
				compData.Collectables_Spots.Unknown14B);

			offset = GetSaveSlotOffset(nameof(currData.Collectables_Spots.GourdSpots), out name);
			diffBytes += CompareValue(name, offset,
				currData.Collectables_Spots.GourdSpots.Value.ToByteArray(),
				compData.Collectables_Spots.GourdSpots.Value.ToByteArray());

			offset = GetSaveSlotOffset(nameof(currData.Collectables_Spots.IngredientSniffSpots), out name);
			diffBytes += CompareValue(name, offset,
				currData.Collectables_Spots.IngredientSniffSpots.Data,
				compData.Collectables_Spots.IngredientSniffSpots.Data);

			//Unknown 15
			offset = GetSaveSlotTypeOffset(typeof(Unknown15), nameof(currData.Collectables_Spots.Unknown15.Data), out name);
			diffBytes += CompareValue(name, offset, currData.Collectables_Spots.Unknown15.Data, compData.Collectables_Spots.Unknown15.Data);
			//Unknown 15

			#region Unknown 16

			offset = GetSaveSlotOffset(nameof(currData.Collectables_Spots.Unknown16A), out name);
			diffBytes += CompareValue(name, offset, currData.Collectables_Spots.Unknown16A, compData.Collectables_Spots.Unknown16A);

			offset = GetSaveSlotOffset(nameof(currData.Collectables_Spots.Unknown16B), out name);
			diffBytes += CompareValue(name, offset,
				currData.Collectables_Spots.Unknown16B.ToByteArray(),
				compData.Collectables_Spots.Unknown16B.ToByteArray());

			offset = GetSaveSlotTypeOffset(typeof(Unknown16C), nameof(currData.Collectables_Spots.Unknown16C.Data), out name);
			diffBytes += CompareValue(name, offset, currData.Collectables_Spots.Unknown16C.Data, compData.Collectables_Spots.Unknown16C.Data);

			#endregion Unknown 16

			#endregion Chunk 18

			#region Chunk 19

			offset = GetSaveSlotOffset(nameof(currData.PossessedStuff.Unknown17A), out name);
			diffBytes += CompareValue(name, offset, currData.PossessedStuff.Unknown17A, compData.PossessedStuff.Unknown17A);

			offset = GetSaveSlotOffset(nameof(currData.PossessedStuff.Unknown17B), out name);
			diffBytes += CompareValue(name, offset, currData.PossessedStuff.Unknown17B, compData.PossessedStuff.Unknown17B);

			#endregion

			#region Chunk 20

			offset = GetSaveSlotOffset(nameof(currData.LastLanding_CurrentWeapon.Unknown17C), out name);
			diffBytes += CompareValue(name, offset, currData.LastLanding_CurrentWeapon.Unknown17C, compData.LastLanding_CurrentWeapon.Unknown17C);

			offset = GetSaveSlotTypeOffset(typeof(Chunk20), nameof(currData.LastLanding_CurrentWeapon.CurrentEquippedWeapon), out name);
			diffBytes += CompareValue(name, offset, currData.LastLanding_CurrentWeapon.CurrentEquippedWeapon.ToUShort(), compData.LastLanding_CurrentWeapon.CurrentEquippedWeapon.ToUShort());

			offset = GetSaveSlotOffset(nameof(currData.LastLanding_CurrentWeapon.Unknown17D), out name);
			diffBytes += CompareValue(name, offset, currData.LastLanding_CurrentWeapon.Unknown17D, compData.LastLanding_CurrentWeapon.Unknown17D);

			offset = GetSaveSlotOffset(nameof(currData.LastLanding_CurrentWeapon.Unknown17E), out name);
			diffBytes += CompareValue(name, offset, currData.LastLanding_CurrentWeapon.Unknown17E, compData.LastLanding_CurrentWeapon.Unknown17E);

			offset = GetSaveSlotOffset(nameof(currData.LastLanding_CurrentWeapon.LastLandingLocation), out name);
			diffBytes += CompareValue(name, offset, currData.LastLanding_CurrentWeapon.LastLandingLocation.ToByteArray(), compData.LastLanding_CurrentWeapon.LastLandingLocation.ToByteArray());

			#endregion

			#region Chunk 21

			offset = GetSaveSlotOffset(nameof(currData.LastLanding_CurrentWeapon.Unknown17F), out name);
			diffBytes += CompareValue(name, offset, currData.LastLanding_CurrentWeapon.Unknown17F, compData.LastLanding_CurrentWeapon.Unknown17F);

			offset = GetSaveSlotOffset(nameof(currData.TradeGoods.Unknown17G), out name);
			diffBytes += CompareValue(name, offset, currData.TradeGoods.Unknown17G, compData.TradeGoods.Unknown17G);

			offset = GetSaveSlotOffset(nameof(currData.TradeGoods.Unknown18), out name);
			diffBytes += CompareValue(name, offset, currData.TradeGoods.Unknown18, compData.TradeGoods.Unknown18);

			#endregion

			return diffBytes;

			#region Helpers

			static string BuildIndexName(string parentName, int index, string fieldName) => $"{parentName}{index}{StructDelimiter}{fieldName}";

			static int GetSaveSlotTypeOffset(Type containingType, string fieldName, out string name) => 
				GetSaveSlotOffset(BuildFieldName(typeof(SaveSlotDataSoE), containingType, fieldName), out name);

			static int GetSaveSlotOffset(string fieldName, out string name)
			{
				var offset = GetSaveSlotBufferOffset(fieldName, out var result);

				name = result.ToString()!;

				return offset;
			}

			#endregion
		}

		protected override int? GetWramOffset(int offset) => 
			SramOffsets.WramOffsetMappings.TryGetValue(offset, out var result) ? result.WramOffset : null;

		#endregion CompareSaveSlot
	}
}