﻿using System;
using System.IO;
using SramComparer.Services;
using SramComparer.SoE.Enums;
using SramComparer.SoE.Extensions;
using SramFormat.SoE;
using SramFormat.SoE.Constants;
using SramFormat.SoE.Models.Structs;

namespace SramComparer.SoE.Services
{
	/// <summary>Command handler implementation for SoE</summary>
	/// <inheritdoc cref="CommandHandler{TSramFile,TSaveSlot}"/>
	public class CommandHandlerSoE: CommandHandler<SramFileSoE, SaveSlot>
	{
		public CommandHandlerSoE() { }
		public CommandHandlerSoE(IConsolePrinter consolePrinter) :base(consolePrinter) {}

		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void Compare(Stream currFile, Stream compFile, IOptions options) => Compare<SramComparerSoE>(currFile, compFile, options);
		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void Compare(Stream currFile, Stream compFile, IOptions options, TextWriter output) => Compare<SramComparerSoE>(currFile, compFile, options, output);
		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void Compare(IOptions options) => Compare<SramComparerSoE>(options);
		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void Compare(IOptions options, TextWriter output) => Compare<SramComparerSoE>(options, output);

		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void ExportComparisonResult(IOptions options) => ExportComparisonResult<SramComparerSoE>(options);
		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void ExportComparisonResult(IOptions options, bool showInExplorer) => ExportComparisonResult<SramComparerSoE>(options, showInExplorer);
		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void ExportComparisonResult(IOptions options, string filePath) => ExportComparisonResult<SramComparerSoE>(options, filePath);
		/// <summary>Convinience method for using the standard <see cref="SramComparerSoE"/></summary>
		public void ExportComparisonResult(IOptions options, string filePath, bool showInExplorer) => ExportComparisonResult<SramComparerSoE>(options, filePath, showInExplorer);

		/// <inheritdoc cref="CommandHandler{TSramFile,TSaveSlot}"/>
		protected override bool OnRunCommand(string command, IOptions options)
		{
			switch (command)
			{
				case nameof(CommandsSoE.Compare):
					Compare(options);
					break;
				case nameof(CommandsSoE.Export):
					ExportComparisonResult(options, true);
					break;
				case nameof(CommandsSoE.U12b) or nameof(CommandsSoE.U12b_IfDiff):
					options.ComparisonFlags = InvertIncludeFlag(options.ComparisonFlags,
						command == nameof(CommandsSoE.U12b)
							? ComparisonFlagsSoE.Unknown12B
							: ComparisonFlagsSoE.Unknown12BIfDifferent);
					break;
				case nameof(CommandsSoE.Checksum) or nameof(CommandsSoE.Checksum_IfDiff):
					options.ComparisonFlags = InvertIncludeFlag(options.ComparisonFlags,
						command == nameof(CommandsSoE.Checksum)
							? ComparisonFlagsSoE.Checksum
							: ComparisonFlagsSoE.ChecksumIfDifferent);
					break;
				default:
					return base.OnRunCommand(command, options);
			}

			return true;
		}

		protected override bool ConvertStreamIfSaveState(ref Stream stream, string? filePath, string? saveStateType)
		{
			if (!base.ConvertStreamIfSaveState(ref stream, filePath, saveStateType)) return false;

			const int length = Sizes.Sram;
			MemoryStream ms;

			try
			{
				ms = stream.GetStreamSlice(length);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				throw;
			}

			if (length != ms.Length)
				throw new InvalidOperationException($"Copied stream has wrong size. Was {ms.Length}, but should be {length}");

			stream = ms;
			
			return true;
		}
	}
}