using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace InverseTorque.Extension
{
    public static unsafe class NativeMemoryEx
    {
		public static int RealThrottlePowerOffset { get; }
		public static int LowSpeedTractionMultOffset { get; }
		public static int TractionCurveLateralOffset { get; }

		[DllImport("ScriptHookV.dll", ExactSpelling = true, EntryPoint = "?getGameVersion@@YA?AW4eGameVersion@@XZ")]
		public static extern int GetGameVersion();

		/// <summary>
		/// Returns pointer to a global variable. IDs may differ between game versions.
		/// </summary>
		/// <param name="index">The variable ID to query.</param>
		/// <returns>Pointer to the variable, or <see cref="IntPtr.Zero"/> if it does not exist.</returns>
		[DllImport("ScriptHookV.dll", ExactSpelling = true, EntryPoint = "?getGlobalPtr@@YAPEA_KH@Z")]
		public static extern IntPtr GetGlobalPtr(int index);

		/// <summary>
		/// Searches the address space of the current process for a memory pattern.
		/// </summary>
		/// <param name="pattern">The pattern.</param>
		/// <param name="mask">The pattern mask.</param>
		/// <returns>The address of a region matching the pattern or <c>null</c> if none was found.</returns>
		static unsafe byte* FindPattern(string pattern, string mask)
		{
			ProcessModule module = Process.GetCurrentProcess().MainModule;
			return FindPattern(pattern, mask, module.BaseAddress, (ulong)module.ModuleMemorySize);
		}

		/// <summary>
		/// Searches the specific address space of the current process for a memory pattern.
		/// </summary>
		/// <param name="pattern">The pattern.</param>
		/// <param name="mask">The pattern mask.</param>
		/// <param name="startAddress">The address to start searching at.</param>
		/// <param name="size">The size where the pattern search will be performed from <paramref name="startAddress"/>.</param>
		/// <returns>The address of a region matching the pattern or <c>null</c> if none was found.</returns>
		static unsafe byte* FindPattern(string pattern, string mask, IntPtr startAddress, ulong size)
		{
			ulong address = (ulong)startAddress.ToInt64();
			ulong endAddress = address + size;

			for (; address < endAddress; address++)
			{
				for (int i = 0; i < pattern.Length; i++)
				{
					if (mask[i] != '?' && ((byte*)address)[i] != pattern[i])
						break;
					else if (i + 1 == pattern.Length)
						return (byte*)address;
				}
			}

			return null;
		}

		static NativeMemoryEx()
        {
            byte* address;

            address = FindPattern("\x74\x0A\xF3\x0F\x11\xB3\x1C\x09\x00\x00\xEB\x25", "xxxxxx????xx");
            if (address != null)
            {
				RealThrottlePowerOffset = *(int*)(address + 6) + 0x10;
            }

			LowSpeedTractionMultOffset = 0x00A8;
			TractionCurveLateralOffset = 0x0098;

		}

		public static float ReadFloat(IntPtr address)
		{
			return *(float*)address.ToPointer();
		}

		public static void WriteFloat(IntPtr address, float value)
		{
			var data = (float*)address.ToPointer();
			*data = value;
		}
	}
}
