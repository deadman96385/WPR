#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2022 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region CASE_SENSITIVITY_HACK Option
// #define CASE_SENSITIVITY_HACK
/* On Linux, the file system is case sensitive.
 * This means that unless you really focused on it, there's a good chance that
 * your filenames are not actually accurate! The result: File/DirectoryNotFound.
 * This is a quick alternative to MONO_IOMAP=all, but the point is that you
 * should NOT depend on either of these two things. PLEASE fix your paths!
 * -flibit
 */
#endregion

#region Using Statements
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
#endregion

namespace Microsoft.Xna.Framework
{
	public static class TitleContainer
	{
		private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>>
			PackagedFileIndexes = new ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>>(
				OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

		#region Public Static Methods

		public static Stream OpenStream(string name)
		{
			string safeName = MonoGame.Utilities.FileHelpers.NormalizeFilePathSeparators(name);

#if CASE_SENSITIVITY_HACK
			if (Path.IsPathRooted(safeName))
			{
				safeName = GetCaseName(safeName);
			}
			safeName = GetCaseName(Path.Combine(TitleLocation.Path, safeName));
#endif
			string realName = Path.IsPathRooted(safeName)
				? safeName
				: Path.Combine(TitleLocation.Path, safeName);
			string resolvedName = ResolvePackagedPath(realName, TitleLocation.Path);
			return new TitleStream(File.OpenRead(resolvedName));
		}

		#endregion

		#region Private Title Package Stream

		/* The phone's title package handed out streams whose Length still
		 * answered after the stream had been closed, and titles depend on it.
		 * Sonic CD's FileIO.CheckRSDKFile opens Data.rsdk, closes it, and then
		 * calls LoadFile, whose first act is to read Length off that same closed
		 * stream - on the success path, on every launch. File.OpenRead returns a
		 * FileStream, which throws ObjectDisposedException there instead, so the
		 * length is captured at open and outlives the handle.
		 *
		 * Only Length is kept alive. Reading a closed stream still fails, which
		 * is what the phone did and what a title would be wrong to rely on.
		 */
		private sealed class TitleStream : Stream
		{
			private readonly Stream inner;
			private readonly long length;

			internal TitleStream(Stream inner)
			{
				this.inner = inner;
				length = inner.Length;
			}

			public override bool CanRead
			{
				get { return inner.CanRead; }
			}

			public override bool CanSeek
			{
				get { return inner.CanSeek; }
			}

			public override bool CanWrite
			{
				get { return false; }
			}

			public override long Length
			{
				get { return length; }
			}

			public override long Position
			{
				get { return inner.Position; }
				set { inner.Position = value; }
			}

			public override void Flush()
			{
				inner.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				return inner.Read(buffer, offset, count);
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				return inner.Seek(offset, origin);
			}

			public override void SetLength(long value)
			{
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				throw new NotSupportedException();
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					inner.Dispose();
				}
				base.Dispose(disposing);
			}
		}

		#endregion

		#region Internal Static Methods

		internal static IntPtr ReadToPointer(string name, out IntPtr size)
		{
			string safeName = MonoGame.Utilities.FileHelpers.NormalizeFilePathSeparators(name);

#if CASE_SENSITIVITY_HACK
			if (Path.IsPathRooted(safeName))
			{
				safeName = GetCaseName(safeName);
			}
			safeName = GetCaseName(Path.Combine(TitleLocation.Path, safeName));
#endif
			string realName;
			if (Path.IsPathRooted(safeName))
			{
				realName = safeName;
			}
			else
			{
				realName = Path.Combine(TitleLocation.Path, safeName);
			}
			realName = ResolvePackagedPath(realName, TitleLocation.Path);
			if (!File.Exists(realName))
			{
				throw new FileNotFoundException(realName);
			}
			return FNAPlatform.ReadFileToPointer(realName, out size);
		}

		#endregion

		internal static string ResolvePackagedPath(string path, string titleRoot)
		{
			if (File.Exists(path))
			{
				return path;
			}

			string fullPath = Path.GetFullPath(path);
			string root = Path.GetFullPath(titleRoot);
			string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
				? root
				: root + Path.DirectorySeparatorChar;
			if (!fullPath.StartsWith(rootPrefix,
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
			{
				return path;
			}

			string relative = fullPath.Substring(rootPrefix.Length);
			string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			for (int index = 0; index < segments.Length - 1; index += 1)
			{
				string segment = segments[index];
				bool cultureSegment = segment.Length == 2 &&
					char.IsLetter(segment[0]) && char.IsLetter(segment[1]);
				if (!cultureSegment && segment.Length == 5 && segment[2] == '-')
				{
					cultureSegment = char.IsLetter(segment[0]) && char.IsLetter(segment[1]) &&
						char.IsLetter(segment[3]) && char.IsLetter(segment[4]);
				}
				if (!cultureSegment)
				{
					continue;
				}

				string candidate = Path.Combine(root,
					Path.Combine(segments.Where((_, candidateIndex) => candidateIndex != index).ToArray()));
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}

			string fileName = Path.GetFileName(fullPath);
			if (!string.IsNullOrEmpty(fileName))
			{
				IReadOnlyDictionary<string, string> index = PackagedFileIndexes.GetOrAdd(root,
					static indexedRoot => new Lazy<IReadOnlyDictionary<string, string>>(
						() => BuildPackagedFileIndex(indexedRoot), true)).Value;
				if (index.TryGetValue(fileName, out string uniquePath) && !string.IsNullOrEmpty(uniquePath))
				{
					return uniquePath;
				}
			}

			return path;
		}

		private static IReadOnlyDictionary<string, string> BuildPackagedFileIndex(string root)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
				{
					string fileName = Path.GetFileName(file);
					if (result.ContainsKey(fileName))
					{
						result[fileName] = string.Empty;
					}
					else
					{
						result.Add(fileName, file);
					}
				}
			}
			catch (IOException)
			{
				result.Clear();
			}
			catch (UnauthorizedAccessException)
			{
				result.Clear();
			}

			return result;
		}

		#region Private Static fcaseopen Method

#if CASE_SENSITIVITY_HACK
		private static string GetCaseName(string name)
		{
			if (File.Exists(name))
			{
				return name;
			}

			string[] splits = name.Split(Path.DirectorySeparatorChar);
			splits[0] = "/";
			int i;

			// The directories...
			for (i = 1; i < splits.Length - 1; i += 1)
			{
				splits[0] += SearchCase(
					splits[i],
					Directory.GetDirectories(splits[0])
				);
			}

			// The file...
			splits[0] += SearchCase(
				splits[i],
				Directory.GetFiles(splits[0])
			);

			// Finally.
			splits[0] = splits[0].Remove(0, 1);
			FNALoggerEXT.LogError(
				"Case sensitivity!\n\t" +
				name.Substring(TitleLocation.Path.Length) + "\n\t" +
				splits[0].Substring(TitleLocation.Path.Length)
			);
			return splits[0];
		}

		private static string SearchCase(string name, string[] list)
		{
			foreach (string l in list)
			{
				string li = l.Substring(l.LastIndexOf("/") + 1);
				if (name.ToLower().Equals(li.ToLower()))
				{
					return Path.DirectorySeparatorChar + li;
				}
			}
			// If you got here, get ready to crash!
			return Path.DirectorySeparatorChar + name;
		}
#endif

		#endregion
	}
}

